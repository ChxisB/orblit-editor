// setState on `this` from an extension is exactly what @protected asks
// for; the analyzer does not count an extension body as inside the class.
// ignore_for_file: invalid_use_of_protected_member

part of 'viewport.dart';

// The surface itself: the renderer's texture and every gesture, key and
// pointer listener layered over it.

extension _Surface on _SceneViewportState {
  Widget _buildSurface() {
    return Focus(
      focusNode: _flyFocus,
      onKeyEvent: _onFlyKey,
      child: Listener(
        onPointerSignal: _onPointerSignal,
        // Trackpad gestures arrive here rather than as scroll events, once
        // something listens for them. That separation is the whole point: a
        // mouse wheel keeps meaning zoom, and two fingers on glass can mean
        // something better than a wheel with no wheel.
        onPointerPanZoomStart: _onPanZoomStart,
        onPointerPanZoomUpdate: _onPanZoomUpdate,
        onPointerPanZoomEnd: _onPanZoomEnd,
        onPointerDown: _onPointerDown,
        onPointerMove: _onPointerMove,
        onPointerUp: _onPointerUp,
        onPointerCancel: _onPointerCancel,
        child: MouseRegion(
          onHover: _onHover,
          onExit: _onExit,
          child: GestureDetector(
            // Opaque so drags land here rather than falling through to whatever
            // scrolls behind the viewport.
            behavior: HitTestBehavior.opaque,
            onTapUp: _onTapUp,
            onPanStart: _onPanStart,
            onPanUpdate: _onPanUpdate,
            onPanEnd: _onPanEnd,
            onPanCancel: _onPanCancel,
            child: !_rendererAvailable
                ? const _Placeholder()
                : OrblitView(
                    // One scene at a time, so the viewport shows one document and
                    // there is never a question about which one an object belongs
                    // to.
                    scene: (widget.workspace.loaded?.scene ?? EditorScene([]))
                        .toRenderScene(
                          widget.camera.toRenderCamera(),
                          projectRoot: widget.projectRoot,
                          // What every scene in the project has in it, drawn alongside
                          // whichever one is open.
                          shared: widget.workspace.shared,
                          geometryOf: widget.geometryOf,
                          // Centred on what this view is looking at, so four views
                          // each get a grid under their own camera rather than one
                          // grid the others have run off the edge of.
                          grid: widget.grid?.planFor(
                            widget.snapping,
                            widget.camera.target,
                          ),
                        )
                        .copyWith(
                          // The selection, outlined by the renderer: after tone
                          // mapping and anti-aliasing, and free when nothing is
                          // selected.
                          outline: widget.outlineSelection
                              ? selectionOutline(
                                  scene: widget.workspace.loaded?.scene,
                                  shared: widget.workspace.shared,
                                  selected: widget.selected,
                                  primary: widget.primary,
                                )
                              : OrblitOutline.none,
                        ),
                    onSceneNotes: widget.onSceneNotes,
                  ),
          ),
        ),
      ),
    );
  }

  void _onPointerSignal(PointerSignalEvent event) {
    if (event is! PointerScrollEvent) return;
    // While flying the wheel sets how fast, not how close: somebody
    // holding the button down is going somewhere, and zooming the orbit
    // distance under them would move the view sideways as they went.
    if (_flying) {
      setState(() {
        _flySpeed = (_flySpeed * math.exp(-event.scrollDelta.dy * 0.0025))
            .clamp(0.25, 400.0);
      });
      return;
    }
    widget.onCameraChanged(widget.camera.zoom(event.scrollDelta.dy));
  }

  void _onPanZoomStart(PointerPanZoomStartEvent _) {
    _panZoomFrom = 1;
    _onTrackpad = true;
  }

  void _onPanZoomUpdate(PointerPanZoomUpdateEvent event) {
    // Pinching is zoom, whichever mode the view is in. It is the one
    // gesture on a trackpad that has never meant anything else.
    if ((event.scale - _panZoomFrom).abs() > 0.001) {
      final step = event.scale / (_panZoomFrom == 0 ? 1 : _panZoomFrom);
      _panZoomFrom = event.scale;
      widget.onCameraChanged(widget.camera.zoom(-math.log(step) * 620));
      return;
    }

    final delta = event.localPanDelta;
    if (delta == Offset.zero) return;

    if (_flying) {
      // Steering, while the keys do the moving. No button held down, which
      // is the whole reason flying can be switched on rather than held.
      widget.onCameraChanged(widget.camera.looking(delta));
      return;
    }

    final shifted = HardwareKeyboard.instance.isShiftPressed;
    widget.onCameraChanged(
      shifted ? widget.camera.pan(delta) : widget.camera.orbit(delta),
    );
  }

  void _onPanZoomEnd(PointerPanZoomEndEvent _) {
    _panZoomFrom = 1;
    _onTrackpad = false;
  }

  void _onPointerDown(PointerDownEvent event) {
    if (event.buttons & kSecondaryButton == 0) return;
    // The focus has to be here before the first key arrives, and a
    // viewport that took focus on hover would steal it from a name being
    // typed in the inspector.
    _flyFocus.requestFocus();
    setState(() {
      _looking = event.localPosition;
      _lastFlew = _elapsed;
    });
    _syncClock();
  }

  void _onPointerMove(PointerMoveEvent event) {
    final was = _looking;
    if (was == null) return;
    if (event.buttons & kSecondaryButton == 0) {
      setState(_stopFlying);
      _syncClock();
      return;
    }
    widget.onCameraChanged(
      widget.camera.looking(event.localPosition - was),
    );
    _looking = event.localPosition;
  }

  void _onPointerUp(PointerUpEvent _) {
    if (!_flying) return;
    setState(_stopFlying);
    // Back to whatever the scene wanted, so a still scene stops drawing.
    _syncClock();
  }

  void _onPointerCancel(PointerCancelEvent _) {
    if (!_flying) return;
    setState(_stopFlying);
    _syncClock();
  }

  // ---- the pointer, in order ----

  /// Who gets a gesture, first to last.
  ///
  /// The mode's tool, then anything being drawn, then the gizmos, then the
  /// selection, then the camera. Each takes what it wants and says so, and
  /// the first to take something is the only one that sees it.
  Iterable<ViewportStage> _stages() sync* {
    if (widget.modeInput case final tool?) yield (name: 'tool', input: tool);
    yield (name: 'drawing', input: _drawingInput);
    if (_gizmoTarget case final target?) {
      for (final type in _gizmoTypes.all) {
        final input = type.input;
        if (input == null || !type.appliesTo(target)) continue;
        yield (name: 'gizmo:${type.name}', input: (g) => input(target, g));
      }
    }
    yield (name: 'selection', input: _selectionInput);
    yield (name: 'orbit', input: _orbitInput);
  }

  ViewportGesture _gesture(ViewportPhase phase, Offset at) {
    final size = _surface;
    return ViewportGesture(
      phase,
      at,
      add: isCommandModifierPressed || HardwareKeyboard.instance.isShiftPressed,
      projection: size == null || size.isEmpty
          ? null
          : ViewportProjection(camera: widget.camera, size: size),
    );
  }

  void _onHover(PointerHoverEvent event) =>
      _input.handle(_gesture(ViewportPhase.hover, event.localPosition));

  void _onExit(PointerExitEvent event) => _input.leave(event.localPosition);

  void _onTapUp(TapUpDetails details) {
    // A click in a view is how that view becomes the one the keyboard
    // is talking to. Focus that followed the pointer instead would take
    // it away from a name half-typed in the inspector.
    _flyFocus.requestFocus();
    _input.handle(_gesture(ViewportPhase.tap, details.localPosition));
  }

  void _onPanStart(DragStartDetails details) {
    // Already handled as a trackpad gesture.
    if (_onTrackpad) return;
    _input.handle(_gesture(ViewportPhase.dragStart, details.localPosition));
  }

  void _onPanUpdate(DragUpdateDetails details) {
    if (_onTrackpad) return;
    _input.handle(_gesture(ViewportPhase.dragUpdate, details.localPosition));
  }

  void _onPanEnd(DragEndDetails details) =>
      _input.handle(_gesture(ViewportPhase.dragEnd, details.localPosition));

  void _onPanCancel() => _input.cancel();

  /// A tool that is being drawn takes every click: putting a point down and
  /// selecting something are different enough that guessing between them
  /// would get one of them wrong constantly.
  bool _drawingInput(ViewportGesture gesture) {
    if (!(widget.drawing?.tool.isDrawing ?? false)) return false;
    switch (gesture.phase) {
      case ViewportPhase.hover:
        _hoverDraw(gesture.at);
      case ViewportPhase.tap:
        _drawAt(gesture.at);
      case ViewportPhase.leave:
        return false;
      // Drawing is clicks, not drags: a drag here would orbit the view out
      // from under the plane being drawn on. Taken, and nothing done with it.
      case ViewportPhase.dragStart ||
          ViewportPhase.dragUpdate ||
          ViewportPhase.dragEnd ||
          ViewportPhase.dragCancel:
        break;
    }
    return true;
  }

  /// The move and turn handles.
  ///
  /// A drag that starts on one is a transform, and anywhere else is not.
  /// Nothing to hold down and no mode to be in — the handles are the mode.
  bool _transformInput(ViewportGesture gesture) {
    switch (gesture.phase) {
      case ViewportPhase.hover:
        _hover(gesture.at);
        return _hovered != null;
      case ViewportPhase.leave:
        if (_hovered != null) setState(() => _hovered = null);
        return false;
      case ViewportPhase.tap:
        return false;
      case ViewportPhase.dragStart:
        return _grab(gesture.at);
      case ViewportPhase.dragUpdate:
        _dragTo(gesture.at);
        return true;
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        _release();
        return true;
    }
  }

  /// Choosing what to work on: objects, or while a mesh is being edited, its
  /// parts.
  bool _selectionInput(ViewportGesture gesture) {
    final editing = widget.editing != null;
    switch (gesture.phase) {
      case ViewportPhase.hover:
        if (!editing) return false;
        final under = _elementAt(gesture.at);
        if (under != _hoveredElement) {
          setState(() => _hoveredElement = under);
        }
        return true;
      case ViewportPhase.leave:
        if (_hoveredElement != null) setState(() => _hoveredElement = null);
        return false;
      case ViewportPhase.tap:
        // While somebody is editing a mesh, a click is about its parts.
        // Picking a different object out from under them mid-extrude is not
        // something anybody means by clicking on their own geometry.
        if (editing) {
          widget.onPickElement?.call(_elementAt(gesture.at), add: gesture.add);
        } else {
          _pick(gesture.at, add: gesture.add);
        }
        return true;
      case ViewportPhase.dragStart:
        // While a mesh is being edited, a drag that missed the handles is a
        // marquee rather than the camera turning. The camera is still there
        // on the right button and on the trackpad, and having to hold
        // something down to select is the wrong way round for the one thing
        // somebody is doing constantly.
        if (!editing || widget.onSelectElements == null) return false;
        setState(() {
          _boxFrom = gesture.at;
          _boxTo = gesture.at;
        });
        return true;
      case ViewportPhase.dragUpdate:
        setState(() => _boxTo = gesture.at);
        return true;
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        if (gesture.phase == ViewportPhase.dragEnd) _takeBox(gesture.add);
        setState(() {
          _boxFrom = null;
          _boxTo = null;
        });
        return true;
    }
  }

  /// A drag that nothing else wanted turns the view.
  bool _orbitInput(ViewportGesture gesture) {
    switch (gesture.phase) {
      case ViewportPhase.dragStart:
        _dragAnchor = gesture.at;
        return true;
      case ViewportPhase.dragUpdate:
        final anchor = _dragAnchor;
        if (anchor == null) return true;
        widget.onCameraChanged(widget.camera.orbit(gesture.at - anchor));
        _dragAnchor = gesture.at;
        return true;
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        _dragAnchor = null;
        return true;
      case ViewportPhase.hover || ViewportPhase.leave || ViewportPhase.tap:
        return false;
    }
  }
}

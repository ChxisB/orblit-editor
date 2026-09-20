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

  void _onHover(PointerHoverEvent event) {
    if (widget.drawing?.tool.isDrawing ?? false) {
      _hoverDraw(event.localPosition);
      return;
    }
    if (widget.editing != null) {
      final under = _elementAt(event.localPosition);
      if (under != _hoveredElement) {
        setState(() => _hoveredElement = under);
      }
      return;
    }
    _hover(event.localPosition);
  }

  void _onExit(PointerExitEvent _) {
    setState(() {
      _hovered = null;
      _hoveredElement = null;
    });
  }

  void _onTapUp(TapUpDetails details) {
    // A click in a view is how that view becomes the one the keyboard
    // is talking to. Focus that followed the pointer instead would take
    // it away from a name half-typed in the inspector.
    _flyFocus.requestFocus();

    // A tool that is being drawn takes every click: putting a point
    // down and selecting something are different enough that guessing
    // between them would get one of them wrong constantly.
    if (widget.drawing?.tool.isDrawing ?? false) {
      _drawAt(details.localPosition);
      return;
    }

    // While somebody is editing a mesh, a click is about its parts.
    // Picking a different object out from under them mid-extrude is not
    // something anybody means by clicking on their own geometry.
    if (widget.editing != null) {
      widget.onPickElement?.call(
        _elementAt(details.localPosition),
        add:
            isCommandModifierPressed ||
            HardwareKeyboard.instance.isShiftPressed,
      );
      return;
    }
    _pick(details.localPosition);
  }

  void _onPanStart(DragStartDetails details) {
    // Already handled as a trackpad gesture.
    if (_onTrackpad) return;
    // Drawing is clicks, not drags: a drag here would orbit the view
    // out from under the plane being drawn on.
    if (widget.drawing?.tool.isDrawing ?? false) return;
    // A handle first: a drag that starts on one is a transform, and
    // anywhere else is the view turning. Nothing to hold down and no
    // mode to be in — the handles are the mode.
    if (_grab(details.localPosition)) return;
    // While a mesh is being edited, a drag that missed the handles is a
    // marquee rather than the camera turning. The camera is still there
    // on the right button and on the trackpad, and having to hold
    // something down to select is the wrong way round for the one thing
    // somebody is doing constantly.
    if (widget.editing != null && widget.onSelectElements != null) {
      setState(() {
        _boxFrom = details.localPosition;
        _boxTo = details.localPosition;
      });
      return;
    }
    _dragAnchor = details.localPosition;
  }

  void _onPanUpdate(DragUpdateDetails details) {
    if (_onTrackpad) return;
    if (_dragging != null) {
      _dragTo(details.localPosition);
      return;
    }
    if (_boxFrom != null) {
      setState(() => _boxTo = details.localPosition);
      return;
    }
    final anchor = _dragAnchor;
    if (anchor == null) return;
    widget.onCameraChanged(
      widget.camera.orbit(details.localPosition - anchor),
    );
    _dragAnchor = details.localPosition;
  }

  void _onPanEnd(DragEndDetails _) {
    if (_boxFrom != null) {
      _takeBox(
        isCommandModifierPressed ||
            HardwareKeyboard.instance.isShiftPressed,
      );
      setState(() {
        _boxFrom = null;
        _boxTo = null;
      });
    }
    _release();
    _dragAnchor = null;
  }

  void _onPanCancel() {
    setState(() {
      _boxFrom = null;
      _boxTo = null;
    });
    _release();
    _dragAnchor = null;
  }
}

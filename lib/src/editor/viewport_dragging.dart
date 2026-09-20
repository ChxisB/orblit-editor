// setState on `this` from an extension is exactly what @protected asks
// for; the analyzer does not count an extension body as inside the class.
// ignore_for_file: invalid_use_of_protected_member

part of 'viewport.dart';

// A drag on a handle, start to finish: what it grabbed, where that lands
// once snapping has had its say, and the undo entry it leaves behind.

extension _Dragging on _SceneViewportState {
  /// The grid as it applies right now.
  ///
  /// Held down suspends it rather than switching it on, because somebody who
  /// wants a shelf half a millimetre off wants it for one drag and not for
  /// the afternoon.
  ///
  /// Deliberately plain Control on every platform rather than routed through
  /// [isCommandModifierPressed]: this is not a Command shortcut standing in
  /// for macOS's Meta, it is Control itself, chosen because it sits under the
  /// same hand as the drag. It does not collide with Control becoming the
  /// Command modifier off macOS — the two are read at different moments, a
  /// held key during a drag against a held key when a drag or a click starts.
  Snapping get _snap {
    final held =
        HardwareKeyboard.instance.isControlPressed ||
        HardwareKeyboard.instance.isAltPressed;
    if (!held) return widget.snapping;
    return Snapping(
      on: false,
      step: widget.snapping.step,
      angle: widget.snapping.angle,
    );
  }

  /// A world-space movement, in the object's own frame.
  ///
  /// A shift means something different inside an object that is turned or
  /// scaled, and a face dragged a metre along the world's X on an object
  /// turned ninety degrees should still end up a metre along the world's X.
  Vector3 _intoObject(Vector3 world, Matrix4 transform) {
    final inverse = Matrix4.inverted(transform);
    return inverse.transformed3(world) - inverse.transformed3(Vector3.zero());
  }

  void _hover(Offset local) {
    final axis = _gizmo?.axisAt(local);
    if (axis == _hovered) return;
    setState(() => _hovered = axis);
  }

  /// Picks whatever is under the pointer, or nothing.
  void _pick(Offset local) {
    final scene = widget.workspace.loaded?.scene;
    final size = _surface;
    if (scene == null || size == null || size.isEmpty) return;

    final ray = ViewportProjection(
      camera: widget.camera,
      size: size,
    ).rayThrough(local);
    // The open scene first, then what every scene has. A shared prop standing
    // in front of a scene's own is the uncommon way round, and picking the
    // thing somebody is working on when both are under the pointer is the
    // better answer of the two.
    final hit =
        scene.objectAlong(
          ray.origin,
          ray.direction,
          boundsOf: widget.models?.of,
        ) ??
        widget.workspace.shared.objectAlong(
          ray.origin,
          ray.direction,
          boundsOf: widget.models?.of,
        );

    // Command adds to the selection on macOS, Control everywhere else; Shift
    // does the same on every platform, so it is checked alongside either.
    final held =
        HardwareKeyboard.instance.isShiftPressed || isCommandModifierPressed;

    widget.onPick?.call(hit, add: held);
  }

  /// Takes hold of a handle, remembering where everything was.
  bool _grab(Offset local) {
    final gizmo = _gizmo;
    final scene = _editing;
    final axis = gizmo?.axisAt(local);
    if (gizmo == null || scene == null || axis == null) return false;
    if (widget.history == null) return false;

    final grabbed = _mode == GizmoMode.move
        ? gizmo.pointOnAxis(local, axis)
        : gizmo.pointOnRing(local, axis);
    // The pointer is on the handle but the plane behind it is edge-on to the
    // camera, so there is no sensible place to have grabbed. Better to orbit
    // than to teleport whatever is selected.
    if (grabbed == null) return false;

    if (_editingElements) {
      if (!_grabElements()) return false;
      setState(() {
        _dragging = axis;
        _grabbed = grabbed;
        _grabbedPivot = gizmo.pivot.clone();
      });
      return true;
    }

    _before.clear();
    _beforeWorld.clear();
    for (final id in _targets) {
      final object = scene[id]!;
      _before[id] = _mode == GizmoMode.move
          ? object.position.clone()
          : object.rotation.clone();
      _beforeWorld[id] = scene.worldOf(id).getRotation();
    }

    setState(() {
      _dragging = axis;
      _grabbed = grabbed;
      _grabbedPivot = gizmo.pivot.clone();
      _grabbedAnchor = _anchorOn(axis, gizmo.pivot);
    });
    return true;
  }

  /// How far the snapping anchor is from the handle, for the object the
  /// handles are on.
  ///
  /// The primary one only. A selection of several keeps its shape, so the
  /// thing that lands on a line is the one being held — the others come
  /// along.
  double _anchorOn(GizmoAxis axis, Vector3 pivot) {
    final scene = _editing;
    final id = widget.primary;
    if (scene == null || id == null) return 0;
    final object = scene[id];
    if (object == null) return 0;

    final local = object.localBounds(reported: widget.models?.of(object));
    final world = scene.worldOf(id);
    // Every corner, because a turned or scaled object's box in the world is
    // not its box multiplied through.
    var low = double.infinity;
    var high = double.negativeInfinity;
    for (final x in [local.min.x, local.max.x]) {
      for (final y in [local.min.y, local.max.y]) {
        for (final z in [local.min.z, local.max.z]) {
          final at = world.transformed3(Vector3(x, y, z)).dot(axis.direction);
          if (at < low) low = at;
          if (at > high) high = at;
        }
      }
    }

    return _snap.anchorFor(axis.direction, pivot, (
      min: axis.direction * low,
      max: axis.direction * high,
    ));
  }

  /// Takes hold of the parts of a mesh.
  ///
  /// Holding shift extrudes first and then drags what came out, which is the
  /// move a modelling tool is built around: a doorway, a chimney and a ledge
  /// are all one face pulled out. Extruding by nothing and then moving is
  /// exactly right — the walls are made where the face was, and the drag
  /// takes the face away from them.
  bool _grabElements() {
    final editing = widget.editing;
    if (editing == null || widget.onDragElements == null) return false;

    var selection = widget.elementSelection.copy();
    final mesh = editing.mesh.copy();
    var extruded = false;

    final wantsExtrude =
        _mode == GizmoMode.move &&
        widget.elementMode == ElementMode.face &&
        HardwareKeyboard.instance.isShiftPressed &&
        selection.faces.isNotEmpty;

    if (wantsExtrude) {
      // The copy's own faces, not the ones on screen. `extrude` matches by
      // identity, and a copied mesh's faces are different objects — passing
      // the originals finds nothing and silently extrudes nothing.
      final made = mesh.extrude(selection.facesIn(mesh), 0);
      if (made.isNotEmpty) {
        final places = <int>{};
        for (var i = 0; i < mesh.faces.length; i++) {
          if (made.contains(mesh.faces[i])) places.add(i);
        }
        selection = ElementSelection(faces: places);
        extruded = true;
      }
    }

    final points = selection.pointsIn(mesh).toList();
    if (points.isEmpty) return false;

    _beforeMesh = mesh;
    _movingPoints = points;
    _draggingSelection = selection;
    _dragStarted = false;

    // The extrude is written straight away rather than waiting for the first
    // movement. Somebody who holds shift, pulls and lets go without moving has
    // still made a face, and it should be there and be undoable.
    if (extruded) {
      widget.onDragElements!(mesh.copy(), selection, 'Extrude', merge: false);
      _dragStarted = true;
    }
    return true;
  }

  /// Moves or turns the parts of a mesh, from where they were when the drag
  /// began.
  void _dragElementsTo(Offset local) {
    final gizmo = _gizmo;
    final editing = widget.editing;
    final start = _beforeMesh;
    final axis = _dragging;
    final grabbed = _grabbed;
    final report = widget.onDragElements;
    if (gizmo == null ||
        editing == null ||
        start == null ||
        axis == null ||
        grabbed == null ||
        report == null) {
      return;
    }

    final next = start.copy();
    final String what;

    if (_mode == GizmoMode.move) {
      final now = gizmo.pointOnAxis(local, axis);
      if (now == null) return;
      // Snapped in the world, where the grid is, and then taken into the
      // object's frame. Snapping after the conversion would put the grid at
      // whatever angle and scale the object happens to have.
      final from = _grabbedPivot ?? gizmo.pivot;
      final shift = _snap.along(from, now - grabbed + from, axis.direction);
      next.movePoints(_movingPoints, _intoObject(shift, editing.transform));
      what = 'Move';
    } else {
      final now = gizmo.pointOnRing(local, axis);
      if (now == null) return;
      final angle = _snap.turn(gizmo.angleBetween(grabbed, now, axis));
      // Into the object's frame first, so the ring somebody grabbed in the
      // world is the axis the corners turn about inside a turned object.
      final about = start.centreOfPoints(_movingPoints);
      if (about == null) return;
      final localAxis = _intoObject(
        axis.direction,
        editing.transform,
      ).normalized();
      next.turnPoints(
        _movingPoints,
        Quaternion.axisAngle(localAxis, angle).asRotationMatrix(),
        about,
      );
      what = 'Turn';
    }

    report(next, _selectedElements, what, merge: _dragStarted);
    _dragStarted = true;
  }

  /// How far the drag has taken things, for saying so on screen.
  ///
  /// In squares as well as metres, because "two squares" is what somebody
  /// means when they are placing something on a grid, and counting them by
  /// eye across a viewport is exactly the sort of thing a computer should be
  /// doing.
  ({double metres, double squares})? get _dragged {
    final axis = _dragging;
    final from = _grabbedPivot;
    if (axis == null || from == null) return null;

    final scene = _editing;
    final id = widget.primary;
    final now = scene == null || id == null
        ? null
        : scene.worldOf(id).getTranslation();
    if (now == null) return null;

    final metres = (now - from).dot(axis.direction);
    return (
      metres: metres,
      squares: widget.snapping.step <= 0 ? 0 : metres / widget.snapping.step,
    );
  }

  /// Applies the drag as it stands: one command, however many objects.
  void _dragTo(Offset local) {
    final gizmo = _gizmo;
    final scene = _editing;
    final axis = _dragging;
    final grabbed = _grabbed;
    final history = widget.history;
    final sceneId = _editingId;
    if (gizmo == null ||
        scene == null ||
        axis == null ||
        grabbed == null ||
        history == null ||
        sceneId == null) {
      return;
    }

    if (_editingElements || _beforeMesh != null) {
      _dragElementsTo(local);
      return;
    }

    final changes = <String, ({Vector3 from, Vector3 to})>{};

    if (_mode == GizmoMode.move) {
      final now = gizmo.pointOnAxis(local, axis);
      if (now == null) return;
      // From where the handle was, so a selection of several keeps its shape
      // and the one the handles are on is the one that lands on a line.
      final from = _grabbedPivot ?? gizmo.pivot;
      final shift = _snap.along(
        from,
        now - grabbed + from,
        axis.direction,
        anchor: _grabbedAnchor,
      );

      for (final entry in _before.entries) {
        final object = scene[entry.key];
        if (object == null) continue;
        // A world-space shift means something different inside a parent that
        // is itself turned or scaled, so it is taken into that parent's frame
        // before it is added to a local position.
        final parentId = object.parentId;
        final local = parentId == null || !scene.contains(parentId)
            ? shift
            : Matrix4.inverted(scene.worldOf(parentId)).rotated3(shift.clone());
        changes[entry.key] = (from: entry.value, to: entry.value + local);
      }
    } else {
      final now = gizmo.pointOnRing(local, axis);
      if (now == null) return;
      final angle = _snap.turn(gizmo.angleBetween(grabbed, now, axis));
      final turn = Quaternion.axisAngle(
        axis.direction,
        angle,
      ).asRotationMatrix();

      for (final entry in _before.entries) {
        final object = scene[entry.key];
        final was = _beforeWorld[entry.key];
        if (object == null || was == null) continue;

        // Turned about the world axis, then read back into the parent's frame.
        // Each object turns where it stands rather than orbiting the handle,
        // so a selection of several keeps its shape.
        final turned = turn.multiplied(was);
        final parentId = object.parentId;
        final localRotation = parentId == null || !scene.contains(parentId)
            ? turned
            : (Matrix3.copy(
                scene.worldOf(parentId).getRotation(),
              )..invert()).multiplied(turned);

        changes[entry.key] = (
          from: entry.value,
          to: eulerDegreesOf(Matrix4.identity()..setRotation(localRotation)),
        );
      }
    }

    if (changes.isEmpty) return;
    history.run(
      TransformMany(
        sceneId: sceneId,
        field: _mode == GizmoMode.move
            ? TransformField.position
            : TransformField.rotation,
        what: changes.length == 1
            ? scene[changes.keys.first]!.name
            : '${changes.length} objects',
        changes: changes,
      ),
    );
  }

  void _release() {
    if (_dragging == null) return;
    // Sealed here rather than on a timer, so the whole gesture is one step
    // however long somebody took over it.
    widget.history?.seal();
    setState(() {
      _dragging = null;
      _grabbed = null;
      _grabbedPivot = null;
      _grabbedAnchor = 0;
      _beforeMesh = null;
      _movingPoints = const [];
      _draggingSelection = null;
      _dragStarted = false;
    });
  }
}

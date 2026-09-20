part of 'editor_shell.dart';

// Drawing a shape with the pointer, and cutting one that is already there.

extension _Drawing on _EditorShellState {
  /// Starts or stops a drawing tool.
  void _useTool(ViewportTool tool) {
    setState(() {
      if (_drawing.tool == tool) {
        _drawing.clear();
        return;
      }
      _drawing.start(tool);
      if (tool == ViewportTool.cut && _shapeSelected == null) {
        _drawing.clear();
        _say('Select a shape to cut first.', level: LogLevel.warning);
      }
    });
  }

  /// Puts down one point.
  void _drawPoint(Vector3 at, Vector3 origin, Vector3 normal, int? face) {
    setState(() {
      _drawing.planeAt(origin, normal, onFace: face);
      _drawing.add(at);
    });
  }

  /// Finishes whatever is being drawn.
  void _finishDrawing() {
    if (!_drawing.canFinish) {
      _say(
        _drawing.tool == ViewportTool.cut
            ? 'A cut needs two points, both on the edge of a face.'
            : 'A shape needs three points.',
        level: LogLevel.warning,
      );
      return;
    }
    switch (_drawing.tool) {
      case ViewportTool.polyShape:
        _makeDrawnShape();
      case ViewportTool.cut:
        _applyCut();
      case ViewportTool.none:
        break;
    }
  }

  /// Turns the outline into an object.
  void _makeDrawnShape() {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) return;

    final normal = _drawing.normal ?? Vector3(0, 1, 0);
    if (outlineCrosses(_drawing.points, normal)) {
      _say('That outline crosses itself.', level: LogLevel.warning);
      return;
    }

    // The points are kept relative to where the object stands, so moving the
    // object later moves the outline with it rather than leaving the two
    // describing different places.
    final middle = Vector3.zero();
    for (final at in _drawing.points) {
      middle.add(at);
    }
    middle.scale(1 / _drawing.points.length);

    final outline = PolyShape(
      points: [for (final at in _drawing.points) at - middle],
      height: 2,
    );

    final object = SceneObject(
      id: _nextObjectId(),
      name: _uniqueName(scene, 'Shape'),
      kind: ObjectKind.shape,
      position: middle,
      outline: outline,
      colour: const Color(0xFF8E99A8),
    );

    _run(AddObject(object, sceneId: open.id));
    setState(_drawing.clear);
    _select(object.id);
    _geometry.forget(object.id);
    _refreshGeometry();
    _say('Drew ${object.name}. Its height is in the inspector.');
  }

  /// Cuts the face the path was drawn on.
  void _applyCut() {
    final chosen = _shapeSelected;
    final face = _drawing.face;
    if (chosen == null || face == null) {
      _say('There is no face to cut.', level: LogLevel.warning);
      return;
    }

    final next = chosen.mesh.copy();
    if (face < 0 || face >= next.faces.length) {
      setState(_drawing.clear);
      return;
    }

    // Into the object's own space, which is where its geometry lives.
    final inverse = Matrix4.inverted(
      chosen.entry.scene!.worldOf(chosen.object.id),
    );
    final path = [
      for (final at in _drawing.points) inverse.transformed3(at.clone()),
    ];

    final made = next.cutFace(next.faces[face], path);
    if (made.isEmpty) {
      _say(
        'A cut has to start and end on the edge of the face, or come back '
        'to where it began.',
        level: LogLevel.warning,
      );
      return;
    }

    _run(
      SetGeometry(
        sceneId: chosen.entry.id,
        id: chosen.object.id,
        name: chosen.object.name,
        to: next,
        what: 'Cut',
      ),
    );
    setState(() {
      _drawing.clear();
      // What came out of the cut, because that is what somebody is about to
      // extrude — which is why they cut it.
      _elements = ElementSelection(
        faces: {
          for (var i = 0; i < next.faces.length; i++)
            if (made.contains(next.faces[i])) i,
        },
      );
      _elementMode = ElementMode.face;
      _context = EditContext.element;
    });
    _geometry.forget(chosen.object.id);
    _geometry.pathFor(chosen.object);
    _say('Cut into ${made.length} faces.');
  }
}

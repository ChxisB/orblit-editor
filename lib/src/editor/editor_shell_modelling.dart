part of 'editor_shell.dart';

// Editing a shape part by part: what is picked, what the tools do to it,
// and the UV, outline and boundary editors over the same selection.

void _toggle<T>(Set<T> set, T value) {
  if (!set.remove(value)) set.add(value);
}

/// The arrow keys, as shortcuts.
///
/// Built rather than written out: four directions times three modifiers is
/// twelve lines that say the same thing, and one of them would be wrong.
final Map<ShortcutActivator, Intent> _nudges = {
  for (final (key, axis, sign) in [
    (LogicalKeyboardKey.arrowLeft, _x, -1),
    (LogicalKeyboardKey.arrowRight, _x, 1),
    (LogicalKeyboardKey.arrowUp, _z, -1),
    (LogicalKeyboardKey.arrowDown, _z, 1),
  ]) ...{
    SingleActivator(key): _NudgeIntent(axis, sign),
    SingleActivator(key, alt: true): _NudgeIntent(axis, sign * 10),
  },
  // Up and down are the exception: there is no arrow for them, so shift
  // turns the near-and-far pair into a high-and-low one.
  SingleActivator(LogicalKeyboardKey.arrowUp, shift: true): _NudgeIntent(_y, 1),
  SingleActivator(LogicalKeyboardKey.arrowDown, shift: true): _NudgeIntent(
    _y,
    -1,
  ),
  SingleActivator(LogicalKeyboardKey.arrowUp, shift: true, alt: true):
      _NudgeIntent(_y, 10),
  SingleActivator(LogicalKeyboardKey.arrowDown, shift: true, alt: true):
      _NudgeIntent(_y, -10),
};

final Vector3 _x = Vector3(1, 0, 0);

final Vector3 _y = Vector3(0, 1, 0);

final Vector3 _z = Vector3(0, 0, 1);

extension _Modelling on _EditorShellState {
  /// The object whose parts are being edited, or null.
  ///
  /// Only a shape, and only while the context says so. Editing the parts of a
  /// referenced glTF model would mean editing a file somebody else's
  /// application also owns.
  EditingMesh? get _editing {
    if (_context != EditContext.element) return null;

    final open = _workspace.sceneHolding(_primary ?? '');
    final scene = open?.scene;
    final object = _primary == null ? null : scene?[_primary!];
    if (object == null || object.kind != ObjectKind.shape) return null;

    final mesh = object.currentMesh;
    if (mesh == null || mesh.isEmpty) return null;

    return (object: object, mesh: mesh, transform: scene!.worldOf(object.id));
  }

  /// The selected shape, whichever context is on.
  ///
  /// Separate from [_editing], which is only about element editing. Conforming
  /// normals or welding a whole shape is something somebody does to the object
  /// without going into it, and requiring them to would be a mode for no
  /// reason.
  ({SceneObject object, Mesh mesh, SceneEntry entry})? get _shapeSelected {
    final open = _workspace.sceneHolding(_primary ?? '');
    final object = _primary == null ? null : open?.scene?[_primary!];
    if (object == null || object.kind != ObjectKind.shape) return null;

    final mesh = object.currentMesh;
    if (mesh == null || mesh.isEmpty) return null;
    return (object: object, mesh: mesh, entry: open!);
  }

  /// Whether the selection could be edited part by part.
  bool get _canEditParts => _shapeSelected != null;

  void _setContext(EditContext context) {
    if (context == EditContext.element && !_canEditParts) return;
    setState(() {
      _context = context;
      if (context == EditContext.object) _elements.clear();
    });
  }

  /// Adds what was clicked to the selection, or replaces it.
  void _pickElement(Object? what, {required bool add}) {
    setState(() {
      if (!add) _elements.clear();
      if (what == null) return;

      switch (what) {
        case final int index when _elementMode == ElementMode.vertex:
          _toggle(_elements.vertices, index);
        case final int index when _elementMode == ElementMode.face:
          _toggle(_elements.faces, index);
        case final MeshEdge edge:
          _toggle(_elements.edges, edge);
        default:
          break;
      }
    });
  }

  /// Does one of the mesh actions and puts the result on the undo stack.
  ///
  /// The whole mesh per step. An extrude adds vertices and faces and moves
  /// others, and describing that as a diff is more code than the extrude —
  /// while a mesh is a few thousand doubles, which is nothing next to a frame.
  void _runMeshAction(MeshAction action) {
    // The selected shape, not the one being element-edited: an object action
    // works without going into the geometry first.
    final chosen = _shapeSelected;
    if (chosen == null) return;

    final next = chosen.mesh.copy();
    final after = action.run(
      next,
      _elements,
      _amounts[action.label] ?? action.amount?.value ?? 1,
    );

    _run(
      SetGeometry(
        sceneId: chosen.entry.id,
        id: chosen.object.id,
        name: chosen.object.name,
        to: next,
        what: action.label,
      ),
    );

    setState(() => _elements = after);
    _geometry.forget(chosen.object.id);
    _refreshGeometry();
  }

  /// Everything somebody does to geometry, in one place.
  ///
  /// Built here rather than in the inspector because it is a panel of its own
  /// now: it stays put when the selection changes, and says what it is
  /// waiting for when there is nothing to work on.
  Widget _modellingTools() {
    final chosen = _shapeSelected;
    return ModellingPanel(
      shape: chosen?.object.shape,
      geometry: chosen?.object.geometry,
      context_: _context,
      mode: _elementMode,
      selection: _elements,
      amounts: _amounts,
      onContext: _setContext,
      onMode: (mode) => setState(() => _elementMode = mode),
      onAction: _runMeshAction,
      onAmount: (action, amount) => setState(() => _amounts[action] = amount),
      seeThrough: _seeThrough,
      onSeeThrough: (value) => setState(() => _seeThrough = value),
      surfaces: chosen?.object.surfaces ?? const [],
      onSurfaces: (surfaces, {required live}) {
        if (chosen == null) return;
        _setSurfaces(chosen.object, surfaces, live: live);
      },
      onPaint: _paintFaces,
      format: _format,
      onFormat: (one) => setState(() => _format = one),
      onExport: () {
        if (chosen != null) _exportShape(chosen.object);
      },
      tool: _drawing.tool,
      onTool: _useTool,
      drawing: _drawing,
      snapping: _snapping,
      onSnapping: (_) => setState(() {}),
    );
  }

  /// The coordinate view, and the rule's numbers under it.
  ///
  /// One panel rather than a section of the inspector: a texture is looked at
  /// while the shape is being turned in the viewport, and something that
  /// takes half a sidebar wants to be somewhere somebody chose to put it.
  Widget _uvEditor() {
    final chosen = _shapeSelected;
    final mesh = chosen?.mesh;
    final faces = mesh == null ? const <Face>[] : _elements.facesIn(mesh);
    // Only meaningful for faces: a vertex has as many coordinates as it has
    // faces, and asking which one somebody means is a question with no good
    // answer.
    final wrongMode =
        _context == EditContext.element && _elementMode != ElementMode.face;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (wrongMode)
          Padding(
            padding: const EdgeInsets.only(bottom: Space.xs),
            child: Text(
              'Texture coordinates belong to faces. Press G until Faces is on.',
              style: OrblitText.caption.copyWith(fontSize: 11),
            ),
          ),
        UvPanel(
          mesh: mesh,
          selection: _elementMode == ElementMode.face
              ? _elements
              : nothingSelected,
          gesture: _uvGesture,
          onGesture: (one) => setState(() => _uvGesture = one),
          onNudge: (by) => _editUvs('Move texture', (mesh, faces) {
            mesh.nudgeUvs(faces, by);
          }),
          onScale: (by) => _editUvs('Scale texture', (mesh, faces) {
            mesh.scaleUvs(faces, by);
          }),
          onTurn: (degrees) => _editUvs('Turn texture', (mesh, faces) {
            mesh.turnUvs(faces, degrees);
          }),
          onDone: () => _gesture = null,
          onAction: _runUvAction,
        ),
        if (faces.length == 1 && !faces.single.uv.isManual) ...[
          const SizedBox(height: Space.sm),
          UvRuleControls(
            uv: faces.single.uv,
            onChanged: (next, {required live}) =>
                _editUvs('Texture', (mesh, faces) {
                  for (final face in faces) {
                    face.uv = next;
                  }
                }, live: live),
            onDone: () => _gesture = null,
          ),
        ],
      ],
    );
  }

  /// Runs one of the coordinate buttons.
  void _runUvAction(UvAction action) {
    _editUvs(action.label, (mesh, faces) {
      switch (action) {
        case UvAction.freeze:
          mesh.freezeUvs(faces);
        case UvAction.release:
          mesh.releaseUvs(faces);
        case UvAction.fit:
          mesh.fitUvs(faces);
        case UvAction.planar:
          mesh.projectPlanar(faces);
        case UvAction.box:
          mesh.projectBox(faces);
      }
    }, live: false);
  }

  /// One coordinate edit, on a copy, through the undo stack.
  ///
  /// [live] folds a run of them into one step, which is what a drag or a
  /// slider needs and what a button must not have — two presses of Fit are
  /// two things somebody did.
  void _editUvs(
    String what,
    void Function(Mesh mesh, List<Face> faces) change, {
    bool live = true,
  }) {
    final chosen = _shapeSelected;
    if (chosen == null) return;

    final next = chosen.mesh.copy();
    final faces = _elements.facesIn(next);
    if (faces.isEmpty) return;

    change(next, faces);

    if (!live) {
      _gesture = null;
    } else {
      _gesture ??= Object();
    }

    _run(
      SetGeometry(
        sceneId: chosen.entry.id,
        id: chosen.object.id,
        name: chosen.object.name,
        to: next,
        what: what,
        gesture: live ? _gesture : null,
      ),
    );
    if (!live) _gesture = null;

    _geometry.forget(chosen.object.id);
    _geometry.pathFor(chosen.object);
    setState(() {});
  }

  /// Changes a drawn shape's height, or turns it over.
  void _setOutline(SceneObject object, PolyShape next, {required bool live}) {
    final entry = _workspace.sceneHolding(object.id);
    if (entry == null) return;
    if (!live) _gesture = Object();

    _run(
      SetOutline(
        sceneId: entry.id,
        id: object.id,
        name: object.name,
        to: next,
        gesture: live ? _gesture : null,
      ),
    );
    if (!live) _gesture = null;
    _geometry.forget(object.id);
    _geometry.pathFor(object);
    setState(() {});
  }

  /// Moves the selection a whole number of squares.
  ///
  /// One press, one step on the undo stack — unlike a drag, which is one step
  /// however many frames it took. Pressing an arrow twice is two things
  /// somebody did.
  void _nudge(Vector3 axis, int squares) {
    final scene = _working?.scene;
    final entry = _working;
    if (scene == null || entry == null) return;

    final ids = [
      for (final id in _selected)
        if (scene[id] != null && scene[id]!.kind != ObjectKind.scene) id,
    ];
    if (ids.isEmpty) return;

    // The grid's step even when the grid is off: an arrow key is a request
    // for a definite amount, and the definite amount on offer is a square.
    final by = axis * (_snapping.step * squares);
    final changes = <String, ({Vector3 from, Vector3 to})>{};
    for (final id in ids) {
      final object = scene[id]!;
      final parentId = object.parentId;
      final local = parentId == null || !scene.contains(parentId)
          ? by
          : Matrix4.inverted(scene.worldOf(parentId)).rotated3(by.clone());
      changes[id] = (
        from: object.position.clone(),
        to: object.position + local,
      );
    }

    _run(
      TransformMany(
        sceneId: entry.id,
        field: TransformField.position,
        what: ids.length == 1
            ? scene[ids.first]!.name
            : '${ids.length} objects',
        changes: changes,
      ),
    );
    // Sealed, so the next press is its own step rather than merging into
    // this one the way a drag's frames do.
    _history.seal();
  }

  /// Changes where an object begins and ends.
  void _setBoundary(SceneObject object, Boundary next, {required bool live}) {
    final entry = _workspace.sceneHolding(object.id);
    if (entry == null) return;
    if (!live) _gesture = Object();

    _run(
      SetBoundary(
        sceneId: entry.id,
        id: object.id,
        name: object.name,
        to: next,
        gesture: live ? _gesture : null,
      ),
    );
    if (!live) _gesture = null;
    setState(() {});
  }

  /// Takes everything a marquee drew round.
  void _selectElements(List<Object> what, {required bool add}) {
    setState(() {
      if (!add) _elements.clear();
      for (final one in what) {
        switch (one) {
          case final int index when _elementMode == ElementMode.vertex:
            _elements.vertices.add(index);
          case final int index when _elementMode == ElementMode.face:
            _elements.faces.add(index);
          case final MeshEdge edge:
            _elements.edges.add(edge);
          default:
            break;
        }
      }
      // A new set object, so anything comparing the old one against the new
      // sees that it changed.
      _elements = _elements.copy();
    });
  }

  /// Changes a shape's material slots.
  void _setSurfaces(
    SceneObject object,
    List<Surface> surfaces, {
    required bool live,
  }) {
    final entry = _workspace.sceneHolding(object.id);
    if (entry == null) return;
    if (!live) _gesture = Object();

    _run(
      SetSurfaces(
        sceneId: entry.id,
        id: object.id,
        name: object.name,
        to: surfaces,
        what: 'Materials',
        // A slider run folds into one step; adding a slot does not.
        gesture: live ? _gesture : null,
      ),
    );
    if (!live) _gesture = null;
    _geometry.forget(object.id);
    _geometry.pathFor(object);
    setState(() {});
  }

  /// Paints the selected faces with one of the shape's material slots.
  void _paintFaces(int slot) {
    final chosen = _shapeSelected;
    if (chosen == null || _elements.faces.isEmpty) return;

    final next = chosen.mesh.copy();
    var painted = 0;
    for (final at in _elements.faces) {
      if (at < 0 || at >= next.faces.length) continue;
      next.faces[at].material = slot;
      painted++;
    }
    if (painted == 0) return;

    _run(
      SetGeometry(
        sceneId: chosen.entry.id,
        id: chosen.object.id,
        name: chosen.object.name,
        to: next,
        what: 'Paint',
      ),
    );
    _geometry.forget(chosen.object.id);
    _geometry.pathFor(chosen.object);
    setState(() {});
  }

  /// Takes a mesh a viewport drag has changed.
  ///
  /// The same path a tool button takes, with two differences: the step is
  /// named after the gesture rather than the tool, and every frame after the
  /// first folds into the first — so a drag across the screen is one thing to
  /// undo however many frames it took.
  void _dragElements(
    Mesh mesh,
    ElementSelection selection,
    String what, {
    required bool merge,
  }) {
    final chosen = _shapeSelected;
    if (chosen == null) return;

    // A key that lasts the gesture. Bumped on the first change of a drag, so
    // two separate drags of the same face never fold into each other.
    if (!merge) _gesture = Object();

    _run(
      SetGeometry(
        sceneId: chosen.entry.id,
        id: chosen.object.id,
        name: chosen.object.name,
        to: mesh,
        what: what,
        gesture: _gesture,
      ),
    );

    setState(() => _elements = selection);
    _geometry.forget(chosen.object.id);
    // Only the one being dragged. Writing every shape in the project on every
    // frame of a drag is the whole project's geometry sixty times a second.
    _geometry.pathFor(chosen.object);
  }
}

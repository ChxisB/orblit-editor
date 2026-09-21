part of 'viewport.dart';

// What the gizmo is acting on: the scene being edited, the handle drawn
// for it, and -- when the selection is elements rather than whole objects
// -- which points those are and where their pivot sits.

extension _Selection on _SceneViewportState {
  /// The scene the handles are working in.
  ///
  /// Whichever holds what is selected: the open scene, or the shared set. A
  /// manager put in the shared set is dragged the same way everything else is.
  EditorScene? get _editing {
    final id = widget.primary;
    if (id == null) return widget.workspace.loaded?.scene;
    return widget.workspace.sceneHolding(id)?.scene;
  }

  /// The id of that scene, for the command a drag runs.
  String? get _editingId {
    final id = widget.primary;
    if (id == null) return widget.workspace.loaded?.id;
    return widget.workspace.sceneHolding(id)?.id;
  }

  /// The gizmo as it stands, or null when there is nothing to put it on.
  Gizmo? get _gizmo {
    final scene = _editing;
    final id = widget.primary;
    final size = _surface;
    if (scene == null || id == null || size == null || size.isEmpty) {
      return null;
    }
    final object = scene[id];
    if (object == null || object.kind == ObjectKind.scene) return null;

    return Gizmo(
      mode: _mode,
      // While parts of a mesh are being edited the handles belong to those
      // parts, not to the object round them: dragging a face should move the
      // face. With nothing selected there is nothing to put them on, and
      // falling back to the object would make an empty selection look like a
      // whole-object move waiting to happen.
      pivot: _elementPivot ?? scene.worldOf(id).getTranslation(),
      projection: ViewportProjection(camera: widget.camera, size: size),
    );
  }

  /// What the gizmos are asked about, or null when there is nothing to put
  /// one on.
  GizmoTarget? get _gizmoTarget {
    final scene = _editing;
    final id = widget.primary;
    final size = _surface;
    if (scene == null || id == null || size == null || size.isEmpty) {
      return null;
    }
    final object = scene[id];
    if (object == null) return null;
    return GizmoTarget(
      object: object,
      scene: scene,
      selected: widget.selected,
      projection: ViewportProjection(camera: widget.camera, size: size),
    );
  }

  /// Every gizmo this view can draw: its own, then whatever it was given.
  ///
  /// A registry rather than a list so two gizmos under one name are caught
  /// where they are put together, instead of the second one quietly drawing
  /// over the first.
  Registry<GizmoType> get _gizmoTypes {
    final types = Registry<GizmoType>();
    for (final type in [..._builtInGizmos, ...widget.gizmos]) {
      types.register(type);
    }
    return types;
  }

  /// The gizmos every view has.
  ///
  /// Registered here rather than by the shell, because what they draw and
  /// what a drag on them does is state this view keeps: which handle is lit,
  /// what was grabbed, where it was when the drag began.
  List<GizmoType> get _builtInGizmos => [
    GizmoType(
      name: 'transform',
      appliesTo: (target) => target.object.kind != ObjectKind.scene,
      overlay: (_) => _handles(),
      input: (_, gesture) => _transformInput(gesture),
    ),
    // What a selected camera sees. Only one: a preview each for a row of
    // them would cover the view they are meant to help with.
    GizmoType(
      name: 'camera view',
      appliesTo: (target) {
        final object = target.object;
        return widget.previewOf != null &&
            object.kind == ObjectKind.camera &&
            target.selected.length == 1 &&
            identical(target.scene, widget.workspace.loaded?.scene) &&
            // A camera that is not in the scene has no shot to preview.
            object.visible &&
            target.scene.isShown(object.id);
      },
      overlay: (target) => _cameraPreview(widget.previewOf!(target.object)),
    ),
  ];

  /// Whether a drag right now moves parts of a mesh rather than objects.
  bool get _editingElements =>
      widget.editing != null && !_selectedElements.isEmpty;

  /// The selection a drag is working on — the one that came in, or the one an
  /// extrude made part-way through the gesture.
  ElementSelection get _selectedElements =>
      _draggingSelection ?? widget.elementSelection;

  /// Where the handles sit while editing elements, in the world.
  Vector3? get _elementPivot {
    final editing = widget.editing;
    if (editing == null) return null;
    final middle = _selectedElements.pivotIn(editing.mesh);
    if (middle == null) return null;
    return editing.transform.transformed3(middle);
  }

  /// Everything a drag moves: the whole selection, or just the one the handles
  /// are on when the selection is empty for some reason.
  List<String> get _targets {
    final scene = _editing;
    if (scene == null) return const [];
    final ids = widget.selected.isEmpty
        ? [if (widget.primary != null) widget.primary!]
        : widget.selected.toList();
    // Only what lives in the same scene as the one the handles are on: a
    // drag is one command, and a command changes one scene.
    return [
      for (final id in ids)
        if (scene[id] != null && scene[id]!.kind != ObjectKind.scene) id,
    ];
  }
}

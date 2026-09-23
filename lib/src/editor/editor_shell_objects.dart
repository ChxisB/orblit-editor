part of 'editor_shell.dart';

// The objects inside a scene: added, named, deleted, reparented, copied
// and framed.

extension _Objects on _EditorShellState {
  /// The scene being worked in, which is not always the one that is loaded.
  ///
  /// The shared set — the objects every scene has — is a scene that is always
  /// there and never the loaded one. Something selected in it is being edited
  /// in it, and every edit that reaches for "the scene" has to mean that one
  /// or the shared set becomes a place things can only be built, never
  /// copied, pasted, duplicated or dragged into.
  SceneEntry? get _working => _primary == null
      ? _current
      : (_workspace.sceneHolding(_primary!) ?? _current);

  void _add(ObjectKind kind) {
    // Wherever the selection is. Selecting something in the shared set and
    // pressing Add means adding to the shared set — anything else would be
    // the button ignoring where somebody is working.
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.', level: LogLevel.warning);
      return;
    }

    final name = _uniqueName(scene, switch (kind) {
      ObjectKind.mesh => 'Mesh',
      ObjectKind.light => 'Light',
      ObjectKind.camera => 'Camera',
      ObjectKind.group => 'Group',
      ObjectKind.scene => 'Scene',
      ObjectKind.weather => 'Weather',
      ObjectKind.canvas => 'Canvas',
      ObjectKind.shape => 'Shape',
    });

    final object = SceneObject(
      id: 'o${DateTime.now().microsecondsSinceEpoch}',
      name: name,
      kind: kind,
      colour: kind == ObjectKind.light
          ? const Color(0xFFFFF3E0)
          : const Color(0xFFD9634F),
    );

    // Added inside whatever is selected when that can hold things, which is
    // what somebody building a hierarchy means by "add" most of the time.
    final selected = _primary == null ? null : scene[_primary!];
    final parent = selected == null
        ? null
        : (selected.kind == ObjectKind.group ? selected.id : selected.parentId);

    _run(AddObject(object, sceneId: open.id, parentId: parent));
    _select(object.id);
  }

  /// Puts a shape in the scene.
  ///
  /// Parametric to begin with: it is a width, a height and a depth until
  /// somebody pulls a face off it, and until then changing the width should
  /// change the width rather than move eight corners.
  void _addShape(ShapeKind kind) {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.', level: LogLevel.warning);
      return;
    }

    final object = SceneObject(
      id: _nextObjectId(),
      name: _uniqueName(scene, kind.label),
      kind: ObjectKind.shape,
      shape: Shape(kind: kind),
      colour: const Color(0xFF8E99A8),
    );

    final selected = _primary == null ? null : scene[_primary!];
    _run(
      AddObject(
        object,
        sceneId: open.id,
        parentId: selected?.kind == ObjectKind.group ? selected!.id : null,
      ),
    );
    _select(object.id);
    _refreshGeometry();
  }

  String _uniqueName(EditorScene scene, String base) {
    final taken = {for (final o in scene.objects) o.name};
    if (!taken.contains(base)) return base;
    for (var i = 2; ; i++) {
      if (!taken.contains('$base $i')) return '$base $i';
    }
  }

  /// Deletes one object, whatever is selected.
  void _delete(String id) {
    final open = _workspace.sceneHolding(id);
    final object = open?.scene?[id];
    if (open == null || object == null) return;

    _run(DeleteObjects(sceneId: open.id, ids: [id], what: object.name));
    setState(() {
      _selected.remove(id);
      if (_primary == id) _primary = _selected.lastOrNull;
    });
  }

  /// Deletes everything selected, as one step.
  void _deleteSelection() {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null || _selected.isEmpty) return;

    final ids = _visibleOrder(scene).where(_selected.contains).toList();
    if (ids.isEmpty) return;

    final what = ids.length == 1
        ? (scene[ids.single]?.name ?? 'object')
        : '${ids.length} objects';

    _run(DeleteObjects(sceneId: open.id, ids: ids, what: what));
    _clearSelection();
  }

  /// Moves an object in the tree, by reparenting, reordering, or both.
  void _move(String id, Drop drop) {
    final open = _workspace.sceneHolding(id);
    final scene = open?.scene;
    final object = scene?[id];
    if (open == null || scene == null || object == null) return;

    // A part is the prefab's, and can be rearranged only inside the instance
    // it belongs to. Anywhere else it would stop being a part, and that is
    // what unpacking is for.
    final instance = doc.EntityPath.instanceOf(id);
    if (instance != null &&
        (drop.sceneId != open.id ||
            drop.parentId == null ||
            !doc.EntityPath.within(instance, drop.parentId!))) {
      _say(
        '${object.name} is part of ${scene[instance]?.name ?? instance}. '
        'Unpack that to move its parts out of it.',
      );
      return;
    }

    // Onto a different scene's row — the shared set, most often. A different
    // operation rather than a refusal: one scene loses the subtree and
    // another gains it.
    if (drop.sceneId != open.id) {
      _run(
        MoveBetweenScenes(
          fromSceneId: open.id,
          sceneId: drop.sceneId,
          id: id,
          name: object.name,
          parentId: drop.parentId,
          index: drop.index,
        ),
      );
      return;
    }

    final fromIndex = scene.indexOf(id);
    var toIndex = drop.index;
    // Removing it first shifts everything after it down by one, so an index
    // taken from the tree as drawn is one too many when moving down.
    if (object.parentId == drop.parentId && fromIndex < toIndex) toIndex -= 1;
    if (object.parentId == drop.parentId && fromIndex == toIndex) return;

    _run(
      MoveObject(
        sceneId: open.id,
        id: id,
        name: object.name,
        from: object.parentId,
        to: drop.parentId,
        fromIndex: fromIndex,
        toIndex: toIndex,
      ),
    );
  }

  /// Puts the selection on the clipboard, and on the system's.
  ///
  /// Written out as text as well, so a copy can cross into another window —
  /// or into a text editor, where it is readable rather than an opaque blob.
  Future<void> _copy() async {
    final scene = _working?.scene;
    if (scene == null || _selected.isEmpty) return;

    // At rest, so what is copied mid-clip is the object and not its pose.
    _atRest(() => _clipboard.take(scene, _selected));
    setState(() {});
    await services.Clipboard.setData(
      services.ClipboardData(text: _clipboard.toText()),
    );
    _say('Copied ${_clipboard.description}.');
  }

  /// Copies the selection and then removes it.
  Future<void> _cut() async {
    final scene = _working?.scene;
    if (scene == null || _selected.isEmpty) return;

    // Copied before it is deleted, since the delete is what makes it
    // unreachable.
    _atRest(() => _clipboard.take(scene, _selected));
    await services.Clipboard.setData(
      services.ClipboardData(text: _clipboard.toText()),
    );
    _deleteSelection();
  }

  /// Puts the clipboard into the loaded scene.
  ///
  /// Beside whatever is selected rather than inside it, which is what somebody
  /// pressing paste usually means — pasting into the thing you were looking at
  /// buries it one level down.
  Future<void> _paste() async {
    // Into whatever holds the selection, so pasting next to a shared prop
    // puts the copy beside it rather than in the scene behind it.
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) return;

    // The system clipboard first, so a copy from another window wins over
    // whatever this one did last.
    final text = await services.Clipboard.getData('text/plain');
    if (!mounted) return;
    _clipboard.takeText(text?.text);

    if (_clipboard.isEmpty) {
      _say(
        'There is nothing on the clipboard to paste.',
        level: LogLevel.warning,
      );
      return;
    }

    final beside = _primary == null ? null : scene[_primary!];
    final content = _clipboard.contents(
      nextId: _nextObjectId,
      parentId: beside?.parentId,
    );

    _run(
      PasteObjects(
        sceneId: open.id,
        objects: content.objects,
        roots: content.roots,
        worlds: content.worlds,
        what: _clipboard.description,
      ),
    );

    setState(() {
      _selected
        ..clear()
        ..addAll(content.roots);
      _primary = content.roots.lastOrNull;
    });
  }

  /// Copies the selection and pastes it straight back.
  void _duplicate() {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null || _selected.isEmpty) return;

    // On its own clipboard, so duplicating does not throw away what somebody
    // had copied earlier.
    final taken = _atRest(() => SceneClipboard()..take(scene, _selected));
    final content = taken.contents(
      nextId: _nextObjectId,
      parentId: _primary == null ? null : scene[_primary!]?.parentId,
    );

    _run(
      PasteObjects(
        sceneId: open.id,
        objects: content.objects,
        roots: content.roots,
        worlds: content.worlds,
        what: taken.description,
      ),
    );

    setState(() {
      _selected
        ..clear()
        ..addAll(content.roots);
      _primary = content.roots.lastOrNull;
    });
  }

  String _nextObjectId() =>
      'o${DateTime.now().microsecondsSinceEpoch}_${_nextObject++}';

  void _frameSelection() {
    final id = _primary;
    final scene = _current?.scene;
    if (scene == null) return;

    final bounds = id == null || !scene.contains(id)
        ? scene.boundsOfEverything()
        : scene.boundsOf(id);

    setState(() {
      _camera = _camera.framing(centre: bounds.centre, radius: bounds.radius);
    });
  }
}

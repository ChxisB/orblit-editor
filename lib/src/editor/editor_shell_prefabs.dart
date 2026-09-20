part of 'editor_shell.dart';

// Prefabs: made from a selection, placed, and kept in step with the file
// every instance of one came from.

extension _Prefabs on _EditorShellState {
  /// Saves an object and everything under it as a prefab asset.
  ///
  /// The object it was made from becomes the first instance, the way it does
  /// in every editor that has prefabs. Anything else and the thing on screen
  /// would look like the prefab while quietly not being one.
  void _makePrefab(ObjectDrag dragged, String directory) {
    final open = _workspace.sceneHolding(dragged.id);
    final scene = open?.scene;
    if (open == null || scene == null) return;

    final object = scene[dragged.id];
    if (object == null) return;
    if (object.kind == ObjectKind.scene) {
      _say('A scene is already a file. Save it instead.');
      return;
    }

    final Prefab prefab;
    try {
      prefab = Prefab.fromScene(scene, dragged.id);
    } on SceneError catch (error) {
      _say(error.message);
      return;
    }

    final made = _assets.write(
      directory,
      '${object.name}${Prefab.extension}',
      prefab.toText(),
    );
    if (made.problem != null || made.path == null) {
      _say('Could not save the prefab: ${made.problem}', level: LogLevel.error);
      return;
    }

    final source = _assets.relative(made.path!);
    _run(
      LinkPrefab(
        sceneId: open.id,
        ids: [
          dragged.id,
          for (final child in scene.descendantsOf(dragged.id)) child.id,
        ],
        source: source,
        what: object.name,
      ),
    );
    _say(
      'Saved ${p.basename(made.path!)}. '
      '${object.name} is now an instance of it.',
    );
  }

  /// Reads a prefab off disk, saying so rather than failing silently.
  Prefab? _readPrefab(String relativeOrAbsolute) {
    final path = p.isAbsolute(relativeOrAbsolute)
        ? relativeOrAbsolute
        : p.join(widget.project.directory, relativeOrAbsolute);

    final file = File(path);
    if (!file.existsSync()) {
      _say('${p.basename(path)} is not in the project any more.');
      return null;
    }

    final prefab = Prefab.read(file.readAsStringSync());
    if (prefab == null) _say('${p.basename(path)} is not a readable prefab.');
    return prefab;
  }

  /// Puts an instance of a prefab into the open scene.
  void _placePrefab(String path) {
    final open = _current;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.');
      return;
    }

    final prefab = _readPrefab(path);
    if (prefab == null) return;

    final selected = _primary == null ? null : scene[_primary!];
    final parent = selected == null
        ? null
        : (selected.kind == ObjectKind.group ? selected.id : selected.parentId);

    final made = prefab.instantiate(
      nextId: _nextObjectId,
      source: _assets.relative(path),
      parentId: parent,
      name: _uniqueName(scene, prefab.name),
    );

    _run(
      PasteObjects(
        sceneId: open.id,
        objects: made.objects,
        roots: [made.rootId],
        what: prefab.name,
      ),
    );
    _select(made.rootId);
  }

  /// Writes what an instance looks like now back to its prefab, and brings
  /// every other instance of it into line.
  ///
  /// The other instances keep where they stand and what they are called;
  /// everything else comes from the asset. Any other property somebody had
  /// changed on one of them goes, which is why this says how many it touched
  /// rather than doing it quietly.
  void _applyPrefab(String id) {
    final open = _workspace.sceneHolding(id);
    final scene = open?.scene;
    final object = scene?[id];
    final source = object?.prefab;
    if (open == null || scene == null || object == null || source == null) {
      return;
    }

    final Prefab prefab;
    try {
      prefab = Prefab.fromScene(scene, id);
    } on SceneError catch (error) {
      _say(error.message);
      return;
    }

    final path = p.join(widget.project.directory, source);
    try {
      File(path).writeAsStringSync(prefab.toText());
    } on FileSystemException catch (error) {
      _say(
        'Could not write ${p.basename(path)}: '
        '${error.osError?.message ?? error.message}',
      );
      return;
    }

    final touched = _syncInstances(prefab, source, except: id);
    _say(
      touched == 0
          ? 'Saved ${p.basename(path)}.'
          : 'Saved ${p.basename(path)} and updated $touched other '
                'instance${touched == 1 ? '' : 's'}.',
    );
  }

  /// Throws away an instance's local changes and takes the prefab's again.
  void _revertPrefab(String id) {
    final open = _workspace.sceneHolding(id);
    final scene = open?.scene;
    final object = scene?[id];
    final source = object?.prefab;
    if (open == null || scene == null || object == null || source == null) {
      return;
    }

    final prefab = _readPrefab(source);
    if (prefab == null) return;

    final made = prefab.resyncing(
      scene,
      id,
      nextId: _nextObjectId,
      source: source,
    );
    _run(
      ReplaceSubtree(
        sceneId: open.id,
        rootId: made.rootId,
        objects: made.objects,
        what: object.name,
      ),
    );
    _select(made.rootId);
  }

  /// Brings every instance of one prefab, in every loaded scene, into line
  /// with it. Returns how many were changed.
  int _syncInstances(Prefab prefab, String source, {String? except}) {
    var touched = 0;

    for (final entry in _workspace.entries) {
      final scene = entry.scene;
      if (scene == null) continue;

      // Roots only: an instance nested inside another instance is replaced by
      // its parent's own resync, and doing both would replace it twice.
      final roots = [
        for (final object in scene.objects.toList())
          if (object.prefab == source &&
              object.id != except &&
              (object.parentId == null ||
                  scene[object.parentId!]?.prefab != source))
            object.id,
      ];

      for (final id in roots) {
        if (!scene.contains(id)) continue;
        final made = prefab.resyncing(
          scene,
          id,
          nextId: _nextObjectId,
          source: source,
        );
        _run(
          ReplaceSubtree(
            sceneId: entry.id,
            rootId: made.rootId,
            objects: made.objects,
            what: scene[id]?.name ?? prefab.name,
            label_: 'Update',
          ),
        );
        touched++;
      }
    }

    return touched;
  }

  /// Cuts an instance loose from its prefab.
  void _unpackPrefab(String id) {
    final open = _workspace.sceneHolding(id);
    final scene = open?.scene;
    final object = scene?[id];
    if (open == null || scene == null || object == null) return;

    _run(
      UnpackPrefab(
        sceneId: open.id,
        ids: [id, for (final child in scene.descendantsOf(id)) child.id],
        what: object.name,
      ),
    );
  }
}

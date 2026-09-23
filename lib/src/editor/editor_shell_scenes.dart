part of 'editor_shell.dart';

// A scene as a file: listed, opened, saved, closed, and what is said about
// it when any of that goes wrong.

extension _Scenes on _EditorShellState {
  String _defaultScenePath() =>
      p.join(widget.project.directory, 'scenes', 'main$sceneExtension');

  /// Whether a scene has changes that are not on disk.
  bool _isUnsaved(SceneEntry entry) =>
      entry.isLoaded &&
      (entry.neverWritten || _history.stampFor(entry.id) != entry.savedStamp);

  bool get _anyUnsaved => _workspace.entries.any(_isUnsaved);

  /// Lists the project's other scenes without loading them.
  void _listSiblingScenes() {
    final folder = Directory(p.join(widget.project.directory, 'scenes'));
    if (!folder.existsSync()) return;

    for (final file in folder.listSync().whereType<File>()) {
      if (p.extension(file.path) != sceneExtension) continue;
      if (_workspace.entryFor(file.path) != null) continue;
      _workspace.add(
        SceneEntry(
          id: 'scene${_nextSceneId++}',
          name: p.basenameWithoutExtension(file.path),
          path: file.path,
        ),
      );
    }
  }

  /// Reads a scene file without touching any state.
  ({EditorScene scene, String? path, bool isNew, List<String> problems}) _read(
    String path, {
    bool quiet = false,
  }) {
    final file = File(path);
    if (!file.existsSync()) {
      return (
        scene: EditorScene.starter(),
        path: path,
        isNew: true,
        problems: quiet ? const <String>[] : ['There is no scene at $path.'],
      );
    }

    try {
      final load = SceneDocument.decode(
        file.readAsStringSync(),
        prefabs: _prefabs.find,
      );
      return (
        scene: load.scene,
        path: path,
        isNew: false,
        problems: load.problems,
      );
    } on SceneFormatException catch (error) {
      return (
        scene: EditorScene.starter(),
        path: null,
        isNew: false,
        problems: [error.message],
      );
    } on FileSystemException catch (error) {
      return (
        scene: EditorScene.starter(),
        path: null,
        isNew: false,
        problems: ['Could not read that scene: ${error.message}'],
      );
    }
  }

  /// Loads a scene, replacing whatever was loaded.
  ///
  /// One at a time, so the viewport shows one document and there is never a
  /// question about which scene an edit belongs to. What was loaded is put
  /// back to being a name and a path — and if it had unsaved changes, that is
  /// asked about first, because unloading is the moment the work would be
  /// lost.
  Future<void> _loadScene(SceneEntry entry) async {
    if (entry.isLoaded) return;

    final leaving = _workspace.loaded;
    if (leaving != null && _isUnsaved(leaving)) {
      final answer = await _confirmLeaving(leaving);
      if (answer == null) return;
      if (answer) {
        _save(leaving);
        // Refused or failed, so the change is still only in memory.
        if (_isUnsaved(leaving)) return;
      }
    }

    final path = entry.path;
    if (path == null) {
      _say('${entry.title} has never been saved, so there is nothing to load.');
      return;
    }

    // A prefab nothing in memory is using is read again, so one changed in
    // another program since shows up in the scene about to be opened. One
    // still in use is not: what is open was opened against it.
    _prefabs.keepOnly(_prefabsInUse());
    final opened = _read(path);
    if (opened.path == null) {
      _report(opened.problems);
      return;
    }

    if (leaving != null) {
      // Its steps go with it: undoing into a scene that is not loaded would be
      // a step that appears to do nothing.
      _history.forget(leaving.id);
      _workspace.unload(leaving);
    }

    _workspace.load(entry, opened.scene);
    entry
      ..name = opened.scene.name
      ..neverWritten = false
      ..savedStamp = _history.stampFor(entry.id);

    setState(() {
      _selected.clear();
      _primary = null;
      _selectedScene = null;
      _camera = OrbitCamera();
      _reportedNotes.clear();
    });
    _report(opened.problems);
  }

  /// Lists a scene file and loads it.
  Future<void> _openScene(String path) async {
    final existing = _workspace.entryFor(path);
    if (existing != null) {
      await _loadScene(existing);
      return;
    }

    final entry = SceneEntry(
      id: 'scene${_nextSceneId++}',
      name: p.basenameWithoutExtension(path),
      path: path,
    );
    _workspace.add(entry);
    await _loadScene(entry);
  }

  void _report(List<String> problems) {
    if (problems.isEmpty) return;
    _say(
      problems.length == 1
          ? problems.single
          : '${problems.length} things in that scene could not be read. '
                'First: ${problems.first}',
    );
  }

  /// Writes the loaded scene back to the file it came from.
  ///
  /// Synchronous on purpose. An awaited write leaves a gap between encoding
  /// the scene and recording that it was saved — an edit landing in that gap
  /// is not in the file, but the history would call itself clean.
  void _save([SceneEntry? which]) {
    final entry = which ?? _current;
    final scene = entry?.scene;
    if (entry == null || scene == null) return;

    final path = entry.path;
    if (path == null) {
      _saveAs(entry);
      return;
    }

    try {
      final file = File(path);
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(
        SceneDocument.encode(scene, prefabs: _prefabs.find),
      );
    } on FileSystemException catch (error) {
      _say('Could not save ${entry.title}: ${error.message}');
      return;
    }

    setState(() {
      entry
        ..neverWritten = false
        ..savedStamp = _history.stampFor(entry.id);
    });
    _say('Saved ${_assets.relative(path)}');

    // What every scene has goes with whichever one was saved. Asking somebody
    // to save two files to keep one project consistent is asking them to
    // forget one of them.
    if (!identical(entry, _workspace.sharedEntry)) _saveShared();
  }

  /// Writes the shared set, if anything has happened to it.
  void _saveShared() {
    final entry = _workspace.sharedEntry;
    final scene = entry.scene;
    final path = entry.path;
    if (scene == null || path == null) return;
    if (entry.savedStamp == _history.stampFor(entry.id) &&
        !entry.neverWritten) {
      return;
    }
    // Nothing in it and never written: no reason to leave an empty file in
    // somebody's repository.
    if (scene.length == 0 && entry.neverWritten) return;

    try {
      final file = File(path);
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(
        SceneDocument.encode(scene, prefabs: _prefabs.find),
      );
    } on FileSystemException catch (error) {
      _say(
        'Could not save the shared objects: ${error.message}',
        level: LogLevel.error,
      );
      return;
    }

    setState(() {
      entry
        ..neverWritten = false
        ..savedStamp = _history.stampFor(entry.id);
    });
  }

  Future<void> _saveAs([SceneEntry? which]) async {
    final open = which ?? _current;
    if (open == null || !open.isLoaded) return;

    final name = await promptForName(
      context,
      title: 'Save scene as',
      initial: open.title,
      hint: 'Goes in scenes/, as $sceneExtension.',
      action: 'Save',
    );
    if (!mounted || name == null || name.isEmpty) return;

    if (name.contains(p.separator)) {
      _say('A scene name cannot contain a path.');
      return;
    }

    final path = _workspace.pathFor(name);
    if (File(path).existsSync() &&
        (open.path == null || !p.equals(path, open.path!))) {
      _say('There is already a scene called $name.');
      return;
    }

    setState(() => open.path = path);
    _save(open);
  }

  /// Starts a new scene, replacing whatever is loaded.
  Future<void> _newScene() async {
    final leaving = _workspace.loaded;
    if (leaving != null && _isUnsaved(leaving)) {
      final answer = await _confirmLeaving(leaving);
      if (answer == null) return;
      if (answer) {
        _save(leaving);
        if (_isUnsaved(leaving)) return;
      }
    }

    if (leaving != null) {
      _history.forget(leaving.id);
      _workspace.unload(leaving);
    }

    final name = _workspace.availableName('Untitled');
    final entry = SceneEntry(
      id: 'scene${_nextSceneId++}',
      name: name,
      neverWritten: true,
    );
    _workspace
      ..add(entry)
      ..load(entry, EditorScene.starter()..name = name);

    setState(() {
      _selected.clear();
      _primary = null;
      _selectedScene = null;
      _camera = OrbitCamera();
    });
  }

  /// Takes a scene off the list, asking first if it has changes.
  Future<void> _closeScene(SceneEntry entry) async {
    if (_isUnsaved(entry)) {
      final answer = await _confirmLeaving(entry);
      if (answer == null) return;
      if (answer) {
        _save(entry);
        if (_isUnsaved(entry)) return;
      }
    }

    _history.forget(entry.id);
    _workspace.remove(entry.id);
    setState(() {
      _selected.clear();
      _primary = null;
      if (_selectedScene == entry.id) _selectedScene = null;
    });
  }

  /// True to save, false to discard, null to stop.
  ///
  /// Asked whenever a scene with changes is about to stop being loaded —
  /// which is the only moment the work could be lost, and so the only moment
  /// worth interrupting for.
  Future<bool?> _confirmLeaving(SceneEntry open) async {
    final answer = await showDialog<String>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: OrblitColors.surface,
        title: Text('Save ${open.title} first?', style: OrblitText.title),
        content: Text(
          open.path == null
              ? 'It has never been written to disk. Closing it loses it.'
              : 'It has changes that have not been written to disk.',
          style: OrblitText.body,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop('cancel'),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop('discard'),
            child: const Text('Discard'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop('save'),
            child: const Text('Save'),
          ),
        ],
      ),
    );

    if (answer == 'save') return true;
    if (answer == 'discard') return false;
    return null;
  }

  void _reportSceneNotes(Map<String, String> notes) {
    // Said once each. The scene is republished on every frame of a drag, and
    // the same missing file would otherwise arrive a hundred times while
    // somebody moved the object that names it.
    _reportedNotes.removeWhere((subject) => !notes.containsKey(subject));

    final fresh = [
      for (final entry in notes.entries)
        if (_reportedNotes.add(entry.key)) entry,
    ];
    if (fresh.isEmpty) return;

    final first = fresh.first;
    // A path is worth shortening to its file name; a subject like "too many
    // lights" is not a path and is left as it is. p.basename leaves a bare
    // word alone (its own basename), so comparing against the original
    // tells the two apart without assuming '/' is the separator in use.
    final basename = p.basename(first.key);
    final subject = basename != first.key ? basename : null;
    _say(
      fresh.length == 1
          ? (subject == null ? first.value : '$subject: ${first.value}')
          : '${fresh.length} things in this scene need attention. '
                '${subject ?? first.key}: ${first.value}',
      level: LogLevel.warning,
      // Every one of them, not just the first: the status bar has room for
      // one line and the console does not.
      detail: [for (final note in fresh) '${note.key}: ${note.value}']
          .join('\n'),
    );
  }
}

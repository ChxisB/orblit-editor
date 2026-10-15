part of 'editor_shell.dart';

// Clips on the timeline: opened, saved, and shown on the loaded scene — and
// taken off it again whenever the scene is read as a document.

extension _Clips on _EditorShellState {
  /// Opens the clip at [path] on the timeline, in the Animation mode where
  /// the timeline is.
  void _openClip(String path) {
    _enterModeNamed('animation');
    if (!_readClip(path)) return;
    _followingMotion = false;
    _open(PanelKind.timeline);
  }

  bool _readClip(String path) {
    final List<String> problems;
    try {
      problems = _bench.openFile(path);
    } on ClipFormatException catch (error) {
      _say(
        '${p.basename(path)} is not a clip: ${error.message}',
        level: LogLevel.error,
      );
      return false;
    } on FileSystemException catch (error) {
      _say(
        '${p.basename(path)} could not be read: ${error.message}',
        level: LogLevel.error,
      );
      return false;
    }
    if (problems.isEmpty) return true;
    _say(
      problems.length == 1
          ? problems.single
          : '${problems.length} things in that clip could not be read. '
                'First: ${problems.first}',
      level: LogLevel.warning,
    );
    return true;
  }

  /// Makes a clip in the project's clips folder and opens it.
  void _newClip() {
    final path = _makeIn('clips', NewAsset.clip);
    if (path == null) return;
    _attachClip(path);
    _openClip(path);
    _bench.owner = _selectedObject?.id;
    _followingMotion = _selectedObject != null;
  }

  /// Asks which model the shown clip should move, and makes the answer a
  /// clip of its own beside the first. The first is left as it was.
  Future<void> _retargetClip() async {
    final shown = _bench.shown;
    if (shown == null) return;
    final owner = _bench.owner;
    final made = await showRetargetDialog(
      context,
      clip: shown.clip,
      files: _retargetFiles,
      from: owner == null ? null : _current?.scene?[owner]?.meshAsset,
    );
    if (!mounted || made == null) return;
    _saveRetargeted(made);
  }

  void _saveRetargeted(RetargetMade made) {
    final folder = Directory(p.join(widget.project.directory, 'clips'));
    try {
      folder.createSync(recursive: true);
    } on FileSystemException catch (error) {
      _say('Could not make the clips folder: ${error.message}');
      return;
    }
    final written = _assets.write(
      folder.path,
      '${made.clip.name}.oclip',
      made.clip.encode(),
    );
    final path = written.path;
    if (path == null) {
      _say('Could not save the clip: ${written.problem}');
      return;
    }
    _openClip(path);
    // Played by the model it was made for, so it goes on the one that is
    // chosen only when that is the model.
    final selected = _selectedObject;
    if (selected != null && selected.meshAsset == made.model) {
      _attachClip(path);
      _bench.owner = selected.id;
      _followingMotion = true;
    }
    _say(
      made.problems.isEmpty
          ? 'Made ${p.basename(path)}.'
          : 'Made ${p.basename(path)}. ${made.problems.length} things could '
                'not be carried. First: ${made.problems.first}',
      level: made.problems.isEmpty ? LogLevel.info : LogLevel.warning,
    );
  }

  /// Links the asset through the same component the game reads.
  void _attachClip(String path) {
    final object = _selectedObject;
    final entry = _current;
    if (object == null || entry == null) return;
    final before = motionOf(object);
    final relative = _assets.relative(path);
    if (before?.clips.contains(relative) ?? false) return;
    _history.seal();
    _history.run(
      SetObjectComponent(
        sceneId: entry.id,
        id: object.id,
        label: 'Add animation to ${object.name}',
        type: doc.SceneComponents.motion,
        from: before,
        to: doc.MotionComponent(
          clips: [...?before?.clips, relative],
          autoplay: before == null ? relative : before.autoplay,
        ),
      ),
    );
    _history.seal();
  }

  void _followMotion({bool force = false}) {
    final object = _selectedObject;
    final motion = object == null ? null : motionOf(object);
    final selection = (_current?.id, object?.id, motion);
    if (!force && selection == _motionSelection) return;
    _motionSelection = selection;
    if (_mode.name != 'scene' && _mode.name != 'animation') return;
    if (motion == null) {
      if (_followingMotion) _bench.hide();
      _followingMotion = false;
      _foldMotionPanel();
      return;
    }
    _bench.hide();
    _followingMotion = true;
    final first = motion.clips.firstOrNull;
    if (first != null && _readClip(p.join(widget.project.directory, first))) {
      _bench.owner = object!.id;
    }
    if (_mode.name == 'scene') {
      _layout = _layout.openBottom(
        const DockPanel(id: 'timeline', kind: PanelKind.timeline),
      );
    }
  }

  void _foldMotionPanel() {
    if (_mode.name != 'scene') return;
    final group = _layout.groupOf('timeline');
    if (group?.current?.id == 'timeline') {
      _layout = _layout.collapse(group!.id);
    }
  }

  /// Writes every clip and cutscene with changes, and says which could not
  /// be written.
  void _saveClips() {
    for (final problem in [..._bench.saveAll(), ..._cuts.saveAll()]) {
      _say(problem, level: LogLevel.error);
    }
  }

  /// Runs [read] on the loaded scene as it rests.
  ///
  /// The clip's pose comes off first and goes back on after, so what is
  /// saved, copied or made into a prefab is the scene and not wherever the
  /// playhead happened to be. A change made in between is made to the scene
  /// at rest, and the pose goes back on top of it.
  T _atRest<T>(T Function() read) {
    final scene = _current?.scene;
    if (scene == null || !_preview.posing) return read();
    _preview.lift(scene);
    try {
      return read();
    } finally {
      _onBenchChanged();
    }
  }
}

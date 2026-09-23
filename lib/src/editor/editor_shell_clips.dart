part of 'editor_shell.dart';

// Clips on the timeline: opened, saved, and shown on the loaded scene — and
// taken off it again whenever the scene is read as a document.

extension _Clips on _EditorShellState {
  /// Opens the clip at [path] on the timeline, and the timeline with it.
  void _openClip(String path) {
    final List<String> problems;
    try {
      problems = _bench.openFile(path);
    } on ClipFormatException catch (error) {
      _say(
        '${p.basename(path)} is not a clip: ${error.message}',
        level: LogLevel.error,
      );
      return;
    } on FileSystemException catch (error) {
      _say(
        '${p.basename(path)} could not be read: ${error.message}',
        level: LogLevel.error,
      );
      return;
    }
    _open(PanelKind.timeline);
    if (problems.isEmpty) return;
    _say(
      problems.length == 1
          ? problems.single
          : '${problems.length} things in that clip could not be read. '
                'First: ${problems.first}',
      level: LogLevel.warning,
    );
  }

  /// Writes every clip with changes, and says which could not be written.
  void _saveClips() {
    for (final problem in _bench.saveAll()) {
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

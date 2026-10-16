part of 'editor_shell.dart';

// Blends in the graph: opened, made, and saved with the clips.

const String _blendFolder = 'blends';

extension _Blends on _EditorShellState {
  /// Opens the blend at [path] in the graph, beside the timeline in the
  /// Animation workspace.
  void _openBlend(String path) {
    _enterModeNamed('animation');
    if (!_readBlend(path)) return;
    setState(() {
      _layout = _layout.openBottom(
        const DockPanel(id: 'blendGraph', kind: blendGraphPanel),
      );
    });
  }

  bool _readBlend(String path) {
    final List<String> problems;
    try {
      problems = _blends.openFile(path);
    } on BlendFormatException catch (error) {
      _say(
        '${p.basename(path)} is not a blend: ${error.message}',
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
          : '${problems.length} things in that blend could not be read. '
                'First: ${problems.first}',
      level: LogLevel.warning,
    );
    return true;
  }

  /// Makes a blend in the project's blends folder and opens it.
  void _newBlend() {
    final path = _makeIn(_blendFolder, NewAsset.blend);
    if (path != null) _openBlend(path);
  }

  /// The names of the project's clips, which is what a state is offered to
  /// play. Whoever plays the blend decides what each is called, so these
  /// are suggestions and not a check.
  List<String> _clipNames() => [
    for (final path in _assets.find((asset) => asset.kind == AssetKind.clip))
      p.basenameWithoutExtension(path),
  ];
}

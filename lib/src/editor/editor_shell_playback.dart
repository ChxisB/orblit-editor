part of 'editor_shell.dart';

extension _Playback on _EditorShellState {
  void _togglePlayback() {
    if (_playing) {
      _playTicker.stop();
      setState(() => _playing = false);
      return;
    }
    if (_current?.scene == null) return;
    _bench.playing = false;
    _cuts.playing = false;
    _playback ??= _atRest(
      () => ScenePlayback(
        _workspace,
        read: _clipForPlayback,
        cutscenes: _cutscenesForPlayback(),
      ),
    );
    _lastPlaybackTick = null;
    _playTicker.start();
    setState(() {
      _playing = true;
      _focusedModes.remove(_mode.name);
      _layout = _layout.add(
        const DockPanel(id: 'game', kind: PanelKind.game),
        intoGroup:
            _layout.groupOf('scene')?.id ?? _layout.groupOf('canvas')?.id,
      );
    });
  }

  ClipDocument? _clipForPlayback(String relative) {
    final path = p.join(widget.project.directory, relative);
    final open = _bench[path];
    if (open != null) return open.clip;
    try {
      return ClipDocument.decode(File(path).readAsStringSync()).clip;
    } on ClipFormatException catch (error) {
      _say('$relative is not a clip: ${error.message}', level: LogLevel.error);
    } on FileSystemException catch (error) {
      _say(
        '$relative could not be read: ${error.message}',
        level: LogLevel.error,
      );
    }
    return null;
  }

  void _tickPlayback(Duration elapsed) {
    final previous = _lastPlaybackTick ?? elapsed;
    _lastPlaybackTick = elapsed;
    _playback?.advance((elapsed - previous).inMicroseconds / 1000000);
    _rebuildForMove();
  }

  void _stopPlayback() {
    _playTicker.stop();
    _playback?.dispose();
    _playback = null;
    setState(() => _playing = false);
  }
}

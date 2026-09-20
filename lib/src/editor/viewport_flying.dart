// setState on `this` from an extension is exactly what @protected asks
// for; the analyzer does not count an extension body as inside the class.
// ignore_for_file: invalid_use_of_protected_member

part of 'viewport.dart';

// Flying the camera on WASD, which is a mode: the viewport takes the
// keyboard while it lasts and gives it back when it ends.

// Not const: LogicalKeyboardKey defines ==, and a constant set may not hold
// anything that does.
final _forwardKeys = {LogicalKeyboardKey.keyW, LogicalKeyboardKey.arrowUp};
final _backKeys = {LogicalKeyboardKey.keyS, LogicalKeyboardKey.arrowDown};
final _leftKeys = {LogicalKeyboardKey.keyA, LogicalKeyboardKey.arrowLeft};
final _rightKeys = {LogicalKeyboardKey.keyD, LogicalKeyboardKey.arrowRight};
final _upKeys = {LogicalKeyboardKey.keyE, LogicalKeyboardKey.space};
final _downKeys = {LogicalKeyboardKey.keyQ};

/// Every key flying answers to, so one held down is not also passed on to
/// whatever else is listening.
final _flyKeys = {
  ..._forwardKeys,
  ..._backKeys,
  ..._leftKeys,
  ..._rightKeys,
  ..._upKeys,
  ..._downKeys,
  LogicalKeyboardKey.shiftLeft,
  LogicalKeyboardKey.shiftRight,
};

extension _Flying on _SceneViewportState {
  bool get _flying => _looking != null || _flyLocked;

  bool _anyHeld(Set<LogicalKeyboardKey> keys) => _held.any(keys.contains);

  /// Which way the keys held down add up to, in right/up/forward.
  Vector3 get _wanted {
    final along = Vector3.zero();
    if (_anyHeld(_rightKeys)) along.x += 1;
    if (_anyHeld(_leftKeys)) along.x -= 1;
    if (_anyHeld(_upKeys)) along.y += 1;
    if (_anyHeld(_downKeys)) along.y -= 1;
    if (_anyHeld(_forwardKeys)) along.z += 1;
    if (_anyHeld(_backKeys)) along.z -= 1;
    // Normalised, or holding two keys would go a metre and a half diagonally
    // for every metre going straight.
    return along.length2 == 0 ? along : along.normalized();
  }

  KeyEventResult _onFlyKey(FocusNode node, KeyEvent event) {
    // The one key that works whether or not the view is already flying.
    if (event is KeyDownEvent &&
        event.logicalKey == LogicalKeyboardKey.backquote) {
      _toggleFlying();
      return KeyEventResult.handled;
    }

    if (_flying &&
        event is KeyDownEvent &&
        event.logicalKey == LogicalKeyboardKey.escape) {
      setState(_stopFlying);
      _syncClock();
      return KeyEventResult.handled;
    }

    // Everything else only while flying. Otherwise W would fly the view every
    // time somebody typed a name into the inspector.
    if (!_flying) return KeyEventResult.ignored;

    if (event is KeyDownEvent) {
      _held.add(event.logicalKey);
    } else if (event is KeyUpEvent) {
      _held.remove(event.logicalKey);
    }

    return _flyKeys.contains(event.logicalKey)
        ? KeyEventResult.handled
        : KeyEventResult.ignored;
  }

  void _stopFlying() {
    _looking = null;
    _flyLocked = false;
    _held.clear();
  }

  /// Switches flying on or off, for the trackpad.
  void _toggleFlying() {
    setState(() {
      if (_flying) {
        _stopFlying();
      } else {
        _flyLocked = true;
        _lastFlew = _elapsed;
        _flyFocus.requestFocus();
      }
    });
    _syncClock();
  }

  /// Moves the camera for one frame of held keys.
  void _fly(Duration elapsed) {
    if (!_flying) return;

    final along = _wanted;
    if (along.length2 == 0) return;

    final seconds = (elapsed - _lastFlew).inMicroseconds / 1e6;
    _lastFlew = elapsed;
    // Clamped: a frame that took a second — a rebuild, a breakpoint — should
    // not throw the camera across the level.
    final step = seconds.clamp(0.0, 0.05);

    final fast =
        _held.contains(LogicalKeyboardKey.shiftLeft) ||
        _held.contains(LogicalKeyboardKey.shiftRight);
    widget.onCameraChanged(
      widget.camera.flying(along * (_flySpeed * (fast ? 4 : 1) * step)),
    );
  }
}

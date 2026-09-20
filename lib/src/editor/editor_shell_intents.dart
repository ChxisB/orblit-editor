part of 'editor_shell.dart';

// What a keystroke means, before anything decides what to do
// about it. Flutter's Intents: one class a verb, no behaviour.

class _UndoIntent extends Intent {}

class _RedoIntent extends Intent {}

class _DeleteIntent extends Intent {}

class _FrameIntent extends Intent {}

class _SaveIntent extends Intent {}

class _SaveAsIntent extends Intent {}

class _NewSceneIntent extends Intent {}

class _CopyIntent extends Intent {}

class _CutIntent extends Intent {}

class _PasteIntent extends Intent {}

class _DuplicateIntent extends Intent {}

/// Back out of a mesh, to the object it belongs to.
class _LeaveEditIntent extends Intent {
  const _LeaveEditIntent();
}

/// Round the three ways of selecting part of a mesh.
/// Makes the grid coarser or finer.
class _GridIntent extends Intent {
  const _GridIntent(this.coarser);

  final bool coarser;
}

/// Finishes whatever is being drawn.
/// Moves the selection by whole squares.
class _NudgeIntent extends Intent {
  const _NudgeIntent(this.axis, this.squares);

  /// Which way, as a unit vector.
  final Vector3 axis;

  /// How many squares, signed.
  final int squares;
}

class _FinishDrawIntent extends Intent {
  const _FinishDrawIntent();
}

class _CycleModeIntent extends Intent {
  const _CycleModeIntent();
}

extension _Keys on _EditorShellState {
  // Which keystroke means which of them.
  Map<ShortcutActivator, Intent> get _shortcuts => {
    commandShortcut(LogicalKeyboardKey.keyZ): _UndoIntent(),
    commandShortcut(LogicalKeyboardKey.keyZ, shift: true): _RedoIntent(),
    // Windows and Linux both also expect Ctrl+Y for redo, alongside the
    // Ctrl+Shift+Z that commandShortcut above already binds; macOS has no
    // second convention to match.
    if (!commandIsMeta)
      const SingleActivator(LogicalKeyboardKey.keyY, control: true):
          _RedoIntent(),
    const SingleActivator(LogicalKeyboardKey.delete): _DeleteIntent(),
    const SingleActivator(LogicalKeyboardKey.backspace): _DeleteIntent(),
    const SingleActivator(LogicalKeyboardKey.keyF): _FrameIntent(),
    // The two keys a modelling tool has. Escape comes back out of the
    // geometry and G goes round the three ways of selecting it, which is
    // what somebody presses without thinking about it.
    const SingleActivator(LogicalKeyboardKey.escape): _LeaveEditIntent(),
    const SingleActivator(LogicalKeyboardKey.enter): _FinishDrawIntent(),
    const SingleActivator(LogicalKeyboardKey.numpadEnter):
        _FinishDrawIntent(),
    const SingleActivator(LogicalKeyboardKey.keyG): _CycleModeIntent(),
    // The brackets, which is where every tool with a brush size puts
    // them.
    const SingleActivator(LogicalKeyboardKey.bracketRight): _GridIntent(
      true,
    ),
    const SingleActivator(LogicalKeyboardKey.bracketLeft): _GridIntent(
      false,
    ),
    // Whole squares at a time, which is the one way of placing something
    // that needs no aim at all. The arrows work the floor, because that
    // is where things are arranged; shift takes them up and down, and
    // holding option does ten at once.
    ..._nudges,
    commandShortcut(LogicalKeyboardKey.keyS): _SaveIntent(),
    commandShortcut(LogicalKeyboardKey.keyS, shift: true): _SaveAsIntent(),
    commandShortcut(LogicalKeyboardKey.keyN): _NewSceneIntent(),
    commandShortcut(LogicalKeyboardKey.keyC): _CopyIntent(),
    commandShortcut(LogicalKeyboardKey.keyX): _CutIntent(),
    commandShortcut(LogicalKeyboardKey.keyV): _PasteIntent(),
    commandShortcut(LogicalKeyboardKey.keyD): _DuplicateIntent(),
  };

  // And what to do about each one.
  Map<Type, Action<Intent>> get _actions => {
    _LeaveEditIntent: CallbackAction<_LeaveEditIntent>(
      onInvoke: (_) {
        // A drawing first: somebody halfway through an outline who
        // presses escape means the outline, not the geometry.
        if (_drawing.tool.isDrawing) {
          setState(_drawing.clear);
          return null;
        }
        _setContext(EditContext.object);
        return null;
      },
    ),
    _FinishDrawIntent: CallbackAction<_FinishDrawIntent>(
      onInvoke: (_) {
        if (_drawing.tool.isDrawing) _finishDrawing();
        return null;
      },
    ),
    _NudgeIntent: CallbackAction<_NudgeIntent>(
      onInvoke: (intent) {
        _nudge(intent.axis, intent.squares);
        return null;
      },
    ),
    _GridIntent: CallbackAction<_GridIntent>(
      onInvoke: (intent) {
        setState(() {
          _snapping.step = intent.coarser
              ? _snapping.coarser
              : _snapping.finer;
          // Changing the grid turns it on: somebody reaching for the key
          // is asking about the grid, and answering with a size that does
          // nothing is the wrong answer.
          _snapping.on = true;
        });
        return null;
      },
    ),
    _CycleModeIntent: CallbackAction<_CycleModeIntent>(
      onInvoke: (_) {
        // Into the geometry if not already, then round the modes: one key
        // that always does the obvious next thing.
        if (_context != EditContext.element) {
          _setContext(EditContext.element);
        } else {
          setState(() => _elementMode = _elementMode.next);
        }
        return null;
      },
    ),
    _UndoIntent: CallbackAction<_UndoIntent>(onInvoke: (_) => _undo()),
    _RedoIntent: CallbackAction<_RedoIntent>(onInvoke: (_) => _redo()),
    _DeleteIntent: CallbackAction<_DeleteIntent>(
      onInvoke: (_) {
        // While drawing, backspace takes back the last point rather
        // than deleting what happens to be selected — which would be a
        // very unwelcome surprise halfway through an outline.
        if (_drawing.tool.isDrawing) {
          setState(_drawing.undo);
          return null;
        }
        _deleteSelection();
        return null;
      },
    ),
    _FrameIntent: CallbackAction<_FrameIntent>(
      onInvoke: (_) {
        _frameSelection();
        return null;
      },
    ),
    _SaveIntent: CallbackAction<_SaveIntent>(
      onInvoke: (_) {
        _save();
        return null;
      },
    ),
    _SaveAsIntent: CallbackAction<_SaveAsIntent>(
      onInvoke: (_) {
        _saveAs();
        return null;
      },
    ),
    _NewSceneIntent: CallbackAction<_NewSceneIntent>(
      onInvoke: (_) {
        _newScene();
        return null;
      },
    ),
    _CopyIntent: CallbackAction<_CopyIntent>(onInvoke: (_) => _copy()),
    _CutIntent: CallbackAction<_CutIntent>(onInvoke: (_) => _cut()),
    _PasteIntent: CallbackAction<_PasteIntent>(onInvoke: (_) => _paste()),
    _DuplicateIntent: CallbackAction<_DuplicateIntent>(
      onInvoke: (_) => _duplicate(),
    ),
  };
}

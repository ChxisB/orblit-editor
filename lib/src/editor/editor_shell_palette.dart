part of 'editor_shell.dart';

// What the command palette can find: the commands the menus and the keyboard
// already have, and the objects in the scene.

PaletteEntry _command(
  String label,
  IconData icon,
  VoidCallback onRun, {
  String? keys,
  String meta = '',
}) => PaletteEntry(
  group: PaletteGroup.commands,
  label: label,
  icon: icon,
  onRun: onRun,
  keys: keys,
  meta: meta,
);

extension _Palette on _EditorShellState {
  Future<void> _openPalette() async {
    final chosen = await showCommandPalette(context, _paletteEntries());
    // Run once the palette has gone, so a command that opens a dialog does it
    // over the editor and not under the palette's barrier.
    if (chosen != null && mounted) chosen.onRun();
  }

  List<PaletteEntry> _paletteEntries() => [
    ..._fileCommands(),
    ..._editCommands(),
    ..._addCommands(),
    ..._viewCommands(),
    ..._objectEntries(),
  ];

  List<PaletteEntry> _fileCommands() => [
    _command(
      'New scene',
      Icons.note_add_outlined,
      () => _newScene(),
      keys: commandShortcutLabel('N'),
    ),
    _command(
      'Save',
      Icons.save_outlined,
      _save,
      keys: commandShortcutLabel('S'),
    ),
    _command(
      'Save as…',
      Icons.save_as_outlined,
      _saveAs,
      keys: commandShortcutLabel('S', shift: true),
    ),
    _command('Open in code editor', Icons.code, _openInCode),
    _command('All projects', Icons.chevron_left, widget.onClose),
  ];

  List<PaletteEntry> _editCommands() => [
    _command(
      'Undo',
      Icons.undo,
      _undo,
      keys: commandShortcutLabel('Z'),
      meta: _history.undoLabel ?? '',
    ),
    _command(
      'Redo',
      Icons.redo,
      _redo,
      keys: commandShortcutLabel('Z', shift: true),
      meta: _history.redoLabel ?? '',
    ),
    _command('Copy', Icons.copy, _copy, keys: commandShortcutLabel('C')),
    _command('Cut', Icons.content_cut, _cut, keys: commandShortcutLabel('X')),
    _command(
      'Paste',
      Icons.content_paste,
      _paste,
      keys: commandShortcutLabel('V'),
    ),
    _command(
      'Duplicate',
      Icons.control_point_duplicate,
      _duplicate,
      keys: commandShortcutLabel('D'),
    ),
    _command('Delete', Icons.delete_outline, _deleteSelection, keys: '⌫'),
    _command(
      'Frame selection',
      Icons.center_focus_strong,
      _frameSelection,
      keys: 'F',
    ),
  ];

  List<PaletteEntry> _addCommands() => [
    for (final shape in ShapeKind.values)
      _command(
        'Add ${shape.label}',
        Icons.category_outlined,
        () => _addShape(shape),
        meta: 'Shape',
      ),
    for (final (kind, label, icon) in _AddMenu._items)
      _command('Add $label', icon, () => _add(kind)),
    _command('Add Terrain', Icons.landscape_outlined, _newTerrain),
  ];

  List<PaletteEntry> _viewCommands() => [
    for (final mode in _registry.modes.all)
      _command(
        'Go to ${mode.label}',
        mode.icon,
        () => _enterMode(mode),
        meta: 'Workspace',
      ),
    _command(
      _playing ? 'Pause' : 'Play',
      _playing ? Icons.pause : Icons.play_arrow,
      _togglePlayback,
    ),
    _command('Stop', Icons.stop, _stopPlayback),
    _command(
      _focusedModes.contains(_mode.name) ? 'Show panels' : 'Focus view',
      Icons.fullscreen,
      _toggleFocus,
    ),
    _command(
      'Reset panels',
      Icons.view_quilt_outlined,
      () => _relayout(_mode.layout().copyWith(locked: _layout.locked)),
    ),
    _command(
      'Toggle stats',
      Icons.speed,
      () => setState(() => _showStats = !_showStats),
    ),
  ];

  List<PaletteEntry> _objectEntries() {
    final scene = _current?.scene;
    if (scene == null) return const [];
    return [
      for (final object in scene.objects)
        PaletteEntry(
          group: PaletteGroup.objects,
          label: object.name,
          icon: object.icon,
          // Where it sits, since a scene of forty crates has forty of the
          // same word and the group is what tells them apart.
          meta: scene[object.parentId ?? '']?.name ?? '',
          onRun: () {
            _select(object.id);
            _frameSelection();
          },
        ),
    ];
  }
}

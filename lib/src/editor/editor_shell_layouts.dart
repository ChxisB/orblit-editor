part of 'editor_shell.dart';

extension _Layouts on _EditorShellState {
  Widget _viewMenu() => _ViewMenu(
    layout: _layout,
    onLayout: _relayout,
    panels: _registry.panels.all,
    modeLayout: _mode.layout,
    focused: _focusedModes.contains(_mode.name),
    onFocus: _toggleFocus,
    stats: _showStats,
    onStats: () => setState(() => _showStats = !_showStats),
    saved: _layoutLibrary.names(_mode.name),
    onSave: _saveLayout,
    onLoad: _loadLayout,
    onDelete: _deleteLayout,
  );

  void _toggleFocus() => setState(() {
    if (!_focusedModes.remove(_mode.name)) _focusedModes.add(_mode.name);
  });

  DockLayout get _visibleLayout {
    if (!_focusedModes.contains(_mode.name)) return _layout;
    final centre = _layout.groupOf('scene') ?? _layout.groupOf('canvas');
    final panel =
        centre?.current ??
        _layout.panels.firstWhere(
          (panel) =>
              panel.kind == PanelKind.viewport ||
              panel.kind == interfaceCanvasPanel,
          orElse: () => _layout.panels.first,
        );
    return _layout.copyWith(
      locked: true,
      root: DockGroup(id: 'focused', panels: [panel]),
    );
  }

  Future<void> _saveLayout() async {
    final mode = _mode.name;
    final layout = _layout;
    final name = await promptForName(
      context,
      title: 'Save ${_mode.label} layout',
      initial: 'My layout',
      hint: 'An existing name replaces that saved layout.',
      action: 'Save',
    );
    if (!mounted || name == null || name.trim().isEmpty) return;
    try {
      _layoutLibrary.save(mode, name.trim(), layout);
      setState(() {});
    } on FileSystemException catch (error) {
      _say(
        'Could not save the layout: ${error.message}',
        level: LogLevel.error,
      );
    }
  }

  void _loadLayout(String name) {
    final saved = _layoutLibrary.find(_mode.name, name);
    if (saved == null) {
      _say('That layout could not be read.', level: LogLevel.error);
      return;
    }
    _focusedModes.remove(_mode.name);
    _relayout(saved.copyWith(revision: _mode.layout().revision));
  }

  void _deleteLayout(String name) {
    try {
      _layoutLibrary.remove(_mode.name, name);
      setState(() {});
    } on FileSystemException catch (error) {
      _say(
        'Could not delete the layout: ${error.message}',
        level: LogLevel.error,
      );
    }
  }
}

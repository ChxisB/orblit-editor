part of 'editor_shell.dart';

// The frame around the panels: the bar above them with the menus, the
// modes and the transport on it, the mode's tool shelf under it, the bar
// below, and the handle between two panels.

/// The bar between the viewport and the project browser.
class _Splitter extends StatefulWidget {
  const _Splitter({required this.onDrag});

  final ValueChanged<double> onDrag;

  @override
  State<_Splitter> createState() => _SplitterState();
}

class _SplitterState extends State<_Splitter> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.resizeRow,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onVerticalDragUpdate: (details) => widget.onDrag(details.delta.dy),
        child: Container(
          height: 6,
          color: _hovering ? OrblitColors.line : Colors.transparent,
        ),
      ),
    );
  }
}

/// Menus on the left, the modes in the middle, and running the game on the
/// right, all on one bar so the panels start as high up the window as they
/// can.
class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.modes,
    required this.mode,
    required this.onMode,
    required this.project,
    required this.playing,
    required this.history,
    required this.dirty,
    required this.onPlay,
    required this.onClose,
    required this.onAdd,
    required this.onAddShape,
    required this.onAddTerrain,
    required this.onSave,
    required this.onSaveAs,
    required this.onNewScene,
    required this.onOpenInCode,
    required this.onReveal,
    required this.layout,
    required this.onLayout,
    required this.panels,
    required this.onUndo,
    required this.onRedo,
    required this.selectionCount,
    required this.clipboard,
    required this.onCopy,
    required this.onCut,
    required this.onPaste,
    required this.onDuplicate,
  });

  final List<EditorMode> modes;
  final EditorMode mode;
  final ValueChanged<EditorMode> onMode;

  final Project project;
  final bool playing;
  final History history;
  final bool dirty;
  final VoidCallback onPlay;
  final VoidCallback onClose;
  final ValueChanged<ObjectKind> onAdd;
  final ValueChanged<ShapeKind> onAddShape;
  final VoidCallback onAddTerrain;
  final VoidCallback onSave;
  final VoidCallback onSaveAs;

  final VoidCallback onNewScene;
  final VoidCallback onOpenInCode;
  final VoidCallback onReveal;

  /// How the panels are arranged, and how to change it.
  final DockLayout layout;
  final ValueChanged<DockLayout> onLayout;

  /// The panels there are to open.
  final List<PanelType> panels;
  final VoidCallback onUndo;
  final VoidCallback onRedo;
  final int selectionCount;

  /// What is on the clipboard, or empty for nothing.
  final String clipboard;

  final VoidCallback onCopy;
  final VoidCallback onCut;
  final VoidCallback onPaste;
  final VoidCallback onDuplicate;

  @override
  Widget build(BuildContext context) {
    final menus = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        OrblitButton(
          label: project.name,
          icon: Icons.chevron_left,
          tone: ButtonTone.flat,
          onPressed: onClose,
        ),
        const _BarDivider(),
        _SceneMenu(
          dirty: dirty,
          onSave: onSave,
          onSaveAs: onSaveAs,
          onNewScene: onNewScene,
          onOpenInCode: onOpenInCode,
          onReveal: onReveal,
        ),
        _AddMenu(
          onAdd: onAdd,
          onAddShape: onAddShape,
          onAddTerrain: onAddTerrain,
        ),
        _EditMenu(
          selectionCount: selectionCount,
          clipboard: clipboard,
          onCopy: onCopy,
          onCut: onCut,
          onPaste: onPaste,
          onDuplicate: onDuplicate,
        ),
        _ViewMenu(
          layout: layout,
          onLayout: onLayout,
          panels: panels,
          modeLayout: mode.layout,
        ),
        const _BarDivider(),
        // Labelled with what they would undo, so the tooltip answers the
        // question somebody actually has before they press it.
        _TransportButton(
          icon: Icons.undo,
          tooltip: history.undoLabel == null
              ? 'Nothing to undo'
              : 'Undo ${history.undoLabel}',
          active: false,
          enabled: history.canUndo,
          onTap: onUndo,
        ),
        _TransportButton(
          icon: Icons.redo,
          tooltip: history.redoLabel == null
              ? 'Nothing to redo'
              : 'Redo ${history.redoLabel}',
          active: false,
          enabled: history.canRedo,
          onTap: onRedo,
        ),
      ],
    );

    final run = Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        const Text('pre-alpha', style: OrblitText.caption),
        const SizedBox(width: Space.md),
        // Set apart on a pad of its own, since it is the one control on the
        // bar that leaves the editor rather than changing something in it.
        Container(
          padding: const EdgeInsets.all(2),
          decoration: BoxDecoration(
            color: OrblitColors.surface,
            borderRadius: BorderRadius.circular(Radii.control + 2),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              _TransportButton(
                icon: playing ? Icons.pause : Icons.play_arrow,
                tooltip: playing ? 'Pause' : 'Play',
                active: playing,
                onTap: onPlay,
              ),
              _TransportButton(
                icon: Icons.stop,
                tooltip: 'Stop',
                active: false,
                onTap: () {},
              ),
            ],
          ),
        ),
      ],
    );

    return Container(
      height: 40,
      padding: const EdgeInsets.symmetric(horizontal: Space.sm),
      // The same colour as the gaps between the panels, so the bar is part of
      // the frame the panels sit in rather than one more panel on top.
      color: OrblitColors.ground,
      child: CustomMultiChildLayout(
        delegate: _BarLayout(),
        children: [
          LayoutId(id: _BarSlot.start, child: menus),
          // With one mode there is nothing to switch between, and a single
          // tab would only be a label saying what the editor is.
          if (modes.length > 1)
            LayoutId(
              id: _BarSlot.middle,
              child: _ModeTabs(modes: modes, mode: mode, onMode: onMode),
            ),
          LayoutId(id: _BarSlot.end, child: run),
        ],
      ),
    );
  }
}

enum _BarSlot { start, middle, end }

/// Lays out the top bar with its middle in the middle of the window.
///
/// Not in the middle of the room left between the two ends: the ends are
/// different widths, and modes that shift along whenever a menu gains an
/// unsaved dot are modes somebody has to look for. Pushed aside only when an
/// end would otherwise run into them.
class _BarLayout extends MultiChildLayoutDelegate {
  @override
  void performLayout(Size size) {
    final loose = BoxConstraints.loose(size);
    final start = layoutChild(_BarSlot.start, loose);
    final end = layoutChild(
      _BarSlot.end,
      loose.copyWith(maxWidth: (size.width - start.width).clamp(0, size.width)),
    );
    positionChild(_BarSlot.start, Offset(0, (size.height - start.height) / 2));
    positionChild(
      _BarSlot.end,
      Offset(size.width - end.width, (size.height - end.height) / 2),
    );

    if (!hasChild(_BarSlot.middle)) return;
    const gap = Space.lg;
    final room = size.width - start.width - end.width - gap * 2;
    final middle = layoutChild(
      _BarSlot.middle,
      loose.copyWith(maxWidth: room.clamp(0, size.width)),
    );
    final lowest = start.width + gap;
    final highest = size.width - end.width - gap - middle.width;
    final centred = (size.width - middle.width) / 2;
    positionChild(
      _BarSlot.middle,
      Offset(
        highest < lowest ? lowest : centred.clamp(lowest, highest),
        (size.height - middle.height) / 2,
      ),
    );
  }

  @override
  bool shouldRelayout(_BarLayout oldDelegate) => false;
}

/// A short upright line between groups of controls on the bar.
class _BarDivider extends StatelessWidget {
  const _BarDivider();

  @override
  Widget build(BuildContext context) => Container(
    width: 1,
    height: 18,
    margin: const EdgeInsets.symmetric(horizontal: Space.sm),
    color: OrblitColors.line,
  );
}

/// How much of each workspace tab the bar has room for.
enum _TabFit { iconAndWord, word, icon }

/// The workspace tabs, with as much of each as the bar has room for.
///
/// The icon goes first and the word last, because the word is what tells
/// somebody new what a tab is for. Only a window too narrow for the words
/// gets icons alone, and those say their word when pointed at.
final class _ModeTabs extends StatelessWidget {
  const _ModeTabs({
    required this.modes,
    required this.mode,
    required this.onMode,
  });

  final List<EditorMode> modes;
  final EditorMode mode;
  final ValueChanged<EditorMode> onMode;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final fit = _fitIn(context, constraints.maxWidth);
      return Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          for (final each in modes)
            _ModeTab(
              key: ValueKey('mode/${each.name}'),
              mode: each,
              fit: fit,
              selected: each.name == mode.name,
              onTap: () => onMode(each),
            ),
        ],
      );
    },
  );

  _TabFit _fitIn(BuildContext context, double room) {
    // Merged as Text merges it, or an inherited letter spacing makes the
    // words wider on screen than they measured.
    final style = DefaultTextStyle.of(context).style.merge(_ModeTab.wording);
    var words = 0.0;
    for (final each in modes) {
      final painter = TextPainter(
        text: TextSpan(text: each.label, style: style),
        textDirection: TextDirection.ltr,
        textScaler: MediaQuery.textScalerOf(context),
      )..layout();
      words += painter.width;
      painter.dispose();
    }
    final padding = modes.length * _ModeTab.padding * 2;
    final icons = modes.length * (_ModeTab.iconSize + _ModeTab.iconGap);
    if (padding + icons + words <= room) return _TabFit.iconAndWord;
    if (padding + words <= room) return _TabFit.word;
    return _TabFit.icon;
  }
}

/// One mode on the top bar. The one the editor is in is lit, not boxed: the
/// row is a choice of what the middle of the window is for, not a row of
/// buttons.
class _ModeTab extends StatefulWidget {
  const _ModeTab({
    super.key,
    required this.mode,
    required this.fit,
    required this.selected,
    required this.onTap,
  });

  // What [_ModeTabs] measures a row of tabs by before it builds one.
  static const padding = Space.sm + 2;
  static const iconSize = 16.0;
  static const iconGap = Space.xs + 2;
  static final wording = OrblitText.label.copyWith(
    fontSize: 13,
    fontWeight: FontWeight.w600,
  );

  final EditorMode mode;
  final _TabFit fit;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_ModeTab> createState() => _ModeTabState();
}

class _ModeTabState extends State<_ModeTab> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final colour = widget.selected
        ? OrblitColors.ember
        : (_hovering ? OrblitColors.ink : OrblitColors.inkMid);

    final fit = widget.fit;
    // Colour alone does not tell a screen reader which workspace is open.
    final tab = Semantics(
      container: true,
      button: true,
      selected: widget.selected,
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hovering = true),
        onExit: (_) => setState(() => _hovering = false),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: widget.onTap,
          child: Padding(
            padding: const EdgeInsets.symmetric(
              horizontal: _ModeTab.padding,
              vertical: Space.xs,
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (fit != _TabFit.word)
                  Icon(
                    widget.mode.icon,
                    size: _ModeTab.iconSize,
                    color: colour,
                  ),
                if (fit == _TabFit.iconAndWord)
                  const SizedBox(width: _ModeTab.iconGap),
                if (fit != _TabFit.icon)
                  Text(
                    widget.mode.label,
                    style: _ModeTab.wording.copyWith(color: colour),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
    if (fit != _TabFit.icon) return tab;
    return Tooltip(message: widget.mode.label, child: tab);
  }
}

class _TransportButton extends StatefulWidget {
  const _TransportButton({
    required this.icon,
    required this.tooltip,
    required this.active,
    required this.onTap,
    this.enabled = true,
  });

  final IconData icon;
  final String tooltip;
  final bool active;
  final VoidCallback onTap;
  final bool enabled;

  @override
  State<_TransportButton> createState() => _TransportButtonState();
}

class _TransportButtonState extends State<_TransportButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: widget.tooltip,
      child: MouseRegion(
        cursor: widget.enabled
            ? SystemMouseCursors.click
            : SystemMouseCursors.basic,
        onEnter: (_) => setState(() => _hovering = true),
        onExit: (_) => setState(() => _hovering = false),
        child: GestureDetector(
          onTap: widget.enabled ? widget.onTap : null,
          child: Container(
            width: 32,
            height: 28,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: widget.active
                  ? OrblitColors.emberWash
                  : (_hovering && widget.enabled
                        ? OrblitColors.raised
                        : Colors.transparent),
              borderRadius: BorderRadius.circular(Radii.control),
            ),
            child: Icon(
              widget.icon,
              size: 17,
              color: !widget.enabled
                  ? OrblitColors.line
                  : (widget.active
                        ? OrblitColors.ember
                        : (_hovering ? OrblitColors.ink : OrblitColors.inkMid)),
            ),
          ),
        ),
      ),
    );
  }
}

/// The bar along the bottom: the last change on the left, and on the right
/// whatever the workspace in use says about what it is working on.
final class _StatusBar extends StatelessWidget {
  const _StatusBar({required this.message, required this.status});

  final String message;
  final Widget status;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 24,
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      // The frame's colour, like the bar at the top.
      color: OrblitColors.ground,
      child: Row(
        children: [
          Expanded(
            child: Text(
              message,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.caption.copyWith(fontSize: 11),
            ),
          ),
          status,
        ],
      ),
    );
  }
}

/// The scene's side of the bar along the bottom: its file, how many objects
/// it has and how fast it draws.
final class _SceneStatus extends StatelessWidget {
  const _SceneStatus({
    required this.objects,
    required this.file,
    required this.dirty,
    this.rate,
    this.frameMs,
    this.gpuBound = false,
  });

  final int objects;
  final String file;
  final bool dirty;

  /// Frames a second, or null before there has been anything to measure.
  final double? rate;

  /// How long the slower half of a frame takes, and which half it is.
  final double? frameMs;
  final bool gpuBound;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          dirty ? '$file •' : file,
          style: OrblitText.mono.copyWith(
            fontSize: 11,
            color: dirty ? OrblitColors.ember : OrblitColors.inkDim,
          ),
        ),
        const SizedBox(width: Space.lg),
        Text(
          '$objects objects',
          style: OrblitText.mono.copyWith(fontSize: 11),
        ),
        const SizedBox(width: Space.lg),
        Text(
          rate == null ? '— fps' : '${rate!.round()} fps',
          style: OrblitText.mono.copyWith(
            fontSize: 11,
            // Below about fifty a frame is late often enough to feel it.
            color: rate != null && rate! < 50
                ? OrblitColors.warn
                : OrblitColors.inkDim,
          ),
        ),
        if (frameMs != null) ...[
          const SizedBox(width: Space.sm),
          Text(
            // Which half of the frame the time went in, because "slow" and
            // "slow at what" are different questions.
            '${frameMs!.toStringAsFixed(1)} ms ${gpuBound ? "gpu" : "cpu"}',
            style: OrblitText.mono.copyWith(
              fontSize: 11,
              color: OrblitColors.inkDim,
            ),
          ),
        ],
      ],
    );
  }
}

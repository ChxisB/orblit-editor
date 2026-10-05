part of 'editor_shell.dart';

// The frame around the panels: the header above them with the menus, the
// search and undo on it, the rail of workspaces beside them, and the play
// controls the scene view carries.

/// The header: the menus and the file on the left, search in the middle of the
/// window, and undo and redo on the right.
final class _AppHeader extends StatelessWidget {
  const _AppHeader({
    required this.projectName,
    required this.file,
    required this.history,
    required this.dirty,
    required this.onSearch,
    required this.onClose,
    required this.onAdd,
    required this.onAddShape,
    required this.onAddTerrain,
    required this.onSave,
    required this.onSaveAs,
    required this.onNewScene,
    required this.onOpenInCode,
    required this.onReveal,
    required this.viewMenu,
    required this.onUndo,
    required this.onRedo,
    required this.selectionCount,
    required this.clipboard,
    required this.onCopy,
    required this.onCut,
    required this.onPaste,
    required this.onDuplicate,
  });

  final String projectName;

  /// What the workspace in use says about the file it is working on.
  final Widget file;

  final History history;
  final bool dirty;
  final VoidCallback onSearch;
  final VoidCallback onClose;
  final ValueChanged<ObjectKind> onAdd;
  final ValueChanged<ShapeKind> onAddShape;
  final VoidCallback onAddTerrain;
  final VoidCallback onSave;
  final VoidCallback onSaveAs;

  final VoidCallback onNewScene;
  final VoidCallback onOpenInCode;
  final VoidCallback onReveal;

  final Widget viewMenu;
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
        _LogoMenu(onClose: onClose, onSearch: onSearch),
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
        viewMenu,
        const _BarDivider(),
        Flexible(
          child: Text(
            projectName,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: OrblitText.label.copyWith(
              fontWeight: FontWeight.w500,
              color: OrblitColors.ink,
            ),
          ),
        ),
        const SizedBox(width: Space.md),
        Flexible(child: file),
      ],
    );

    return SizedBox(
      height: 48,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: Space.md),
        child: CentredBar(
          start: menus,
          // Up to 360 wide and no wider, and narrower in a window with less
          // room: the bar hands it only what the two ends leave.
          middle: SizedBox(width: 360, child: _SearchButton(onTap: onSearch)),
          end: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              // Labelled with what they would undo, so the tooltip answers
              // the question somebody actually has before they press it.
              _HeaderButton(
                tooltip: history.undoLabel == null
                    ? 'Nothing to undo'
                    : 'Undo ${history.undoLabel} '
                          '(${commandShortcutLabel('Z')}).',
                enabled: history.canUndo,
                onTap: onUndo,
                child: const Icon(Icons.undo),
              ),
              _HeaderButton(
                tooltip: history.redoLabel == null
                    ? 'Nothing to redo'
                    : 'Redo ${history.redoLabel} '
                          '(${commandShortcutLabel('⇧Z')}).',
                enabled: history.canRedo,
                onTap: onRedo,
                child: const Icon(Icons.redo),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// The scene's file, and how many objects are in it.
final class _SceneFile extends StatelessWidget {
  const _SceneFile({
    required this.file,
    required this.unsaved,
    required this.objects,
  });

  /// Null before any scene is open.
  final String? file;
  final bool unsaved;
  final int objects;

  @override
  Widget build(BuildContext context) {
    final name = file;
    if (name == null) {
      return const Text('No scene loaded', style: OrblitText.caption);
    }
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Flexible(
          child: FileStatus(file: name, unsaved: unsaved),
        ),
        const SizedBox(width: Space.md),
        Text(
          objects == 1 ? '1 object' : '$objects objects',
          maxLines: 1,
          style: OrblitText.caption.copyWith(fontSize: 11.5),
        ),
      ],
    );
  }
}

/// The engine's mark, which opens what belongs to the app and not to the
/// project: the way back to the launcher, and the palette.
final class _LogoMenu extends StatelessWidget {
  const _LogoMenu({required this.onClose, required this.onSearch});

  final VoidCallback onClose;
  final VoidCallback onSearch;

  @override
  Widget build(BuildContext context) => MenuAnchor(
    style: orblitMenuStyle,
    menuChildren: [
      MenuItemButton(
        onPressed: onClose,
        leadingIcon: const Icon(Icons.chevron_left),
        child: const Text('All projects'),
      ),
      MenuItemButton(
        onPressed: onSearch,
        leadingIcon: const Icon(Icons.search),
        trailingIcon: Text(
          commandShortcutLabel('K'),
          style: OrblitText.mono.copyWith(fontSize: 11),
        ),
        child: const Text('Command palette'),
      ),
    ],
    builder: (context, controller, child) => _HeaderButton(
      tooltip: 'Orblit menu',
      onTap: () => controller.isOpen ? controller.close() : controller.open(),
      child: const OrblitMark(),
    ),
  );
}

/// The way into the palette, which stands in the middle of the header so it is
/// the first thing the eye lands on.
final class _SearchButton extends StatefulWidget {
  const _SearchButton({required this.onTap});

  final VoidCallback onTap;

  @override
  State<_SearchButton> createState() => _SearchButtonState();
}

class _SearchButtonState extends State<_SearchButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    child: MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: widget.onTap,
        child: Container(
          height: 30,
          padding: const EdgeInsets.only(left: 10, right: 6),
          decoration: BoxDecoration(
            color: _hovering ? OrblitColors.raised : OrblitColors.surface,
            borderRadius: BorderRadius.circular(Radii.card),
          ),
          child: Row(
            spacing: Space.sm,
            children: [
              const Icon(Icons.search, size: 14, color: OrblitColors.inkDim),
              const Expanded(
                child: Text(
                  'Search commands and objects',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(fontSize: 12.5, color: OrblitColors.inkDim),
                ),
              ),
              Keycap(commandShortcutLabel('K')),
            ],
          ),
        ),
      ),
    ),
  );
}

/// A short upright line between groups of controls on the bar.
class _BarDivider extends StatelessWidget {
  const _BarDivider();

  @override
  Widget build(BuildContext context) => Container(
    width: 1,
    height: 16,
    margin: const EdgeInsets.symmetric(horizontal: 10),
    color: OrblitColors.line,
  );
}

/// An icon on the header, with a tooltip because it has no word.
final class _HeaderButton extends StatefulWidget {
  const _HeaderButton({
    required this.child,
    required this.tooltip,
    required this.onTap,
    this.enabled = true,
  });

  final Widget child;
  final String tooltip;
  final VoidCallback onTap;
  final bool enabled;

  @override
  State<_HeaderButton> createState() => _HeaderButtonState();
}

class _HeaderButtonState extends State<_HeaderButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final lit = _hovering && widget.enabled;
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
            height: 32,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: lit ? OrblitColors.hover : Colors.transparent,
              borderRadius: BorderRadius.circular(Radii.card),
            ),
            child: IconTheme(
              data: IconThemeData(
                size: 17,
                color: !widget.enabled
                    ? OrblitColors.line
                    : (lit ? OrblitColors.ink : OrblitColors.inkMid),
              ),
              child: widget.child,
            ),
          ),
        ),
      ),
    );
  }
}

/// The workspaces, one icon each, in a column beside the panels.
///
/// A column and not tabs along the top: it is a choice of what the whole
/// window is for, so it sits outside the panels it rearranges, and it costs
/// no height. The one the editor is in is lit; the others say their word
/// when pointed at.
final class _WorkspaceRail extends StatelessWidget {
  const _WorkspaceRail({
    required this.modes,
    required this.mode,
    required this.onMode,
  });

  final List<EditorMode> modes;
  final EditorMode mode;
  final ValueChanged<EditorMode> onMode;

  @override
  Widget build(BuildContext context) => Container(
    width: 44,
    padding: const EdgeInsets.all(6),
    decoration: BoxDecoration(
      color: OrblitColors.surface,
      borderRadius: BorderRadius.circular(Radii.panel),
      border: Border.all(color: OrblitColors.rim),
    ),
    child: Column(
      mainAxisSize: MainAxisSize.min,
      spacing: 2,
      children: [
        for (final each in modes)
          _RailButton(
            key: ValueKey('mode/${each.name}'),
            mode: each,
            selected: each.name == mode.name,
            onTap: () => onMode(each),
          ),
      ],
    ),
  );
}

class _RailButton extends StatefulWidget {
  const _RailButton({
    super.key,
    required this.mode,
    required this.selected,
    required this.onTap,
  });

  final EditorMode mode;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_RailButton> createState() => _RailButtonState();
}

class _RailButtonState extends State<_RailButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final selected = widget.selected;
    // Colour alone does not tell a screen reader which workspace is open.
    return Semantics(
      container: true,
      button: true,
      selected: selected,
      label: widget.mode.label,
      child: Tooltip(
        message: widget.mode.label,
        child: MouseRegion(
          cursor: SystemMouseCursors.click,
          onEnter: (_) => setState(() => _hovering = true),
          onExit: (_) => setState(() => _hovering = false),
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: widget.onTap,
            child: Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(
                color: selected
                    ? OrblitColors.raised
                    : (_hovering ? OrblitColors.hover : Colors.transparent),
                borderRadius: BorderRadius.circular(Radii.card),
              ),
              child: Stack(
                alignment: Alignment.center,
                clipBehavior: Clip.none,
                children: [
                  // Out in the rail's padding, so the lit one is marked by
                  // more than a change of colour.
                  if (selected)
                    Positioned(
                      left: -6,
                      top: 8,
                      child: Container(
                        width: 3,
                        height: 16,
                        decoration: BoxDecoration(
                          color: OrblitColors.ember,
                          borderRadius: BorderRadius.circular(2),
                        ),
                      ),
                    ),
                  Icon(
                    widget.mode.icon,
                    size: 18,
                    color: selected
                        ? OrblitColors.ember
                        : (_hovering ? OrblitColors.ink : OrblitColors.inkMid),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// Play, and stop, on a pad of their own in the middle of the scene view's
/// header: they are the one pair of controls that leaves the editor rather
/// than changing something in it.
final class _PlayPill extends StatelessWidget {
  const _PlayPill({
    required this.playing,
    required this.onPlay,
    required this.onStop,
  });

  final bool playing;
  final VoidCallback onPlay;
  final VoidCallback onStop;

  @override
  Widget build(BuildContext context) => Container(
    padding: const EdgeInsets.all(3),
    decoration: BoxDecoration(
      color: OrblitColors.raised,
      borderRadius: BorderRadius.circular(9),
    ),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      spacing: 2,
      children: [
        _PillButton(
          icon: playing ? Icons.pause : Icons.play_arrow,
          tooltip: playing
              ? 'Pause scene animation.'
              : 'Play scene animation.',
          // Ember while it is the thing to press, so a paused scene reads as
          // waiting for somebody.
          filled: !playing,
          onTap: onPlay,
        ),
        _PillButton(
          icon: Icons.stop,
          tooltip: 'Stop animation and return to editing.',
          filled: false,
          onTap: onStop,
        ),
      ],
    ),
  );
}

class _PillButton extends StatefulWidget {
  const _PillButton({
    required this.icon,
    required this.tooltip,
    required this.filled,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final bool filled;
  final VoidCallback onTap;

  @override
  State<_PillButton> createState() => _PillButtonState();
}

class _PillButtonState extends State<_PillButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) => Tooltip(
    message: widget.tooltip,
    child: MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          width: 36,
          height: 26,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: widget.filled
                ? OrblitColors.ember
                : (_hovering ? OrblitColors.hover : Colors.transparent),
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          child: Icon(
            widget.icon,
            size: 16,
            color: widget.filled
                ? OrblitColors.emberInk
                : (_hovering ? OrblitColors.ink : OrblitColors.inkMid),
          ),
        ),
      ),
    ),
  );
}

part of 'editor_shell.dart';

// The frame around the panels: the bar above them, the transport
// buttons on it, the mode's own bar under it, the bar below, and the
// handle between two panels.

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

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.project,
    required this.playing,
    required this.history,
    required this.dirty,
    required this.onPlay,
    required this.onClose,
    required this.onAdd,
    required this.onAddShape,
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

  final Project project;
  final bool playing;
  final History history;
  final bool dirty;
  final VoidCallback onPlay;
  final VoidCallback onClose;
  final ValueChanged<ObjectKind> onAdd;
  final ValueChanged<ShapeKind> onAddShape;
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
    return Container(
      height: 44,
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        children: [
          OrblitButton(
            label: project.name,
            icon: Icons.chevron_left,
            tone: ButtonTone.quiet,
            onPressed: onClose,
          ),
          const SizedBox(width: Space.md),
          _SceneMenu(
            dirty: dirty,
            onSave: onSave,
            onSaveAs: onSaveAs,
            onNewScene: onNewScene,
            onOpenInCode: onOpenInCode,
            onReveal: onReveal,
          ),
          const SizedBox(width: Space.xs),
          _AddMenu(onAdd: onAdd, onAddShape: onAddShape),
          const SizedBox(width: Space.xs),
          _EditMenu(
            selectionCount: selectionCount,
            clipboard: clipboard,
            onCopy: onCopy,
            onCut: onCut,
            onPaste: onPaste,
            onDuplicate: onDuplicate,
          ),
          const SizedBox(width: Space.xs),
          _ViewMenu(layout: layout, onLayout: onLayout, panels: panels),
          const SizedBox(width: Space.md),
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
          const SizedBox(width: Space.xs),
          _TransportButton(
            icon: Icons.redo,
            tooltip: history.redoLabel == null
                ? 'Nothing to redo'
                : 'Redo ${history.redoLabel}',
            active: false,
            enabled: history.canRedo,
            onTap: onRedo,
          ),
          const Spacer(),
          // Transport in the centre, where it is in every editor that has one,
          // because muscle memory is worth more than novelty here.
          _TransportButton(
            icon: playing ? Icons.pause : Icons.play_arrow,
            tooltip: playing ? 'Pause' : 'Play',
            active: playing,
            onTap: onPlay,
          ),
          const SizedBox(width: Space.xs),
          _TransportButton(
            icon: Icons.stop,
            tooltip: 'Stop',
            active: false,
            onTap: () {},
          ),
          const Spacer(),
          Text('pre-alpha', style: OrblitText.caption),
        ],
      ),
    );
  }
}

/// Which mode the editor is in, and the tools that mode puts on the shelf.
///
/// Only there when it has something on it. With one mode and no tools it
/// would be a strip of nothing, taking height from the panels.
class _ModeBar extends StatelessWidget {
  const _ModeBar({
    required this.modes,
    required this.mode,
    required this.onMode,
  });

  final List<EditorMode> modes;
  final EditorMode mode;
  final ValueChanged<EditorMode> onMode;

  @override
  Widget build(BuildContext context) {
    final tools = mode.tools;

    return Container(
      height: 36,
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        children: [
          if (modes.length > 1) ...[
            for (final each in modes)
              Padding(
                padding: const EdgeInsets.only(right: Space.xs),
                child: OrblitButton(
                  label: each.label,
                  icon: each.icon,
                  tone: each.name == mode.name
                      ? ButtonTone.normal
                      : ButtonTone.quiet,
                  onPressed: () => onMode(each),
                ),
              ),
            const SizedBox(width: Space.md),
          ],
          if (tools != null) Expanded(child: tools(context)),
        ],
      ),
    );
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

class _StatusBar extends StatelessWidget {
  const _StatusBar({
    required this.objects,
    required this.message,
    required this.file,
    required this.dirty,
    this.rate,
    this.frameMs,
    this.gpuBound = false,
  });

  final int objects;
  final String message;
  final String file;
  final bool dirty;

  /// Frames a second, or null before there has been anything to measure.
  final double? rate;

  /// How long the slower half of a frame takes, and which half it is.
  final double? frameMs;
  final bool gpuBound;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 24,
      padding: const EdgeInsets.symmetric(horizontal: Space.md),
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(top: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        children: [
          Flexible(
            child: Text(
              message,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.caption.copyWith(fontSize: 11),
            ),
          ),
          const Spacer(),
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
      ),
    );
  }
}

part of 'viewport.dart';

// The small widgets around the render -- toolbar buttons, readouts, and
// the stand-in shown where there is no renderer to show.

/// One of the two things a drag on a handle can do.
class _ToolButton extends StatelessWidget {
  const _ToolButton({
    required this.mode,
    required this.selected,
    required this.onPressed,
  });

  final GizmoMode mode;
  final bool selected;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: mode.label,
      child: GestureDetector(
        onTap: onPressed,
        child: Container(
          width: 28,
          height: 28,
          decoration: BoxDecoration(
            color: selected ? OrblitColors.ember : OrblitColors.raised,
            borderRadius: BorderRadius.circular(Radii.control),
            border: Border.all(
              color: selected ? OrblitColors.ember : OrblitColors.lineSoft,
            ),
          ),
          child: Icon(
            mode.icon,
            size: 15,
            color: selected ? Colors.white : OrblitColors.inkDim,
          ),
        ),
      ),
    );
  }
}

/// The grid shown where Filament cannot run.
class _Placeholder extends StatelessWidget {
  const _Placeholder();

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        const Positioned.fill(child: CustomPaint(painter: _GridPainter())),
        Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(
                Icons.view_in_ar_outlined,
                size: 34,
                color: OrblitColors.inkDim,
              ),
              const SizedBox(height: Space.md),
              Text('Viewport', style: OrblitText.label),
              const SizedBox(height: Space.xs),
              Text(rendererUnavailableMessage, style: OrblitText.caption),
            ],
          ),
        ),
      ],
    );
  }
}

class _ViewportChip extends StatelessWidget {
  const _ViewportChip(this.label, {this.on, this.onTap, this.tooltip});

  final String label;

  /// Null for a chip that only says something. Set for one that is also a
  /// switch, which then reads as on or off rather than as a label.
  final bool? on;

  final VoidCallback? onTap;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final lit = on ?? false;

    Widget chip = Container(
      padding: const EdgeInsets.symmetric(horizontal: Space.sm, vertical: 3),
      decoration: BoxDecoration(
        color: on == null
            ? OrblitColors.surface.withValues(alpha: 0.8)
            : (lit
                  ? OrblitColors.emberWash
                  : OrblitColors.surface.withValues(alpha: 0.8)),
        borderRadius: BorderRadius.circular(Radii.control),
        border: Border.all(
          color: lit ? OrblitColors.ember : OrblitColors.lineSoft,
        ),
      ),
      child: Text(
        label,
        style: OrblitText.caption.copyWith(
          fontSize: 11,
          color: lit ? OrblitColors.ember : null,
        ),
      ),
    );

    if (tooltip != null) chip = Tooltip(message: tooltip!, child: chip);
    if (onTap == null) return chip;

    return MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(onTap: onTap, child: chip),
    );
  }
}

/// A small window onto what a camera sees.
///
/// Deliberately small and in a corner: it answers "is this shot right" without
/// becoming the thing somebody is looking at. Anything bigger is the game
/// view, which is a panel and can be docked wherever it is wanted.
class _CameraPreview extends StatelessWidget {
  const _CameraPreview({required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 240,
      height: 135,
      decoration: BoxDecoration(
        color: OrblitColors.ground,
        borderRadius: BorderRadius.circular(Radii.control),
        border: Border.all(color: OrblitColors.line),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 20,
            padding: const EdgeInsets.symmetric(horizontal: Space.sm),
            alignment: Alignment.centerLeft,
            child: Text('CAMERA', style: OrblitText.section),
          ),
          Expanded(
            child: ClipRRect(
              borderRadius: const BorderRadius.vertical(
                bottom: Radius.circular(Radii.control),
              ),
              // Nothing in it takes the pointer: it is a picture of the shot,
              // and a click here should still select what is behind it in the
              // viewport.
              child: IgnorePointer(child: child),
            ),
          ),
        ],
      ),
    );
  }
}

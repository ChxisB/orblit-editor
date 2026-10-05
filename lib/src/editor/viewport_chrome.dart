part of 'viewport.dart';

// The small widgets around the render -- toolbar buttons, readouts, and
// the stand-in shown where there is no renderer to show.

/// The tools a drag on a handle can be, in a strip that floats over the
/// corner of the view.
///
/// Translucent, so the scene shows through the gaps rather than ending at a
/// panel edge.
class _ToolPalette extends StatelessWidget {
  const _ToolPalette({required this.mode, required this.onSelect});

  /// A tile and the padding either side of it.
  static const width = 40.0;

  final GizmoMode mode;
  final ValueChanged<GizmoMode> onSelect;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: OrblitColors.surface.withValues(alpha: 0.8),
        borderRadius: BorderRadius.circular(Radii.panel),
        border: Border.all(color: const Color(0x0FFFFFFF)),
      ),
      child: Padding(
        padding: const EdgeInsets.all(Space.xs),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          spacing: 2,
          children: [
            for (final each in GizmoMode.values)
              IconTile(
                tooltip: each.label,
                iconSize: 16,
                radius: Radii.control,
                active: each == mode,
                onTap: () => onSelect(each),
                child: Icon(each.icon),
              ),
          ],
        ),
      ),
    );
  }
}

/// Which way the world's axes run on screen, so that somebody who has
/// orbited away from the grid can find their way back.
///
/// Out of the pointer's way: it is a readout, and a drag that starts on it
/// should still orbit the view.
class _OrientationGizmo extends StatelessWidget {
  const _OrientationGizmo({required this.camera});

  static const size = 56.0;

  final OrbitCamera camera;

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: CustomPaint(
        size: const Size.square(size),
        painter: _OrientationPainter(camera.basis),
      ),
    );
  }
}

/// The grid shown where Filament cannot run.
class _Placeholder extends StatelessWidget {
  const _Placeholder();

  @override
  Widget build(BuildContext context) {
    return const Stack(
      children: [
        Positioned.fill(child: CustomPaint(painter: _GridPainter())),
        Center(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.view_in_ar_outlined,
                size: 34,
                color: OrblitColors.inkDim,
              ),
              SizedBox(height: Space.md),
              Text('Viewport', style: OrblitText.label),
              SizedBox(height: Space.xs),
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
            child: const Text('CAMERA', style: OrblitText.section),
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

part of 'timeline.dart';

// The small pieces the panel is built from.

/// The panel with no clip open: what a clip is, and the one button that
/// makes one.
///
/// Scrolls rather than overflows, because the strip under the view can be
/// dragged shorter than this.
final class _NoClip extends StatelessWidget {
  const _NoClip({required this.onNew});

  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(Space.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.animation, size: 32, color: OrblitColors.inkDim),
            const SizedBox(height: Space.md),
            const Text(
              'No clip open',
              style: OrblitText.title,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.xs),
            const Text(
              'A clip is how something moves over time, such as a door that '
              'swings or a light that flickers. Make one here, or open one '
              'from Project.',
              style: OrblitText.caption,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.md),
            OrblitButton(
              label: 'Make a clip',
              icon: Icons.add,
              tone: ButtonTone.primary,
              onPressed: onNew,
            ),
          ],
        ),
      ),
    );
  }
}

class _Message extends StatelessWidget {
  const _Message(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Text(
          text,
          style: OrblitText.caption,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

class _Action extends StatelessWidget {
  const _Action({
    required this.icon,
    required this.tooltip,
    required this.onTap,
    this.on = false,
  });

  final IconData icon;
  final String tooltip;

  /// Null greys it out.
  final VoidCallback? onTap;

  final bool on;

  @override
  Widget build(BuildContext context) {
    final colour = onTap == null
        ? OrblitColors.line
        : on
        ? OrblitColors.ember
        : OrblitColors.inkMid;
    return Tooltip(
      message: tooltip,
      child: MouseRegion(
        cursor: onTap == null ? MouseCursor.defer : SystemMouseCursors.click,
        child: GestureDetector(
          onTap: onTap,
          child: Padding(
            padding: const EdgeInsets.all(Space.xs),
            child: Icon(icon, size: 16, color: colour),
          ),
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({required this.label, required this.on, required this.onTap});

  final String label;
  final bool on;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: Space.sm,
            vertical: 3,
          ),
          decoration: BoxDecoration(
            color: on ? OrblitColors.raised : Colors.transparent,
            borderRadius: BorderRadius.circular(Radii.control),
            border: Border.all(
              color: on ? OrblitColors.line : Colors.transparent,
            ),
          ),
          child: Text(
            label,
            style: OrblitText.caption.copyWith(
              fontSize: 11,
              color: on ? OrblitColors.ink : OrblitColors.inkDim,
            ),
          ),
        ),
      ),
    );
  }
}

const _menuStyle = MenuStyle(
  backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
  surfaceTintColor: WidgetStatePropertyAll(Colors.transparent),
  shape: WidgetStatePropertyAll(
    RoundedRectangleBorder(
      borderRadius: BorderRadius.all(Radius.circular(Radii.panel)),
      side: BorderSide(color: OrblitColors.line),
    ),
  ),
);

/// A label that opens a menu, the way every choice on the toolbar is made.
class _Menu extends StatelessWidget {
  const _Menu({
    required this.label,
    required this.items,
    this.icon,
    this.tooltip,
    this.enabled = true,
    this.warn = false,
  });

  final String label;
  final List<Widget> items;
  final IconData? icon;
  final String? tooltip;
  final bool enabled;
  final bool warn;

  @override
  Widget build(BuildContext context) {
    final colour = !enabled
        ? OrblitColors.line
        : warn
        ? OrblitColors.warn
        : OrblitColors.inkMid;
    final menu = MenuAnchor(
      style: _menuStyle,
      menuChildren: items,
      builder: (context, controller, _) => MouseRegion(
        cursor: enabled ? SystemMouseCursors.click : MouseCursor.defer,
        child: GestureDetector(
          onTap: !enabled
              ? null
              : () => controller.isOpen
                    ? controller.close()
                    : controller.open(),
          child: Container(
            padding: const EdgeInsets.symmetric(
              horizontal: Space.sm,
              vertical: 3,
            ),
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(Radii.control),
              border: Border.all(color: OrblitColors.lineSoft),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (icon != null) ...[
                  Icon(icon, size: 13, color: colour),
                  const SizedBox(width: 5),
                ],
                Text(
                  label,
                  style: OrblitText.caption.copyWith(
                    fontSize: 11,
                    color: enabled ? OrblitColors.ink : OrblitColors.inkDim,
                  ),
                ),
                const SizedBox(width: 2),
                Icon(Icons.arrow_drop_down, size: 14, color: colour),
              ],
            ),
          ),
        ),
      ),
    );
    return tooltip == null ? menu : Tooltip(message: tooltip, child: menu);
  }
}

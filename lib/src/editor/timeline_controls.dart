part of 'timeline.dart';

// The small pieces the panel is built from.

/// What the panel calls what it edits.
typedef _Words = ({
  String none,
  String about,
  String make,
  String makeTip,
  String length,
  IconData icon,
});

/// A clip in the Animation workspace, where it is played on one thing, and
/// a cutscene in Cinematics, where it moves the whole scene.
_Words _wordsFor(PlayedOn playedOn) => switch (playedOn) {
  PlayedOn.owner => (
    none: 'No clip open',
    about:
        'A clip is how something moves over time, such as a door that '
        'swings or a light that flickers. Make one here, or open one '
        'from Project.',
    make: 'Make a clip',
    makeTip: 'Create an animation clip for the selected object.',
    length: 'Clip length',
    icon: Icons.animation,
  ),
  PlayedOn.scene => (
    none: 'No cutscene open',
    about:
        'A cutscene moves things in the scene and cuts between cameras, '
        'such as an intro before a level. Make one here, or open one from '
        'Project.',
    make: 'New cutscene',
    makeTip: 'Create a cutscene for this scene.',
    length: 'Cutscene length',
    icon: Icons.movie_outlined,
  ),
};

/// The panel with nothing open: what a clip or cutscene is, and the one
/// button that makes one.
///
/// Scrolls rather than overflows, because the strip under the view can be
/// dragged shorter than this.
final class _NoClip extends StatelessWidget {
  const _NoClip({required this.words, required this.onNew});

  final _Words words;
  final VoidCallback onNew;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(Space.lg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(words.icon, size: 32, color: OrblitColors.inkDim),
            const SizedBox(height: Space.md),
            Text(
              words.none,
              style: OrblitText.title,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.xs),
            Text(
              words.about,
              style: OrblitText.caption,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.md),
            OrblitButton(
              label: words.make,
              tooltip: words.makeTip,
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
    return Tooltip(
      message: label == 'Keys'
          ? 'Show the timing of each key.'
          : 'Show how values change between keys.',
      child: MouseRegion(
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
      ),
    );
  }
}

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
      style: orblitMenuStyle,
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

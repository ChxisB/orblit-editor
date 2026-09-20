part of 'ui_editor.dart';

// The small pieces the panels are built from.

/// What the canvas is being laid out at, and which prefixed classes that
/// puts in play.
///
/// The one thing a responsive canvas has to say out loud. A `md:` class that
/// appears to do nothing is somebody's afternoon, and the answer is always
/// that the canvas is narrower than they thought.
class _Showing extends StatelessWidget {
  const _Showing({required this.document, required this.preview});

  final UiDocument document;
  final Size? preview;

  @override
  Widget build(BuildContext context) {
    final size = UiCanvasView.layoutFor(document, preview);
    final at = const UiTheme().breakpoints.labelAt(size.width);

    return Tooltip(
      message:
          'Laid out at ${size.width.round()} × ${size.height.round()}. '
          'Classes prefixed $at: and narrower are in effect.',
      waitDuration: const Duration(milliseconds: 400),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            '${size.width.round()} × ${size.height.round()}',
            style: OrblitText.label.copyWith(
              fontSize: 11,
              color: OrblitColors.inkDim,
            ),
          ),
          const SizedBox(width: Space.sm),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 1),
            decoration: BoxDecoration(
              color: OrblitColors.raised,
              borderRadius: BorderRadius.circular(Radii.control),
              border: Border.all(color: OrblitColors.line),
            ),
            child: Text(
              at,
              style: OrblitText.label.copyWith(
                fontSize: 10.5,
                color: OrblitColors.inkMid,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

/// A class list with any direction it named taken out.
///
/// The type says `row` now, and a leftover `stack` or `md:row` in the classes
/// is applied over the top of it — the element would keep its old layout and
/// the change would look like it did nothing.
String _flowing(String classes) {
  const directions = {'col', 'column', 'stack', 'row'};
  final kept = [
    for (final name in classes.split(RegExp(r'\s+')))
      if (name.isNotEmpty)
        if (!directions.contains(name) &&
            !directions.contains(name.split(':').last))
          name,
  ];
  if (!kept.any((name) => name.startsWith('gap-'))) kept.add('gap-4');
  return kept.join(' ');
}

/// How many columns the grid is drawing, when it is not the authored number.
class _GridCount extends StatelessWidget {
  const _GridCount({required this.document, required this.preview});

  final UiDocument document;
  final Size? preview;

  @override
  Widget build(BuildContext context) {
    final width = UiCanvasView.layoutFor(document, preview).width;
    final drawn = document.canvas.columnsAt(width);
    final authored = document.canvas.columns;

    return Padding(
      padding: const EdgeInsets.only(top: Space.xs),
      child: Text(
        drawn == authored
            ? '$authored across this screen'
            : (drawn == 0
                  ? 'No room for a column at this width'
                  : '$drawn across this screen — $authored is too fine here'),
        style: OrblitText.label.copyWith(
          fontSize: 10.5,
          color: OrblitColors.inkDim,
        ),
      ),
    );
  }
}

class _Chip extends StatelessWidget {
  const _Chip({
    required this.label,
    required this.onTap,
    this.icon,
    this.tooltip,
    this.selected = false,
  });

  final String label;
  final VoidCallback onTap;
  final IconData? icon;
  final String? tooltip;
  final bool selected;

  @override
  Widget build(BuildContext context) {
    final chip = _chip();
    return tooltip == null
        ? chip
        : Tooltip(
            message: tooltip!,
            waitDuration: const Duration(milliseconds: 400),
            child: chip,
          );
  }

  Widget _chip() {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          padding: const EdgeInsets.symmetric(
            horizontal: Space.sm,
            vertical: Space.xs,
          ),
          decoration: BoxDecoration(
            color: selected ? OrblitColors.emberWash : OrblitColors.raised,
            borderRadius: BorderRadius.circular(Radii.control),
            border: Border.all(
              color: selected ? OrblitColors.ember : OrblitColors.line,
            ),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (icon != null) ...[
                Icon(icon, size: 12, color: OrblitColors.inkMid),
                const SizedBox(width: 5),
              ],
              // Flexible so a long label ellipsizes inside its own chip. An
              // overflowing Row in a fixed-width panel is a striped bar
              // across the inspector and, one step further, a layout that
              // throws — see the unbounded-width traps this editor has hit
              // before.
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  softWrap: false,
                  style: OrblitText.label.copyWith(
                    fontSize: 11,
                    color: selected ? OrblitColors.ember : OrblitColors.inkMid,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Toggle extends StatelessWidget {
  const _Toggle({
    required this.label,
    required this.icon,
    required this.on,
    required this.onChanged,
  });

  final String label;
  final IconData icon;
  final bool on;
  final ValueChanged<bool>? onChanged;

  @override
  Widget build(BuildContext context) {
    return OrblitButton(
      label: label,
      icon: icon,
      tone: on ? ButtonTone.primary : ButtonTone.quiet,
      onPressed: onChanged == null ? null : () => onChanged!(!on),
    );
  }
}

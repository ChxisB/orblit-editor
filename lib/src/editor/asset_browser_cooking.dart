part of 'asset_browser.dart';

// What the browser says about an asset that has to be cooked before the
// renderer can use it.

/// A small dot in the corner of a tile, saying where the file stands with the
/// cook.
///
/// A dot rather than a badge or a strip of text. There are four hundred of
/// these in a folder of textures, the thing being said is one of three, and
/// anything larger would compete with the picture it sits on — which is what
/// somebody is actually looking at when they are looking for a file.
///
/// Ringed in the panel's own colour so that it reads on a dark thumbnail and
/// on a bright one alike. A dot that disappears against half the textures in
/// the project says nothing about those textures.
class _CookMark extends StatelessWidget {
  const _CookMark({required this.state, required this.child});

  final CookState? state;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    final colour = state?.mark;
    if (colour == null) return child;

    return Stack(
      clipBehavior: Clip.none,
      children: [
        child,
        Positioned(
          top: 0,
          right: 0,
          child: Container(
            width: 8,
            height: 8,
            decoration: BoxDecoration(
              color: colour,
              shape: BoxShape.circle,
              border: Border.all(color: OrblitColors.surface, width: 1.5),
            ),
          ),
        ),
      ],
    );
  }
}

/// What the cook has left to do, in the header.
///
/// Only what is outstanding: the number cooked is the number nobody has to
/// act on, and a header that reads "396 cooked, 4 need cooking" buries the
/// four. Nothing at all is shown when there is nothing to do, which is the
/// state a project should mostly be in.
class _CookSummary extends StatelessWidget {
  const _CookSummary({required this.status});

  final CookStatusIndex status;

  @override
  Widget build(BuildContext context) {
    if (status.problem != null) {
      return Tooltip(
        message: 'The cook could not be asked.\n${status.problem}',
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, size: 13, color: OrblitColors.bad),
            const SizedBox(width: 4),
            Text(
              'Cook unknown',
              style: OrblitText.caption.copyWith(
                fontSize: 11,
                color: OrblitColors.bad,
              ),
            ),
          ],
        ),
      );
    }

    if (status.asking) {
      return Text(
        'Checking assets…',
        style: OrblitText.caption.copyWith(fontSize: 11),
      );
    }

    final counts = status.counts;
    final stale = counts[CookState.stale] ?? 0;
    final failed = counts[CookState.failed] ?? 0;
    if (stale == 0 && failed == 0) return const SizedBox.shrink();

    final parts = [
      if (failed > 0) '$failed would not cook',
      if (stale > 0) '$stale to cook',
    ];
    return Tooltip(
      message:
          'For ${status.target.name}. '
          '${counts[CookState.cooked] ?? 0} already cooked.',
      child: Text(
        parts.join(' · '),
        style: OrblitText.caption.copyWith(
          fontSize: 11,
          color: failed > 0 ? OrblitColors.bad : OrblitColors.warn,
        ),
      ),
    );
  }
}

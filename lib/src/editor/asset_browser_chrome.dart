part of 'asset_browser.dart';

// The bar across the top: where you are, what you can do from here.

class _Header extends StatelessWidget {
  const _Header({
    required this.crumb,
    required this.count,
    required this.canGoUp,
    required this.previewing,
    required this.filter,
    required this.onFilter,
    required this.onPreview,
    required this.onUp,
    required this.onRefresh,
    this.cookStatus,
  });

  final String crumb;
  final int count;
  final CookStatusIndex? cookStatus;
  final bool canGoUp;

  /// Whether the preview is showing, or null where there is no room for one.
  final bool? previewing;
  final TextEditingController filter;
  final ValueChanged<String> onFilter;
  final VoidCallback onPreview;
  final VoidCallback onUp;
  final VoidCallback onRefresh;

  /// Below this the bar has no room for a field worth typing in, so it has
  /// none. What was typed is still filtering the files.
  static const _filterRoom = 340.0;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 36,
      padding: const EdgeInsets.symmetric(horizontal: Space.xs),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: LayoutBuilder(
        builder: (context, box) => Row(
          spacing: Space.xs,
          children: [
            IconTile(
              tooltip: 'Up one folder',
              size: 28,
              iconSize: 15,
              radius: Radii.control,
              onTap: canGoUp ? onUp : null,
              child: const Icon(Icons.arrow_upward),
            ),
            // No title here: the tab above says what this panel is, and a
            // heading that repeats the tab is a line of pixels saying nothing.
            Flexible(
              child: Text(
                crumb.isEmpty ? '/' : crumb,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: OrblitText.mono,
              ),
            ),
            const Spacer(),
            // Gives way before anything else on the bar does, in a panel
            // down the side of the window rather than under the view.
            if (cookStatus != null)
              Flexible(child: _CookSummary(status: cookStatus!)),
            Flexible(
              child: Text(
                '$count item${count == 1 ? '' : 's'}',
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: OrblitText.caption,
              ),
            ),
            if (box.maxWidth >= _filterRoom)
              SizedBox(
                width: (box.maxWidth * 0.34).clamp(120.0, 184.0),
                child: FilterField(
                  controller: filter,
                  hint: 'Filter assets',
                  onChanged: onFilter,
                ),
              ),
            // The same menu the right-click opens. Here as well, because a
            // menu nobody knows to right-click for is a menu nobody has.
            Builder(
              builder: (context) => IconTile(
                tooltip: 'New folder or file',
                size: 28,
                iconSize: 16,
                radius: Radii.control,
                onTap: () {
                  final button = context.findRenderObject()! as RenderBox;
                  AssetMenu.open(
                    context,
                    button.localToGlobal(button.size.bottomLeft(Offset.zero)),
                  );
                },
                child: const Icon(Icons.add),
              ),
            ),
            if (previewing case final previewing?)
              IconTile(
                tooltip: previewing ? 'Hide the preview' : 'Show the preview',
                size: 28,
                iconSize: 16,
                radius: Radii.control,
                onTap: onPreview,
                child: Icon(
                  previewing
                      ? Icons.visibility_outlined
                      : Icons.visibility_off_outlined,
                ),
              ),
            IconTile(
              tooltip: 'Read the folder again',
              size: 28,
              iconSize: 16,
              radius: Radii.control,
              onTap: onRefresh,
              child: const Icon(Icons.refresh),
            ),
          ],
        ),
      ),
    );
  }
}

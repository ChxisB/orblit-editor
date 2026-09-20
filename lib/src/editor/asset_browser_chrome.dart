part of 'asset_browser.dart';

// The bar across the top: where you are, what you can do from here.

class _Header extends StatelessWidget {
  const _Header({
    required this.crumb,
    required this.count,
    required this.canGoUp,
    required this.previewing,
    required this.onPreview,
    required this.onUp,
    required this.onRefresh,
    this.cookStatus,
  });

  final String crumb;
  final int count;
  final CookStatusIndex? cookStatus;
  final bool canGoUp;
  final bool previewing;
  final VoidCallback onPreview;
  final VoidCallback onUp;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    return Container(
      height: 30,
      padding: const EdgeInsets.only(left: Space.sm, right: Space.xs),
      decoration: const BoxDecoration(
        border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Row(
        children: [
          _IconAction(
            icon: Icons.arrow_upward,
            tooltip: 'Up one folder',
            enabled: canGoUp,
            onTap: onUp,
          ),
          const SizedBox(width: Space.sm),
          // No title here: the tab above says what this panel is, and a
          // heading that repeats the tab is a line of pixels saying nothing.
          Flexible(
            child: Text(
              crumb.isEmpty ? '/' : crumb,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.mono.copyWith(fontSize: 11),
            ),
          ),
          const Spacer(),
          if (cookStatus != null) ...[
            _CookSummary(status: cookStatus!),
            const SizedBox(width: Space.sm),
          ],
          Text(
            '$count item${count == 1 ? '' : 's'}',
            style: OrblitText.caption.copyWith(fontSize: 11),
          ),
          const SizedBox(width: Space.xs),
          // The same menu the right-click opens. Here as well, because a
          // menu nobody knows to right-click for is a menu nobody has.
          Builder(
            builder: (context) => _IconAction(
              icon: Icons.add,
              tooltip: 'New folder or file',
              enabled: true,
              onTap: () {
                final box = context.findRenderObject()! as RenderBox;
                AssetMenu.open(
                  context,
                  box.localToGlobal(box.size.bottomLeft(Offset.zero)),
                );
              },
            ),
          ),
          const SizedBox(width: Space.xs),
          _IconAction(
            icon: previewing
                ? Icons.visibility_outlined
                : Icons.visibility_off_outlined,
            tooltip: previewing ? 'Hide the preview' : 'Show the preview',
            enabled: true,
            onTap: onPreview,
          ),
          const SizedBox(width: Space.xs),
          _IconAction(
            icon: Icons.refresh,
            tooltip: 'Read the folder again',
            enabled: true,
            onTap: onRefresh,
          ),
        ],
      ),
    );
  }
}

class _IconAction extends StatelessWidget {
  const _IconAction({
    required this.icon,
    required this.tooltip,
    required this.enabled,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final bool enabled;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: MouseRegion(
        cursor: enabled ? SystemMouseCursors.click : SystemMouseCursors.basic,
        child: GestureDetector(
          onTap: enabled ? onTap : null,
          child: Padding(
            padding: const EdgeInsets.all(4),
            child: Icon(
              icon,
              size: 14,
              color: enabled ? OrblitColors.inkMid : OrblitColors.lineSoft,
            ),
          ),
        ),
      ),
    );
  }
}

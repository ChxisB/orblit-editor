part of 'interface_mode.dart';

// The list of elements down the side.

/// The elements on the canvas, as a tree.
class _Tree extends StatelessWidget {
  const _Tree({required this.bench, required this.root});

  final InterfaceBench bench;
  final UiNode root;

  @override
  Widget build(BuildContext context) {
    final rows = root.walk().toList();
    return ListView.builder(
      padding: const EdgeInsets.symmetric(vertical: Space.xs),
      itemCount: rows.length,
      itemBuilder: (context, index) {
        final row = rows[index];
        return _TreeRow(
          node: row.node,
          path: row.path,
          selected: UiCanvasView.samePath(bench.selected, row.path),
          hovered: UiCanvasView.samePath(bench.hovered, row.path),
          onTap: () => bench.selected = row.path,
          onHover: (path) => bench.hovered = path,
          onRemove: row.path.isEmpty ? null : () => bench.remove(row.path),
        );
      },
    );
  }
}

class _TreeRow extends StatelessWidget {
  const _TreeRow({
    required this.node,
    required this.path,
    required this.selected,
    required this.hovered,
    required this.onTap,
    required this.onHover,
    required this.onRemove,
  });

  final UiNode node;
  final List<int> path;
  final bool selected;
  final bool hovered;
  final VoidCallback onTap;
  final ValueChanged<List<int>?> onHover;
  final VoidCallback? onRemove;

  static IconData _iconFor(String type) {
    for (final element in UiElement.values) {
      if (element.type == type) return element.icon;
    }
    return Icons.crop_square;
  }

  @override
  Widget build(BuildContext context) {
    final colour = selected
        ? OrblitColors.ember
        : (hovered ? OrblitColors.ink : OrblitColors.inkMid);

    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => onHover(path),
      onExit: (_) => onHover(null),
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          height: 24,
          padding: EdgeInsets.only(
            left: Space.sm + path.length * 13.0,
            right: Space.xs,
          ),
          color: selected
              ? OrblitColors.emberWash
              : (hovered ? OrblitColors.raised : Colors.transparent),
          child: Row(
            children: [
              Icon(_iconFor(node.type), size: 13, color: colour),
              const SizedBox(width: Space.sm),
              Expanded(
                child: Text(
                  uiNodeName(node),
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(
                    fontSize: 11.5,
                    color: colour,
                  ),
                ),
              ),
              if (hovered && onRemove != null)
                GestureDetector(
                  onTap: onRemove,
                  child: const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 3),
                    child: Icon(
                      Icons.close,
                      size: 12,
                      color: OrblitColors.inkDim,
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

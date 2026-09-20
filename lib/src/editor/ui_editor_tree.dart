part of 'ui_editor.dart';

// The list of elements down the side, and what dragging a row in it does.

/// The elements on the canvas, as a tree.
class _Tree extends StatelessWidget {
  const _Tree({
    required this.root,
    required this.selected,
    required this.hovered,
    required this.onSelect,
    required this.onHover,
    required this.onRemove,
  });

  final UiNode root;
  final List<int>? selected;
  final List<int>? hovered;
  final ValueChanged<List<int>> onSelect;
  final ValueChanged<List<int>?> onHover;
  final ValueChanged<List<int>> onRemove;

  @override
  Widget build(BuildContext context) {
    final rows = root.walk().toList();

    return Container(
      width: 248,
      decoration: const BoxDecoration(
        color: OrblitColors.surface,
        border: Border(right: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            height: 30,
            padding: const EdgeInsets.symmetric(horizontal: Space.md),
            alignment: Alignment.centerLeft,
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: OrblitColors.lineSoft)),
            ),
            child: Text('ELEMENTS', style: OrblitText.section),
          ),
          Expanded(
            child: ListView.builder(
              padding: const EdgeInsets.symmetric(vertical: Space.xs),
              itemCount: rows.length,
              itemBuilder: (context, index) {
                final row = rows[index];
                return _TreeRow(
                  node: row.node,
                  path: row.path,
                  selected: UiCanvasView.samePath(selected, row.path),
                  hovered: UiCanvasView.samePath(hovered, row.path),
                  onTap: () => onSelect(row.path),
                  onHover: onHover,
                  onRemove: row.path.isEmpty ? null : () => onRemove(row.path),
                );
              },
            ),
          ),
        ],
      ),
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
                  // The words when it has any, since "Start game" says more
                  // about which button this is than "button" does.
                  node.text?.trim().isNotEmpty ?? false
                      ? node.text!.trim()
                      : node.type,
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

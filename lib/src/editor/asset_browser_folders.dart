part of 'asset_browser.dart';

// The folder tree down the left.

/// The folders, down the left.
class _FolderTree extends StatelessWidget {
  const _FolderTree({
    super.key,
    required this.tree,
    required this.folders,
    required this.current,
    required this.onOpen,
    this.onDropObject,
  });

  final AssetTree tree;
  final List<({String path, int depth})> folders;
  final String current;
  final ValueChanged<String> onOpen;

  /// Called with the object dropped and the folder it landed on.
  final void Function(ObjectDrag object, String directory)? onDropObject;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 176,
      decoration: const BoxDecoration(
        border: Border(right: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: ListView(
        padding: const EdgeInsets.symmetric(vertical: Space.xs),
        children: [
          _FolderRow(
            name: 'Project',
            depth: 0,
            icon: Icons.home_outlined,
            selected: p.equals(current, tree.root),
            onTap: () => onOpen(tree.root),
            onDropObject: onDropObject == null
                ? null
                : (object) => onDropObject!(object, tree.root),
          ),
          for (final folder in folders)
            _FolderRow(
              name: p.basename(folder.path),
              depth: folder.depth + 1,
              icon: Icons.folder_outlined,
              selected: p.equals(current, folder.path),
              onTap: () => onOpen(folder.path),
              onDropObject: onDropObject == null
                  ? null
                  : (object) => onDropObject!(object, folder.path),
            ),
        ],
      ),
    );
  }
}

class _FolderRow extends StatefulWidget {
  const _FolderRow({
    required this.name,
    required this.depth,
    required this.icon,
    required this.selected,
    required this.onTap,
    this.onDropObject,
  });

  final String name;
  final int depth;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;

  /// Dropping an object on a folder makes a prefab there, without having to
  /// open the folder first.
  final ValueChanged<ObjectDrag>? onDropObject;

  @override
  State<_FolderRow> createState() => _FolderRowState();
}

class _FolderRowState extends State<_FolderRow> {
  bool _hovering = false;

  /// Whether something is being held over this row, which is worth showing:
  /// the rows are 24 pixels apart and dropping on the wrong one is easy.
  bool _catching = false;

  @override
  Widget build(BuildContext context) {
    final colour = widget.selected || _catching
        ? OrblitColors.ember
        : (_hovering ? OrblitColors.ink : OrblitColors.inkMid);

    final row = MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          height: 24,
          padding: EdgeInsets.only(
            left: Space.sm + widget.depth * 12.0,
            right: Space.sm,
          ),
          color: widget.selected || _catching
              ? OrblitColors.emberWash
              : (_hovering ? OrblitColors.raised : Colors.transparent),
          child: Row(
            children: [
              Icon(widget.icon, size: 13, color: colour),
              const SizedBox(width: Space.sm),
              Expanded(
                child: Text(
                  widget.name,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(
                    fontSize: 11.5,
                    color: colour,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );

    if (widget.onDropObject == null) return row;

    return DragTarget<ObjectDrag>(
      onWillAcceptWithDetails: (_) {
        setState(() => _catching = true);
        return true;
      },
      onLeave: (_) => setState(() => _catching = false),
      onAcceptWithDetails: (details) {
        setState(() => _catching = false);
        widget.onDropObject!(details.data);
      },
      builder: (context, candidate, _) => row,
    );
  }
}

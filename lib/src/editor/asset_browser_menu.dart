part of 'asset_browser.dart';

// The menu an asset offers, which is public because the tile is not the
// only place it is raised from.

/// The right-click menu, wherever it is opened from.
///
/// One menu rather than two, because what somebody can make does not depend
/// on whether the pointer happened to be over a file when they asked. What a
/// tile adds is what can be done *to* it, at the top where it reads first.
///
/// Built with [MenuAnchor] rather than `showMenu`, which cannot nest: the
/// list of things to make had grown into a wall of six, and every kind added
/// made it worse.
class AssetMenu extends StatefulWidget {
  const AssetMenu({super.key, required this.onCreate, required this.child});

  final ValueChanged<NewAsset> onCreate;

  /// What the menu opens over — the whole browser, so a right-click anywhere
  /// in it opens the menu at the pointer.
  final Widget child;

  /// Opens the menu belonging to the browser this context sits in.
  ///
  /// Static so a tile deep in the grid can open the one menu rather than
  /// carrying its own, which would put a menu controller on every file in
  /// the project.
  static void open(
    BuildContext context,
    Offset at, {
    VoidCallback? onOpen,
    VoidCallback? onRename,
    VoidCallback? onDelete,
    VoidCallback? onBuild,
  }) {
    context.findAncestorStateOfType<_AssetMenuState>()?.show(
      at,
      onOpen: onOpen,
      onRename: onRename,
      onDelete: onDelete,
      onBuild: onBuild,
    );
  }

  @override
  State<AssetMenu> createState() => _AssetMenuState();
}

class _AssetMenuState extends State<AssetMenu> {
  final MenuController _controller = MenuController();
  final GlobalKey _anchor = GlobalKey();

  VoidCallback? _onOpen;
  VoidCallback? _onRename;
  VoidCallback? _onDelete;
  VoidCallback? _onBuild;

  void show(
    Offset at, {
    VoidCallback? onOpen,
    VoidCallback? onRename,
    VoidCallback? onDelete,
    VoidCallback? onBuild,
  }) {
    final box = _anchor.currentContext?.findRenderObject() as RenderBox?;
    if (box == null) return;

    setState(() {
      _onOpen = onOpen;
      _onRename = onRename;
      _onDelete = onDelete;
      _onBuild = onBuild;
    });
    // Reopened rather than moved: a menu already showing somewhere else would
    // otherwise stay where it was and look like the right-click did nothing.
    _controller.close();
    _controller.open(position: box.globalToLocal(at));
  }

  static final MenuStyle _style = MenuStyle(
    backgroundColor: const WidgetStatePropertyAll(OrblitColors.raised),
    surfaceTintColor: const WidgetStatePropertyAll(Colors.transparent),
    shape: WidgetStatePropertyAll(
      RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(Radii.panel),
        side: const BorderSide(color: OrblitColors.line),
      ),
    ),
  );

  Widget _item(String label, IconData icon, VoidCallback? onPressed) {
    return MenuItemButton(
      onPressed: onPressed,
      leadingIcon: Icon(icon, size: 14, color: OrblitColors.inkMid),
      child: Text(label, style: OrblitText.label),
    );
  }

  Widget _make(NewAsset what) => MenuItemButton(
    onPressed: () => widget.onCreate(what),
    leadingIcon: Icon(what.icon, size: 14, color: OrblitColors.inkMid),
    child: Row(
      children: [
        Text(what.label, style: OrblitText.label),
        // The extension is what somebody is really choosing between, so
        // it is shown rather than left to be guessed from the name.
        if (what.extension.isNotEmpty) ...[
          const SizedBox(width: Space.md),
          Text(what.extension, style: OrblitText.caption),
        ],
      ],
    ),
  );

  @override
  Widget build(BuildContext context) {
    final acting =
        _onOpen != null ||
        _onRename != null ||
        _onDelete != null ||
        _onBuild != null;

    return MenuAnchor(
      key: _anchor,
      controller: _controller,
      style: _style,
      menuChildren: [
        if (acting) ...[
          // First, because somebody who right-clicked a script wants to know
          // whether it compiles more often than they want to rename it.
          if (_onBuild != null) _item('Build', Icons.build_outlined, _onBuild),
          _item('Open', Icons.open_in_new, _onOpen),
          _item('Rename', Icons.drive_file_rename_outline, _onRename),
          _item('Delete', Icons.delete_outline, _onDelete),
          const Divider(height: 9, color: OrblitColors.line),
        ],
        _make(NewAsset.folder),
        const Divider(height: 9, color: OrblitColors.line),
        for (final group in NewAssetGroup.values)
          SubmenuButton(
            menuStyle: _style,
            leadingIcon: Icon(group.icon, size: 14, color: OrblitColors.inkMid),
            menuChildren: [for (final what in group.members) _make(what)],
            child: Text(group.label, style: OrblitText.label),
          ),
        const Divider(height: 9, color: OrblitColors.line),
        // The documents, which are what somebody opens rather than what they
        // write: a scene and a screen, not a file to type into.
        _make(NewAsset.canvas),
        _make(NewAsset.scene),
      ],
      child: widget.child,
    );
  }
}

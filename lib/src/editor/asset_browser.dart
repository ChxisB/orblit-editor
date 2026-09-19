import 'dart:async';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'asset_preview.dart';
import 'assets.dart';
import 'cook_status.dart';
import 'scene.dart';

/// The project's files, along the bottom.
///
/// Along the bottom rather than beside the outliner, because the two answer
/// different questions — what is *in* this scene, against what this project
/// *has* — and stacking them in one column makes each look like part of the
/// other.
class AssetBrowser extends StatefulWidget {
  const AssetBrowser({
    super.key,
    required this.tree,
    this.height,
    this.onOpenAsset,
    this.onSelectAsset,
    this.onProblem,
    this.onMakePrefab,
    this.onBuild,
    this.cookStatus,
  });

  final AssetTree tree;

  /// Where each asset stands with the cook, for the mark beside each file.
  ///
  /// Optional, because the browser is worth having before a project has been
  /// cooked once and in tests that are about folders rather than about
  /// builds. Without one the panel simply draws no marks.
  final CookStatusIndex? cookStatus;

  /// How tall to be, or null to fill whatever it is put in.
  ///
  /// Null is the ordinary case now that panels are docked: a panel in a
  /// layout is given its space by the layout, and one that insisted on a
  /// height would fight whatever it was docked beside.
  final double? height;

  /// Called when somebody drags an object out of the scene and drops it here.
  ///
  /// The browser knows where it was dropped; the shell knows what the object
  /// is. This is where the two meet.
  final void Function(ObjectDrag object, String directory)? onMakePrefab;

  /// Called when somebody asks for a source file to be compiled.
  final ValueChanged<Asset>? onBuild;

  /// Called when somebody opens a file, rather than a folder.
  final ValueChanged<Asset>? onOpenAsset;

  /// Called when the selection changes, including when it is cleared.
  ///
  /// One click, not two: a data object is edited in the inspector, and having
  /// to double-click to see what is in a file would be a rule that applies to
  /// exactly one kind of asset.
  final ValueChanged<Asset?>? onSelectAsset;

  /// Called when a file operation fails, so the shell can say so.
  final ValueChanged<String>? onProblem;

  @override
  State<AssetBrowser> createState() => _AssetBrowserState();
}

class _AssetBrowserState extends State<AssetBrowser> {
  late String _directory = widget.tree.root;
  String? _selected;

  /// The selected file itself, which the preview needs — its kind decides
  /// whether there is anything to draw, and its path is only half of that.
  Asset? _showing;

  /// Whether the preview is open. A panel along the bottom does not have
  /// width to spare, so somebody working in a narrow window can shut it.
  bool _previewing = true;

  /// Bumped to force a re-read, by the watcher or by the button.
  int _revision = 0;

  StreamSubscription<void>? _changes;

  @override
  void initState() {
    super.initState();
    _listen();
    widget.cookStatus?.addListener(_onCookStatus);
    // Not awaited: the panel should be drawable before the first answer, and
    // on a large project the first answer is a second or two away.
    unawaited(widget.cookStatus?.refresh() ?? Future.value());
  }

  void _listen() {
    _changes?.cancel();
    _changes = widget.tree.changes.listen((_) {
      if (!mounted) return;
      setState(() => _revision++);
      // Anything that changed a file may have changed what the cook would do
      // with it. The index coalesces this, so a save that touches forty files
      // is one answer rather than forty.
      unawaited(widget.cookStatus?.refresh() ?? Future.value());
    });
  }

  void _onCookStatus() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _changes?.cancel();
    widget.cookStatus?.removeListener(_onCookStatus);
    super.dispose();
  }

  @override
  void didUpdateWidget(AssetBrowser oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.cookStatus != widget.cookStatus) {
      oldWidget.cookStatus?.removeListener(_onCookStatus);
      widget.cookStatus?.addListener(_onCookStatus);
      unawaited(widget.cookStatus?.refresh() ?? Future.value());
    }
    if (oldWidget.tree.root != widget.tree.root) {
      _directory = widget.tree.root;
      _selected = null;
      _showing = null;
      _listen();
    }
  }

  Future<void> _confirmDelete(Asset asset) async {
    final agreed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: OrblitColors.surface,
        title: Text('Delete ${asset.name}?', style: OrblitText.title),
        content: Text(
          asset.isFolder
              ? 'This deletes the folder and everything in it. It does not go '
                    'to the Trash, and undo does not cover files.'
              : 'This does not go to the Trash, and undo does not cover files.',
          style: OrblitText.body,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(false),
            child: const Text('Cancel'),
          ),
          TextButton(
            onPressed: () => Navigator.of(context).pop(true),
            child: const Text('Delete'),
          ),
        ],
      ),
    );
    if (agreed != true) return;

    final problem = widget.tree.delete(asset.path);
    if (problem != null) {
      widget.onProblem?.call('Could not delete ${asset.name}: $problem');
      return;
    }
    if (mounted) {
      setState(() {
        _revision++;
        if (_selected == asset.path) _selected = null;
      });
    }
  }

  /// Makes a new folder or file in the folder being looked at.
  ///
  /// The name box opens with a suggestion already in it and the stem
  /// selected, so the common case is type-and-return. What comes out is
  /// opened straight away when it is a file: nobody asks for a new script in
  /// order to look at its icon.
  Future<void> _promptCreate(NewAsset what) async {
    final name = await promptForName(
      context,
      title: 'New ${what.label.toLowerCase()}',
      initial: what.suggested,
      hint: what.isFolder ? null : 'Adds ${what.extension} for you',
      action: 'Create',
    );
    if (name == null || !mounted) return;

    final made = widget.tree.create(_directory, what, name);
    if (made.problem != null) {
      widget.onProblem?.call('Could not create it: ${made.problem}');
      return;
    }

    setState(() {
      _revision++;
      _selected = made.path;
    });

    if (!what.isFolder && made.path != null) {
      widget.onOpenAsset?.call(widget.tree.describe(made.path!));
    }
  }

  Future<void> _promptRename(Asset asset) async {
    final name = await promptForName(
      context,
      title: 'Rename',
      initial: asset.name,
      action: 'Rename',
    );
    if (name == null || !mounted) return;

    final problem = widget.tree.rename(asset.path, name);
    if (problem != null) {
      widget.onProblem?.call('Could not rename ${asset.name}: $problem');
      return;
    }
    if (mounted) setState(() => _revision++);
  }

  @override
  Widget build(BuildContext context) {
    // Read once per build rather than per row.
    final entries = widget.tree.read(_directory);
    final folders = widget.tree.folders();

    return _sized(
      // One menu for the whole panel: the header button, the empty space
      // between tiles and every file in the grid all open the same one,
      // rather than a menu controller per file in the project.
      AssetMenu(
        onCreate: _promptCreate,
        child: Container(
          decoration: const BoxDecoration(
            color: OrblitColors.surface,
            border: Border(top: BorderSide(color: OrblitColors.lineSoft)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _Header(
                crumb: widget.tree.relative(_directory),
                count: entries.length,
                cookStatus: widget.cookStatus,
                previewing: _previewing,
                onPreview: () => setState(() => _previewing = !_previewing),
                canGoUp: !p.equals(_directory, widget.tree.root),
                onUp: () => setState(() {
                  _directory = p.dirname(_directory);
                  _selected = null;
                  _showing = null;
                }),
                onRefresh: () => setState(() => _revision++),
              ),
              Expanded(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _FolderTree(
                      key: ValueKey(_revision),
                      tree: widget.tree,
                      folders: folders,
                      current: _directory,
                      onOpen: (path) {
                        setState(() {
                          _directory = path;
                          _selected = null;
                          _showing = null;
                        });
                        widget.onSelectAsset?.call(null);
                      },
                      onDropObject: widget.onMakePrefab,
                    ),
                    Expanded(
                      child: _Grid(
                        key: ValueKey('$_directory/$_revision'),
                        entries: entries,
                        cookStatus: widget.cookStatus,
                        selected: _selected,
                        onSelect: (asset) {
                          setState(() {
                            _selected = asset.path;
                            _showing = asset;
                          });
                          widget.onSelectAsset?.call(asset);
                        },
                        onDelete: _confirmDelete,
                        onRename: _promptRename,
                        onBuild: widget.onBuild,
                        onDropObject: widget.onMakePrefab == null
                            ? null
                            : (object) =>
                                  widget.onMakePrefab!(object, _directory),
                        onOpen: (asset) {
                          if (!asset.isFolder) {
                            widget.onOpenAsset?.call(asset);
                            return;
                          }
                          setState(() {
                            _directory = asset.path;
                            _selected = null;
                            _showing = null;
                          });
                        },
                      ),
                    ),
                    if (_previewing) AssetPreview(asset: _showing),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _sized(Widget child) => widget.height == null
      ? child
      : SizedBox(height: widget.height, child: child);
}

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

/// The files in the current folder.
class _Grid extends StatelessWidget {
  const _Grid({
    super.key,
    required this.entries,
    required this.selected,
    required this.onSelect,
    required this.onOpen,
    required this.onDelete,
    required this.onRename,
    this.onBuild,
    this.onDropObject,
    this.cookStatus,
  });

  final List<Asset> entries;
  final CookStatusIndex? cookStatus;
  final String? selected;
  final ValueChanged<Asset> onSelect;
  final ValueChanged<Asset> onOpen;
  final ValueChanged<Asset> onDelete;
  final ValueChanged<Asset> onRename;

  /// Offered only for the files that can be built. Null for the rest, so the
  /// menu does not carry an action whose answer is "not that kind of file".
  final ValueChanged<Asset>? onBuild;

  final ValueChanged<ObjectDrag>? onDropObject;

  @override
  Widget build(BuildContext context) {
    final grid = entries.isEmpty
        ? Center(
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text('This folder is empty.', style: OrblitText.caption),
                const SizedBox(height: Space.xs),
                Text(
                  'Right-click to add something.',
                  style: OrblitText.caption,
                ),
              ],
            ),
          )
        : _grid();

    // Opaque so the right-click lands on the gaps between tiles and on the
    // empty folder, which is exactly where somebody reaches for "new".
    final catching = GestureDetector(
      behavior: HitTestBehavior.opaque,
      onSecondaryTapUp: (details) =>
          AssetMenu.open(context, details.globalPosition),
      child: grid,
    );

    if (onDropObject == null) return catching;

    return DragTarget<ObjectDrag>(
      onWillAcceptWithDetails: (_) => true,
      onAcceptWithDetails: (details) => onDropObject!(details.data),
      builder: (context, candidate, _) => Stack(
        fit: StackFit.expand,
        children: [
          catching,
          // Only while something is over it: an outline drawn all the time
          // would be one more line in a panel that is mostly lines.
          if (candidate.isNotEmpty)
            IgnorePointer(
              child: Container(
                decoration: BoxDecoration(
                  color: OrblitColors.emberWash,
                  border: Border.all(color: OrblitColors.ember),
                  borderRadius: BorderRadius.circular(Radii.control),
                ),
                alignment: Alignment.center,
                child: Text(
                  'Drop to make a prefab',
                  style: OrblitText.label.copyWith(color: OrblitColors.ink),
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _grid() {
    return GridView.builder(
      padding: const EdgeInsets.all(Space.sm),
      gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
        maxCrossAxisExtent: 96,
        // Taller than it was, for the thumbnail. A grid of identical glyphs
        // tells you the kind of every file and which file is which of none
        // of them, and finding a texture by name in four hundred is not
        // finding it.
        mainAxisExtent: 100,
        crossAxisSpacing: Space.xs,
        mainAxisSpacing: Space.xs,
      ),
      itemCount: entries.length,
      itemBuilder: (context, index) {
        final asset = entries[index];
        return _Tile(
          asset: asset,
          cookState: asset.isFolder ? null : cookStatus?[asset.path],
          selected: asset.path == selected,
          onTap: () => onSelect(asset),
          onDoubleTap: () => onOpen(asset),
          onDelete: () => onDelete(asset),
          onRename: () => onRename(asset),
          onBuild: onBuild == null || !asset.canBuild
              ? null
              : () => onBuild!(asset),
        );
      },
    );
  }
}

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

class _Tile extends StatefulWidget {
  const _Tile({
    required this.asset,
    required this.selected,
    required this.onTap,
    required this.onDoubleTap,
    required this.onDelete,
    required this.onRename,
    this.onBuild,
    this.cookState,
  });

  final Asset asset;

  /// Where this file stands with the cook, or null when nothing has been
  /// asked — a folder, a file outside the assets folder, a project nobody
  /// has cooked.
  final CookState? cookState;
  final bool selected;
  final VoidCallback onTap;
  final VoidCallback onDoubleTap;
  final VoidCallback onDelete;
  final VoidCallback onRename;
  final VoidCallback? onBuild;

  @override
  State<_Tile> createState() => _TileState();
}

class _TileState extends State<_Tile> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final asset = widget.asset;

    final state = widget.cookState;
    final tile = Tooltip(
      message:
          '${asset.name}\n${asset.kind.label}'
          '${asset.bytes == null ? '' : ' · ${asset.size}'}'
          '${state == null ? '' : '\n${state.label}. ${state.explanation}'}',
      waitDuration: const Duration(milliseconds: 600),
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hovering = true),
        onExit: (_) => setState(() => _hovering = false),
        child: GestureDetector(
          onTap: widget.onTap,
          onDoubleTap: widget.onDoubleTap,
          onSecondaryTapUp: (details) => AssetMenu.open(
            context,
            details.globalPosition,
            onOpen: widget.onDoubleTap,
            onRename: widget.onRename,
            onDelete: widget.onDelete,
            onBuild: widget.onBuild,
          ),
          child: Container(
            padding: const EdgeInsets.symmetric(vertical: Space.sm),
            decoration: BoxDecoration(
              color: widget.selected
                  ? OrblitColors.emberWash
                  : (_hovering ? OrblitColors.raised : Colors.transparent),
              borderRadius: BorderRadius.circular(Radii.control),
              border: Border.all(
                color: widget.selected
                    ? OrblitColors.ember
                    : Colors.transparent,
              ),
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                _CookMark(
                  state: state,
                  child: _Thumbnail(asset: asset, selected: widget.selected),
                ),
                const SizedBox(height: Space.xs),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 4),
                  child: Text(
                    asset.name,
                    maxLines: 2,
                    textAlign: TextAlign.center,
                    overflow: TextOverflow.ellipsis,
                    style: OrblitText.label.copyWith(
                      fontSize: 11,
                      color: widget.selected
                          ? OrblitColors.ink
                          : OrblitColors.inkMid,
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );

    // Folders are for navigating, not for dropping into a scene.
    if (asset.isFolder) return tile;

    return Draggable<String>(
      data: asset.path,
      dragAnchorStrategy: pointerDragAnchorStrategy,
      feedback: _DragLabel(asset: asset),
      childWhenDragging: Opacity(opacity: 0.35, child: tile),
      child: tile,
    );
  }
}

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

/// What an asset looks like, where that can be shown, and its kind where it
/// cannot.
///
/// Only the formats Flutter can decode get a picture. A .ktx2 is a compressed
/// texture meant for a GPU and a .hdr carries more range than a screen has;
/// neither is something `Image.file` can open, and pretending otherwise would
/// put a broken-image box where an icon at least says what the file is.
/// Those, and everything that is not an image at all, keep the glyph — until
/// the renderer draws their previews, which is the next piece of this.
class _Thumbnail extends StatelessWidget {
  const _Thumbnail({required this.asset, required this.selected});

  final Asset asset;
  final bool selected;

  /// What Flutter's own decoders handle.
  static const _decodable = {'.png', '.jpg', '.jpeg', '.gif', '.webp', '.bmp'};

  static bool showsPicture(Asset asset) =>
      !asset.isFolder &&
      asset.kind == AssetKind.texture &&
      _decodable.contains(p.extension(asset.path).toLowerCase());

  @override
  Widget build(BuildContext context) {
    final colour = asset.isFolder
        ? OrblitColors.inkMid
        : (selected ? OrblitColors.ember : OrblitColors.inkDim);

    if (!showsPicture(asset)) {
      return SizedBox(
        height: 44,
        child: Center(child: Icon(asset.kind.icon, size: 26, color: colour)),
      );
    }

    return SizedBox(
      height: 44,
      width: 44,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(3),
        // Checked, so a texture with transparency reads as transparent
        // rather than as a hole or as black.
        child: ColoredBox(
          color: OrblitColors.ground,
          child: Image.file(
            File(asset.path),
            fit: BoxFit.cover,
            filterQuality: FilterQuality.medium,
            // Decoded at the size it is drawn at rather than at the size it
            // was authored at. A folder of 4K maps is a gigabyte of pixels
            // nobody is looking at closely.
            cacheWidth: 88,
            gaplessPlayback: true,
            errorBuilder: (context, error, stack) =>
                Center(child: Icon(asset.kind.icon, size: 26, color: colour)),
          ),
        ),
      ),
    );
  }
}

/// What follows the pointer while an asset is dragged.
class _DragLabel extends StatelessWidget {
  const _DragLabel({required this.asset});

  final Asset asset;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: Space.sm,
          vertical: Space.xs,
        ),
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(Radii.control),
          border: Border.all(color: OrblitColors.ember),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(asset.kind.icon, size: 13, color: OrblitColors.ember),
            const SizedBox(width: Space.sm),
            Text(
              asset.name,
              style: OrblitText.label.copyWith(color: OrblitColors.ink),
            ),
          ],
        ),
      ),
    );
  }
}

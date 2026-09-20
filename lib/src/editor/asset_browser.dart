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

part 'asset_browser_chrome.dart';
part 'asset_browser_folders.dart';
part 'asset_browser_grid.dart';
part 'asset_browser_menu.dart';
part 'asset_browser_cooking.dart';

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

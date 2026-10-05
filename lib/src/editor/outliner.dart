import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;

import '../platform/command_shortcuts.dart';
import '../theme/orblit_theme.dart';
import '../widgets/filter_field.dart';
import '../widgets/icon_tile.dart';
import 'scene.dart';
import 'workspace.dart';

/// Where a dragged row would land if it were dropped now.
enum DropKind {
  /// Above the row it is over, as a sibling.
  before,

  /// Inside it, as a child.
  inside,

  /// Below it, as a sibling.
  after,
}

/// Where something is being dropped.
typedef Drop = ({String sceneId, String? parentId, int index});

/// One row of the flattened tree. A null [object] is a scene's own row.
typedef OutlinerRow = ({
  SceneEntry entry,
  SceneObject? object,
  int depth,
  bool hasChildren,
});

/// What is in the open scenes, as a tree.
///
/// Each scene is a root with its objects under it, so several can be worked on
/// at once and it is always clear which one a thing belongs to — the question
/// that a flat list of objects from two files cannot answer.
class Outliner extends StatefulWidget {
  const Outliner({
    super.key,
    required this.workspace,
    required this.selected,
    required this.primary,
    required this.onSelect,
    required this.onSelectScene,
    required this.onLoadScene,
    required this.onMove,
    required this.onDelete,
    required this.onCloseScene,
    required this.onAdd,
  });

  final Workspace workspace;

  /// The selected objects. Empty when a scene itself is selected.
  final Set<String> selected;

  /// The one the inspector shows, and the anchor a shift-click ranges from.
  final String? primary;

  /// [additive] toggles one in or out; [range] takes everything between the
  /// anchor and this row.
  final void Function(String id, {bool additive, bool range}) onSelect;

  /// Selecting a scene's row, which shows what it is without opening it.
  final ValueChanged<SceneEntry> onSelectScene;

  /// Loading a scene, which unloads whatever was loaded before.
  final ValueChanged<SceneEntry> onLoadScene;

  /// Called with what is being moved and where it should land.
  final void Function(String id, Drop drop) onMove;

  final ValueChanged<String> onDelete;
  final ValueChanged<SceneEntry> onCloseScene;

  /// The plus beside the filter: something to put in the scene.
  final VoidCallback onAdd;

  @override
  State<Outliner> createState() => _OutlinerState();
}

class _OutlinerState extends State<Outliner> {
  /// Collapsed rather than expanded, so the set is empty for a fresh scene and
  /// a newly added object is visible rather than hidden inside a closed parent.
  final Set<String> _collapsed = {};

  /// Which scene's row is highlighted, which is not the same as which is
  /// loaded — a scene can be looked at before it is opened.
  String? _selectedScene;

  /// While it holds something, every match is shown whatever was collapsed:
  /// a match inside a closed parent is a match nobody can see.
  final _filter = TextEditingController();

  @override
  void dispose() {
    _filter.dispose();
    super.dispose();
  }

  /// Rows in draw order, skipping anything inside a collapsed parent.
  List<OutlinerRow> get _rows {
    final query = _filter.text.trim().toLowerCase();

    // Shared first, because what every scene has comes before whichever one
    // is open.
    return [
      for (final entry in [
        widget.workspace.sharedEntry,
        ...widget.workspace.entries,
      ])
        ..._rowsOf(entry, query),
    ];
  }

  /// One scene's own row, then its objects unless it is closed.
  List<OutlinerRow> _rowsOf(SceneEntry entry, String query) {
    final scene = entry.scene;
    final kept = scene == null || query.isEmpty ? null : _keptBy(scene, query);
    final named = entry.title.toLowerCase().contains(query);
    if (query.isNotEmpty && !named && (kept?.isEmpty ?? true)) return const [];

    final rows = <OutlinerRow>[
      (
        entry: entry,
        object: null,
        depth: 0,
        hasChildren: scene != null && scene.roots.isNotEmpty,
      ),
    ];

    // An unloaded scene has no objects to show. Listing children it does not
    // hold would suggest they could be edited, and they cannot.
    if (scene == null || (kept == null && _collapsed.contains(entry.id))) {
      return rows;
    }

    bool shown(SceneObject object) => kept == null || kept.contains(object.id);

    void walk(List<SceneObject> objects, int depth) {
      for (final object in objects.where(shown)) {
        final children = scene.childrenOf(object.id).where(shown).toList();
        rows.add((
          entry: entry,
          object: object,
          depth: depth,
          hasChildren: children.isNotEmpty,
        ));
        if (kept != null || !_collapsed.contains(object.id)) {
          walk(children, depth + 1);
        }
      }
    }

    walk(scene.roots, 1);
    return rows;
  }

  /// The objects whose names hold [query], and every parent above them, so a
  /// match shows where it sits in the tree and not loose.
  Set<String> _keptBy(EditorScene scene, String query) {
    final kept = <String>{};
    for (final object in scene.objects) {
      if (!scene.displayNameOf(object).toLowerCase().contains(query)) continue;
      String? id = object.id;
      while (id != null && kept.add(id)) {
        id = scene[id]?.parentId;
      }
    }
    return kept;
  }

  void _toggle(String key) => setState(() {
        if (!_collapsed.remove(key)) _collapsed.add(key);
      });

  /// Turns a drop on a row into a place in the tree.
  Drop _placeFor(OutlinerRow row, DropKind kind) {
    final object = row.object;
    final scene = row.entry.scene!;

    // Onto a scene's own row: into that scene, at the top level.
    if (object == null) {
      return (
        sceneId: row.entry.id,
        parentId: null,
        index: kind == DropKind.before ? 0 : scene.roots.length,
      );
    }

    if (kind == DropKind.inside) {
      return (
        sceneId: row.entry.id,
        parentId: object.id,
        index: scene.childrenOf(object.id).length,
      );
    }

    final at = scene.indexOf(object.id);
    return (
      sceneId: row.entry.id,
      parentId: object.parentId,
      index: kind == DropKind.before ? at : at + 1,
    );
  }

  @override
  Widget build(BuildContext context) {
    final rows = _rows;

    // No title of its own: the tab above it already says what it is.
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(8, 2, 6, 8),
          child: Row(
            spacing: Space.xs,
            children: [
              Expanded(
                child: FilterField(
                  controller: _filter,
                  hint: 'Filter',
                  onChanged: (_) => setState(() {}),
                ),
              ),
              IconTile(
                tooltip: 'Add an object',
                size: 28,
                iconSize: 16,
                radius: Radii.control,
                onTap: widget.onAdd,
                child: const Icon(Icons.add),
              ),
            ],
          ),
        ),
        Expanded(
          child: rows.isEmpty
              ? const _NothingMatches()
              : ListView.builder(
                  padding: const EdgeInsets.fromLTRB(6, 0, 6, 8),
                  itemCount: rows.length,
                  itemBuilder: (context, index) => _rowAt(rows[index]),
                ),
        ),
        _SceneCount(count: widget.workspace.entries.length),
      ],
    );
  }

  Widget _rowAt(OutlinerRow row) {
    final key = row.object?.id ?? row.entry.id;

    return _Row(
      key: ValueKey('${row.entry.id}/$key'),
      row: row,
      workspace: widget.workspace,
      selected: row.object == null
          ? widget.selected.isEmpty && _selectedScene == row.entry.id
          : widget.selected.contains(row.object!.id),
      primary: row.object?.id == widget.primary,
      collapsed: _collapsed.contains(key),
      onTap: () {
        if (row.object != null) {
          widget.onSelect(
            row.object!.id,
            additive: isCommandModifierPressed,
            range: HardwareKeyboard.instance.isShiftPressed,
          );
          return;
        }
        setState(() => _selectedScene = row.entry.id);
        widget.onSelectScene(row.entry);
      },
      onDoubleTap: row.object == null
          ? () => widget.onLoadScene(row.entry)
          : null,
      onToggle: () => _toggle(key),
      onDrop: (id, kind) => widget.onMove(id, _placeFor(row, kind)),
      onDelete: () => row.object == null
          ? widget.onCloseScene(row.entry)
          : widget.onDelete(row.object!.id),
    );
  }
}

/// What is shown when the filter holds nothing the scenes have.
class _NothingMatches extends StatelessWidget {
  const _NothingMatches();

  @override
  Widget build(BuildContext context) => const Padding(
    padding: EdgeInsets.all(Space.lg),
    child: Text('Nothing here has that name.', style: OrblitText.caption),
  );
}

/// How many scenes are open, along the foot of the panel.
class _SceneCount extends StatelessWidget {
  const _SceneCount({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) => Container(
    height: 24,
    padding: const EdgeInsets.symmetric(horizontal: Space.md),
    alignment: Alignment.centerLeft,
    decoration: const BoxDecoration(
      border: Border(top: BorderSide(color: OrblitColors.lineSoft)),
    ),
    child: Text(
      '$count scene${count == 1 ? '' : 's'} open',
      style: OrblitText.mono.copyWith(fontSize: 10.5),
    ),
  );
}

class _Row extends StatefulWidget {
  const _Row({
    super.key,
    required this.row,
    required this.workspace,
    required this.selected,
    required this.primary,
    required this.collapsed,
    required this.onTap,
    required this.onDoubleTap,
    required this.onToggle,
    required this.onDrop,
    required this.onDelete,
  });

  final OutlinerRow row;
  final Workspace workspace;
  final bool selected;

  /// The one of several the inspector is showing.
  final bool primary;

  final bool collapsed;
  final VoidCallback onTap;
  final VoidCallback? onDoubleTap;
  final VoidCallback onToggle;
  final void Function(String id, DropKind kind) onDrop;
  final VoidCallback onDelete;

  @override
  State<_Row> createState() => _RowState();
}

class _RowState extends State<_Row> {
  bool _hovering = false;
  DropKind? _dropping;

  static const double _height = 28;

  /// How far each level of the tree steps in, and the room a disclosure
  /// arrow takes before the icon, so a leaf lines up with the folders
  /// beside it.
  static const double _indent = 18;

  bool get _isScene => widget.row.object == null;

  bool get _isLoaded => widget.row.entry.isLoaded;

  /// Which of the three the pointer is over.
  ///
  /// The edges reorder and the middle reparents, which is how every tree that
  /// does both distinguishes them — and the reason the bands are a quarter
  /// each rather than a third is that reparenting is the commoner intent and
  /// deserves the bigger target.
  DropKind _kindFor(Offset local) {
    if (_isScene) return DropKind.inside;
    if (local.dy < _height * 0.25) return DropKind.before;
    if (local.dy > _height * 0.75) return DropKind.after;
    return DropKind.inside;
  }

  /// Whether this row would accept the thing being dragged.
  ///
  /// Refused before the drop rather than after: an editor that lets you drop
  /// and then shows an error has already made you do the work twice.
  bool _accepts(String id) {
    // Nothing can be dropped into a scene that is not loaded — there is no
    // document there to put it in.
    if (!_isLoaded) return false;

    final object = widget.row.object;
    if (object == null) return true;
    if (id == object.id) return false;

    final holder = widget.workspace.sceneHolding(id);
    if (holder == null) return false;

    // From another scene — out of the shared set, most often. Nothing to
    // check: an object cannot be its own ancestor across two documents, and
    // the move takes it out of one and puts it in the other.
    if (holder.id != widget.row.entry.id) return true;

    return !holder.scene!.isAncestorOf(id, object.id);
  }

  @override
  Widget build(BuildContext context) {
    final row = DragTarget<ObjectDrag>(
      onWillAcceptWithDetails: (details) => _accepts(details.data.id),
      onMove: _onMove,
      onLeave: (_) => setState(() => _dropping = null),
      onAcceptWithDetails: _onDrop,
      builder: (context, candidate, _) =>
          _content(candidate.isEmpty ? null : _dropping),
    );

    // A scene's row is a drop target and a heading, not something to drag.
    if (_isScene) {
      return Tooltip(
        message: _isLoaded ? _name : 'Double-click to load $_name',
        waitDuration: const Duration(milliseconds: 700),
        child: row,
      );
    }

    return Draggable<ObjectDrag>(
      data: ObjectDrag(widget.row.object!.id, _name),
      dragAnchorStrategy: pointerDragAnchorStrategy,
      feedback: _DragLabel(name: _name, icon: _icon),
      child: row,
    );
  }

  // Through the scene rather than off the object, so a light that follows
  // the sky says Moon here at the same moment the viewport goes dark.
  String get _name {
    final object = widget.row.object;
    final entry = widget.row.entry;
    final scene = entry.scene;

    return object == null
        ? (entry.id == widget.workspace.sharedEntry.id
              ? 'In every scene'
              : entry.title)
        : (scene?.displayNameOf(object) ?? object.name);
  }

  IconData get _icon {
    final object = widget.row.object;
    final scene = widget.row.entry.scene;

    return object == null
        ? (_isLoaded ? Icons.public : Icons.public_off)
        : (scene?.displayIconOf(object) ?? object.icon);
  }

  void _onMove(DragTargetDetails<ObjectDrag> details) {
    final box = context.findRenderObject() as RenderBox?;
    if (box == null) return;
    final kind = _kindFor(box.globalToLocal(details.offset));
    if (kind != _dropping) setState(() => _dropping = kind);
  }

  void _onDrop(DragTargetDetails<ObjectDrag> details) {
    final kind = _dropping ?? DropKind.inside;
    setState(() => _dropping = null);
    widget.onDrop(details.data.id, kind);
  }

  // A fill and a rim for a reparent, a line for a reorder: the two answers
  // look different because they are different.
  BoxDecoration _decoration(DropKind? dropping) => BoxDecoration(
    color: widget.selected
        ? OrblitColors.emberWash
        : (_hovering || dropping == DropKind.inside
              ? OrblitColors.hover
              : Colors.transparent),
    borderRadius: BorderRadius.circular(Radii.control),
    border: dropping == DropKind.inside
        ? Border.all(color: OrblitColors.ember)
        : null,
  );

  // The row itself, and the line that says where a drop would land.
  Widget _content(DropKind? dropping) => MouseRegion(
    cursor: SystemMouseCursors.click,
    onEnter: (_) => setState(() => _hovering = true),
    onExit: (_) => setState(() => _hovering = false),
    child: Padding(
      padding: const EdgeInsets.only(bottom: 1),
      child: Stack(
        children: [
          Container(
            height: _height,
            padding: EdgeInsets.only(
              left: 8 + widget.row.depth * _indent,
              right: 6,
            ),
            decoration: _decoration(dropping),
            child: _RowBar(
              row: widget.row,
              name: _name,
              icon: _icon,
              selected: widget.selected,
              primary: widget.primary,
              collapsed: widget.collapsed,
              hovering: _hovering,
              onTap: widget.onTap,
              onDoubleTap: widget.onDoubleTap,
              onToggle: widget.onToggle,
              onDelete: widget.onDelete,
            ),
          ),
          if (dropping == DropKind.before) const _DropLine(top: true),
          if (dropping == DropKind.after) const _DropLine(top: false),
        ],
      ),
    ),
  );
}

class _DropLine extends StatelessWidget {
  const _DropLine({required this.top});

  final bool top;

  @override
  Widget build(BuildContext context) => Positioned(
    left: 0,
    right: 0,
    top: top ? 0 : null,
    bottom: top ? null : 0,
    height: 2,
    child: const ColoredBox(color: OrblitColors.ember),
  );
}

/// What is inside a row's box: the disclosure arrow, the icon and name, and
/// what belongs at the end.
class _RowBar extends StatelessWidget {
  const _RowBar({
    required this.row,
    required this.name,
    required this.icon,
    required this.selected,
    required this.primary,
    required this.collapsed,
    required this.hovering,
    required this.onTap,
    required this.onDoubleTap,
    required this.onToggle,
    required this.onDelete,
  });

  final OutlinerRow row;
  final String name;
  final IconData icon;
  final bool selected;
  final bool primary;
  final bool collapsed;
  final bool hovering;
  final VoidCallback onTap;
  final VoidCallback? onDoubleTap;
  final VoidCallback onToggle;
  final VoidCallback onDelete;

  bool get _isScene => row.object == null;

  // An unloaded scene is dimmer, because it is a place rather than a thing
  // you can currently change.
  Color get _ink {
    if (selected) return OrblitColors.ink;
    if (_isScene && !row.entry.isLoaded) return OrblitColors.inkDim;
    return hovering ? OrblitColors.ink : OrblitColors.inkMid;
  }

  @override
  Widget build(BuildContext context) {
    // The disclosure arrow sits outside the tap area rather than inside
    // it. A double-tap handler above the arrow makes every single tap on
    // it wait for the double-tap timeout, which is a real lag on the
    // commonest thing anybody does in a tree.
    return Row(
      children: [
        _Disclosure(
          visible: row.hasChildren,
          collapsed: collapsed,
          onTap: onToggle,
        ),
        Expanded(
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: onTap,
            onDoubleTap: onDoubleTap,
            child: Row(
              children: [
                Icon(
                  icon,
                  size: 14,
                  color: selected ? OrblitColors.ember : OrblitColors.inkDim,
                ),
                const SizedBox(width: Space.sm),
                Expanded(
                  child: Text(
                    name,
                    overflow: TextOverflow.ellipsis,
                    style: OrblitText.label.copyWith(
                      color: _ink,
                      fontWeight: _isScene ? FontWeight.w600 : FontWeight.w400,
                    ),
                  ),
                ),
                // A quiet mark on the one of several whose fields
                // the inspector is showing, so a multiple selection
                // does not look like it lost track of itself.
                if (primary && selected)
                  const _Mark(Icons.edit_outlined, color: OrblitColors.ember),
                // An instance says which prefab it is one of. What is
                // under it is that prefab's, and goes where it goes.
                if (row.object?.prefab?.asset case final asset?)
                  _Mark(
                    Icons.widgets_outlined,
                    tooltip: 'Instance of ${p.basename(asset)}',
                  ),
              ],
            ),
          ),
        ),
        _RowTail(
          row: row,
          name: name,
          hovering: hovering,
          onDelete: onDelete,
        ),
      ],
    );
  }
}

/// What follows a row's name at the far end: that a scene is not loaded, that
/// it was never written, and the close button while the pointer is over.
class _RowTail extends StatelessWidget {
  const _RowTail({
    required this.row,
    required this.name,
    required this.hovering,
    required this.onDelete,
  });

  final OutlinerRow row;
  final String name;
  final bool hovering;
  final VoidCallback onDelete;

  @override
  Widget build(BuildContext context) {
    final entry = row.entry;
    final isScene = row.object == null;

    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (isScene && !entry.isLoaded && !hovering)
          Text('not loaded', style: OrblitText.caption.copyWith(fontSize: 10)),
        if (isScene && entry.isLoaded && entry.neverWritten)
          Padding(
            padding: const EdgeInsets.only(right: 4),
            child: Text(
              '•',
              style: OrblitText.mono.copyWith(color: OrblitColors.ember),
            ),
          ),
        if (hovering)
          _RowAction(
            icon: Icons.close,
            tooltip: isScene ? 'Close $name' : 'Delete $name',
            onTap: onDelete,
          ),
      ],
    );
  }
}

/// Where a disclosure arrow goes, and the gap after it. Empty for a row with
/// nothing under it, so its icon still lines up with a folder's.
class _Disclosure extends StatelessWidget {
  const _Disclosure({
    required this.visible,
    required this.collapsed,
    required this.onTap,
  });

  final bool visible;
  final bool collapsed;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => SizedBox(
    width: 22,
    child: visible
        ? GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: onTap,
            child: Align(
              alignment: Alignment.centerLeft,
              child: Icon(
                collapsed ? Icons.chevron_right : Icons.expand_more,
                size: 14,
                color: OrblitColors.inkDim,
              ),
            ),
          )
        : null,
  );
}

/// A small icon after a row's name, with its meaning on hover when it has one.
class _Mark extends StatelessWidget {
  const _Mark(this.icon, {this.color = OrblitColors.inkDim, this.tooltip});

  final IconData icon;
  final Color color;
  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final mark = Icon(icon, size: 11, color: color);
    return Padding(
      padding: const EdgeInsets.only(left: Space.xs),
      child: tooltip == null ? mark : Tooltip(message: tooltip, child: mark),
    );
  }
}

class _RowAction extends StatelessWidget {
  const _RowAction({
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: tooltip,
      child: GestureDetector(
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 3),
          child: Icon(icon, size: 13, color: OrblitColors.inkDim),
        ),
      ),
    );
  }
}

/// What follows the pointer while a row is being dragged.
class _DragLabel extends StatelessWidget {
  const _DragLabel({required this.name, required this.icon});

  final String name;
  final IconData icon;

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
            Icon(icon, size: 13, color: OrblitColors.ember),
            const SizedBox(width: Space.sm),
            Text(name, style: OrblitText.label.copyWith(color: OrblitColors.ink)),
          ],
        ),
      ),
    );
  }
}

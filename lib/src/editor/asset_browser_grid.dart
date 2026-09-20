part of 'asset_browser.dart';

// The files themselves: the grid they are laid out in, one tile, its
// thumbnail, and what is shown under the pointer while one is dragged.

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

import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

import 'export_folder.dart';

/// One asset a pack serves, and the file it comes from.
final class ContentsRow {
  const ContentsRow({
    required this.id,
    required this.bytes,
    required this.hash,
    required this.pack,
  });

  final String id;
  final int bytes;

  /// The first twelve characters of the asset's content hash.
  final String hash;

  /// The file name of the pack that answers for [id]. In a patch's chain it
  /// is the patch that replaced the asset, or the whole pack beneath for one
  /// no patch touched.
  final String pack;
}

/// What an export serves once its patches are applied.
final class PackContents {
  const PackContents({
    required this.packs,
    required this.rows,
    required this.removed,
  });

  /// The file names in the chain, the whole pack first.
  final List<String> packs;

  /// Every asset served, the largest first.
  final List<ContentsRow> rows;

  /// The ids the top pack takes away from the one under it.
  final List<String> removed;

  int get bytes => rows.fold(0, (sum, row) => sum + row.bytes);

  /// Whether more than one pack is mounted, which is when an asset's owner
  /// says something.
  bool get layered => packs.length > 1;
}

/// What the chain ending at [path] serves, read from each pack's directory.
///
/// No blob is read, so the answer for a pack of several gigabytes comes back
/// as fast as one of a few kilobytes.
Future<PackContents> readContents(String projectDirectory, String path) async {
  final folder = await OpenedExports.of(projectDirectory);
  try {
    final top = folder.at(path);
    if (top == null) {
      throw ExportChainBroken(
        '${p.basename(path)} is not a readable pack in the exports folder.',
      );
    }
    final chain = folder.chainTo(top);
    return contentsOf([
      for (final pack in chain) (p.basename(folder.pathOf(pack)), pack),
    ]);
  } finally {
    await folder.close();
  }
}

/// What [chain] serves, as it would be mounted: the whole pack first, then
/// each patch on the one before.
PackContents contentsOf(List<(String name, AssetPack pack)> chain) {
  final mounted = MountedPacks(target: _targetOf(chain.first.$2))
    ..start(chain.first.$2);
  for (final (_, pack) in chain.skip(1)) {
    mounted.patch(pack);
  }
  final names = {for (final (name, pack) in chain) pack: name};
  final rows = [
    for (final MapEntry(:key, :value) in mounted.manifest.entries.entries)
      ContentsRow(
        id: key.toString(),
        bytes: value.bytes,
        hash: value.hash.hex.substring(0, 12),
        // Every id in the manifest has an owner, since the manifest is built
        // from the same map.
        pack: names[mounted.ownerOf(key)]!,
      ),
  ]..sort(_largestFirst);
  return PackContents(
    packs: [for (final (name, _) in chain) name],
    rows: rows,
    removed: [for (final id in chain.last.$2.removed) id.toString()],
  );
}

int _largestFirst(ContentsRow a, ContentsRow b) {
  final bySize = b.bytes.compareTo(a.bytes);
  return bySize != 0 ? bySize : a.id.compareTo(b.id);
}

CookTarget _targetOf(AssetPack pack) {
  final target = CookTargets.find(pack.target);
  if (target == null) {
    throw ExportChainBroken(
      'The pack is for ${pack.target}, which Orblit does not cook for.',
    );
  }
  return target;
}

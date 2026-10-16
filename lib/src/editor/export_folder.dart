import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

/// The folder a project's exports go in.
String exportsPath(String projectDirectory) =>
    p.join(projectDirectory, 'exports');

/// The first twelve characters of [hash], which is how a pack is named in a
/// file and in a sentence.
String shortHash(ContentHash hash) => hash.hex.substring(0, 12);

/// A pack in the exports folder, as plain values.
///
/// Plain values so that a list of them can cross from the isolate that read
/// the folder to the window that draws it. The pack itself holds a file open.
final class ExportedPack {
  const ExportedPack({
    required this.path,
    required this.target,
    required this.hash,
    required this.base,
    required this.assets,
    required this.removed,
    required this.bytes,
    required this.modified,
  });

  final String path;

  /// The cook target the pack was made for.
  final String target;

  /// The directory hash, in hex. It is what a patch names as its base.
  final String hash;

  /// The hash of the pack this one was built on, or null for a whole pack.
  final String? base;

  /// How many assets the pack holds. A patch holds only what changed.
  final int assets;

  /// How many ids a patch takes away from the pack under it.
  final int removed;

  /// The size of the file.
  final int bytes;

  final DateTime modified;

  bool get isPatch => base != null;

  String get fileName => p.basename(path);
}

/// What the exports folder holds.
final class ExportListing {
  const ExportListing({this.packs = const [], this.unreadable = const []});

  /// Newest first.
  final List<ExportedPack> packs;

  /// Files that end in `.opack` and do not open, each with the reason. One
  /// bad file does not hide the others.
  final List<String> unreadable;

  /// The packs for [target], newest first.
  List<ExportedPack> forTarget(String target) => [
    for (final pack in packs)
      if (pack.target == target) pack,
  ];
}

/// A patch was built on a pack that is not in the exports folder.
final class ExportChainBroken implements Exception {
  const ExportChainBroken(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Every pack in a project's exports folder, opened and held open.
///
/// Opening a pack reads its header and its directory and nothing else, so
/// opening a folder of large packs is cheap. Close it when done: each pack
/// holds a file handle, and a handle on a pack blocks renaming it on Windows.
final class OpenedExports {
  OpenedExports._(this._opened, this.unreadable);

  /// Opens every `.opack` file directly in the project's exports folder.
  static Future<OpenedExports> of(String projectDirectory) async {
    final folder = Directory(exportsPath(projectDirectory));
    final opened = <_Opened>[];
    final unreadable = <String>[];
    if (folder.existsSync()) {
      final files =
          folder
              .listSync(followLinks: false)
              .whereType<File>()
              .where((file) => p.extension(file.path) == '.opack')
              .toList()
            ..sort((a, b) => a.path.compareTo(b.path));
      for (final file in files) {
        final source = FilePackBytes(file.path);
        try {
          final pack = await AssetPack.open(source);
          opened.add(_Opened(file, pack, source));
        } on Exception catch (error) {
          await source.close();
          unreadable.add('${p.basename(file.path)}: $error');
        }
      }
    }
    return OpenedExports._(opened, unreadable);
  }

  final List<_Opened> _opened;

  /// The files that did not open, each with the reason.
  final List<String> unreadable;

  /// The pack at [path], or null when no pack in the folder is there.
  AssetPack? at(String path) {
    for (final item in _opened) {
      if (p.equals(item.file.path, path)) return item.pack;
    }
    return null;
  }

  /// The file [pack] was opened from.
  String pathOf(AssetPack pack) =>
      _opened.firstWhere((item) => identical(item.pack, pack)).file.path;

  /// The packs from the whole pack up to [top], lowest first, which is the
  /// order `packBundle` takes a base in and `MountedPacks` mounts them.
  ///
  /// Each patch is found by the hash it names as its base, so what the files
  /// are called and when they were made do not matter.
  List<AssetPack> chainTo(AssetPack top) {
    final byHash = {for (final item in _opened) item.pack.hash: item.pack};
    final chain = <AssetPack>[top];
    var next = top;
    for (var base = next.base; base != null; base = next.base) {
      final below = byHash[base];
      if (below == null) {
        throw ExportChainBroken(
          '${p.basename(pathOf(next))} was built on ${shortHash(base)}, and '
          'no pack in the exports folder is that one.',
        );
      }
      chain.insert(0, below);
      next = below;
    }
    return chain;
  }

  /// What the folder holds, newest first.
  ExportListing listing() {
    final packs = [
      for (final item in _opened)
        ExportedPack(
          path: item.file.path,
          target: item.pack.target,
          hash: item.pack.hash.hex,
          base: item.pack.base?.hex,
          assets: item.pack.manifest.entries.length,
          removed: item.pack.removed.length,
          bytes: item.file.lengthSync(),
          modified: item.file.lastModifiedSync(),
        ),
    ]..sort((a, b) => b.modified.compareTo(a.modified));
    return ExportListing(packs: packs, unreadable: unreadable);
  }

  Future<void> close() async {
    for (final item in _opened) {
      await item.source.close();
    }
  }
}

final class _Opened {
  const _Opened(this.file, this.pack, this.source);

  final File file;
  final AssetPack pack;
  final FilePackBytes source;
}

/// What the exports folder holds, from each pack's directory alone. No blob
/// is read, so a folder of large packs lists at the speed of the disk's
/// first block of each.
Future<ExportListing> listExports(String projectDirectory) async {
  final folder = await OpenedExports.of(projectDirectory);
  try {
    return folder.listing();
  } finally {
    await folder.close();
  }
}

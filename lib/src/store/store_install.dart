import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';

/// Writes [kit] into [projectDirectory] and records it in `kits.lock`.
///
/// With [into], the files go under that folder of the project. With [only],
/// just those files are written and listed. A file the project has with
/// other bytes stops the install with a [KitConflicts], unless the person
/// agreed to its loss by naming it in [replacing]. No file is written when
/// that is thrown, when `kits.lock` is damaged, or when the new lock would
/// be too big, because the lock is settled before the first file goes out.
///
/// The lock lists every kept file, identical ones too, so a later update can
/// tell a file the person edited from one they did not. A second install of
/// the kit into the same folder adds to the files already listed.
Future<LockedKit> installKit(
  Kit kit, {
  required String projectDirectory,
  String into = '',
  Set<AssetId>? only,
  Set<AssetId> replacing = const {},
}) async {
  final lock = _readLock(projectDirectory);
  final clashes = await kit.conflictsIn(
    DirectoryAssetSource(projectDirectory),
    into: into,
    only: only,
  );
  final unagreed = [
    for (final id in clashes)
      if (!replacing.contains(id)) id,
  ];
  if (unagreed.isNotEmpty) throw KitConflicts(unagreed);
  final manifest = kit.manifest;
  final locked = _lockedFor(
    kit,
    into: into,
    only: only,
    earlier: lock.find(manifest.publisher, manifest.slug),
  );
  final next = lock.adding(locked).encode();
  await _extract(
    kit,
    projectDirectory,
    into: into,
    only: only,
    replacing: clashes.toSet(),
  );
  _writeLock(projectDirectory, next);
  return locked;
}

LockedKit _lockedFor(
  Kit kit, {
  required String into,
  required Set<AssetId>? only,
  required LockedKit? earlier,
}) {
  final entries = kit.pack.manifest.entries;
  final manifest = kit.manifest;
  return LockedKit(
    publisher: manifest.publisher,
    slug: manifest.slug,
    version: manifest.version,
    hash: kit.pack.hash,
    into: into,
    files: {
      if (earlier != null && earlier.into == into) ...earlier.files,
      for (final id in kit.chosen(only)) kitPlace(id, into): entries[id]!.hash,
    },
  );
}

// Two calls, so that only the files the person agreed to lose may be
// overwritten. A file that appeared after the check is refused by the first.
Future<void> _extract(
  Kit kit,
  String projectDirectory, {
  required String into,
  required Set<AssetId>? only,
  required Set<AssetId> replacing,
}) async {
  final fresh = {
    for (final id in kit.chosen(only))
      if (!replacing.contains(id)) id,
  };
  if (fresh.isNotEmpty) {
    await extractKit(kit, projectDirectory, into: into, only: fresh);
  }
  if (replacing.isNotEmpty) {
    await extractKit(
      kit,
      projectDirectory,
      overwrite: true,
      into: into,
      only: replacing,
    );
  }
}

File _lockFile(String projectDirectory) =>
    File('$projectDirectory${Platform.pathSeparator}$kitLockName');

KitLock _readLock(String projectDirectory) {
  final file = _lockFile(projectDirectory);
  return file.existsSync()
      ? KitLock.parse(file.readAsBytesSync())
      : KitLock.empty;
}

// Written beside and renamed, so the lock is whole or as it was.
void _writeLock(String projectDirectory, Uint8List bytes) {
  final file = _lockFile(projectDirectory);
  final pending = File('${file.path}.tmp');
  pending.writeAsBytesSync(bytes);
  pending.renameSync(file.path);
}

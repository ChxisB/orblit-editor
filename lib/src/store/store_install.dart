import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// Writes [kit] into [projectDirectory] and records it in `kits.lock`.
///
/// With [into], the files go under that folder of the project. With [only],
/// just those files are written and listed. A file the project has with
/// other bytes stops the install with a [KitConflicts], unless the person
/// agreed to its loss by naming it in [replacing]. Nothing is written when
/// that is thrown, or when `kits.lock` is damaged, because a lock that cannot
/// be read cannot be added to.
///
/// The lock lists every kept file, identical ones too, so a later update can
/// tell a file the person edited from one they did not.
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
  await extractKit(
    kit,
    projectDirectory,
    overwrite: clashes.isNotEmpty,
    into: into,
    only: only,
  );
  final entries = kit.pack.manifest.entries;
  final locked = LockedKit(
    publisher: kit.manifest.publisher,
    slug: kit.manifest.slug,
    version: kit.manifest.version,
    hash: kit.pack.hash,
    into: into,
    files: {
      for (final id in kit.chosen(only)) kitPlace(id, into): entries[id]!.hash,
    },
  );
  _writeLock(projectDirectory, lock.adding(locked));
  return locked;
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
void _writeLock(String projectDirectory, KitLock lock) {
  final file = _lockFile(projectDirectory);
  final pending = File('${file.path}.tmp');
  pending.writeAsBytesSync(lock.encode());
  pending.renameSync(file.path);
}

import 'dart:io';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:path/path.dart' as p;

import 'assets.dart';
import 'model_skeletons.dart';
import 'retarget_dialog.dart';

/// Where the retarget dialog finds models and keeps bone maps: the project
/// folder.
final class ProjectRetargetFiles implements RetargetFiles {
  ProjectRetargetFiles({required this.assets, required this.skeletons});

  final AssetTree assets;
  final ModelSkeletons skeletons;

  static const String folder = 'retargets';

  @override
  List<String> get models => _found(
    (asset) =>
        asset.kind == AssetKind.mesh &&
        const {'.glb', '.gltf'}.contains(p.extension(asset.path).toLowerCase()),
  );

  @override
  List<String> get maps => _found((asset) => asset.kind == AssetKind.boneMap);

  @override
  RestSkeleton? skeletonOf(String model) => skeletons.ofAsset(model);

  @override
  BoneMapLoad readMap(String path) {
    try {
      return BoneMap.decode(File(p.join(assets.root, path)).readAsStringSync());
    } on FileSystemException catch (error) {
      throw BoneMapFormatException(error.osError?.message ?? error.message);
    }
  }

  @override
  ({String? problem, String? path}) writeMap(String name, BoneMap map) {
    final directory = Directory(p.join(assets.root, folder));
    try {
      directory.createSync(recursive: true);
    } on FileSystemException catch (error) {
      return (problem: error.osError?.message ?? error.message, path: null);
    }
    final made = assets.write(
      directory.path,
      '$name$boneMapExtension',
      map.encode(),
    );
    return (
      problem: made.problem,
      path: made.path == null ? null : assets.relative(made.path!),
    );
  }

  /// The project paths of every file under the project that [test] takes.
  List<String> _found(bool Function(Asset) test) {
    final out = <String>[];
    for (final directory in [
      assets.root,
      for (final found in assets.folders()) found.path,
    ]) {
      for (final asset in assets.read(directory)) {
        if (!asset.isFolder && test(asset)) {
          out.add(assets.relative(asset.path));
        }
      }
    }
    return out;
  }
}

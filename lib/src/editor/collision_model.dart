import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_mesh/orblit_mesh.dart';

import 'scene.dart';

/// Reads the geometry the selected object draws in its own frame.
final class ModelCollision {
  ModelCollision(String root) : source = DirectoryAssetSource(root);

  final AssetSource source;

  Future<CollisionMesh> read(SceneObject object) async {
    final mesh = object.currentMesh;
    if (mesh != null && !mesh.isEmpty) {
      final triangles = mesh.triangulate();
      return CollisionMesh(
        vertices: triangles.positions,
        indices: triangles.indices,
      );
    }
    final path = object.meshAsset;
    if (path == null) {
      throw const FormatException('This object has no model geometry.');
    }
    final id = AssetId.parse(path);
    return CollisionMesh.fromGltf(id, await source.read(id), source);
  }
}

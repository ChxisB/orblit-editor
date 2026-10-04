import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/collision_model.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_mesh/orblit_mesh.dart';

void main() {
  test('the editor reads imported model triangles from the project', () async {
    final root = Directory.systemTemp.createTempSync('orblit_collision');
    try {
      final geometry = const Shape(kind: ShapeKind.cube).build().triangulate();
      File('${root.path}/crate.glb')
          .writeAsBytesSync(const Shape(kind: ShapeKind.cube).build().toGlb());
      final collider = await ModelCollision(root.path).read(
        SceneObject(
          id: 'crate',
          name: 'Crate',
          kind: ObjectKind.mesh,
          meshAsset: 'crate.glb',
        ),
      );
      expect(collider.vertices, geometry.positions);
      expect(collider.indices, geometry.indices);
      await expectLater(
        ModelCollision(root.path).read(
          SceneObject(
            id: 'missing',
            name: 'Missing',
            kind: ObjectKind.mesh,
            meshAsset: 'missing.glb',
          ),
        ),
        throwsA(isA<Exception>()),
      );
    } finally {
      root.deleteSync(recursive: true);
    }
  });
}

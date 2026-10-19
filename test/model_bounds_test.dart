import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/model_bounds.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_mesh/orblit_mesh.dart';

SceneObject _object(String asset) =>
    SceneObject(id: 'm', name: 'M', kind: ObjectKind.mesh, meshAsset: asset);

void main() {
  late Directory root;
  late ModelBounds models;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_bounds');
    models = ModelBounds(root.path);
  });
  tearDown(() => root.deleteSync(recursive: true));

  test('an obj is sized from its points', () {
    File('${root.path}/crate.obj')
        .writeAsStringSync('v -1 0 2\nv 3 4 5\nv 0 0 0\nf 1 2 3\n');
    final box = models.of(_object('crate.obj'))!;
    expect([box.min.x, box.min.y, box.min.z], [-1, 0, 0]);
    expect([box.max.x, box.max.y, box.max.z], [3, 4, 5]);
  });

  test('an obj the editor wrote is sized as the mesh it came from', () {
    final cube = const Shape(kind: ShapeKind.cube).build();
    File('${root.path}/cube.obj').writeAsBytesSync(cube.toObj().first.bytes);
    final box = models.of(_object('cube.obj'))!;
    expect(box.min.distanceTo(cube.bounds.min), closeTo(0, 1e-6));
    expect(box.max.distanceTo(cube.bounds.max), closeTo(0, 1e-6));
  });

  test('a file that is not a model, or not there, says nothing', () {
    File('${root.path}/empty.obj').writeAsStringSync('v 1 2 3\n');
    File('${root.path}/crate.fbx').writeAsBytesSync([1, 2, 3]);
    expect(models.of(_object('empty.obj')), isNull);
    expect(models.of(_object('crate.fbx')), isNull);
    expect(models.of(_object('gone.obj')), isNull);
  });
}

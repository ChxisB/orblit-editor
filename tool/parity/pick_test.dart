import 'dart:convert';
import 'dart:io';
import 'dart:ui' show Offset, Size;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart';

// Writes what the Dart view answers to a grid of pixels, for the C# view to
// answer the same way: the ray through each pixel, the object it meets, and
// the handle of each gizmo under it. Run from the editor package with
// ORBLIT_PARITY_OUT set to the folder the fixtures go in:
//
//   flutter test tool/parity/pick_test.dart

const _size = Size(800, 600);

List<double> _list(Vector3 v) => [v.x, v.y, v.z];

List<double>? _point(Offset? at) => at == null ? null : [at.dx, at.dy];

EditorScene _scene() {
  SceneObject cube(String id, Vector3 at, {Vector3? scale, Vector3? rotation}) =>
      SceneObject(
        id: id,
        name: id,
        kind: ObjectKind.shape,
        position: at,
        scale: scale,
        rotation: rotation,
        shape: Shape.of(ShapeKind.cube),
      );

  return EditorScene([
    cube('a', Vector3.zero()),
    cube('b', Vector3(3, 0, 0), scale: Vector3(1, 3, 1)),
    cube('c', Vector3(-3, 0, 2), rotation: Vector3(0, 45, 0)),
    cube('d', Vector3(0, 0, -4), scale: Vector3(2, 2, 2)),
    cube('e', Vector3(0, 0, 2)),
  ]);
}

void main() {
  test('writes what the view answers to a grid of pixels', () {
    final out = Platform.environment['ORBLIT_PARITY_OUT'];
    if (out == null) fail('Set ORBLIT_PARITY_OUT to the fixtures folder.');

    final scene = _scene();
    final cameras = [
      OrbitCamera(),
      OrbitCamera(
        yaw: -1.2,
        pitch: -0.2,
        distance: 6,
        fieldOfView: 60,
        target: Vector3(2, 1, -1),
      ),
      OrbitCamera(yaw: 2.4, pitch: 1.2, distance: 20, target: Vector3.zero()),
    ];

    final entries = <Map<String, Object?>>[];
    for (final camera in cameras) {
      final projection = ViewportProjection(camera: camera, size: _size);
      final gizmos = {
        for (final mode in GizmoMode.values)
          mode: Gizmo(mode: mode, pivot: Vector3.zero(), projection: projection),
      };

      // A grid, and the places each handle runs through, because a grid on
      // its own seldom lands on a line nine pixels wide.
      final pixels = <Offset>[
        for (var y = 0.0; y <= 600; y += 75)
          for (var x = 0.0; x <= 800; x += 100) Offset(x, y),
      ];
      final move = gizmos[GizmoMode.move]!;
      final rotate = gizmos[GizmoMode.rotate]!;
      for (final axis in GizmoAxis.values) {
        final middle = projection.project(
          move.pivot + axis.direction * (move.length * 0.5),
        );
        if (middle != null) {
          pixels
            ..add(middle)
            ..add(middle + const Offset(4, 0))
            ..add(middle + const Offset(0, 12));
        }
        final ring = rotate.ringOnScreen(axis);
        if (ring.length > 8) {
          pixels
            ..add(ring[8])
            ..add(ring[8] + const Offset(0, 5));
        }
      }

      entries.add({
        'yaw': camera.yaw,
        'pitch': camera.pitch,
        'distance': camera.distance,
        'fieldOfView': camera.fieldOfView,
        'target': _list(camera.target),
        'eye': _list(projection.eye),
        'forward': _list(projection.forward),
        'pivotAt': _point(projection.project(Vector3.zero())),
        'handleLength': move.length,
        'pixels': [
          for (final pixel in pixels)
            () {
              final ray = projection.rayThrough(pixel);
              return {
                'at': _point(pixel),
                'origin': _list(ray.origin),
                'direction': _list(ray.direction),
                'object': scene.objectAlong(ray.origin, ray.direction),
                'move': move.axisAt(pixel)?.name,
                'rotate': rotate.axisAt(pixel)?.name,
              };
            }(),
        ],
      });
    }

    final file = File(p.join(out, 'pick.json'));
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(
      const JsonEncoder.withIndent('  ').convert({
        'size': [_size.width, _size.height],
        'cameras': entries,
      }),
    );
  });
}

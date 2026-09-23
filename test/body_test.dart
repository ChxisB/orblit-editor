import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/body_gizmo.dart';
import 'package:orblit_editor/src/editor/body_section.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// A physics body on an object: carried through the editor without the editor
// having fields for it, edited as one thing, and drawn as the simulation
// will see it.

SceneObject crate({
  doc.BodyComponent? body,
  Vector3? scale,
  Vector3? rotation,
}) => SceneObject(
  id: 'crate',
  name: 'Crate',
  kind: ObjectKind.mesh,
  scale: scale,
  rotation: rotation,
  components: {doc.SceneComponents.body: ?body},
);

/// A scene saved by the editor and opened again.
EditorScene reopened(EditorScene scene) =>
    SceneDocument.decode(SceneDocument.encode(scene)).scene;

/// A document the editor did not write, opened and saved by it.
doc.SceneDocument throughTheEditor(doc.SceneDocument document) =>
    doc.SceneDocument.decode(
      SceneDocument.encode(SceneDocument.decode(document.encode()).scene),
    ).document;

SetObjectComponent put(
  EditorScene scene,
  doc.BodyComponent? to, {
  String label = 'Set Crate body',
}) => SetObjectComponent(
  sceneId: 'a',
  id: 'crate',
  label: label,
  type: doc.SceneComponents.body,
  from: physicsBodyOf(scene['crate']!),
  to: to,
);

void main() {
  group('a body in a scene file', () {
    test('comes back as it was saved', () {
      final body = doc.BodyComponent(
        shape: doc.BodyShape.capsule,
        radius: 0.3,
        height: 1.8,
        centre: Vector3(0, 0.9, 0),
        motion: doc.BodyMotion.driven,
        friction: 0.8,
      );

      final back = reopened(EditorScene([crate(body: body)]));

      expect(physicsBodyOf(back['crate']!)!.toJson(), body.toJson());
    });

    test('an object without one does not grow one', () {
      final back = reopened(EditorScene([crate()]));
      expect(physicsBodyOf(back['crate']!), isNull);
      expect(SceneDocument.encode(back), isNot(contains('"body"')));
    });

    test('a component the editor has never heard of is written back whole', () {
      // What a newer Orblit or a project's own tool wrote. Opening the scene
      // and saving it must not be how it is lost.
      final back = throughTheEditor(
        doc.SceneDocument(
          entities: [
            doc.SceneEntity(
              id: 'crate',
              name: 'Crate',
              components: {
                doc.SceneComponents.transform: doc.TransformComponent(),
                'wobble': const doc.UnknownComponent('wobble', {'rate': 3}),
              },
            ),
          ],
        ),
      );

      expect(back['crate']!['wobble']!.toJson(), {'rate': 3});
    });

    test('a lamp keeps its mesh, though its row is only a light', () {
      final back = throughTheEditor(
        doc.SceneDocument(
          entities: [
            doc.SceneEntity(
              id: 'lamp',
              name: 'Lamp',
              components: {
                doc.SceneComponents.transform: doc.TransformComponent(),
                doc.SceneComponents.light: const doc.LightComponent(),
                doc.SceneComponents.mesh: const doc.MeshComponent(
                  asset: 'assets/meshes/lamp.glb',
                ),
              },
            ),
          ],
        ),
      );

      final mesh = back['lamp']![doc.SceneComponents.mesh];
      expect(mesh, isA<doc.MeshComponent>());
      expect((mesh! as doc.MeshComponent).asset, 'assets/meshes/lamp.glb');
      expect(back['lamp']!.has(doc.SceneComponents.light), isTrue);
    });

    test('a copy carries it, and does not share the map', () {
      final object = crate(body: doc.BodyComponent());
      final copy = object.copyAs(id: 'other');
      copy.components.remove(doc.SceneComponents.body);

      expect(physicsBodyOf(object), isNotNull);
      expect(physicsBodyOf(copy), isNull);
    });
  });

  group('editing a body', () {
    late EditorScene scene;
    late History history;

    setUp(() {
      scene = EditorScene([crate()]);
      history = History(
        Workspace('/project')
          ..add(SceneEntry(id: 'a', name: 'A', scene: scene)),
      );
    });

    test('adding one is undone by taking it off again', () {
      history.run(put(scene, doc.BodyComponent(), label: 'Add body'));
      expect(physicsBodyOf(scene['crate']!), isNotNull);

      history.undo();
      expect(physicsBodyOf(scene['crate']!), isNull);

      history.redo();
      expect(physicsBodyOf(scene['crate']!), isNotNull);
    });

    test('a drag through a field is one step', () {
      history
        ..run(put(scene, doc.BodyComponent(mass: 2), label: 'Add body'))
        ..seal();
      for (var i = 1; i <= 30; i++) {
        final body = physicsBodyOf(scene['crate']!)!;
        history.run(put(scene, body.copyWith(mass: 2.0 + i)));
      }

      expect(physicsBodyOf(scene['crate']!)!.mass, 32);
      expect(history.labels, ['Add body', 'Set Crate body']);

      // Back to before the drag, and the body stays on: the drag and the
      // adding are separate steps.
      history.undo();
      expect(physicsBodyOf(scene['crate']!)!.mass, 2);
    });

    test('taking it off is its own step, not the end of a drag', () {
      history
        ..run(put(scene, doc.BodyComponent(), label: 'Add body'))
        ..seal()
        ..run(put(scene, physicsBodyOf(scene['crate']!)!.copyWith(mass: 5)))
        ..run(put(scene, null, label: 'Remove body'));

      expect(physicsBodyOf(scene['crate']!), isNull);
      history.undo();
      expect(physicsBodyOf(scene['crate']!)!.mass, 5);
    });
  });

  group('the body gizmo', () {
    const size = Size(800, 600);

    GizmoTarget targetOf(EditorScene scene, Set<String> selected) =>
        GizmoTarget(
          object: scene[selected.first]!,
          scene: scene,
          selected: selected,
          projection: ViewportProjection(camera: OrbitCamera(), size: size),
        );

    /// Where the wireframe lands on screen.
    Future<Rect> drawn(WidgetTester tester, EditorScene scene) async {
      await tester.pumpWidget(
        Directionality(
          textDirection: TextDirection.ltr,
          child: SizedBox.fromSize(
            size: size,
            child: Stack(
              children: [
                bodyGizmo().overlay!(targetOf(scene, {'crate'})),
              ],
            ),
          ),
        ),
      );
      late Rect bounds;
      expect(
        find.byType(CustomPaint),
        paints..something((method, arguments) {
          if (method != #drawPath) return false;
          bounds = (arguments[0] as Path).getBounds();
          return true;
        }),
      );
      return bounds;
    }

    void expectSame(Rect a, Rect b) {
      for (final (x, y) in [
        (a.left, b.left),
        (a.top, b.top),
        (a.right, b.right),
        (a.bottom, b.bottom),
      ]) {
        expect(x, closeTo(y, 1e-6));
      }
    }

    test('is there for a selection with a body, and only then', () {
      final scene = EditorScene([
        crate(body: doc.BodyComponent()),
        SceneObject(id: 'rock', name: 'Rock', kind: ObjectKind.mesh),
      ]);
      final gizmo = bodyGizmo();

      expect(gizmo.appliesTo(targetOf(scene, {'rock'})), isFalse);
      expect(gizmo.appliesTo(targetOf(scene, {'crate'})), isTrue);
      // Any of the selection, not only the one the handles are on.
      expect(gizmo.appliesTo(targetOf(scene, {'rock', 'crate'})), isTrue);
    });

    testWidgets('a box scales with its object', (tester) async {
      final scaled = await drawn(
        tester,
        EditorScene([crate(body: doc.BodyComponent(), scale: Vector3.all(2))]),
      );
      final big = await drawn(
        tester,
        EditorScene([crate(body: doc.BodyComponent(size: Vector3.all(2)))]),
      );
      final small = await drawn(
        tester,
        EditorScene([crate(body: doc.BodyComponent())]),
      );

      expectSame(scaled, big);
      expect(small.width, lessThan(big.width));
    });

    testWidgets('a box turns the way its object turns', (tester) async {
      // Partway round and tipped: a quarter turn draws a centred box the same
      // whichever way it went, so it would not catch a turn the wrong way.
      final scene = EditorScene([
        crate(
          body: doc.BodyComponent(size: Vector3(3, 1, 1)),
          rotation: Vector3(20, 40, 0),
        ),
      ]);
      final world = scene.worldOf('crate');
      final projection = ViewportProjection(camera: OrbitCamera(), size: size);
      final corners = [
        for (final x in [-1.5, 1.5])
          for (final y in [-0.5, 0.5])
            for (final z in [-0.5, 0.5])
              projection.project(world.transformed3(Vector3(x, y, z)))!,
      ];

      final box = await drawn(tester, scene);

      // Where the object's own matrix puts the corners, as a model is drawn.
      final xs = corners.map((corner) => corner.dx);
      final ys = corners.map((corner) => corner.dy);
      expect(box.left, closeTo(xs.reduce(math.min), 1e-3));
      expect(box.right, closeTo(xs.reduce(math.max), 1e-3));
      expect(box.top, closeTo(ys.reduce(math.min), 1e-3));
      expect(box.bottom, closeTo(ys.reduce(math.max), 1e-3));
    });

    testWidgets('a ball takes the largest of its scales', (tester) async {
      // The simulation cannot stretch a ball, so neither does the drawing: it
      // is the ball the physics will use, not an egg nobody simulates.
      final stretched = await drawn(
        tester,
        EditorScene([
          crate(
            body: doc.BodyComponent(shape: doc.BodyShape.sphere),
            scale: Vector3(1, 3, 1),
          ),
        ]),
      );
      final round = await drawn(
        tester,
        EditorScene([
          crate(
            body: doc.BodyComponent(shape: doc.BodyShape.sphere, radius: 1.5),
          ),
        ]),
      );

      expectSame(stretched, round);
    });

    testWidgets('a short capsule is drawn as the ball it is simulated as', (
      tester,
    ) async {
      final capsule = await drawn(
        tester,
        EditorScene([
          crate(
            body: doc.BodyComponent(shape: doc.BodyShape.capsule, height: 0.8),
          ),
        ]),
      );
      final ball = await drawn(
        tester,
        EditorScene([
          crate(body: doc.BodyComponent(shape: doc.BodyShape.sphere)),
        ]),
      );

      expectSame(capsule, ball);
    });
  });
}

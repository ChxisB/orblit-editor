import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/collision_button.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_mesh/orblit_mesh.dart' as mesh;
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/editor_shell.dart';

// A physics body, put on from the inspector and seen in the scene view.

/// The wireframe the body gizmo draws, found by what paints it.
final wireframe = find.descendant(
  of: find.byType(SceneViewport),
  matching: find.byWidgetPredicate(
    (widget) =>
        widget is CustomPaint &&
        widget.painter.runtimeType.toString() == '_BodyPainter',
  ),
);

doc.BodyComponent? savedBody(String id) {
  final text = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
      .readAsStringSync();
  return switch (doc.SceneDocument.decode(text)
      .document[id]?[doc.SceneComponents.body]) {
    final doc.BodyComponent body => body,
    _ => null,
  };
}

void main() {
  useShell();

  group('a physics body', () {
    testWidgets('is put on from the inspector, fitted to what is drawn', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      expect(wireframe, findsNothing);

      await tapInInspector(tester, 'Add body');

      expect(find.text('Remove body'), findsOneWidget);
      expect(wireframe, findsOneWidget);

      await save(tester);
      final body = savedBody('crate')!;
      // The crate has no mesh of its own yet, so it draws the placeholder,
      // which is two metres across. The body is what somebody sees.
      expect(body.shape, doc.BodyShape.box);
      expect(body.size, Vector3.all(2));
      expect(body.centre, Vector3.zero());
    });

    testWidgets('is taken off again by undo, and by its own button', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      await tapInInspector(tester, 'Add body');
      await undo(tester);
      expect(find.text('Remove body'), findsNothing);
      expect(wireframe, findsNothing);

      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Remove body');
      expect(wireframe, findsNothing);

      await save(tester);
      expect(savedBody('crate'), isNull);
    });

    testWidgets('changes shape, and keeps the size it had for another', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Ball');
      await save(tester);

      final body = savedBody('crate')!;
      expect(body.shape, doc.BodyShape.sphere);
      // Written whole, so switching back is the box it was.
      expect(body.size, Vector3.all(2));
    });

    testWidgets('model triangles survive save, removal and undo', (
      tester,
    ) async {
      final object = SceneObject(
        id: 'model',
        name: 'Cube',
        kind: ObjectKind.shape,
        shape: const mesh.Shape(kind: mesh.ShapeKind.cube),
      );
      File(p.join(root.path, 'scenes', 'main$sceneExtension'))
          .writeAsStringSync(SceneDocument.encode(EditorScene([object])));
      await open(tester);
      await tester.tap(row('Cube'));
      await tester.pumpAndSettle();
      final collide = find.byType(CollisionButton);
      await reach(tester, collide);
      await tester.ensureVisible(collide);
      await tester.pumpAndSettle();
      await tester.tap(collide);
      await tester.pumpAndSettle();
      await save(tester);
      final text = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
          .readAsStringSync();
      final document = doc.SceneDocument.decode(text).document;
      final cube = document.entities.firstWhere((e) => e.name == 'Cube');
      final body = cube[doc.SceneComponents.body] as doc.BodyComponent;
      expect(body.shape, doc.BodyShape.mesh);
      expect(body.motion, doc.BodyMotion.fixed);
      expect(body.meshIndices.length, 36);
      expect(wireframe, findsOneWidget);
      await tapInInspector(tester, 'Remove body');
      await undo(tester);
      await save(tester);
      expect(savedBody(cube.id)!.meshIndices, body.meshIndices);
    });

    testWidgets('compound parts survive save, removal and undo', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Compound');
      await tapInInspector(tester, 'Add part');
      await save(tester);
      expect(savedBody('crate')!.parts.length, 2);
      expect(wireframe, findsOneWidget);
      await tapInInspector(tester, 'Remove part 2');
      await save(tester);
      expect(savedBody('crate')!.parts.length, 1);
      await undo(tester);
      await save(tester);
      expect(savedBody('crate')!.parts.length, 2);
    });

    testWidgets('becomes a cylinder, and is fitted to what is drawn', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Cylinder');
      await tapInInspector(tester, 'Fit to mesh');
      await save(tester);

      final body = savedBody('crate')!;
      expect(body.shape, doc.BodyShape.cylinder);
      // The placeholder is two metres across and high.
      expect(body.radius, 1);
      expect(body.height, 2);
    });

    testWidgets('becomes a hull cut from what is drawn', (tester) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Hull');

      // The placeholder has no mesh to cut from, so the corners of its box.
      expect(find.text('8 corners.'), findsOneWidget);
      expect(wireframe, findsOneWidget);

      await save(tester);
      final body = savedBody('crate')!;
      expect(body.shape, doc.BodyShape.hull);
      expect(body.hull, hasLength(24));
      expect(body.hull.every((value) => value.abs() == 1), isTrue);
    });

    testWidgets('says what a hull without a volume is missing', (tester) async {
      File(p.join(root.path, 'scenes', 'main$sceneExtension'))
          .writeAsStringSync(
            SceneDocument.encode(
              EditorScene([
                SceneObject(
                  id: 'crate',
                  name: 'Crate',
                  kind: ObjectKind.mesh,
                  components: {
                    doc.SceneComponents.body: doc.BodyComponent(
                      shape: doc.BodyShape.hull,
                      hull: const [0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1],
                    ),
                  },
                ),
              ]),
            ),
          );
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      final note = find.textContaining('Needs four points');
      await reach(tester, note);

      expect(note, findsOneWidget);
    });
  });
}

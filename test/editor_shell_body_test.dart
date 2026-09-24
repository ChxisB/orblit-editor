import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
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
  });
}

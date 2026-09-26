import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// A joint, put on from the inspector and seen in the scene view.

/// What the joint gizmo draws, found by what paints it.
final jointLines = find.descendant(
  of: find.byType(SceneViewport),
  matching: find.byWidgetPredicate(
    (widget) =>
        widget is CustomPaint &&
        widget.painter.runtimeType.toString() == '_JointPainter',
  ),
);

doc.JointComponent? savedJoint(String id) {
  final text = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
      .readAsStringSync();
  return switch (doc.SceneDocument.decode(text)
      .document[id]?[doc.SceneComponents.joint]) {
    final doc.JointComponent joint => joint,
    _ => null,
  };
}

void main() {
  useShell();

  group('a joint', () {
    testWidgets('is offered only where there is a body to hold', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      expect(find.text('Add joint'), findsNothing);

      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Add joint');

      expect(find.text('Remove joint'), findsOneWidget);
      expect(find.text('Crate to the world'), findsOneWidget);
      expect(jointLines, findsOneWidget);

      await save(tester);
      expect(savedJoint('crate')!.kind, doc.JointKind.hinge);
    });

    testWidgets('changes kind from its menu, and limits what that kind reads', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Add joint');

      await tapInInspector(tester, 'Hinge');
      await tester.tap(find.text('Slider').last);
      await tester.pumpAndSettle();
      expect(find.text('Travel'), findsOneWidget);

      await tapInInspector(tester, 'Range');
      await save(tester);

      final joint = savedJoint('crate')!;
      expect(joint.kind, doc.JointKind.slider);
      expect(joint.limits[doc.JointAxis.alongX]!.low, -0.5);
      expect(joint.limits[doc.JointAxis.alongX]!.high, 0.5);
    });

    testWidgets('is taken off again by undo, and by its own button', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');

      await tapInInspector(tester, 'Add joint');
      await undo(tester);
      expect(find.text('Remove joint'), findsNothing);
      expect(jointLines, findsNothing);

      await tapInInspector(tester, 'Add joint');
      await tapInInspector(tester, 'Remove joint');
      await save(tester);
      expect(savedJoint('crate'), isNull);
    });
  });
}

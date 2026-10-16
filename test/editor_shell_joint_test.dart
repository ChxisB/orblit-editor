import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
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

/// A cell of the toggle row with [label], found there because words such as
/// On are in more than one row of the inspector.
Future<void> tapCell(WidgetTester tester, String label, String cell) async {
  final row = find.ancestor(
    of: find.text(label),
    matching: find.byType(ToggleRow),
  );
  final button = find.descendant(of: row.first, matching: find.text(cell));
  await tester.ensureVisible(button);
  await tester.pumpAndSettle();
  await tester.tap(button);
  await tester.pumpAndSettle();
}

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

    testWidgets('a pulley and a path are given points to start from', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Add joint');

      await tapInInspector(tester, 'Hinge');
      await tester.tap(find.text('Pulley').last);
      await tester.pumpAndSettle();
      expect(find.text('Over 1'), findsOneWidget);
      expect(find.text('Over 2'), findsOneWidget);

      await tapInInspector(tester, 'Pulley');
      await tester.tap(find.text('Path').last);
      await tester.pumpAndSettle();
      expect(find.text('Point 1'), findsOneWidget);
      expect(find.text('Point 2'), findsOneWidget);
      expect(find.text('Point 3'), findsNothing);

      await tapInInspector(tester, 'Add point');
      expect(find.text('Point 3'), findsOneWidget);
      await save(tester);

      final joint = savedJoint('crate')!;
      expect(joint.kind, doc.JointKind.path);
      expect(joint.over, hasLength(2));
      expect(joint.path, hasLength(3));
      expect(joint.path.last.x, 2);
    });

    testWidgets('a spring and a place to hold are switched on from rows', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');
      await tapInInspector(tester, 'Add joint');
      expect(find.text('Stiffness'), findsNothing);

      await tapCell(tester, 'Spring', 'On');
      expect(find.text('Stiffness'), findsOneWidget);

      await tapCell(tester, 'Drive', 'Place');
      expect(find.text('Hold at'), findsOneWidget);
      expect(find.text('Motor'), findsNothing);
      await save(tester);

      final joint = savedJoint('crate')!;
      expect(joint.springs[doc.JointAxis.aboutX]!.hertz, greaterThan(0));
      expect(joint.goalHertz, greaterThan(0));
      expect(joint.strength, greaterThan(0));
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

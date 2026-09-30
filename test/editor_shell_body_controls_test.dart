import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// What a free body is held to, what it is made of, and the layers it meets.

doc.SceneDocument savedDocument() => doc.SceneDocument.decode(
  File(p.join(root.path, 'scenes', 'main$sceneExtension')).readAsStringSync(),
).document;

doc.BodyComponent savedBody(String id) =>
    savedDocument()[id]![doc.SceneComponents.body]! as doc.BodyComponent;

Finder inInspector(String text) =>
    find.descendant(of: find.byType(Inspector), matching: find.text(text));

Finder fieldRow(String field) => find
    .ancestor(of: inInspector(field), matching: find.byType(FieldRow))
    .first;

/// [text] inside the row labelled [field], for a word that turns up in more
/// than one row.
Finder inRow(String field, String text) =>
    find.descendant(of: fieldRow(field), matching: find.text(text));

Future<void> tapIn(WidgetTester tester, String field, String text) async {
  final target = inRow(field, text);
  await reach(tester, target);
  await tester.ensureVisible(target);
  await tester.pumpAndSettle();
  await tester.tap(target);
  await tester.pumpAndSettle();
}

/// A crate with a body that falls, which is the one the controls are for.
Future<void> freeCrate(WidgetTester tester) async {
  await open(tester);
  await tester.tap(row('Crate'));
  await tester.pumpAndSettle();
  await tapInInspector(tester, 'Add body');
}

void main() {
  useShell();

  group('a free body', () {
    testWidgets('holds an axis still, and gives it back on undo', (
      tester,
    ) async {
      await freeCrate(tester);

      await tapIn(tester, 'Lock move', 'X');
      await tapIn(tester, 'Lock turn', 'Y');
      await save(tester);
      expect(savedBody('crate').locks, {
        doc.BodyLock.moveX,
        doc.BodyLock.turnY,
      });

      await undo(tester);
      await save(tester);
      expect(savedBody('crate').locks, {doc.BodyLock.moveX});

      await tapIn(tester, 'Lock move', 'X');
      await save(tester);
      expect(savedBody('crate').locks, isEmpty);
    });

    testWidgets('is offered no movement controls once it is fixed', (
      tester,
    ) async {
      await freeCrate(tester);
      await reach(tester, inInspector('Lock move'));

      await tapInInspector(tester, 'Fixed');

      expect(inInspector('Lock move'), findsNothing);
      expect(inInspector('Gravity'), findsNothing);
      // What it collides with does not depend on whether it moves.
      expect(inInspector('Is in'), findsOneWidget);
    });

    testWidgets('is made of a preset, and reads as custom after a change', (
      tester,
    ) async {
      await freeCrate(tester);
      await reach(tester, inInspector('Material'));
      expect(inRow('Material', 'Custom'), findsOneWidget);

      await tester.tap(inRow('Material', 'Custom'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Ice'));
      await tester.pumpAndSettle();
      await save(tester);

      final ice = savedBody('crate');
      expect(ice.friction, 0.05);
      expect(ice.restitution, 0.05);
      expect(inRow('Material', 'Ice'), findsOneWidget);

      await undo(tester);
      expect(inRow('Material', 'Custom'), findsOneWidget);
    });
  });

  group('the layers of a body', () {
    testWidgets('are toggled one at a time from a grid', (tester) async {
      await freeCrate(tester);

      // Layer one is where a body starts, and it sees every layer.
      await tapIn(tester, 'Is in', '3');
      await tapIn(tester, 'Sees', '1');
      await save(tester);

      final body = savedBody('crate');
      expect(body.layers, 1 | 4);
      expect(body.cares, doc.BodyComponent.everyLayer ^ 1);

      await undo(tester);
      await save(tester);
      expect(savedBody('crate').cares, doc.BodyComponent.everyLayer);
    });

    testWidgets('are named from the scene, one field for each', (tester) async {
      await freeCrate(tester);
      await tester.tap(sceneRow('main'));
      await tester.pumpAndSettle();

      await reach(tester, inInspector('Layer 1'));
      final field = find.descendant(
        of: fieldRow('Layer 1'),
        matching: find.byType(TextField),
      );
      await tester.enterText(field, 'Player');
      FocusManager.instance.primaryFocus?.unfocus();
      await tester.pumpAndSettle();
      await save(tester);

      expect(savedDocument().settings.layerNames, ['Player']);

      await undo(tester);
      await save(tester);
      expect(savedDocument().settings.layerNames, isEmpty);
    });
  });
}

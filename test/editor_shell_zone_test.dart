import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/editor_shell.dart';

// A trigger, a belt and a zone, put on from the inspector.

doc.SceneEntity? savedEntity(String id) {
  final text = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
      .readAsStringSync();
  return doc.SceneDocument.decode(text).document[id];
}

doc.BodyComponent savedBody(String id) =>
    savedEntity(id)![doc.SceneComponents.body]! as doc.BodyComponent;

doc.ZoneComponent? savedZone(String id) =>
    switch (savedEntity(id)?[doc.SceneComponents.zone]) {
      final doc.ZoneComponent zone => zone,
      _ => null,
    };

/// Taps [option] in the row labelled [field]: the same words turn up in more
/// than one row, and a zone offers the same two for each of its fields.
Future<void> choose(WidgetTester tester, String field, String option) async {
  final labelled = find
      .ancestor(of: inInspector(field), matching: find.byType(FieldRow))
      .first;
  final button = find.descendant(of: labelled, matching: find.text(option));
  await reach(tester, button);
  await tester.ensureVisible(button);
  await tester.pumpAndSettle();
  await tester.tap(button);
  await tester.pumpAndSettle();
}

/// A crate with a body that stays where it is.
Future<void> fixedCrate(WidgetTester tester) async {
  await open(tester);
  await tester.tap(row('Crate'));
  await tester.pumpAndSettle();
  await tapInInspector(tester, 'Add body');
  await tapInInspector(tester, 'Fixed');
}

Finder inInspector(String text) =>
    find.descendant(of: find.byType(Inspector), matching: find.text(text));

void main() {
  useShell();

  group('a trigger', () {
    testWidgets('is a choice for a body that stays put, and not for one that '
        'falls', (tester) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await tapInInspector(tester, 'Add body');
      await reach(tester, inInspector('Stay events'));
      expect(inInspector('Trigger'), findsNothing);

      await tapInInspector(tester, 'Fixed');
      await reach(tester, inInspector('Trigger'));
      expect(inInspector('Solid'), findsOneWidget);
    });

    testWidgets('turns a body into a place, which cannot be a belt', (
      tester,
    ) async {
      await fixedCrate(tester);
      await reach(tester, inInspector('Belt speed'));
      expect(inInspector('Belt speed'), findsOneWidget);

      await tapInInspector(tester, 'Trigger');
      expect(inInspector('Belt speed'), findsNothing);
      await save(tester);

      expect(savedBody('crate').trigger, isTrue);
    });

    testWidgets('asks to hear every step of what is inside it', (tester) async {
      await fixedCrate(tester);
      await tapInInspector(tester, 'Trigger');
      await choose(tester, 'Stay events', 'On');
      await save(tester);

      expect(savedBody('crate').stay, isTrue);
    });

    testWidgets('is undone with one step', (tester) async {
      await fixedCrate(tester);
      await tapInInspector(tester, 'Trigger');
      await undo(tester);
      await save(tester);

      expect(savedBody('crate').trigger, isFalse);
    });
  });

  group('a zone', () {
    testWidgets('is offered only on a body that stays put', (tester) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      expect(find.text('Add zone'), findsNothing);

      await tapInInspector(tester, 'Add body');
      expect(find.text('Add zone'), findsNothing);

      await tapInInspector(tester, 'Fixed');
      await tapInInspector(tester, 'Add zone');

      expect(find.text('Remove zone'), findsOneWidget);
      await save(tester);
      expect(savedZone('crate'), isNotNull);
    });

    testWidgets('makes its body a trigger, and says so', (tester) async {
      await fixedCrate(tester);
      await tapInInspector(tester, 'Add zone');

      await reach(tester, inInspector('Trigger'));
      expect(inInspector('Solid'), findsNothing);
      expect(inInspector('Belt speed'), findsNothing);
    });

    testWidgets('decides a field only when asked to, and not before', (
      tester,
    ) async {
      await fixedCrate(tester);
      await tapInInspector(tester, 'Add zone');
      expect(find.text('Pull'), findsNothing);

      await choose(tester, 'Gravity', 'Own');
      expect(find.text('Pull'), findsOneWidget);
      await save(tester);

      final zone = savedZone('crate')!;
      // Where it starts is the world's own, so nothing moves until it is
      // dragged.
      expect(zone.gravity, Vector3(0, -9.81, 0));
      expect(zone.linearDamping, isNull);
      expect(zone.angularDamping, isNull);
    });

    testWidgets('gives a field back to the body it left', (tester) async {
      await fixedCrate(tester);
      await tapInInspector(tester, 'Add zone');
      await choose(tester, 'Gravity', 'Own');
      await choose(tester, 'Gravity', 'Same');
      await save(tester);

      expect(savedZone('crate')!.gravity, isNull);
      expect(find.text('Pull'), findsNothing);
    });

    testWidgets('is taken off again by undo, and by its own button', (
      tester,
    ) async {
      await fixedCrate(tester);

      await tapInInspector(tester, 'Add zone');
      await undo(tester);
      expect(find.text('Remove zone'), findsNothing);

      await tapInInspector(tester, 'Add zone');
      await tapInInspector(tester, 'Remove zone');
      await save(tester);
      expect(savedZone('crate'), isNull);
    });
  });
}

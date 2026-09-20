import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// The outliner and the inspector beside it: what the tree shows, what
// selecting does to it, and that an edit is saved and undone.

void main() {
  useShell();

  testWidgets('the tree shows what is nested inside what', (tester) async {
    await open(tester);

    // Props holds two meshes in the starter scene, so all four are on screen.
    expect(row('Props'), findsOneWidget);
    expect(row('Cube'), findsOneWidget);
    expect(row('Crate'), findsOneWidget);
  });

  testWidgets('collapsing a parent hides its children', (tester) async {
    await open(tester);

    // The first chevron belongs to the scene's own row; the second to Props.
    await tester.tap(find.byIcon(Icons.expand_more).at(1));
    await tester.pumpAndSettle();

    expect(row('Props'), findsOneWidget);
    expect(row('Crate'), findsNothing);
  });

  testWidgets('collapsing the scene hides everything in it', (tester) async {
    await open(tester);

    await tester.tap(find.byIcon(Icons.expand_more).first);
    await tester.pumpAndSettle();

    expect(row('Props'), findsNothing);
    expect(row('Ground'), findsNothing);
  });

  testWidgets('selecting shows that object in the inspector', (tester) async {
    await open(tester);

    await tester.tap(row('Ground'));
    await tester.pumpAndSettle();

    // The name field carries the selection.
    final field = tester.widget<TextField>(find.byType(TextField).first);
    expect(field.controller?.text, 'Ground');
  });

  testWidgets('dragging a transform number changes it, and undo puts it back', (
    tester,
  ) async {
    await open(tester);
    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();

    String positionX() => tester
        .widgetList<Text>(
          find.descendant(of: find.byType(Row), matching: find.byType(Text)),
        )
        .map((t) => t.data)
        .firstWhere((d) => d == '2.20', orElse: () => '')!;

    expect(positionX(), '2.20', reason: 'the crate starts at x 2.2');

    // One gesture made of many moves, which is one undo step.
    final field = find.text('2.20');
    final gesture = await tester.startGesture(tester.getCenter(field));
    for (var i = 0; i < 10; i++) {
      await gesture.moveBy(const Offset(5, 0));
      await tester.pump();
    }
    await gesture.up();
    await tester.pumpAndSettle();

    expect(find.text('2.20'), findsNothing, reason: 'the drag did nothing');

    await undo(tester);

    expect(
      find.text('2.20'),
      findsOneWidget,
      reason: 'one undo should cover the whole drag',
    );
  });

  testWidgets('adding puts a new object in and undo takes it out', (
    tester,
  ) async {
    await open(tester);

    await add(tester, 'Group');

    expect(
      find.textContaining('Add Group'),
      findsOneWidget,
      reason: 'the status bar should name the last change',
    );

    await undo(tester);

    expect(find.textContaining('Add Group'), findsNothing);
  });

  testWidgets('a second object of the same kind gets its own name', (
    tester,
  ) async {
    await open(tester);

    await add(tester, 'Group');
    await add(tester, 'Group');

    expect(find.textContaining('Add Group 2'), findsOneWidget);
  });

  testWidgets('the project browser lists what is on disk', (tester) async {
    await open(tester);

    expect(find.text('PROJECT'), findsOneWidget);
    expect(find.text('scenes'), findsWidgets);
  });

  testWidgets('opening a folder shows its files', (tester) async {
    await open(tester);

    File(p.join(root.path, 'scenes', 'notes.txt')).writeAsStringSync('x');
    await openFolder(tester, 'scenes');

    expect(find.text('notes.txt'), findsOneWidget);
  });

  testWidgets('undo is offered only once there is something to undo', (
    tester,
  ) async {
    await open(tester);
    expect(find.byTooltip('Nothing to undo'), findsOneWidget);

    await add(tester, 'Mesh object');

    expect(find.byTooltip('Nothing to undo'), findsNothing);
    expect(find.byTooltip('Undo Add Mesh'), findsOneWidget);
  });

  testWidgets('saving writes a scene file that opens again', (tester) async {
    await open(tester);

    // Nothing has been written yet, so the status bar says so.
    expect(
      unsavedMarker(),
      findsOneWidget,
      reason: 'a fresh scene is unsaved and should be marked',
    );

    await save(tester);

    final written = File(p.join(root.path, 'scenes', 'main$sceneExtension'));
    expect(written.existsSync(), isTrue);

    final load = SceneDocument.decode(written.readAsStringSync());
    expect(load.hasProblems, isFalse);
    expect(
      load.scene.childrenOf('props').length,
      2,
      reason: 'the hierarchy should survive the save',
    );
  });

  testWidgets('the unsaved marker clears on save and comes back on an edit', (
    tester,
  ) async {
    await open(tester);

    await save(tester);
    expect(unsavedMarker(), findsNothing);

    await add(tester, 'Group');
    expect(unsavedMarker(), findsOneWidget);
  });

  testWidgets('undoing back to the saved state reads as saved again', (
    tester,
  ) async {
    await open(tester);
    await save(tester);

    await add(tester, 'Group');
    expect(unsavedMarker(), findsOneWidget);

    await undo(tester);

    // Somebody who changed their mind has not changed the file.
    expect(unsavedMarker(), findsNothing);
  });

  testWidgets('a scene written by a newer editor is refused, not half-read', (
    tester,
  ) async {
    File(p.join(root.path, 'scenes', 'main$sceneExtension'))
        .writeAsStringSync('{"formatVersion": 99, "objects": []}');

    await open(tester);

    // The starter scene is still there rather than an empty one.
    expect(row('Props'), findsOneWidget);
    expect(find.textContaining('newer Orblit'), findsOneWidget);
  });

  testWidgets('an existing scene file is what opens', (tester) async {
    File(p.join(root.path, 'scenes', 'main$sceneExtension')).writeAsStringSync(
      SceneDocument.encode(
        EditorScene([
          SceneObject(id: 'x', name: 'Only Thing', kind: ObjectKind.mesh),
        ]),
      ),
    );

    await open(tester);

    expect(row('Only Thing'), findsOneWidget);
    expect(
      row('Props'),
      findsNothing,
      reason:
          'the starter scene should not '
          'be used when there is a file',
    );
  });
}

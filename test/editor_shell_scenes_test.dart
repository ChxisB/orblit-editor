import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// More than one scene: saving under a new name, starting one, closing one,
// moving between them, and every prompt that stands in the way.

void main() {
  useShell();

  testWidgets('save as writes a second scene and edits it from then on', (
    tester,
  ) async {
    await open(tester);
    await save(tester);

    await menu(tester, 'Scene', 'Save as…');
    await answerPrompt(tester, 'second', 'Save');

    expect(
      File(p.join(root.path, 'scenes', 'second$sceneExtension')).existsSync(),
      isTrue,
    );
    // And the first one is still there, rather than moved.
    expect(
      File(p.join(root.path, 'scenes', 'main$sceneExtension')).existsSync(),
      isTrue,
    );
    // Edits now go to the new file, which the status bar names.
    expect(
      find.descendant(
        of: find.byType(Row),
        matching: find.textContaining('scenes/second$sceneExtension'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('save as refuses to overwrite a scene that exists', (
    tester,
  ) async {
    File(p.join(root.path, 'scenes', 'taken$sceneExtension'))
        .writeAsStringSync(SceneDocument.encode(EditorScene([])));

    await open(tester);
    await menu(tester, 'Scene', 'Save as…');
    await answerPrompt(tester, 'taken', 'Save');

    expect(find.textContaining('already a scene'), findsOneWidget);
    // Untouched.
    expect(
      File(p.join(root.path, 'scenes', 'taken$sceneExtension'))
          .readAsStringSync(),
      SceneDocument.encode(EditorScene([])),
    );
  });

  testWidgets('a new scene is listed and loaded, and the old one is not', (
    tester,
  ) async {
    await open(tester);
    await save(tester);

    await menu(tester, 'Scene', 'New scene');

    // Both are listed, but only the new one holds anything.
    expect(sceneCount('2 scenes'), findsOneWidget);
    expect(sceneRow('main'), findsOneWidget);
    expect(sceneRow('Untitled'), findsOneWidget);
    expect(find.text('not loaded'), findsOneWidget);

    // The old scene's objects are gone from the tree, because it no longer
    // holds them.
    expect(
      row('Props'),
      findsOneWidget,
      reason: 'the new scene has its own Props',
    );
  });

  testWidgets('closing a scene with changes asks first', (tester) async {
    await open(tester);
    await save(tester);
    await add(tester, 'Group');

    // The close action appears on the scene's row when it is hovered.
    final mouse = await tester.createGesture(kind: PointerDeviceKind.mouse);
    await mouse.addPointer(location: tester.getCenter(sceneRow('main')));
    addTearDown(mouse.removePointer);
    await tester.pump();

    await tester.tap(find.byTooltip('Close main'));
    await tester.pumpAndSettle();

    expect(find.textContaining('Save main first?'), findsOneWidget);
    await tester.tap(find.text('Discard'));
    await tester.pumpAndSettle();

    expect(sceneCount('0 scenes'), findsOneWidget);
  });

  testWidgets('a dropped mesh is recorded relative to the project', (
    tester,
  ) async {
    Directory(p.join(root.path, 'assets')).createSync();
    File(p.join(root.path, 'assets', 'crate.glb')).writeAsBytesSync([1, 2]);

    await open(tester);
    await openFolder(tester, 'assets');

    final tile = find.descendant(
      of: find.byType(GridView),
      matching: find.text('crate.glb'),
    );
    final gesture = await tester.startGesture(tester.getCenter(tile));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();
    await save(tester);

    // Relative, so the scene file survives the project being moved or shared.
    final written = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
        .readAsStringSync();
    expect(written, contains('"assets/crate.glb"'));
    expect(written, isNot(contains(root.path)));
  });

  testWidgets('the scene is the root, with its objects under it', (
    tester,
  ) async {
    await open(tester);

    final sceneY = tester.getCenter(sceneRow('main')).dy;
    // Everything in it is drawn below it and indented.
    for (final name in ['Sun', 'Ground', 'Props']) {
      expect(tester.getCenter(row(name)).dy, greaterThan(sceneY));
      expect(
        tester.getTopLeft(row(name)).dx,
        greaterThan(tester.getTopLeft(sceneRow('main')).dx),
      );
    }
  });

  testWidgets('selecting the scene shows its own settings', (tester) async {
    await open(tester);
    await tester.tap(sceneRow('main'));
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    // The sky belongs to the scene, not to anything in it.
    expect(find.text('ENVIRONMENT'), findsOneWidget);
    expect(find.text('Sky'), findsOneWidget);
    expect(find.text('Ambient'), findsOneWidget);
    // And not an object's fields.
    expect(find.text('TRANSFORM'), findsNothing);
  });

  testWidgets('the scene settings are saved with the scene', (tester) async {
    await open(tester);
    await tester.tap(sceneRow('main'));
    await tester.pump(const Duration(milliseconds: 400));
    await tester.pumpAndSettle();

    // Pick a sky from the swatches. The first slider is the ambient; the
    // scene panel has fog sliders under it now.
    await tester.tap(find.byType(Slider).first);
    await tester.pumpAndSettle();
    await save(tester);

    final written = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
        .readAsStringSync();
    expect(written, contains('"sky"'));
    expect(written, contains('"ambient"'));
  });

  testWidgets('dragging a row onto another makes it a child', (tester) async {
    await open(tester);

    // Onto the middle of the row, which is the reparent band.
    final gesture = await tester.startGesture(tester.getCenter(row('Ground')));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(row('Props')));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    expect(find.textContaining('Move Ground'), findsOneWidget);
  });

  testWidgets('dragging to the edge of a row reorders instead', (tester) async {
    await open(tester);

    final box = tester.getRect(rowBox('Sun'));
    final gesture = await tester.startGesture(tester.getCenter(row('Ground')));
    await tester.pump(const Duration(milliseconds: 200));
    // The top sliver of the Sun row: before it, as a sibling.
    await gesture.moveTo(Offset(box.center.dx, box.top + 2));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    expect(find.textContaining('Reorder Ground'), findsOneWidget);
  });

  testWidgets('a thing cannot be dropped into its own child', (tester) async {
    await open(tester);

    final gesture = await tester.startGesture(tester.getCenter(row('Props')));
    await tester.pump(const Duration(milliseconds: 200));
    await gesture.moveTo(tester.getCenter(row('Cube')));
    await tester.pump();
    await gesture.up();
    await tester.pumpAndSettle();

    // Refused before the drop, so nothing happened and nothing was said.
    expect(find.textContaining('Move Props'), findsNothing);
    expect(row('Props'), findsOneWidget);
  });

  testWidgets('only the loaded scene has objects in the tree', (tester) async {
    // A second scene file in the project, listed but not loaded.
    File(p.join(root.path, 'scenes', 'props$sceneExtension')).writeAsStringSync(
      SceneDocument.encode(
        EditorScene([
          SceneObject(id: 'barrel', name: 'Barrel', kind: ObjectKind.mesh),
        ]),
      ),
    );

    await open(tester);

    expect(sceneRow('props'), findsOneWidget);
    expect(find.text('not loaded'), findsOneWidget);
    // Its object is not in the tree, because the scene is not loaded.
    expect(row('Barrel'), findsNothing);
  });

  testWidgets('double-clicking a scene loads it', (tester) async {
    File(p.join(root.path, 'scenes', 'props$sceneExtension')).writeAsStringSync(
      SceneDocument.encode(
        EditorScene([
          SceneObject(id: 'barrel', name: 'Barrel', kind: ObjectKind.mesh),
        ], name: 'Props'),
      ),
    );

    await open(tester);
    await save(tester);

    final target = sceneRow('props');
    await tester.tap(target);
    await tester.pump(const Duration(milliseconds: 40));
    await tester.tap(target);
    await tester.pumpAndSettle();

    // The other scene is now the one with objects.
    expect(row('Barrel'), findsOneWidget);
    expect(row('Props'), findsNothing, reason: 'the old scene was unloaded');
    expect(find.textContaining('props$sceneExtension'), findsWidgets);
  });

  testWidgets('leaving a scene with changes asks before dropping them', (
    tester,
  ) async {
    File(p.join(root.path, 'scenes', 'props$sceneExtension'))
        .writeAsStringSync(SceneDocument.encode(EditorScene([])));

    await open(tester);
    await save(tester);
    await add(tester, 'Group');

    final target = sceneRow('props');
    await tester.tap(target);
    await tester.pump(const Duration(milliseconds: 40));
    await tester.tap(target);
    await tester.pumpAndSettle();

    expect(find.textContaining('Save main first?'), findsOneWidget);
  });

  testWidgets('cancelling the prompt keeps you where you were', (tester) async {
    File(p.join(root.path, 'scenes', 'props$sceneExtension'))
        .writeAsStringSync(SceneDocument.encode(EditorScene([])));

    await open(tester);
    await save(tester);
    await add(tester, 'Group');

    final target = sceneRow('props');
    await tester.tap(target);
    await tester.pump(const Duration(milliseconds: 40));
    await tester.tap(target);
    await tester.pumpAndSettle();

    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();

    // Still in the first scene, with the change still there.
    expect(row('Props'), findsOneWidget);
    expect(row('Group'), findsOneWidget);
    expect(unsavedMarker(), findsOneWidget);
  });

  testWidgets('choosing Save writes the change and then moves on', (
    tester,
  ) async {
    File(p.join(root.path, 'scenes', 'props$sceneExtension')).writeAsStringSync(
      SceneDocument.encode(
        EditorScene([
          SceneObject(id: 'barrel', name: 'Barrel', kind: ObjectKind.mesh),
        ]),
      ),
    );

    await open(tester);
    await save(tester);
    await add(tester, 'Group');

    final target = sceneRow('props');
    await tester.tap(target);
    await tester.pump(const Duration(milliseconds: 40));
    await tester.tap(target);
    await tester.pumpAndSettle();

    await tester.tap(
      find.descendant(
        of: find.byType(AlertDialog),
        matching: find.text('Save'),
      ),
    );
    await tester.pumpAndSettle();

    // The change reached the file it belonged to...
    final written = File(p.join(root.path, 'scenes', 'main$sceneExtension'))
        .readAsStringSync();
    expect(written, contains('"Group"'));
    // ...and the other scene is now loaded.
    expect(row('Barrel'), findsOneWidget);
  });

  testWidgets(
    'an unloaded scene offers to load rather than pretending to edit',
    (tester) async {
      File(p.join(root.path, 'scenes', 'props$sceneExtension'))
          .writeAsStringSync(SceneDocument.encode(EditorScene([])));

      await open(tester);
      await tester.tap(sceneRow('props'));
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();

      // No environment fields to fiddle with — there is no document behind them.
      expect(find.text('Load scene'), findsOneWidget);
      expect(find.text('ENVIRONMENT'), findsNothing);
    },
  );
}

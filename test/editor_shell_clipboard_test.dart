import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clipboard.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Copy, cut and paste -- within a scene, between scenes, and to and from
// the system clipboard -- and selecting more than one thing to do it to.

void main() {
  useShell();

  /// Loads a scene from its row in the hierarchy.
  Future<void> loadScene(WidgetTester tester, String name) async {
    final target = sceneRow(name);
    await tester.tap(target);
    await tester.pump(const Duration(milliseconds: 40));
    await tester.tap(target);
    await tester.pumpAndSettle();
  }

  Future<void> press(WidgetTester tester, LogicalKeyboardKey key) async {
    await tester.sendKeyDownEvent(commandKey);
    await tester.sendKeyEvent(key);
    await tester.sendKeyUpEvent(commandKey);
    await tester.pumpAndSettle();
  }

  testWidgets('an object can be copied from one scene into another', (
    tester,
  ) async {
    File(
      p.join(root.path, 'scenes', 'props$sceneExtension'),
    ).writeAsStringSync(SceneDocument.encode(EditorScene([], name: 'Props')));

    await open(tester);
    await save(tester);

    // Copy a whole subtree out of the first scene...
    await tester.tap(row('Props'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyC);

    // ...leave it, and paste into the other.
    await loadScene(tester, 'props');
    expect(row('Props'), findsNothing, reason: 'the other scene is empty');

    await press(tester, LogicalKeyboardKey.keyV);

    expect(row('Props'), findsOneWidget);
    expect(row('Cube'), findsOneWidget, reason: 'the children came too');
    expect(row('Crate'), findsOneWidget);
  });

  testWidgets('the clipboard survives the scene it came from being unloaded', (
    tester,
  ) async {
    File(
      p.join(root.path, 'scenes', 'props$sceneExtension'),
    ).writeAsStringSync(SceneDocument.encode(EditorScene([], name: 'Props')));

    await open(tester);
    await save(tester);

    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyC);

    await loadScene(tester, 'props');
    await press(tester, LogicalKeyboardKey.keyV);
    await save(tester);

    final written = File(p.join(root.path, 'scenes', 'props$sceneExtension'))
        .readAsStringSync();
    expect(written, contains('"Crate"'));
  });

  testWidgets('cut removes it from the scene it was in', (tester) async {
    await open(tester);

    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyX);

    expect(row('Crate'), findsNothing);
    expect(find.textContaining('Delete Crate'), findsOneWidget);
  });

  testWidgets('duplicate leaves the original alone', (tester) async {
    await open(tester);

    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyD);

    // Two now, sharing a name and nothing else.
    expect(row('Crate'), findsNWidgets(2));
    expect(find.textContaining('Paste Crate'), findsOneWidget);
  });

  testWidgets('pasting is undoable in one step', (tester) async {
    await open(tester);

    await tester.tap(row('Props'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyD);
    expect(row('Cube'), findsNWidgets(2));

    await press(tester, LogicalKeyboardKey.keyZ);

    // The whole subtree went, not just its root.
    expect(row('Cube'), findsOneWidget);
    expect(row('Props'), findsOneWidget);
  });

  testWidgets('the edit menu says what paste would do', (tester) async {
    await open(tester);

    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    // Nothing copied yet, so it is just Paste.
    expect(find.text('Paste'), findsOneWidget);
    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();

    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyC);

    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Paste Crate'), findsOneWidget);
  });

  /// Clicks a row with a modifier held.
  Future<void> clickWith(
    WidgetTester tester,
    String name,
    LogicalKeyboardKey modifier,
  ) async {
    await tester.sendKeyDownEvent(modifier);
    await tester.tap(row(name));
    await tester.pumpAndSettle();
    await tester.sendKeyUpEvent(modifier);
  }

  testWidgets('command-click adds to the selection', (tester) async {
    await open(tester);

    await tester.tap(row('Sun'));
    await tester.pumpAndSettle();
    await clickWith(tester, 'Ground', commandKeyLeft);

    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Copy 2 objects'), findsOneWidget);
  });

  testWidgets('shift-click takes everything between', (tester) async {
    await open(tester);

    await tester.tap(row('Sun'));
    await tester.pumpAndSettle();
    await clickWith(tester, 'Cube', LogicalKeyboardKey.shiftLeft);

    // Sun, Ground, Props, Cube — in the order the tree draws them.
    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Copy 4 objects'), findsOneWidget);
  });

  testWidgets('command-click again takes one back out', (tester) async {
    await open(tester);

    await tester.tap(row('Sun'));
    await tester.pumpAndSettle();
    await clickWith(tester, 'Ground', commandKeyLeft);
    await clickWith(tester, 'Ground', commandKeyLeft);

    await tester.tap(find.text('Edit'));
    await tester.pumpAndSettle();
    expect(find.text('Copy'), findsOneWidget, reason: 'back to one');
  });

  testWidgets('deleting several is one step', (tester) async {
    await open(tester);

    await tester.tap(row('Sun'));
    await tester.pumpAndSettle();
    await clickWith(tester, 'Ground', commandKeyLeft);

    await tester.sendKeyEvent(LogicalKeyboardKey.delete);
    await tester.pumpAndSettle();

    expect(row('Sun'), findsNothing);
    expect(row('Ground'), findsNothing);
    expect(find.textContaining('Delete 2 objects'), findsOneWidget);

    await press(tester, LogicalKeyboardKey.keyZ);
    expect(row('Sun'), findsOneWidget);
    expect(row('Ground'), findsOneWidget);
  });

  testWidgets('several can be copied into another scene at once', (
    tester,
  ) async {
    File(
      p.join(root.path, 'scenes', 'props$sceneExtension'),
    ).writeAsStringSync(SceneDocument.encode(EditorScene([], name: 'Props')));

    await open(tester);
    await save(tester);

    await tester.tap(row('Sun'));
    await tester.pumpAndSettle();
    await clickWith(tester, 'Ground', commandKeyLeft);
    await press(tester, LogicalKeyboardKey.keyC);

    await loadScene(tester, 'props');
    await press(tester, LogicalKeyboardKey.keyV);

    expect(row('Sun'), findsOneWidget);
    expect(row('Ground'), findsOneWidget);
  });

  testWidgets('a copy leaves readable text on the system clipboard', (
    tester,
  ) async {
    await open(tester);

    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyC);

    // What lands in a text editor is the scene's own encoding, not a blob.
    expect(systemClipboard, isNotNull);
    expect(systemClipboard, contains('"Crate"'));
    expect(systemClipboard, contains('orblit.objects'));
  });

  testWidgets('a copy from elsewhere can be pasted in', (tester) async {
    await open(tester);

    // As though another window had put it there.
    final elsewhere = SceneClipboard()
      ..take(
        EditorScene([
          SceneObject(id: 'x', name: 'From Elsewhere', kind: ObjectKind.mesh),
        ]),
        ['x'],
      );
    systemClipboard = elsewhere.toText();

    await press(tester, LogicalKeyboardKey.keyV);

    expect(row('From Elsewhere'), findsOneWidget);
  });

  testWidgets('text that is not ours is not pasted', (tester) async {
    await open(tester);
    systemClipboard = 'just some notes I had copied';

    await press(tester, LogicalKeyboardKey.keyV);

    expect(find.textContaining('nothing on the clipboard'), findsOneWidget);
  });

  testWidgets('a pasted object lands where it was in the world', (
    tester,
  ) async {
    File(
      p.join(root.path, 'scenes', 'props$sceneExtension'),
    ).writeAsStringSync(SceneDocument.encode(EditorScene([], name: 'Props')));

    await open(tester);
    await save(tester);

    // The crate sits inside Props, at 2.2 along x.
    await tester.tap(row('Crate'));
    await tester.pumpAndSettle();
    await press(tester, LogicalKeyboardKey.keyC);

    await loadScene(tester, 'props');
    await press(tester, LogicalKeyboardKey.keyV);
    await save(tester);

    // The other scene has no Props to inherit from, so the local transform has
    // to carry what the parent used to contribute.
    final written = File(p.join(root.path, 'scenes', 'props$sceneExtension'))
        .readAsStringSync();
    final load = SceneDocument.decode(written);
    final crate = load.scene.objects.firstWhere((o) => o.name == 'Crate');
    expect(crate.position.x, closeTo(2.2, 1e-6));
  });
}

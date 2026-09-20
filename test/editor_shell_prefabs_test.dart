import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/asset_browser.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Prefabs and the shared set behind them, and making a new thing from the
// browser.

void main() {
  useShell();

  group('prefabs', () {
    /// Selects a row and drags it onto the project browser.
    ///
    /// Selected first, because making a prefab of something is a thing done
    /// to the thing in front of you — and the inspector has to be showing it
    /// for the band that appears afterwards to be worth looking at.
    Future<void> dragToBrowser(WidgetTester tester, String name) async {
      await tester.tap(row(name));
      await tester.pumpAndSettle();

      final gesture = await tester.startGesture(
        tester.getCenter(row(name)),
        kind: PointerDeviceKind.mouse,
      );
      await tester.pump(kLongPressTimeout);
      await gesture.moveTo(tester.getCenter(find.byType(GridView)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();

      // What it says stays on screen until its own timer fires, and a snack
      // bar still showing holds the next one back in the queue.
      await tester.pump(const Duration(seconds: 5));
      await tester.pumpAndSettle();
    }

    /// The prefab band in the inspector, rather than the tile in the browser
    /// that carries the same file name.
    Finder band(String text) =>
        find.descendant(of: find.byType(Inspector), matching: find.text(text));

    /// The prefab files in the project, whatever folder they landed in.
    List<File> prefabs() => root
        .listSync(recursive: true)
        .whereType<File>()
        .where((f) => f.path.endsWith('.oprefab'))
        .toList();

    testWidgets('dropping an object on the project makes one', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      final made = prefabs();
      expect(made, hasLength(1));
      expect(p.basename(made.single.path), 'Cube.oprefab');
      expect(made.single.readAsStringSync(), contains('orblit.prefab'));
    });

    testWidgets('what it was made from becomes an instance', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      // The band only appears for an object that remembers a prefab.
      expect(band('Cube.oprefab'), findsOneWidget);
      expect(band('Apply'), findsOneWidget);
      expect(band('Unpack'), findsOneWidget);
    });

    testWidgets('making one is undoable, link and all', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      await undo(tester);

      expect(band('Cube.oprefab'), findsNothing);
      // The file stays: undo covers the scene, not the project folder, and
      // pretending otherwise would delete somebody's asset behind their back.
      expect(prefabs(), hasLength(1));
    });

    testWidgets('unpacking takes the link off', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      await tester.tap(band('Unpack'));
      await tester.pumpAndSettle();

      expect(band('Cube.oprefab'), findsNothing);
    });

    testWidgets('a prefab dropped in the viewport comes back', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      final tile = find.descendant(
        of: find.byType(GridView),
        matching: find.text('Cube.oprefab'),
      );
      final gesture = await tester.startGesture(
        tester.getCenter(tile),
        kind: PointerDeviceKind.mouse,
      );
      await tester.pump(kLongPressTimeout);
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();

      // Two cubes now: the one it was made from and the one just placed.
      expect(row('Cube'), findsOneWidget);
      expect(row('Cube 2'), findsOneWidget);
    });

    testWidgets('a change applied reaches the other instances', (tester) async {
      await open(tester);
      await dragToBrowser(tester, 'Cube');

      final source = prefabs().single;
      // Two more instances, placed straight through the dropped file.
      for (var i = 0; i < 2; i++) {
        final tile = find.descendant(
          of: find.byType(GridView),
          matching: find.text('Cube.oprefab'),
        );
        final gesture = await tester.startGesture(
          tester.getCenter(tile),
          kind: PointerDeviceKind.mouse,
        );
        await tester.pump(kLongPressTimeout);
        await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
        await tester.pump();
        await gesture.up();
        await tester.pumpAndSettle();
      }

      // Back to the original, and apply from it.
      await tester.tap(row('Cube'));
      await tester.pumpAndSettle();
      await tester.tap(band('Apply'));
      // Pumped rather than settled: what it says goes in a snack bar, and
      // settling waits out the four seconds it is on screen for.
      await tester.pump();

      expect(source.readAsStringSync(), contains('orblit.prefab'));
      expect(find.textContaining('updated 2 other instances'), findsOneWidget);
      await tester.pumpAndSettle();
    });
  });

  group('the shared set', () {
    /// The Shared scene's own row, which sits above every scene.
    Finder sharedRow() => find.descendant(
      of: find.byType(Outliner),
      matching: find.text('Shared'),
    );

    Future<void> dragOnto(WidgetTester tester, Finder from, Finder to) async {
      final gesture = await tester.startGesture(tester.getCenter(from));
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(to));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
    }

    testWidgets('an object can be dragged into it', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Crate'), sharedRow());

      // Out of the scene it was in and into the set every scene has: still
      // one row, but the scene is one object lighter.
      expect(find.textContaining('Move Crate'), findsOneWidget);
      expect(row('Crate'), findsOneWidget);
      expect(find.textContaining('6 objects'), findsOneWidget);
    });

    testWidgets('and dragged back out again', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Crate'), sharedRow());
      expect(find.textContaining('6 objects'), findsOneWidget);

      await dragOnto(tester, row('Crate'), row('Props'));

      expect(row('Crate'), findsOneWidget);
      expect(find.textContaining('7 objects'), findsOneWidget);
    });

    testWidgets('moving into it is undoable', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Crate'), sharedRow());
      await press(tester, LogicalKeyboardKey.keyZ);

      // Back in the scene it came from, counted there again.
      expect(row('Crate'), findsOneWidget);
      expect(find.textContaining('7 objects'), findsOneWidget);
    });

    testWidgets('something in it can be copied and pasted', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Crate'), sharedRow());

      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await press(tester, LogicalKeyboardKey.keyC);
      await press(tester, LogicalKeyboardKey.keyV);

      // Both in the shared set, since that is where the selection was.
      expect(row('Crate'), findsNWidgets(2));
    });

    testWidgets('something in it can be duplicated', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Crate'), sharedRow());

      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await press(tester, LogicalKeyboardKey.keyD);

      expect(row('Crate'), findsNWidgets(2));
    });

    testWidgets('pasting with nothing selected still goes to the scene', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await press(tester, LogicalKeyboardKey.keyC);
      await press(tester, LogicalKeyboardKey.keyV);

      expect(row('Crate'), findsNWidgets(2));
    });

    testWidgets('an object copied out of it lands in the open scene', (
      tester,
    ) async {
      await open(tester);
      await dragOnto(tester, row('Sun'), sharedRow());

      await tester.tap(row('Sun'));
      await tester.pumpAndSettle();
      await press(tester, LogicalKeyboardKey.keyC);
      await tester.tap(row('Props'));
      await tester.pumpAndSettle();
      await press(tester, LogicalKeyboardKey.keyV);

      expect(row('Sun'), findsNWidgets(2));
    });

    testWidgets('the scene it left reads as unsaved too', (tester) async {
      await open(tester);
      await save(tester);
      expect(unsavedMarker(), findsNothing);

      await dragOnto(tester, row('Crate'), sharedRow());

      // Both documents changed, so the one it came out of is a whole object
      // short of what is on disk and has to say so.
      expect(unsavedMarker(), findsWidgets);
    });

    testWidgets('a whole subtree moves into it at once', (tester) async {
      await open(tester);
      await dragOnto(tester, row('Props'), sharedRow());

      // The group and both its children went together.
      expect(row('Props'), findsOneWidget);
      expect(row('Cube'), findsOneWidget);
      expect(row('Crate'), findsOneWidget);
      // Three fewer in the scene: the group and both its children.
      expect(find.textContaining('4 objects'), findsOneWidget);
    });
  });

  group('making things from the browser', () {
    /// Right-clicks the empty space in the file grid.
    Future<void> rightClickGrid(WidgetTester tester) async {
      await tester.tapAt(
        tester.getCenter(find.byType(AssetBrowser)),
        buttons: kSecondaryButton,
      );
      await tester.pumpAndSettle();
    }

    /// Picks a menu entry, opening the group it is in when it has one.
    Future<void> pick(WidgetTester tester, String label, {String? from}) async {
      if (from != null) {
        await tester.tap(find.widgetWithText(SubmenuButton, from));
        await tester.pumpAndSettle();
      }
      await tester.tap(
        find.descendant(
          of: find.byType(MenuItemButton),
          matching: find.text(label),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('the menu groups what it can make', (tester) async {
      await open(tester);
      await rightClickGrid(tester);

      // The common ones stay in front; the rest are behind a heading.
      expect(find.widgetWithText(MenuItemButton, 'Folder'), findsOneWidget);
      expect(find.widgetWithText(MenuItemButton, 'Scene'), findsOneWidget);
      expect(find.widgetWithText(SubmenuButton, 'Script'), findsOneWidget);
      expect(find.widgetWithText(SubmenuButton, 'Style'), findsOneWidget);

      // Nothing inside a group is on the menu until the group is opened.
      expect(find.text('TypeScript'), findsNothing);
      expect(find.text('Stylesheet'), findsNothing);
    });

    testWidgets('a group opens to what is in it', (tester) async {
      await open(tester);
      await rightClickGrid(tester);
      await tester.tap(find.widgetWithText(SubmenuButton, 'Script'));
      await tester.pumpAndSettle();

      expect(find.text('TypeScript'), findsOneWidget);
      expect(find.text('Interface'), findsOneWidget);
      expect(find.text('C++'), findsOneWidget);
      expect(find.text('C++ header'), findsOneWidget);
      // And what each one would actually write.
      expect(find.text('.tsx'), findsOneWidget);
    });

    testWidgets('a folder is made from the menu', (tester) async {
      await open(tester);
      await rightClickGrid(tester);
      await pick(tester, 'Folder');
      await answerPrompt(tester, 'props', 'Create');

      expect(Directory(p.join(root.path, 'props')).existsSync(), isTrue);
    });

    testWidgets('a script is made inside its group', (tester) async {
      await open(tester);
      await rightClickGrid(tester);
      await pick(tester, 'TypeScript', from: 'Script');
      await answerPrompt(tester, 'walker', 'Create');

      final made = File(p.join(root.path, 'walker.ts'));
      expect(made.existsSync(), isTrue);
      expect(made.readAsStringSync(), contains('onFrame'));
    });

    testWidgets('a theme and a stylesheet are both offered', (tester) async {
      await open(tester);
      await rightClickGrid(tester);
      await pick(tester, 'Theme', from: 'Style');
      await answerPrompt(tester, 'dark', 'Create');

      final made = File(p.join(root.path, 'dark.css'));
      expect(made.existsSync(), isTrue);
      // The theme is the values, not the rules.
      expect(made.readAsStringSync(), contains('--accent'));
    });

    testWidgets('right-clicking a file offers what to do with it too', (
      tester,
    ) async {
      await open(tester);
      await rightClickGrid(tester);
      await pick(tester, 'Folder');
      await answerPrompt(tester, 'props', 'Create');

      await tester.tapAt(
        tester.getCenter(
          find.descendant(
            of: find.byType(GridView),
            matching: find.text('props'),
          ),
        ),
        buttons: kSecondaryButton,
      );
      await tester.pumpAndSettle();

      expect(find.widgetWithText(MenuItemButton, 'Rename'), findsOneWidget);
      expect(find.widgetWithText(MenuItemButton, 'Delete'), findsOneWidget);
      // And still everything the empty space offers.
      expect(find.widgetWithText(SubmenuButton, 'Script'), findsOneWidget);
    });
  });
}

import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/asset_browser.dart';
import 'package:orblit_editor/src/editor/console_panel.dart';
import 'package:orblit_editor/src/editor/dock.dart';
import 'package:orblit_editor/src/editor/game_view.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Arranging the panels: docking, splitting and what is remembered.

void main() {
  useShell();

  group('arranging the panels', () {
    Future<void> viewMenu(WidgetTester tester, String item) async {
      // The toolbar button, not the word "Viewport" wherever else it appears
      // — and it gains a dot when the layout is locked.
      await tester.tap(
        find.byWidgetPredicate(
          (widget) => widget is OrblitButton && widget.label.startsWith('View'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byType(MenuItemButton),
          matching: find.text(item),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('the editor opens with the panels it always had', (
      tester,
    ) async {
      await open(tester);

      expect(find.byType(Outliner), findsOneWidget);
      expect(find.byType(SceneViewport), findsOneWidget);
      expect(find.byType(Inspector), findsOneWidget);
      expect(find.byType(AssetBrowser), findsOneWidget);
    });

    testWidgets('four views is four scene panels at once', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');

      expect(find.byType(SceneViewport), findsNWidgets(4));
      // Each one is a camera of its own, so moving one does not move the rest.
      expect(find.text('SCENE 2'), findsOneWidget);
      expect(find.text('SCENE 4'), findsOneWidget);
    });

    testWidgets('and back to one', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');
      await viewMenu(tester, 'One view');

      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('the game view is a tab beside the scene', (tester) async {
      await open(tester);
      expect(find.text('GAME'), findsOneWidget);
      expect(find.byType(GameView), findsNothing);

      await tester.tap(find.text('GAME'));
      await tester.pumpAndSettle();

      expect(find.byType(GameView), findsOneWidget);
      // One at a time: the scene view is behind it, not beside it.
      expect(find.byType(SceneViewport), findsNothing);
    });

    testWidgets('a panel can be closed and opened again', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Console');
      expect(find.byType(ConsolePanel), findsOneWidget);

      // Closing the console leaves the project browser where it was.
      await viewMenu(tester, 'Project');
      expect(find.byType(AssetBrowser), findsOneWidget);
    });

    testWidgets('the arrangement is remembered', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');
      expect(
        File(p.join(root.path, '.orblit', 'layout.json')).existsSync(),
        isTrue,
      );

      // Opened again, the panels are where they were left.
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pumpAndSettle();
      await open(tester);

      expect(find.byType(SceneViewport), findsNWidgets(4));
    });

    testWidgets('locking says so and stops the tabs being dragged', (
      tester,
    ) async {
      await open(tester);
      await viewMenu(tester, 'Lock the layout');

      expect(find.textContaining('View •'), findsOneWidget);
      // Nothing to drag: a locked layout draws its tabs without handles.
      expect(find.byType(Draggable<PanelDrag>), findsNothing);

      await viewMenu(tester, 'Unlock the layout');
      expect(find.byType(Draggable<PanelDrag>), findsWidgets);
    });

    testWidgets('a saved layout that cannot be read is not fatal', (
      tester,
    ) async {
      Directory(p.join(root.path, '.orblit')).createSync(recursive: true);
      File(p.join(root.path, '.orblit', 'layout.json'))
          .writeAsStringSync('not a layout at all');

      await open(tester);

      // The standard arrangement, rather than an editor that will not open.
      expect(find.byType(SceneViewport), findsOneWidget);
      expect(find.byType(Inspector), findsOneWidget);
    });
  });
}

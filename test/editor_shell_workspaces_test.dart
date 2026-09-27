import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/modelling_panel.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Workspaces: one tab per job along the top, each with its own panels and
// its own saved arrangement.

void main() {
  useShell();

  group('workspaces', () {
    Future<void> addCube(WidgetTester tester) async {
      await tester.tap(find.text('Add'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(SubmenuButton, 'Shape'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byType(MenuItemButton),
          matching: find.text('Cube'),
        ),
      );
      await tester.pumpAndSettle();
    }

    /// Whether the viewport is showing the parts of a shape rather than
    /// whole objects.
    bool editingParts(WidgetTester tester) =>
        tester.widget<SceneViewport>(find.byType(SceneViewport)).editing !=
        null;

    testWidgets('the tabs follow the order a level is made in', (
      tester,
    ) async {
      await open(tester);

      final lefts = [
        for (final name in ['scene', 'modelling', 'terrain', 'animation'])
          tester.getTopLeft(modeTab(name)).dx,
      ];
      expect(lefts, [...lefts]..sort());
      expect(inMode(tester, 'scene'), isTrue);
    });

    testWidgets('each one shows the panels for its job', (tester) async {
      await open(tester);
      for (final tab in ['modelling', 'uvs', 'timeline']) {
        expect(dockTab(tab), findsNothing, reason: tab);
      }

      await enterMode(tester, 'modelling');
      expect(inMode(tester, 'modelling'), isTrue);
      expect(find.byType(ModellingPanel), findsOneWidget);
      expect(dockTab('uvs'), findsOneWidget);

      await enterMode(tester, 'animation');
      expect(dockTab('timeline'), findsOneWidget);
      expect(dockTab('modelling'), findsNothing);
      // The same world in every one of them.
      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('Modelling goes into the parts of the selected shape', (
      tester,
    ) async {
      await open(tester);
      await addCube(tester);
      expect(editingParts(tester), isFalse);

      await enterMode(tester, 'modelling');
      expect(editingParts(tester), isTrue);

      // And back out when somebody goes back to placing things.
      await enterMode(tester, 'scene');
      expect(editingParts(tester), isFalse);
    });

    testWidgets('Modelling with nothing selected has no parts to go into', (
      tester,
    ) async {
      await open(tester);
      await enterMode(tester, 'modelling');

      expect(editingParts(tester), isFalse);
      expect(find.textContaining('Select a shape'), findsOneWidget);
    });

    testWidgets('Modelling says why a model has no parts to change', (
      tester,
    ) async {
      await open(tester);
      await add(tester, 'Mesh object');
      await enterMode(tester, 'modelling');

      // Not "select a shape", which reads as if the click had missed.
      expect(editingParts(tester), isFalse);
      expect(find.textContaining('is not a shape'), findsOneWidget);
      expect(find.textContaining('Select a shape'), findsNothing);
    });

    testWidgets('Reset panels puts back the workspace in use', (
      tester,
    ) async {
      await open(tester);
      await enterMode(tester, 'animation');
      await viewMenu(tester, 'Four views');
      expect(dockTab('timeline'), findsNothing);

      await viewMenu(tester, 'Reset panels');

      // The Animation arrangement, rather than the Scene one.
      expect(dockTab('timeline'), findsOneWidget);
      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('each one remembers its own arrangement', (tester) async {
      await open(tester);
      await enterMode(tester, 'animation');
      await viewMenu(tester, 'Four views');
      expect(
        File(p.join(root.path, '.orblit', 'layout.animation.json'))
            .existsSync(),
        isTrue,
      );

      // Scene is as it was.
      await enterMode(tester, 'scene');
      expect(find.byType(SceneViewport), findsOneWidget);

      await enterMode(tester, 'animation');
      expect(find.byType(SceneViewport), findsNWidgets(4));
    });
  });
}

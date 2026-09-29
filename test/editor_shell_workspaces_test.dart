import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/editor_mode.dart';
import 'package:orblit_editor/src/editor/interface_mode.dart';
import 'package:orblit_editor/src/editor/modelling_panel.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_ui/orblit_ui.dart';
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
        for (final name in [
          'scene',
          'modelling',
          'terrain',
          'animation',
          'cinematics',
          'interface',
        ])
          tester.getTopLeft(modeTab(name)).dx,
      ];
      expect(lefts, [...lefts]..sort());
      expect(inMode(tester, 'scene'), isTrue);
    });

    testWidgets('the tabs give up their icons before their words', (
      tester,
    ) async {
      await open(tester);
      Finder icon() => find.descendant(
        of: modeTab('interface'),
        matching: find.byType(Icon),
      );
      Finder word() => find.descendant(
        of: modeTab('interface'),
        matching: find.text('Interface'),
      );

      // The test font is wider than any real one, so these widths are
      // where it runs out of room rather than where a real window does.
      await tester.binding.setSurfaceSize(const Size(1800, 900));
      await tester.pumpAndSettle();
      expect(icon(), findsOneWidget);
      expect(word(), findsOneWidget);

      await tester.binding.setSurfaceSize(const Size(1600, 900));
      await tester.pumpAndSettle();
      expect(icon(), findsNothing);
      expect(word(), findsOneWidget);

      await tester.binding.setSurfaceSize(const Size(960, 620));
      await tester.pumpAndSettle();
      expect(icon(), findsOneWidget);
      expect(word(), findsNothing);
      expect(find.byTooltip('Interface'), findsOneWidget);
    });

    testWidgets('each one fits the smallest window', (tester) async {
      await open(tester);
      // The minimum the macOS runner allows. Anything that runs over its
      // edge fails the test.
      await tester.binding.setSurfaceSize(const Size(960, 620));
      await tester.pumpAndSettle();
      // Selected, because a terrain's sections have the longest headings
      // and the colour swatches.
      await add(tester, 'Terrain');
      for (final name in [
        'modelling',
        'terrain',
        'animation',
        'cinematics',
        'interface',
        'scene',
      ]) {
        await enterMode(tester, name);
      }
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

  group('the Interface workspace', () {
    /// Writes hud.oui into the project and returns where.
    String writeHud() {
      final path = p.join(root.path, 'hud.oui');
      File(path).writeAsStringSync(
        const UiDocument(
          root: UiNode(
            type: 'stack',
            classes: 'w-full h-full',
            children: [
              UiNode(
                type: 'text',
                css: 'left: 40px; top: 40px',
                text: 'Health 100',
              ),
            ],
          ),
        ).toText(),
      );
      return path;
    }

    Finder hudTile() => find.descendant(
      of: find.byType(GridView),
      matching: find.text('hud.oui'),
    );

    Finder onCanvas(String text) => find.descendant(
      of: find.byType(InterfaceCanvas),
      matching: find.text(text),
    );

    // The bar along the bottom says the same of a file at the project's
    // root.
    Finder onShelf(String text) => find.descendant(
      of: find.byType(InterfaceShelf),
      matching: find.text(text),
    );

    Finder inElements(String text) => find.descendant(
      of: find.byType(InterfaceElements),
      matching: find.text(text),
    );

    /// Opens hud.oui from Project, which takes a double tap.
    Future<void> openHud(WidgetTester tester) async {
      await tester.tap(hudTile());
      await tester.pump(const Duration(milliseconds: 50));
      await tester.tap(hudTile());
      await tester.pumpAndSettle();
    }

    Future<void> addBox(WidgetTester tester) async {
      // The word itself. The first Container around it is the whole
      // palette, and its middle is a different button.
      await tester.tap(
        find.descendant(
          of: find.byType(InterfaceDesign),
          matching: find.text('Box'),
        ),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('opening an interface from Project goes there', (
      tester,
    ) async {
      writeHud();
      await open(tester);
      await openHud(tester);

      expect(inMode(tester, 'interface'), isTrue);
      expect(onCanvas('Health 100'), findsOneWidget);
      expect(inElements('Health 100'), findsOneWidget);
    });

    testWidgets('the editor keys undo and save what is laid out', (
      tester,
    ) async {
      final path = writeHud();
      await open(tester);
      await openHud(tester);

      await addBox(tester);
      expect(inElements('box'), findsOneWidget);
      expect(onShelf('hud.oui •'), findsOneWidget);

      await undo(tester);
      expect(inElements('box'), findsNothing);
      expect(onShelf('hud.oui •'), findsNothing);

      await addBox(tester);
      await save(tester);
      expect(onShelf('hud.oui •'), findsNothing);
      final saved = UiDocument.read(File(path).readAsStringSync());
      expect(saved!.root.children.last.type, 'box');
    });

    testWidgets('it opens on the interface the scene shows', (tester) async {
      writeHud();
      await open(tester);
      await dropOnViewport(tester, hudTile());

      await enterMode(tester, 'interface');

      expect(onCanvas('Health 100'), findsOneWidget);
    });

    testWidgets('delete takes out the element, not the object', (
      tester,
    ) async {
      writeHud();
      await open(tester);
      await dropOnViewport(tester, hudTile());
      await enterMode(tester, 'interface');

      await tester.tap(inElements('Health 100'));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.delete);
      await tester.pumpAndSettle();

      expect(onCanvas('Health 100'), findsNothing);
      await enterMode(tester, 'scene');
      expect(row('hud'), findsOneWidget);
    });

    testWidgets('with nothing open it offers to make one', (tester) async {
      await open(tester);
      await enterMode(tester, 'interface');
      expect(find.text('No interface open'), findsOneWidget);
      // Once, on the canvas. A second on the shelf read as a second thing,
      // and a shelf with nothing on it is an empty strip.
      expect(find.text('New interface'), findsOneWidget);
      expect(find.byType(ModeShelf), findsNothing);

      await tester.tap(onCanvas('New interface'));
      await tester.pumpAndSettle();

      expect(
        File(p.join(root.path, 'interfaces', 'screen.oui')).existsSync(),
        isTrue,
      );
      expect(find.text('No interface open'), findsNothing);
      // The shelf's, for the next one.
      expect(
        find.descendant(
          of: find.byType(InterfaceShelf),
          matching: find.text('New interface'),
        ),
        findsOneWidget,
      );
      // The scene showed nothing, so it shows the new one.
      await enterMode(tester, 'scene');
      expect(row('screen'), findsOneWidget);
    });

    testWidgets('the bar along the bottom names the interface', (
      tester,
    ) async {
      final counts = find.textContaining(RegExp(r'^\d+ objects$'));
      await open(tester);
      expect(counts, findsOneWidget);

      await enterMode(tester, 'interface');
      await tester.tap(onCanvas('New interface'));
      await tester.pumpAndSettle();

      // Where it is in the project, since the shelf gives only its name.
      final where = find.text('interfaces/screen.oui');
      expect(where, findsOneWidget);
      // Against the bar's right edge, not left wherever the last change's
      // words happen to end.
      expect(tester.getTopRight(where).dx, closeTo(1440 - 12, 1));
      expect(find.byType(ModeShelf), findsOneWidget);
      // The scene's count and frame rate say nothing about a screen.
      expect(counts, findsNothing);
      expect(find.textContaining('fps'), findsNothing);

      await enterMode(tester, 'scene');
      expect(counts, findsOneWidget);
    });
  });
}

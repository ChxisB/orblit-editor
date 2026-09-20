import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/asset_browser.dart';
import 'package:orblit_editor/src/editor/data_object.dart';
import 'package:orblit_editor/src/editor/console_panel.dart';
import 'package:orblit_editor/src/editor/data_panel.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// The panels beside the scene: data objects, a scene's interface, and the
// console.

void main() {
  useShell();

  group('data objects', () {
    Future<void> rightClickGrid(WidgetTester tester) async {
      await tester.tapAt(
        tester.getCenter(find.byType(AssetBrowser)),
        buttons: kSecondaryButton,
      );
      await tester.pumpAndSettle();
    }

    /// Makes one through the menu and leaves it selected.
    Future<void> makeOne(WidgetTester tester, String name) async {
      await rightClickGrid(tester);
      await tester.tap(find.widgetWithText(SubmenuButton, 'Data'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byType(MenuItemButton),
          matching: find.text('Data object'),
        ),
      );
      await tester.pumpAndSettle();
      await answerPrompt(tester, name, 'Create');
    }

    Finder tile(String name) =>
        find.descendant(of: find.byType(GridView), matching: find.text(name));

    /// Scrolls the inspector to the bottom.
    ///
    /// Its sections are a lazy list, so one below the fold is not built and a
    /// finder cannot see it. Which sections fit depends on the window and on
    /// how the panels are arranged, so a test that assumes one is on screen is
    /// a test that breaks when somebody moves a panel.
    /// Brings the modelling panel to the front. It is a tab beside the
    /// inspector, and a tab that is not showing is not built. Tabs are drawn
    Future<void> scrollInspector(WidgetTester tester) async {
      await tester.drag(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.byType(ListView),
        ),
        const Offset(0, -600),
      );
      await tester.pumpAndSettle();
    }

    /// Selects a file in the grid.
    ///
    /// The wait is the double-tap window: a tile answers both, so a single
    /// tap is not a single tap until the timer for the second one has run
    /// out, and pumpAndSettle does not run out a timer.
    Future<void> select(WidgetTester tester, String name) async {
      await tester.tap(tile(name));
      await tester.pump(kDoubleTapTimeout + const Duration(milliseconds: 20));
      await tester.pumpAndSettle();
    }

    testWidgets('one is made with something already in it', (tester) async {
      await open(tester);
      await makeOne(tester, 'ball');

      final made = File(p.join(root.path, 'ball.odata'));
      expect(made.existsSync(), isTrue);
      expect(DataObject.read(made.readAsStringSync())!.fields, isNotEmpty);
    });

    testWidgets('selecting one edits it in the inspector', (tester) async {
      await open(tester);
      await makeOne(tester, 'ball');

      await select(tester, 'ball.odata');

      expect(find.byType(DataPanel), findsOneWidget);
      expect(find.text('Add a value'), findsOneWidget);
    });

    testWidgets('a value typed in reaches the file', (tester) async {
      await open(tester);
      await makeOne(tester, 'ball');
      await select(tester, 'ball.odata');

      // The number the blank object starts with.
      await tester.enterText(
        find
            .descendant(
              of: find.byType(FieldRow),
              matching: find.byType(TextField),
            )
            .first,
        '12.5',
      );
      // Written shortly after the last keystroke rather than on every one.
      await tester.pump(const Duration(milliseconds: 400));
      await tester.pumpAndSettle();

      final data = DataObject.read(
        File(p.join(root.path, 'ball.odata')).readAsStringSync(),
      )!;
      expect(data.fields.first.asNumber, 12.5);
    });

    testWidgets('selecting an object puts the inspector back', (tester) async {
      await open(tester);
      await makeOne(tester, 'ball');
      await select(tester, 'ball.odata');
      expect(find.byType(DataPanel), findsOneWidget);

      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      expect(find.byType(DataPanel), findsNothing);
    });

    testWidgets('selecting a mesh does not take the inspector', (tester) async {
      Directory(p.join(root.path, 'assets')).createSync();
      File(p.join(root.path, 'assets', 'rock.glb')).writeAsBytesSync([1]);

      await open(tester);
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();
      await openFolder(tester, 'assets');
      await select(tester, 'rock.glb');

      // Still the object: only a data object claims the panel.
      expect(find.byType(DataPanel), findsNothing);
    });

    testWidgets('dropping one on the selection attaches it', (tester) async {
      await open(tester);
      await makeOne(tester, 'weight');

      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      final gesture = await tester.startGesture(
        tester.getCenter(tile('weight.odata')),
      );
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();

      // Listed on the object, and named as what it is rather than a path.
      await scrollInspector(tester);
      expect(find.text('DATA'), findsOneWidget);
      expect(find.text('weight.odata'), findsWidgets);
    });

    testWidgets('attaching is undoable', (tester) async {
      await open(tester);
      await makeOne(tester, 'weight');
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      final gesture = await tester.startGesture(
        tester.getCenter(tile('weight.odata')),
      );
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
      await scrollInspector(tester);
      expect(find.text('DATA'), findsOneWidget);

      await press(tester, LogicalKeyboardKey.keyZ);
      expect(find.text('DATA'), findsNothing);
    });

    testWidgets('what it says is saved with the scene', (tester) async {
      await open(tester);
      await makeOne(tester, 'weight');
      await tester.tap(row('Crate'));
      await tester.pumpAndSettle();

      final gesture = await tester.startGesture(
        tester.getCenter(tile('weight.odata')),
      );
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
      await scrollInspector(tester);
      expect(find.text('DATA'), findsOneWidget);

      await save(tester);
      final written = File(p.join(root.path, 'scenes', 'main.oscene'))
          .readAsStringSync();
      expect(written, contains('weight.odata'));
    });

    testWidgets('the bindings it writes land beside it', (tester) async {
      await open(tester);
      await makeOne(tester, 'ball');
      await select(tester, 'ball.odata');

      await tester.tap(find.text('Write script bindings'));
      await tester.pumpAndSettle();

      // Both languages from the one declaration, which is what makes a field
      // renamed here a build error in whatever reads it.
      final types = File(p.join(root.path, 'ball.d.ts'));
      expect(types.existsSync(), isTrue);
      expect(types.readAsStringSync(), contains('export interface'));

      final header = File(p.join(root.path, 'ball.h'));
      expect(header.existsSync(), isTrue);
      expect(header.readAsStringSync(), contains('namespace Ball'));
    });
  });

  group('an interface on a scene', () {
    /// Makes a .oui in the project and returns its tile in the grid.
    Finder makeInterface(String name) {
      File(p.join(root.path, '$name.oui')).writeAsStringSync(
        const UiDocument(
          name: 'Heads-up',
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
      return find.descendant(
        of: find.byType(GridView),
        matching: find.text('$name.oui'),
      );
    }

    Future<void> dropOnViewport(WidgetTester tester, Finder tile) async {
      final gesture = await tester.startGesture(tester.getCenter(tile));
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();
    }

    testWidgets('dropping one makes a canvas object that shows it', (
      tester,
    ) async {
      final tile = makeInterface('hud');
      await open(tester);
      await dropOnViewport(tester, tile);

      // An object in the tree, and the interface itself over the viewport.
      expect(row('hud'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(SceneViewport),
          matching: find.text('Health 100'),
        ),
        findsOneWidget,
      );
    });

    testWidgets('the object says which interface it shows', (tester) async {
      final tile = makeInterface('hud');
      await open(tester);
      await dropOnViewport(tester, tile);

      expect(find.text('INTERFACE'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.text('hud.oui'),
        ),
        findsOneWidget,
      );
    });

    testWidgets('it is saved with the scene and comes back', (tester) async {
      final tile = makeInterface('hud');
      await open(tester);
      await dropOnViewport(tester, tile);
      await save(tester);

      final written = File(p.join(root.path, 'scenes', 'main.oscene'))
          .readAsStringSync();
      expect(written, contains('hud.oui'));

      final back = SceneDocument.decode(written).scene;
      final canvas = back.objects.firstWhere(
        (o) => o.kind == ObjectKind.canvas,
      );
      expect(canvas.interfaceAsset, 'hud.oui');
    });

    testWidgets('hiding the object takes the interface off the scene', (
      tester,
    ) async {
      final tile = makeInterface('hud');
      await open(tester);
      await dropOnViewport(tester, tile);

      Finder drawn() => find.descendant(
        of: find.byType(SceneViewport),
        matching: find.text('Health 100'),
      );
      expect(drawn(), findsOneWidget);

      await tester.tap(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.text('Hidden'),
        ),
      );
      await tester.pumpAndSettle();

      // What hides it here is what hides it in the game: the object's own
      // visibility, not a view setting.
      expect(drawn(), findsNothing);
    });

    testWidgets('the viewport can put it aside without changing the scene', (
      tester,
    ) async {
      final tile = makeInterface('hud');
      await open(tester);
      await dropOnViewport(tester, tile);
      await save(tester);

      await tester.tap(
        find.descendant(
          of: find.byType(SceneViewport),
          matching: find.text('Interface'),
        ),
      );
      await tester.pumpAndSettle();

      expect(
        find.descendant(
          of: find.byType(SceneViewport),
          matching: find.text('Health 100'),
        ),
        findsNothing,
      );
      // A view setting, so the scene is still saved.
      expect(unsavedMarker(), findsNothing);
    });

    testWidgets('dropping a second one onto the selected canvas replaces it', (
      tester,
    ) async {
      makeInterface('hud');
      final other = makeInterface('menu');
      await open(tester);

      await dropOnViewport(
        tester,
        find.descendant(
          of: find.byType(GridView),
          matching: find.text('hud.oui'),
        ),
      );
      await dropOnViewport(tester, other);

      // One canvas, showing the second interface.
      expect(row('hud'), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.text('menu.oui'),
        ),
        findsOneWidget,
      );
    });
  });

  group('the console', () {
    testWidgets('there are two panels at the bottom', (tester) async {
      await open(tester);

      expect(find.text('PROJECT'), findsOneWidget);
      expect(find.text('CONSOLE'), findsOneWidget);
      // The project one is showing to begin with.
      expect(find.byType(AssetBrowser), findsOneWidget);
      expect(find.byType(ConsolePanel), findsNothing);
    });

    testWidgets('opening it shows what the editor has said', (tester) async {
      await open(tester);
      await save(tester);

      await tester.tap(find.text('CONSOLE'));
      await tester.pumpAndSettle();

      expect(find.byType(ConsolePanel), findsOneWidget);
      expect(find.byType(AssetBrowser), findsNothing);
      expect(find.textContaining('Saved'), findsWidgets);
    });

    testWidgets('a message that has gone from the status bar is still there', (
      tester,
    ) async {
      await open(tester);
      await save(tester);

      // Long enough for the snack bar to have come and gone.
      await tester.pump(const Duration(seconds: 6));
      await tester.pumpAndSettle();

      await tester.tap(find.text('CONSOLE'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Saved'), findsWidgets);
    });

    testWidgets('a refusal is an error, and says so on the tab', (
      tester,
    ) async {
      Directory(p.join(root.path, 'assets')).createSync();
      File(p.join(root.path, 'assets', 'rock.png')).writeAsBytesSync([1]);

      await open(tester);
      await openFolder(tester, 'assets');

      final tile = find.descendant(
        of: find.byType(GridView),
        matching: find.text('rock.png'),
      );
      final gesture = await tester.startGesture(tester.getCenter(tile));
      await tester.pump(const Duration(milliseconds: 200));
      await gesture.moveTo(tester.getCenter(find.byType(SceneViewport)));
      await tester.pump();
      await gesture.up();
      await tester.pumpAndSettle();

      // Kept, and counted where somebody who is not looking at the console
      // will still see it.
      await tester.tap(find.text('CONSOLE'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Select an object first'), findsWidgets);
    });

    testWidgets('it can be cleared', (tester) async {
      await open(tester);
      await save(tester);
      await tester.tap(find.text('CONSOLE'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Saved'), findsWidgets);

      await tester.tap(find.byIcon(Icons.delete_sweep_outlined));
      await tester.pumpAndSettle();

      expect(find.text('Nothing to report.'), findsOneWidget);
    });

    testWidgets('the filters hide a level', (tester) async {
      await open(tester);
      await save(tester);
      await tester.tap(find.text('CONSOLE'));
      await tester.pumpAndSettle();
      expect(find.textContaining('Saved'), findsWidgets);

      await tester.tap(find.textContaining('Info '));
      await tester.pumpAndSettle();

      // Scoped to the panel: the snack bar that said the same thing is still
      // on screen, because a snack bar goes away on its own timer and
      // pumpAndSettle does not run one out.
      expect(
        find.descendant(
          of: find.byType(ConsolePanel),
          matching: find.textContaining('Saved'),
        ),
        findsNothing,
      );
      expect(find.text('Nothing at these levels.'), findsOneWidget);
    });
  });
}

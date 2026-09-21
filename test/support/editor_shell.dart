import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/editor_shell.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/launcher/project.dart';
import 'package:orblit_editor/src/platform/command_shortcuts.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

// How a test drives the editor shell: a project on disk to open, and the
// gestures -- adding, saving, undoing, answering a prompt -- that every
// test here needs and none of them should spell out again.

late Directory root;

/// Stands in for the system clipboard, which has no implementation under
/// the test binding. Copy writes here and paste reads it, so the round trip
/// through text is what the tests actually exercise.
String? systemClipboard;

/// Opens the editor on [root], with whatever [extend] adds to it.
Future<void> open(
  WidgetTester tester, {
  void Function(EditorRegistry registry)? extend,
}) async {
  // The editor's real minimum. At the default 800x600 the panels sit below
  // the fold and a test passes while nothing is on screen.
  await tester.binding.setSurfaceSize(const Size(1440, 900));
  addTearDown(() => tester.binding.setSurfaceSize(null));

  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: EditorShell(
        project: Project(
          name: 'Test',
          directory: root.path,
          lastOpened: DateTime(2026),
        ),
        onClose: () {},
        extend: extend,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

/// Adds through the Add menu.
///
/// Scoped to the menu item, because find.text also matches the outliner row
/// and the inspector's name field — an unscoped tap on "Cube" selects the
/// cube that is already there instead of making one.
Future<void> add(WidgetTester tester, String label) async {
  await tester.tap(find.text('Add'));
  await tester.pumpAndSettle();
  await tester.tap(
    find.descendant(
      of: find.byType(MenuItemButton),
      matching: find.text(label),
    ),
  );
  await tester.pumpAndSettle();
}

/// Picks an item out of a toolbar menu.
Future<void> menu(WidgetTester tester, String button, String item) async {
  await tester.tap(find.textContaining(button).first);
  await tester.pumpAndSettle();
  await tester.tap(
    find.descendant(of: find.byType(MenuItemButton), matching: find.text(item)),
  );
  await tester.pumpAndSettle();
}

/// Picks an item out of the View menu.
///
/// The toolbar button, not the word "Viewport" wherever else it appears —
/// and it gains a dot when the layout is locked.
Future<void> viewMenu(WidgetTester tester, String item) async {
  await tester.tap(
    find.byWidgetPredicate(
      (widget) => widget is OrblitButton && widget.label.startsWith('View'),
    ),
  );
  await tester.pumpAndSettle();
  await tester.tap(
    find.descendant(of: find.byType(MenuItemButton), matching: find.text(item)),
  );
  await tester.pumpAndSettle();
}

/// Types into the dialog on screen and confirms it.
Future<void> answerPrompt(
  WidgetTester tester,
  String text,
  String action,
) async {
  await tester.enterText(
    find.descendant(
      of: find.byType(AlertDialog),
      matching: find.byType(TextField),
    ),
    text,
  );
  await tester.tap(
    find.descendant(of: find.byType(AlertDialog), matching: find.text(action)),
  );
  await tester.pumpAndSettle();
}

/// The unsaved marker in the status bar, rather than the one on the Scene
/// menu — both are shown, in the two places somebody looks.
Finder unsavedMarker() => find.descendant(
  of: find.byType(Row),
  matching: find.textContaining(RegExp(r'\.oscene •|Unsaved')),
);

// Meta on macOS, Control everywhere else — matched to whatever
// commandShortcut actually bound, rather than assuming Meta and leaving
// every one of these presses matching nothing once the default test
// platform is not macOS (it is not: Flutter's test binding defaults to
// Android unless a test says otherwise).
final commandKey = commandIsMeta
    ? LogicalKeyboardKey.meta
    : LogicalKeyboardKey.control;

final commandKeyLeft = commandIsMeta
    ? LogicalKeyboardKey.metaLeft
    : LogicalKeyboardKey.controlLeft;

/// Saves with the keyboard.
Future<void> save(WidgetTester tester) async {
  await tester.sendKeyDownEvent(commandKey);
  await tester.sendKeyEvent(LogicalKeyboardKey.keyS);
  await tester.sendKeyUpEvent(commandKey);
  await tester.pumpAndSettle();
}

/// Undoes with the keyboard.
Future<void> undo(WidgetTester tester) async {
  await tester.sendKeyDownEvent(commandKey);
  await tester.sendKeyEvent(LogicalKeyboardKey.keyZ);
  await tester.sendKeyUpEvent(commandKey);
  await tester.pumpAndSettle();
}

/// Opens a folder from the file grid.
///
/// The grid tile rather than the folder-tree row beside it, because both
/// carry the same text. A tile opens on a double tap; a single one selects.
Future<void> openFolder(WidgetTester tester, String name) async {
  final tile = find.descendant(
    of: find.byType(GridView),
    matching: find.text(name),
  );
  await tester.tap(tile);
  await tester.pump(const Duration(milliseconds: 50));
  await tester.tap(tile);
  await tester.pumpAndSettle();
}

/// The hierarchy panel's own count, rather than the viewport's chip.
Finder sceneCount(String text) => find.descendant(
  of: find.byType(Outliner),
  matching: find.textContaining(text),
);

/// An object's row in the outliner, rather than the same name in the
/// inspector's title field or in a menu. Objects are draggable; scenes are
/// headings and are not.
Finder row(String name) => find.descendant(
  of: find.byType(Draggable<ObjectDrag>),
  matching: find.text(name),
);

/// The whole row a name sits in, rather than just its text — the text is
/// centred, so its top-left is halfway down the row.
Finder rowBox(String name) =>
    find.ancestor(of: row(name), matching: find.byType(Draggable<ObjectDrag>));

/// A scene's own row.
Finder sceneRow(String name) =>
    find.descendant(of: find.byType(Outliner), matching: find.text(name));

Future<void> dropTexture(WidgetTester tester) async {
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
}

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

/// The project directory and the clipboard, made fresh for each test.
void useShell() {
  setUp(() {
    systemClipboard = null;
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, (call) async {
          switch (call.method) {
            case 'Clipboard.setData':
              systemClipboard =
                  (call.arguments as Map<Object?, Object?>)['text'] as String?;
              return null;
            case 'Clipboard.getData':
              return systemClipboard == null ? null : {'text': systemClipboard};
          }
          return null;
        });

    root = Directory.systemTemp.createTempSync('orblit_shell');
    Directory(p.join(root.path, 'scenes')).createSync();
  });

  tearDown(() {
    TestDefaultBinaryMessengerBinding.instance.defaultBinaryMessenger
        .setMockMethodCallHandler(SystemChannels.platform, null);
    root.deleteSync(recursive: true);
  });
}

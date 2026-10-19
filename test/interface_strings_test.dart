import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/interface_bench.dart';
import 'package:orblit_editor/src/editor/inspector.dart' show FieldRow;
import 'package:orblit_editor/src/editor/interface_mode.dart';
import 'package:orblit_editor/src/editor/ui_canvas.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_ui/orblit_ui.dart';

import 'support/interface_workspace.dart';

void main() {
  late Directory root;

  setUp(() => root = Directory.systemTemp.createTempSync('orblit_ui_words'));
  tearDown(() => root.deleteSync(recursive: true));

  const menu = UiDocument(
    name: 'Menu',
    root: UiNode(
      type: 'column',
      children: [
        UiNode(type: 'text', props: {'textKey': 'greet'}),
        UiNode(type: 'button', props: {'textKey': 'play'}),
        UiNode(type: 'button', props: {'textKey': 'quit'}),
        UiNode(type: 'button', text: 'Fixed'),
      ],
    ),
  );

  const languages = {
    'en': '{"greet": "Hello", "play": "Play", "quit": "Quit"}',
    'fr': '{"greet": "Bonjour", "play": "Jouer"}',
  };

  Finder onCanvas(String text) => find.descendant(
    of: find.byType(UiCanvasView),
    matching: find.text(text),
  );

  Future<InterfaceBench> open(WidgetTester tester) => openInterface(
    tester,
    folder: root,
    document: menu,
    languages: languages,
  );

  /// Chooses a language. The section is last in a panel that scrolls, so it
  /// is brought into view first.
  Future<void> choose(WidgetTester tester, String locale) async {
    final chip = find.widgetWithText(Container, locale);
    await tester.ensureVisible(chip);
    await tester.pumpAndSettle();
    await tester.tap(chip);
    await tester.pumpAndSettle();
  }

  group('the canvas speaks a language', () {
    testWidgets('shows the words of the language chosen', (tester) async {
      await open(tester);
      expect(onCanvas('Play'), findsOneWidget);

      await choose(tester, 'fr');

      expect(onCanvas('Jouer'), findsOneWidget);
      expect(onCanvas('Play'), findsNothing);
      // Words written into the file are the same in every language.
      expect(onCanvas('Fixed'), findsOneWidget);
    });

    testWidgets('lists what the language is missing', (tester) async {
      await open(tester);
      expect(find.text('MISSING IN EN'), findsNothing);

      await choose(tester, 'fr');

      expect(find.text('MISSING IN FR'), findsOneWidget);
      expect(find.text('quit'), findsWidgets);
    });

    testWidgets('a key is typed into the Key field and can be undone', (
      tester,
    ) async {
      final bench = await open(tester);
      await tester.tap(onCanvas('Fixed'));
      await tester.pumpAndSettle();

      await tester.enterText(
        find.descendant(
          of: find.widgetWithText(FieldRow, 'Key'),
          matching: find.byType(TextField),
        ),
        'play',
      );
      await tester.pumpAndSettle();

      expect(bench.element?.textKey, 'play');
      expect(onCanvas('Fixed'), findsNothing);

      bench.history.undo();
      await tester.pumpAndSettle();
      expect(bench.element?.textKey, isNull);
      expect(onCanvas('Fixed'), findsOneWidget);
    });

    testWidgets('the section is left out of a project in one language', (
      tester,
    ) async {
      await openInterface(
        tester,
        folder: root,
        document: const UiDocument(
          name: 'Plain',
          root: UiNode(type: 'column', children: [UiNode(type: 'text')]),
        ),
      );
      expect(find.text('Languages'), findsNothing);
    });

    testWidgets('an element is named by its key in the tree', (tester) async {
      await open(tester);
      expect(
        find.descendant(
          of: find.byType(InterfaceElements),
          matching: find.text('play'),
        ),
        findsOneWidget,
      );
    });
  });

  group('the language files', () {
    InterfaceBench bench() {
      final history = History(Workspace(root.path));
      final made = InterfaceBench(
        history: history,
        stringsFolder: stringsPath(root),
      );
      addTearDown(() {
        made.dispose();
        history.dispose();
      });
      return made;
    }

    void write(String name, String text) => File('${stringsPath(root)}/$name')
      ..createSync(recursive: true)
      ..writeAsStringSync(text);

    test('one that cannot be read is reported and the rest still load', () {
      write('fr.json', '{"play": "Jouer"}');
      write('de.json', '[]');
      write('notes.txt', 'not a language');
      final loaded = bench()..loadStrings();

      expect(loaded.strings.locales, ['fr']);
      expect(loaded.stringProblems, hasLength(1));
      expect(loaded.stringProblems.single, startsWith('de.json'));
    });

    test('reading again forgets a key that was taken out', () {
      write('fr.json', '{"play": "Jouer", "quit": "Quitter"}');
      final loaded = bench()
        ..loadStrings()
        ..strings.use('fr');
      expect(loaded.strings.text('quit'), 'Quitter');

      write('fr.json', '{"play": "Jouer"}');
      loaded.loadStrings();

      expect(loaded.strings.text('quit'), 'quit');
      expect(loaded.strings.locale, 'fr');
    });

    test('the one table is kept, so a view that holds it stays live', () {
      write('fr.json', '{"play": "Jouer"}');
      final loaded = bench()..loadStrings();
      final held = loaded.strings;
      var heard = 0;
      held.addListener(() => heard++);

      loaded.loadStrings();
      loaded.strings.use('fr');

      expect(identical(loaded.strings, held), isTrue);
      expect(heard, greaterThan(0));
    });

    test('opens in a language the project has when it has no en', () {
      write('fr.json', '{"play": "Jouer"}');
      write('de.json', '{"play": "Spielen"}');
      final loaded = bench()..loadStrings();
      expect(loaded.strings.locale, 'de');
      expect(loaded.strings.text('play'), 'Spielen');
    });

    test('a folder that cannot be listed is reported, not thrown', () {
      write('fr.json', '{"play": "Jouer"}');
      final folder = Directory(stringsPath(root));
      Process.runSync('chmod', ['000', folder.path]);
      addTearDown(() => Process.runSync('chmod', ['755', folder.path]));
      // A user that may list it anyway, such as root, proves nothing here.
      var listable = true;
      try {
        folder.listSync();
      } on FileSystemException {
        listable = false;
      }
      if (listable) return;

      final loaded = bench()..loadStrings();

      expect(loaded.strings.locales, isEmpty);
      expect(loaded.stringProblems.single, startsWith('strings/'));
    });

    test('a project with no strings folder has no languages', () {
      final loaded = bench()..loadStrings();
      expect(loaded.strings.locales, isEmpty);
      expect(loaded.stringProblems, isEmpty);
    });
  });
}

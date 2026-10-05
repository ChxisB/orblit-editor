import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/command_palette.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';

PaletteEntry command(String label, {VoidCallback? onRun}) => PaletteEntry(
  group: PaletteGroup.commands,
  label: label,
  icon: Icons.bolt,
  onRun: onRun ?? () {},
);

PaletteEntry object(String label) => PaletteEntry(
  group: PaletteGroup.objects,
  label: label,
  icon: Icons.circle,
  onRun: () {},
);

List<String> labels(List<PaletteMatch> found) => [
  for (final match in found) match.entry.label,
];

void main() {
  group('searching the palette', () {
    final entries = [
      command('Save scene'),
      command('Add cube'),
      command('Add light'),
      command('Select all'),
      object('Cube'),
      object('Ground'),
    ];

    test('with nothing typed lists everything, a group at a time', () {
      final found = searchPalette(entries, '');

      expect(labels(found), [
        'Save scene',
        'Add cube',
        'Add light',
        'Select all',
        'Cube',
        'Ground',
      ]);
      expect(
        found.first.hitEnd,
        found.first.hitStart,
        reason: 'there is nothing to light up',
      );
    });

    test('keeps only labels holding every word, in any order', () {
      expect(labels(searchPalette(entries, 'cube add')), ['Add cube']);
      expect(labels(searchPalette(entries, 'zzz')), isEmpty);
    });

    test('ignores case and the space around the words', () {
      expect(labels(searchPalette(entries, '  SAVE   scene ')), ['Save scene']);
    });

    test('puts the earliest hit first and keeps the order of a tie', () {
      final found = searchPalette(entries, 'cube');

      // "Cube" the object hits at 0 but is in its own group, which follows
      // the commands. Within the commands, nothing else begins with it.
      expect(labels(found), ['Add cube', 'Cube']);

      final tied = searchPalette([
        command('Edit all'),
        command('Select all'),
        command('All'),
      ], 'all');
      expect(labels(tied), ['All', 'Edit all', 'Select all']);
    });

    test('says which part of the label matched', () {
      final match = searchPalette(entries, 'light').single;

      expect(
        match.entry.label.substring(match.hitStart, match.hitEnd),
        'light',
      );
    });

    test('shows fewer of a group before anything is typed', () {
      final many = [
        for (var i = 0; i < 20; i++) command('Command $i'),
        for (var i = 0; i < 20; i++) object('Object $i'),
      ];

      expect(searchPalette(many, ''), hasLength(12));
      expect(searchPalette(many, 'command'), hasLength(12));
      expect(searchPalette(many, 'object 1'), hasLength(11));
    });
  });

  group('the palette dialog', () {
    PaletteEntry? chosen;
    var closed = false;

    Future<void> open(
      WidgetTester tester,
      List<PaletteEntry> entries, {
      String initialQuery = '',
    }) async {
      chosen = null;
      closed = false;
      await tester.pumpWidget(
        MaterialApp(
          theme: orblitTheme(),
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () async {
                  chosen = await showCommandPalette(
                    context,
                    entries,
                    initialQuery: initialQuery,
                  );
                  closed = true;
                },
                child: const Text('open'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('open'));
      await tester.pumpAndSettle();
    }

    testWidgets('opens with its field ready and the first thing chosen', (
      tester,
    ) async {
      await open(tester, [command('Save scene'), object('Cube')]);

      expect(find.text('Save scene'), findsOneWidget);
      expect(find.text('Cube'), findsOneWidget);
      expect(find.text('2 results'), findsOneWidget);

      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();

      expect(chosen?.label, 'Save scene');
    });

    testWidgets('typing narrows the list', (tester) async {
      await open(tester, [command('Save scene'), object('Cube')]);

      await tester.enterText(find.byType(TextField), 'cub');
      await tester.pumpAndSettle();

      expect(find.text('Save scene'), findsNothing);
      expect(find.text('1 result'), findsOneWidget);
    });

    testWidgets('says so when nothing matches, and Enter does nothing', (
      tester,
    ) async {
      await open(tester, [command('Save scene')]);

      await tester.enterText(find.byType(TextField), 'zzz');
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();

      expect(find.text('Nothing matches.'), findsOneWidget);
      expect(closed, isFalse);
    });

    testWidgets('the arrows move the choice and wrap round', (tester) async {
      await open(tester, [command('One'), command('Two'), command('Three')]);

      await tester.sendKeyEvent(LogicalKeyboardKey.arrowDown);
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowDown);
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowDown);
      await tester.sendKeyEvent(LogicalKeyboardKey.arrowUp);
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();

      // Down three times is back at the top, and up from there is the end.
      expect(chosen?.label, 'Three');
    });

    testWidgets('Escape closes it without choosing', (tester) async {
      await open(tester, [command('Save scene')]);

      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();

      expect(closed, isTrue);
      expect(chosen, isNull);
      expect(find.byType(TextField), findsNothing);
    });

    testWidgets('a tap on a row chooses it', (tester) async {
      await open(tester, [command('Save scene'), command('Add cube')]);

      await tester.tap(find.text('Add cube'));
      await tester.pumpAndSettle();

      expect(chosen?.label, 'Add cube');
    });

    testWidgets('starts on the query it was given', (tester) async {
      await open(tester, [
        command('Save scene'),
        command('Add cube'),
      ], initialQuery: 'add');

      expect(find.text('Save scene'), findsNothing);
      expect(find.text('Add cube'), findsOneWidget);
    });
  });
}

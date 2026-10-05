import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/scene.dart';

import 'support/editor_shell.dart';

// The Hierarchy's filter and the plus beside it.

final _filter = find.descendant(
  of: find.byType(Outliner),
  matching: find.byType(TextField),
);

Future<void> filterFor(WidgetTester tester, String text) async {
  await tester.enterText(_filter, text);
  await tester.pumpAndSettle();
}

void main() {
  useShell();

  group('the hierarchy filter', () {
    testWidgets('keeps the names that hold the text', (tester) async {
      await open(tester);

      await filterFor(tester, 'cub');

      expect(row('Cube'), findsOneWidget);
      expect(row('Ground'), findsNothing);
    });

    testWidgets('ignores case and the spaces around the text', (tester) async {
      await open(tester);

      await filterFor(tester, '  GROUND ');

      expect(row('Ground'), findsOneWidget);
      expect(row('Cube'), findsNothing);
    });

    testWidgets('keeps the parent of a match, so it shows where it sits', (
      tester,
    ) async {
      await open(tester);

      await filterFor(tester, 'crate');

      expect(row('Crate'), findsOneWidget);
      expect(row('Props'), findsOneWidget);
      expect(row('Ground'), findsNothing);
    });

    testWidgets('shows a match inside a parent that was closed', (
      tester,
    ) async {
      await open(tester);
      await tester.tap(find.byIcon(Icons.expand_more).at(1));
      await tester.pumpAndSettle();
      expect(row('Crate'), findsNothing);

      await filterFor(tester, 'crate');

      expect(row('Crate'), findsOneWidget);
    });

    testWidgets('says so when nothing has that name', (tester) async {
      await open(tester);

      await filterFor(tester, 'zzz');

      expect(find.text('Nothing here has that name.'), findsOneWidget);
      expect(find.byType(Draggable<ObjectDrag>), findsNothing);
    });

    testWidgets('clearing it brings the whole tree back', (tester) async {
      await open(tester);
      await filterFor(tester, 'cub');

      await filterFor(tester, '');

      expect(row('Ground'), findsOneWidget);
      expect(row('Cube'), findsOneWidget);
    });
  });

  group('the plus beside the filter', () {
    testWidgets('opens the palette on the add commands', (tester) async {
      await open(tester);

      await tester.tap(find.byTooltip('Add an object'));
      await tester.pumpAndSettle();

      final field = tester.widget<TextField>(find.byType(TextField).last);
      expect(field.controller?.text, 'Add ');
      expect(find.textContaining('Add ', findRichText: true), findsWidgets);
      // Only what adds something: the rest of the palette is not offered.
      expect(find.textContaining('Go to', findRichText: true), findsNothing);
    });

    testWidgets('adds what is chosen', (tester) async {
      await open(tester);
      final before = tester
          .widgetList(find.byType(Draggable<ObjectDrag>))
          .length;

      await tester.tap(find.byTooltip('Add an object'));
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.enter);
      await tester.pumpAndSettle();

      expect(
        tester.widgetList(find.byType(Draggable<ObjectDrag>)).length,
        before + 1,
      );
    });
  });
}

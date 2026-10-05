import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/orblit_switch.dart';

import 'support/editor_shell.dart';

// What heads the Inspector and how its sections behave: the kind under the
// name, a switch for visibility, headings that fold, and a letter on each
// axis.

void main() {
  useShell();

  Finder inInspector(Finder finder) =>
      find.descendant(of: find.byType(Inspector), matching: finder);

  /// The line under the name, rather than a section that shares its word.
  Finder kind(String text) => inInspector(
    find.byWidgetPredicate(
      (w) => w is Text && w.data == text && w.style == OrblitText.caption,
    ),
  );

  Future<void> select(WidgetTester tester, String name) async {
    await tester.tap(row(name));
    await tester.pumpAndSettle();
  }

  bool visibleSwitch(WidgetTester tester) =>
      tester.widget<OrblitSwitch>(inInspector(find.byType(OrblitSwitch))).value;

  group('the header', () {
    testWidgets('says what kind of thing is selected', (tester) async {
      await open(tester);

      await select(tester, 'Crate');
      expect(kind('Mesh object'), findsOneWidget);

      await select(tester, 'Sun');
      expect(kind('Light'), findsOneWidget);

      await select(tester, 'Camera');
      expect(kind('Camera'), findsOneWidget);
    });

    testWidgets('has a switch that hides the object, and undo shows it', (
      tester,
    ) async {
      await open(tester);
      await select(tester, 'Crate');
      expect(visibleSwitch(tester), isTrue);

      await tester.tap(inInspector(find.byTooltip('Visible')));
      await tester.pumpAndSettle();
      expect(visibleSwitch(tester), isFalse);

      await undo(tester);
      expect(visibleSwitch(tester), isTrue);
    });

    testWidgets('has no switch for the scene itself', (tester) async {
      await open(tester);
      await tester.tap(sceneRow('main'));
      await tester.pumpAndSettle();

      expect(inInspector(find.byType(OrblitSwitch)), findsNothing);
    });
  });

  group('a section', () {
    testWidgets('folds when its heading is tapped, and opens again', (
      tester,
    ) async {
      await open(tester);
      await select(tester, 'Crate');
      expect(inInspector(find.text('Position')), findsOneWidget);

      await tester.tap(inInspector(find.text('Transform')));
      await tester.pumpAndSettle();
      expect(inInspector(find.text('Position')), findsNothing);

      await tester.tap(inInspector(find.text('Transform')));
      await tester.pumpAndSettle();
      expect(inInspector(find.text('Position')), findsOneWidget);
    });

    testWidgets('stays folded on the next object', (tester) async {
      await open(tester);
      await select(tester, 'Crate');
      await tester.tap(inInspector(find.text('Transform')));
      await tester.pumpAndSettle();

      await select(tester, 'Ground');

      expect(inInspector(find.text('Transform')), findsOneWidget);
      expect(inInspector(find.text('Position')), findsNothing);
    });
  });

  testWidgets('each axis of a vector wears its own letter', (tester) async {
    await open(tester);
    await select(tester, 'Crate');

    // Position, rotation and scale, one letter each per row.
    for (final axis in ['X', 'Y', 'Z']) {
      expect(inInspector(find.text(axis)), findsNWidgets(3));
    }
  });
}

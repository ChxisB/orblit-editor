import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';

void main() {
  group('a button that fills its parent', () {
    Future<void> pumpIn(WidgetTester tester, double width) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: orblitTheme(),
          home: Center(
            child: SizedBox(
              width: width,
              child: OrblitButton(
                label: 'Cut',
                icon: Icons.content_cut,
                expand: true,
                onPressed: () {},
              ),
            ),
          ),
        ),
      );
    }

    // The test font draws every letter as wide as it is tall, so "Cut" is
    // 36 wide. The padding and the border take 26 more, the icon 15 and the
    // gap after it 8.
    testWidgets('gives up its icon before its word', (tester) async {
      await pumpIn(tester, 200);
      expect(find.byIcon(Icons.content_cut), findsOneWidget);
      expect(find.text('Cut'), findsOneWidget);

      await pumpIn(tester, 70);
      expect(find.byIcon(Icons.content_cut), findsNothing);
      expect(find.text('Cut'), findsOneWidget);

      // Narrower than its word: the icon alone, which says the word when
      // pointed at. A row of these at the smallest window ran over its
      // edge before.
      await pumpIn(tester, 48);
      expect(find.byIcon(Icons.content_cut), findsOneWidget);
      expect(find.text('Cut'), findsNothing);
      expect(find.byTooltip('Cut'), findsOneWidget);
    });
  });

  group('a row of buttons', () {
    Future<void> pumpIn(WidgetTester tester, double width) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: orblitTheme(),
          home: Center(
            child: SizedBox(
              width: width,
              child: OrblitButtonRow(
                buttons: [
                  for (final (label, icon) in [
                    ('Draw shape', Icons.polyline_outlined),
                    ('Cut', Icons.content_cut),
                  ])
                    OrblitButton(
                      label: label,
                      icon: icon,
                      expand: true,
                      onPressed: () {},
                    ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    double top(WidgetTester tester, String label) =>
        tester.getTopLeft(find.widgetWithText(OrblitButton, label)).dy;

    // "Draw shape" is 120 wide in the test font, and 143 with its icon.
    // Each button's padding and border take 26 of its share of the row.
    testWidgets('gives up its icons together, then stacks', (tester) async {
      await pumpIn(tester, 400);
      expect(top(tester, 'Cut'), top(tester, 'Draw shape'));
      expect(find.byType(Icon), findsNWidgets(2));

      // Room for every word but not for the longer one's icon. Cut has room
      // for its own icon, but a row that mixes the two reads as broken.
      await pumpIn(tester, 316);
      expect(top(tester, 'Cut'), top(tester, 'Draw shape'));
      expect(find.byType(Icon), findsNothing);
      expect(find.text('Cut'), findsOneWidget);

      // No room for a word: one above the other, each with the whole width,
      // rather than icons nobody new can read.
      await pumpIn(tester, 200);
      expect(top(tester, 'Cut'), greaterThan(top(tester, 'Draw shape')));
      expect(find.byType(Icon), findsNWidgets(2));
      expect(find.text('Draw shape'), findsOneWidget);
    });
  });
}

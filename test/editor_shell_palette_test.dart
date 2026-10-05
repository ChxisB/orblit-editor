import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';

import 'support/editor_shell.dart';

// The command palette as the editor wires it: the ways in, what it finds in
// the scene, and that what is chosen runs once the palette has gone.

void main() {
  useShell();

  final field = find.byWidgetPredicate(
    (widget) =>
        widget is TextField &&
        widget.decoration?.hintText == 'Search commands and objects',
  );

  final objectRows = find.byType(Draggable<ObjectDrag>);

  Future<void> openWithKey(WidgetTester tester) async {
    await tester.sendKeyDownEvent(commandKey);
    await tester.sendKeyEvent(LogicalKeyboardKey.keyK);
    await tester.sendKeyUpEvent(commandKey);
    await tester.pumpAndSettle();
  }

  Future<void> typeAndRun(WidgetTester tester, String text) async {
    await tester.enterText(field, text);
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pumpAndSettle();
  }

  testWidgets('the command key and K open it, and Escape closes it', (
    tester,
  ) async {
    await open(tester);
    expect(field, findsNothing);

    await openWithKey(tester);
    expect(field, findsOneWidget);

    await tester.sendKeyEvent(LogicalKeyboardKey.escape);
    await tester.pumpAndSettle();
    expect(field, findsNothing);
  });

  testWidgets('the search button in the top bar opens it', (tester) async {
    await open(tester);

    await tester.tap(find.text('Search commands and objects'));
    await tester.pumpAndSettle();

    expect(field, findsOneWidget);
  });

  testWidgets('a workspace can be reached by name', (tester) async {
    await open(tester);
    expect(inMode(tester, 'terrain'), isFalse);

    await openWithKey(tester);
    await typeAndRun(tester, 'go to terrain');

    expect(field, findsNothing);
    expect(inMode(tester, 'terrain'), isTrue);
  });

  testWidgets('an object in the scene can be found and selected', (
    tester,
  ) async {
    await open(tester);
    final kind = find.descendant(
      of: find.byType(Inspector),
      matching: find.byWidgetPredicate(
        (w) =>
            w is Text &&
            w.data == 'Mesh object' &&
            w.style == OrblitText.caption,
      ),
    );
    expect(kind, findsNothing);

    await openWithKey(tester);
    await typeAndRun(tester, 'crate');

    expect(kind, findsOneWidget);
  });

  testWidgets('a command that changes the scene can be undone', (tester) async {
    await open(tester);
    final before = objectRows.evaluate().length;

    await openWithKey(tester);
    await typeAndRun(tester, 'add cube');
    expect(objectRows.evaluate().length, before + 1);

    await undo(tester);
    expect(objectRows.evaluate().length, before);
  });
}

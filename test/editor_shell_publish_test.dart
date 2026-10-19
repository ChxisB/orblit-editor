import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/store/publish_dialog.dart';

import 'support/editor_shell.dart';

// The ways into the publish dialog: the Scene menu and the command palette.

void main() {
  useShell();

  final field = find.byWidgetPredicate(
    (widget) =>
        widget is TextField &&
        widget.decoration?.hintText == 'Search commands and objects',
  );

  testWidgets('the Scene menu opens it', (tester) async {
    await open(tester);
    expect(find.byType(PublishDialog), findsNothing);

    await menu(tester, 'Scene', 'Publish kit…');

    expect(find.byType(PublishDialog), findsOneWidget);
    expect(find.text('Folder in the project'.toUpperCase()), findsOneWidget);
  });

  testWidgets('the palette finds it', (tester) async {
    await open(tester);
    await tester.sendKeyDownEvent(commandKey);
    await tester.sendKeyEvent(LogicalKeyboardKey.keyK);
    await tester.sendKeyUpEvent(commandKey);
    await tester.pumpAndSettle();

    await tester.enterText(field, 'publish kit');
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pumpAndSettle();

    expect(find.byType(PublishDialog), findsOneWidget);
  });
}

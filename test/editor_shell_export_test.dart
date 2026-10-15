import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/export_dialog.dart';

import 'support/editor_shell.dart';

// The ways into the export dialog: the Scene menu and the command palette.

void main() {
  useShell();

  final field = find.byWidgetPredicate(
    (widget) =>
        widget is TextField &&
        widget.decoration?.hintText == 'Search commands and objects',
  );

  testWidgets('the Scene menu opens it', (tester) async {
    await open(tester);
    expect(find.byType(ExportDialog), findsNothing);

    await menu(tester, 'Scene', 'Export…');

    expect(find.byType(ExportDialog), findsOneWidget);
    expect(find.textContaining('Nothing exported yet'), findsOneWidget);
  });

  testWidgets('the palette finds it', (tester) async {
    await open(tester);
    await tester.sendKeyDownEvent(commandKey);
    await tester.sendKeyEvent(LogicalKeyboardKey.keyK);
    await tester.sendKeyUpEvent(commandKey);
    await tester.pumpAndSettle();

    await tester.enterText(field, 'export');
    await tester.pumpAndSettle();
    await tester.sendKeyEvent(LogicalKeyboardKey.enter);
    await tester.pumpAndSettle();

    expect(find.byType(ExportDialog), findsOneWidget);
  });

  testWidgets('closes without disturbing the scene', (tester) async {
    await open(tester);
    final unsaved = unsavedMarker().evaluate().length;
    await menu(tester, 'Scene', 'Export…');

    await tester.tap(find.widgetWithText(TextButton, 'Close'));
    await tester.pumpAndSettle();

    expect(find.byType(ExportDialog), findsNothing);
    expect(unsavedMarker().evaluate().length, unsaved);
  });
}

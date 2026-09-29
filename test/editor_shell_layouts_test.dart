import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/viewport.dart';

import 'support/editor_shell.dart';

void main() {
  useShell();

  Future<void> namedLayout(
    WidgetTester tester,
    String action,
    String name,
  ) async {
    await tester.tap(find.text('View'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(SubmenuButton, action));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(MenuItemButton, name));
    await tester.pumpAndSettle();
  }

  testWidgets('saved arrangements reload per workspace after reopening', (
    tester,
  ) async {
    await open(tester);
    await enterMode(tester, 'animation');
    await viewMenu(tester, 'Four views');
    await viewMenu(tester, 'Save layout…');
    await answerPrompt(tester, 'Four cameras', 'Save');
    await viewMenu(tester, 'Reset panels');
    await tester.pumpWidget(const SizedBox());
    await open(tester);
    await enterMode(tester, 'animation');
    await namedLayout(tester, 'Load layout', 'Four cameras');
    expect(find.byType(SceneViewport), findsNWidgets(4));
    await enterMode(tester, 'scene');
    expect(find.byType(SceneViewport), findsOneWidget);
    await enterMode(tester, 'animation');
    await namedLayout(tester, 'Delete layout', 'Four cameras');
    // Deleting a saved name leaves the current arrangement alone.
    expect(find.byType(SceneViewport), findsNWidgets(4));
  });

  testWidgets('focus keeps each workspace arrangement intact', (tester) async {
    await open(tester);
    await viewMenu(tester, 'Focus view');
    expect(dockTab('inspector'), findsNothing);
    expect(find.byType(SceneViewport), findsOneWidget);
    await enterMode(tester, 'animation');
    expect(dockTab('inspector'), findsOneWidget);
    await enterMode(tester, 'scene');
    expect(dockTab('inspector'), findsNothing);
    await viewMenu(tester, 'Show panels');
    expect(dockTab('inspector'), findsOneWidget);
  });

  testWidgets('performance numbers are behind Stats', (tester) async {
    await open(tester);
    expect(find.textContaining('fps'), findsNothing);
    expect(find.textContaining('drawn'), findsNothing);
    await viewMenu(tester, 'Stats');
    expect(find.textContaining('fps'), findsOneWidget);
    expect(find.textContaining('drawn'), findsOneWidget);
  });
}

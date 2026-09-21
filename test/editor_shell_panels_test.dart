import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/asset_browser.dart';
import 'package:orblit_editor/src/editor/console_panel.dart';
import 'package:orblit_editor/src/editor/dock.dart';
import 'package:orblit_editor/src/editor/game_view.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/modelling_panel.dart';
import 'package:orblit_editor/src/editor/outliner.dart';
import 'package:orblit_editor/src/editor/uv_panel.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Arranging the panels: docking, splitting and what is remembered.

void main() {
  useShell();

  group('arranging the panels', () {
    testWidgets('the editor opens with the panels it always had', (
      tester,
    ) async {
      await open(tester);

      expect(find.byType(Outliner), findsOneWidget);
      expect(find.byType(SceneViewport), findsOneWidget);
      expect(find.byType(Inspector), findsOneWidget);
      expect(find.byType(AssetBrowser), findsOneWidget);
    });

    testWidgets('four views is four scene panels at once', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');

      expect(find.byType(SceneViewport), findsNWidgets(4));
      // Each one is a camera of its own, so moving one does not move the rest.
      expect(find.text('SCENE 2'), findsOneWidget);
      expect(find.text('SCENE 4'), findsOneWidget);
    });

    testWidgets('and back to one', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');
      await viewMenu(tester, 'One view');

      expect(find.byType(SceneViewport), findsOneWidget);
    });

    testWidgets('the game view is a tab beside the scene', (tester) async {
      await open(tester);
      expect(find.text('GAME'), findsOneWidget);
      expect(find.byType(GameView), findsNothing);

      await tester.tap(find.text('GAME'));
      await tester.pumpAndSettle();

      expect(find.byType(GameView), findsOneWidget);
      // One at a time: the scene view is behind it, not beside it.
      expect(find.byType(SceneViewport), findsNothing);
    });

    testWidgets('a panel can be closed and opened again', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Console');
      expect(find.byType(ConsolePanel), findsOneWidget);

      // Closing the console leaves the project browser where it was.
      await viewMenu(tester, 'Project');
      expect(find.byType(AssetBrowser), findsOneWidget);
    });

    testWidgets('the arrangement is remembered', (tester) async {
      await open(tester);
      await viewMenu(tester, 'Four views');
      expect(
        File(p.join(root.path, '.orblit', 'layout.json')).existsSync(),
        isTrue,
      );

      // Opened again, the panels are where they were left.
      await tester.pumpWidget(const SizedBox.shrink());
      await tester.pumpAndSettle();
      await open(tester);

      expect(find.byType(SceneViewport), findsNWidgets(4));
    });

    testWidgets('locking says so and stops the tabs being dragged', (
      tester,
    ) async {
      await open(tester);
      await viewMenu(tester, 'Lock the layout');

      expect(find.textContaining('View •'), findsOneWidget);
      // Nothing to drag: a locked layout draws its tabs without handles.
      expect(find.byType(Draggable<PanelDrag>), findsNothing);

      await viewMenu(tester, 'Unlock the layout');
      expect(find.byType(Draggable<PanelDrag>), findsWidgets);
    });

    testWidgets('a saved layout that cannot be read is not fatal', (
      tester,
    ) async {
      Directory(p.join(root.path, '.orblit')).createSync(recursive: true);
      File(p.join(root.path, '.orblit', 'layout.json'))
          .writeAsStringSync('not a layout at all');

      await open(tester);

      // The standard arrangement, rather than an editor that will not open.
      expect(find.byType(SceneViewport), findsOneWidget);
      expect(find.byType(Inspector), findsOneWidget);
    });

    testWidgets('a layout saved before panels were registered still opens', (
      tester,
    ) async {
      Directory(p.join(root.path, '.orblit')).createSync(recursive: true);
      File(
        p.join(root.path, '.orblit', 'layout.json'),
      ).writeAsStringSync(_savedBeforeRegistration);

      await open(tester);

      for (final tab in [
        'HIERARCHY',
        'SCENE',
        'GAME',
        'INSPECTOR',
        'MODELLING',
        'PROJECT',
        'CONSOLE',
        'UVS',
      ]) {
        expect(
          find.descendant(
            of: find.byType(Draggable<PanelDrag>),
            matching: find.text(tab),
          ),
          findsOneWidget,
          reason: tab,
        );
      }
      // And the tabs that were in front are in front again.
      expect(find.byType(Outliner), findsOneWidget);
      expect(find.byType(SceneViewport), findsOneWidget);
      expect(find.byType(ModellingPanel), findsOneWidget);
      expect(find.byType(UvPanel), findsOneWidget);
    });
  });
}

/// Every kind of panel, as the build before the panel registry wrote them:
/// the standard arrangement with the console, the UVs and the modelling tools
/// opened from the View menu. Produced by that build's own writer, and only
/// compacted here.
const _savedBeforeRegistration = '''
{"kind":"orblit.layout","formatVersion":1,"locked":false,"root":{
  "id":"root","split":"column","weights":[0.74,0.26],"children":[
    {"id":"middle","split":"row","weights":[0.19,0.58,0.23],"children":[
      {"id":"left","active":0,"panels":[{"id":"outliner","kind":"outliner"}]},
      {"id":"centre","active":0,"panels":[
        {"id":"scene","kind":"viewport"},{"id":"game","kind":"game"}]},
      {"id":"right","active":1,"panels":[
        {"id":"inspector","kind":"inspector"},
        {"id":"modelling","kind":"modelling"}]}]},
    {"id":"bottom","active":2,"panels":[
      {"id":"project","kind":"project"},
      {"id":"console","kind":"console"},
      {"id":"uvs","kind":"uvs"}]}]}}
''';

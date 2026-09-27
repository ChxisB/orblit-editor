import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/drawing.dart';
import 'package:orblit_editor/src/editor/editor_shell.dart';
import 'package:orblit_editor/src/editor/mesh_panel.dart';
import 'package:orblit_editor/src/editor/modelling_panel.dart';
import 'package:orblit_editor/src/launcher/project.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';

void main() {
  late Directory root;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_tools');
    Directory('${root.path}/scenes').createSync(recursive: true);
  });

  tearDown(() {
    if (root.existsSync()) root.deleteSync(recursive: true);
  });

  Future<void> open(WidgetTester tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(MaterialApp(
      theme: orblitTheme(),
      home: EditorShell(
        project: Project(
          name: 'Test',
          directory: root.path,
          lastOpened: DateTime(2026),
        ),
        onClose: () {},
      ),
    ));
    await tester.pumpAndSettle();
  }

  Future<void> openTools(WidgetTester tester) async {
    await tester.tap(find.byKey(const ValueKey('mode/modelling')));
    await tester.pumpAndSettle();
  }

  Future<void> addShape(WidgetTester tester, String kind) async {
    await tester.tap(find.text('Add'));
    await tester.pumpAndSettle();
    await tester.tap(find.widgetWithText(SubmenuButton, 'Shape'));
    await tester.pumpAndSettle();
    await tester.tap(find.descendant(
      of: find.byType(MenuItemButton),
      matching: find.text(kind),
    ));
    await tester.pumpAndSettle();
  }

  testWidgets('it has a workspace of its own, one tab away', (tester) async {
    await open(tester);
    // The Scene workspace keeps to placing things, so the tools are not
    // crowding it.
    expect(find.byKey(const ValueKey('dock-tab/modelling')), findsNothing);

    // A tool nobody can find is a tool nobody uses, so it is a tab at the
    // top rather than an item in a menu.
    await openTools(tester);
    expect(find.byType(ModellingPanel), findsOneWidget);
  });

  testWidgets('it says what it is waiting for with nothing selected',
      (tester) async {
    await open(tester);
    await openTools(tester);

    expect(find.textContaining('Select a shape'), findsOneWidget);
    // But the drawing tools are offered anyway, because drawing one is how
    // somebody gets a shape in the first place. Twice: the panel and the
    // chip over the viewport, which are the same tool from two places.
    expect(find.text('Draw shape'), findsWidgets);
  });

  testWidgets('cutting waits for something to cut', (tester) async {
    await open(tester);
    await openTools(tester);

    final cut = tester.widget<OrblitButton>(
      find.widgetWithText(OrblitButton, 'Cut'),
    );
    expect(cut.onPressed, isNull, reason: 'offered, and clearly not ready');

    await addShape(tester, 'Cube');
    await openTools(tester);
    expect(
      tester
          .widget<OrblitButton>(find.widgetWithText(OrblitButton, 'Cut'))
          .onPressed,
      isNotNull,
    );
  });

  testWidgets('the actions are grouped rather than a column of twenty-five',
      (tester) async {
    await open(tester);
    await addShape(tester, 'Cube');
    await openTools(tester);

    // With nothing selected, only the ones that work on the whole shape.
    expect(find.text('WHOLE SHAPE'), findsOneWidget);
    expect(find.text('SELECTION'), findsNothing);

    // No G first: the workspace went into the cube's parts on the way in.
    final viewport = find.byType(SceneViewport);
    tester.widget<SceneViewport>(viewport).onSelectElements!(
      const [0],
      add: false,
    );
    await tester.pumpAndSettle();

    // And now the two that need something selected.
    expect(find.text('GEOMETRY'), findsWidgets);
    expect(find.text('SELECTION'), findsOneWidget);
  });

  testWidgets('the drawing tool is started from the panel', (tester) async {
    await open(tester);
    await openTools(tester);

    tester
        .widget<ModellingPanel>(find.byType(ModellingPanel))
        .onTool(ViewportTool.polyShape);
    await tester.pumpAndSettle();

    expect(find.textContaining('enter finishes'), findsWidgets);
    expect(find.textContaining('0 points'), findsWidgets);
  });

  testWidgets('the inspector keeps the shape and points at the rest',
      (tester) async {
    await open(tester);
    await addShape(tester, 'Stairs');

    // The shape's own numbers are properties of the object and stay put.
    expect(find.byType(MeshPanel), findsOneWidget);
    final panel = tester.widget<MeshPanel>(find.byType(MeshPanel));
    expect(panel.shape, isNotNull);

    // And pressing the pointer goes to the workspace it points at.
    expect(find.byType(ModellingPanel), findsNothing);
    panel.onOpenTools();
    await tester.pumpAndSettle();
    expect(find.byType(ModellingPanel), findsOneWidget);
  });
}

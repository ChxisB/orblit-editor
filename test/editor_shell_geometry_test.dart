import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/modelling_panel.dart';
import 'package:orblit_editor/src/editor/surface.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Building geometry inside the shell -- a shape, its parameters, and the
// material painted onto it.

void main() {
  useShell();

  group('building geometry', () {
    /// Adds a shape through the Add menu.
    Future<void> addShape(WidgetTester tester, String kind) async {
      await tester.tap(find.text('Add'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(SubmenuButton, 'Shape'));
      await tester.pumpAndSettle();
      await tester.tap(
        find.descendant(
          of: find.byType(MenuItemButton),
          matching: find.text(kind),
        ),
      );
      await tester.pumpAndSettle();
    }

    /// The shape object in the open scene.
    SceneObject shapeIn(WidgetTester tester) {
      final shell = tester.widget<SceneViewport>(find.byType(SceneViewport));
      return shell.workspace.loaded!.scene!.objects.firstWhere(
        (o) => o.kind == ObjectKind.shape,
      );
    }

    /// in capitals, so the finder is too.
    Future<void> openTools(WidgetTester tester) async {
      await tester.tap(find.text('MODELLING').first);
      await tester.pumpAndSettle();
    }

    /// Its callbacks, driven directly rather than by hunting for a button
    /// below the fold of a lazy list.
    ModellingPanel panelIn(WidgetTester tester) =>
        tester.widget<ModellingPanel>(find.byType(ModellingPanel));

    Future<void> scrollInspector(WidgetTester tester) async {
      await tester.drag(
        find.descendant(
          of: find.byType(Inspector),
          matching: find.byType(ListView),
        ),
        const Offset(0, -2000),
      );
      await tester.pumpAndSettle();
    }

    testWidgets('a shape can be added and is parametric', (tester) async {
      await open(tester);
      await addShape(tester, 'Stairs');

      final object = shapeIn(tester);
      expect(object.shape!.kind, ShapeKind.stairs);
      expect(
        object.isParametric,
        isTrue,
        reason: 'it is a set of numbers until somebody edits it',
      );
      expect(object.geometry, isNull);
    });

    testWidgets('its parameters are in the inspector', (tester) async {
      await open(tester);
      await addShape(tester, 'Cylinder');
      await scrollInspector(tester);

      // The controls that belong to a cylinder and to nothing else.
      expect(find.text('Sides'), findsOneWidget);
      expect(find.text('Height cuts'), findsOneWidget);
      // And not a torus's.
      expect(find.text('Tube'), findsNothing);
    });

    testWidgets('every kind offers its own parameters', (tester) async {
      await open(tester);

      for (final (kind, control) in const [
        ('Torus', 'Tube'),
        ('Door', 'Side width'),
        ('Sphere', 'Divisions'),
      ]) {
        await addShape(tester, kind);
        await scrollInspector(tester);
        expect(find.text(control), findsOneWidget, reason: kind);
      }
    });

    testWidgets('it is written out so the renderer can draw it', (
      tester,
    ) async {
      await open(tester);
      await addShape(tester, 'Cube');

      // An object is the built-in cube or a glTF file, and there is no third
      // way in — so geometry built here becomes a file.
      final built = Directory(p.join(root.path, '.orblit', 'geometry'));
      expect(built.existsSync(), isTrue);
      expect(
        built.listSync().where((f) => f.path.endsWith('.glb')),
        isNotEmpty,
      );
    });

    testWidgets('the geometry mode is offered for a shape', (tester) async {
      await open(tester);
      await addShape(tester, 'Cube');
      // In the modelling panel now, not four scrolls down the inspector.
      await openTools(tester);

      expect(find.text('GEOMETRY'), findsOneWidget);
      expect(find.text('Editing'), findsOneWidget);
    });

    testWidgets('the inspector says where the tools went', (tester) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await scrollInspector(tester);

      // Somebody who used to find extrude here will look here for it.
      expect(find.textContaining('modelling panel'), findsOneWidget);
      expect(find.text('Modelling tools'), findsOneWidget);
      // And the shape's own numbers are still where they belong.
      expect(find.text('CUBE'), findsOneWidget);
    });

    testWidgets('G goes into the geometry and round the modes', (tester) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await openTools(tester);

      await tester.sendKeyEvent(LogicalKeyboardKey.keyG);
      await tester.pumpAndSettle();
      expect(find.text('Faces'), findsWidgets);

      await tester.sendKeyEvent(LogicalKeyboardKey.keyG);
      await tester.pumpAndSettle();
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();

      // Back out to the object, which is what escape is for.
      expect(find.textContaining('Click'), findsNothing);
    });

    testWidgets('a material slot is added and painted onto a face', (
      tester,
    ) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await scrollInspector(tester);

      await tester.sendKeyEvent(LogicalKeyboardKey.keyG);
      await tester.pumpAndSettle();
      await openTools(tester);

      panelIn(tester).onSurfaces(const [Surface(name: 'Stone')], live: false);
      await tester.pumpAndSettle();
      expect(shapeIn(tester).surfaces, hasLength(1));

      // A second, so painting with one is a choice rather than the only
      // thing that could have happened.
      panelIn(tester).onSurfaces(const [
        Surface(name: 'Stone'),
        Surface(name: 'Brass', metallic: 1),
      ], live: false);
      await tester.pumpAndSettle();

      // Picked through the model rather than the viewport: what is under
      // test is painting, and where a click lands is somebody else's test.
      final viewport = tester.widget<SceneViewport>(find.byType(SceneViewport));
      viewport.onPickElement!(0, add: false);
      await tester.pumpAndSettle();

      panelIn(tester).onPaint(1);
      await tester.pumpAndSettle();

      final mesh = shapeIn(tester).currentMesh!;
      expect(mesh.faces[0].material, 1);
      expect(mesh.faces[1].material, 0, reason: 'nothing else was painted');
    });

    testWidgets('painting is one undoable step', (tester) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await scrollInspector(tester);
      await tester.sendKeyEvent(LogicalKeyboardKey.keyG);
      await tester.pumpAndSettle();
      await openTools(tester);

      panelIn(tester).onSurfaces(const [
        Surface(name: 'Stone'),
        Surface(name: 'Brass'),
      ], live: false);
      await tester.pumpAndSettle();

      final viewport = tester.widget<SceneViewport>(find.byType(SceneViewport));
      viewport.onPickElement!(2, add: false);
      await tester.pumpAndSettle();
      panelIn(tester).onPaint(1);
      await tester.pumpAndSettle();
      expect(shapeIn(tester).currentMesh!.faces[2].material, 1);

      await undo(tester);

      expect(shapeIn(tester).currentMesh!.faces[2].material, 0);
      expect(
        shapeIn(tester).surfaces,
        hasLength(2),
        reason: 'the slots are a separate step and are still there',
      );
    });

    testWidgets('the object actions are offered without a selection', (
      tester,
    ) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await openTools(tester);

      expect(find.text('Conform normals'), findsOneWidget);
      expect(find.text('Flip all normals'), findsOneWidget);
      // And nothing that needs a face selected.
      expect(find.text('Extrude'), findsNothing);
    });

    testWidgets('flipping the normals is one undoable step', (tester) async {
      await open(tester);
      await addShape(tester, 'Cube');
      await openTools(tester);

      await tester.tap(find.text('Flip all normals'));
      await tester.pumpAndSettle();

      // It stopped being a shape the moment it was edited.
      final object = shapeIn(tester);
      expect(object.geometry, isNotNull);
      expect(object.isParametric, isFalse);

      await press(tester, LogicalKeyboardKey.keyZ);
      expect(shapeIn(tester).geometry, isNull);
    });

    testWidgets('changing a parameter changes the geometry', (tester) async {
      await open(tester);
      await addShape(tester, 'Stairs');
      await scrollInspector(tester);

      final was = shapeIn(tester).shape!.steps;
      final slider = find.descendant(
        of: find.widgetWithText(FieldRow, 'Steps'),
        matching: find.byType(Slider),
      );
      // Scrolled to rather than assumed: the inspector's lazy list builds
      // what is near the viewport, and a widget it has built can still be
      // above the top of it.
      await tester.ensureVisible(slider);
      await tester.pumpAndSettle();
      await tester.drag(slider, const Offset(60, 0));
      await tester.pumpAndSettle();

      expect(shapeIn(tester).shape!.steps, isNot(was));
      // Still a shape: changing a number is not editing the geometry.
      expect(shapeIn(tester).isParametric, isTrue);
    });

    testWidgets('a shape is saved with the scene and comes back', (
      tester,
    ) async {
      await open(tester);
      await addShape(tester, 'Arch');
      await save(tester);

      final written = File(p.join(root.path, 'scenes', 'main.oscene'))
          .readAsStringSync();
      expect(written, contains('"shape"'));

      final back = SceneDocument.decode(written).scene;
      final object = back.objects.firstWhere((o) => o.kind == ObjectKind.shape);
      expect(object.shape!.kind, ShapeKind.arch);
      expect(object.currentMesh, isNotNull);
    });
  });
}

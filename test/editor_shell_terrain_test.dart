import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/terrain_bench.dart';
import 'package:orblit_editor/src/editor/terrain_brush_panel.dart';
import 'package:orblit_editor/src/editor/terrain_mode.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';

// Terrain in the editor: made from the Add menu, shaped in its own mode,
// and saved with everything else.

const file = 'terrain/terrain/terrain.oterrain';

Finder modeButton(IconData icon) => find.byWidgetPredicate(
  (widget) => widget is OrblitButton && widget.icon == icon,
);

Future<void> enterTerrainMode(WidgetTester tester) async {
  await tester.tap(modeButton(Icons.landscape_outlined));
  await tester.pumpAndSettle();
}

/// The terrain as it is on disk, read by a bench of its own.
OpenTerrain? onDisk() => TerrainBench(
  history: History(Workspace(root.path)),
  projectRoot: root.path,
).terrainFor(file);

void main() {
  useShell();

  testWidgets('Add › Terrain writes the ground and shows what it is made of', (
    tester,
  ) async {
    await open(tester);
    await add(tester, 'Terrain');

    final written = onDisk();
    expect(written, isNotNull, reason: 'the file is written as it is made');
    expect(written!.terrain.regions, hasLength(4));
    expect(
      [for (final set in written.terrain.sets) set.name],
      ['Rock', 'Grass'],
    );

    expect(find.textContaining('Add Terrain'), findsOneWidget);
    for (final part in const ['REGIONS', 'SETS', 'AUTOMATIC COVER']) {
      await reach(tester, find.text(part));
      expect(find.text(part), findsOneWidget);
    }

    // The scene names it, so it comes back with the scene.
    await save(tester);
    final scene = File(p.join(root.path, 'scenes', 'main.oscene'));
    expect(scene.readAsStringSync(), contains(file));
  });

  testWidgets('a set added in the inspector is one step to undo', (
    tester,
  ) async {
    await open(tester);
    await add(tester, 'Terrain');

    await tapInInspector(tester, 'Add set');
    expect(find.textContaining('3. Set 3'), findsOneWidget);

    await undo(tester);
    expect(find.textContaining('3. Set 3'), findsNothing);
  });

  testWidgets('the terrain mode brings the brush, and the shelf picks the '
      'tool', (tester) async {
    await open(tester);
    await add(tester, 'Terrain');
    expect(find.byType(TerrainToolShelf), findsNothing);

    await enterTerrainMode(tester);
    expect(find.byType(TerrainToolShelf), findsOneWidget);
    expect(find.byType(TerrainBrushPanel), findsOneWidget);
    expect(
      find.descendant(
        of: find.byType(TerrainBrushPanel),
        matching: find.text('terrain'),
      ),
      findsOneWidget,
      reason: 'the brush says which terrain it is on',
    );

    await tester.tap(find.byIcon(Icons.arrow_downward));
    await tester.pumpAndSettle();
    expect(
      find.descendant(
        of: find.byType(TerrainBrushPanel),
        matching: find.text('LOWER'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('a drag in the view shapes the ground as one step, and saving '
      'writes it', (tester) async {
    await open(tester);
    await add(tester, 'Terrain');
    await save(tester);
    await enterTerrainMode(tester);

    final view = tester.getCenter(find.byType(SceneViewport));
    await tester.dragFrom(view, const Offset(60, 0));
    await tester.pumpAndSettle();

    expect(find.textContaining('Raise terrain'), findsOneWidget);
    expect(
      find.text('Scene •'),
      findsOneWidget,
      reason: 'the ground is unsaved',
    );

    await save(tester);
    final written = onDisk()!;
    final raised = [
      for (final region in written.terrain.regions)
        ...region.heights.where((height) => height > 0),
    ];
    expect(raised, isNotEmpty);

    await undo(tester);
    expect(find.textContaining('Raise terrain'), findsNothing);
  });
}

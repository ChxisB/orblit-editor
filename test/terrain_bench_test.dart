import 'dart:io';
import 'dart:ui' show Offset, Size;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/terrain_bench.dart';
import 'package:orblit_editor/src/editor/terrain_edits.dart';
import 'package:orblit_editor/src/editor/terrain_mode.dart';
import 'package:orblit_editor/src/editor/viewport.dart' show OrbitCamera;
import 'package:orblit_editor/src/editor/viewport_input.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:path/path.dart' as p;

// The terrain mode's side of the editor: terrains written and read back,
// strokes and settings changed through the undo stack, and the brush taking
// the pointer only where there is ground under it.

const file = 'terrain/hills/hills.oterrain';

late Directory root;

({TerrainBench bench, History history, OpenTerrain open, List<String> said})
rig() {
  final history = History(Workspace(root.path));
  final said = <String>[];
  final bench = TerrainBench(
    history: history,
    projectRoot: root.path,
    onProblem: said.add,
  );
  final terrain = Terrain(
    regionSize: 32,
    sets: [
      TerrainSet(name: 'Rock'),
      TerrainSet(name: 'Grass'),
    ],
  );
  for (final z in const [-1, 0]) {
    for (final x in const [-1, 0]) {
      terrain.addRegion(RegionKey(x, z));
    }
  }
  expect(bench.create(file, terrain), isNull);
  return (bench: bench, history: history, open: bench[file]!, said: said);
}

/// An object that puts the terrain in [file] in the scene.
SceneObject ground(
  String id, {
  String file = file,
  bool castShadows = true,
  bool visible = true,
}) => SceneObject(
  id: id,
  name: id,
  kind: ObjectKind.group,
  visible: visible,
  components: {
    doc.SceneComponents.terrain: doc.TerrainComponent(
      file: file,
      castShadows: castShadows,
    ),
  },
);

/// Every height in [terrain], so two can be compared whole.
List<double> heights(Terrain terrain) => [
  for (final region in terrain.regions) ...region.heights,
];

/// A drag of the brush from x = 0 to x = 8 along z = 0, as one gesture.
void stroke(TerrainBench bench, History history, OpenTerrain open) {
  final stroke = bench.strokeOn(open, seed: 1);
  final gesture = Object();
  for (final x in const [0.0, 4.0, 8.0]) {
    history.run(
      TerrainStrokeStep(
        bench: bench,
        file: open.file,
        stroke: stroke,
        x: x,
        z: 0,
        gesture: gesture,
        label: 'Raise hills',
      ),
    );
  }
  history.seal();
}

void main() {
  setUp(() => root = Directory.systemTemp.createTempSync('terrain_bench'));
  tearDown(() => root.deleteSync(recursive: true));

  group('files', () {
    test('a terrain made here is read back as it was written', () {
      final (:bench, :history, :open, said: _) = rig();
      bench.brush = const Brush(size: 8, strength: 1);
      stroke(bench, history, open);
      expect(bench.save(open), isNull);

      final again = TerrainBench(
        history: History(Workspace(root.path)),
        projectRoot: root.path,
      ).terrainFor(file)!;
      expect(again.terrain.regions.length, 4);
      expect(
        [for (final set in again.terrain.sets) set.name],
        ['Rock', 'Grass'],
      );
      expect(heights(again.terrain), heights(open.terrain));
      expect(again.terrain.heightAt(4, 0), greaterThan(0));
    });

    test('a file that cannot be read is said once and not tried again', () {
      final (:bench, history: _, open: _, :said) = rig();
      expect(bench.terrainFor('terrain/none/none.oterrain'), isNull);
      expect(bench.terrainFor('terrain/none/none.oterrain'), isNull);
      expect(said, hasLength(1));
      expect(said.single, contains('none.oterrain'));
    });

    test('saving writes what changed, and a stroke undone after a save is '
        'a change again', () {
      final (:bench, :history, :open, said: _) = rig();
      expect(bench.isUnsaved(open), isFalse);

      stroke(bench, history, open);
      expect(bench.isUnsaved(open), isTrue);
      expect(bench.anyUnsaved, isTrue);

      expect(bench.saveAll(), isEmpty);
      expect(bench.isUnsaved(open), isFalse);

      history.undo();
      expect(bench.isUnsaved(open), isTrue);
    });
  });

  group('strokes', () {
    test('a drag is one step however many moves it takes, and undoing it '
        'puts the ground back', () {
      final (:bench, :history, :open, said: _) = rig();
      bench.brush = const Brush(size: 8, strength: 1);
      final flat = heights(open.terrain);

      stroke(bench, history, open);
      final raised = heights(open.terrain);
      expect(open.terrain.heightAt(0, 0), greaterThan(0));
      expect(open.terrain.heightAt(8, 0), greaterThan(0));
      expect(history.undoLabel, 'Raise hills');

      history.undo();
      expect(heights(open.terrain), flat);
      expect(history.canUndo, isFalse, reason: 'one drag, one step');

      history.redo();
      expect(heights(open.terrain), raised);
    });

    test('a step keeps only the tiles the stroke touched', () {
      final (:bench, :history, open: _, said: _) = rig();
      // One region of 128 is sixteen tiles of 32. A brush 4 m across in the
      // middle of one of them touches that one and no other.
      const big = 'terrain/big/big.oterrain';
      expect(
        bench.create(
          big,
          Terrain(regionSize: 128)..addRegion(const RegionKey(0, 0)),
        ),
        isNull,
      );
      bench.brush = const Brush(size: 4, strength: 1);
      final step = TerrainStrokeStep(
        bench: bench,
        file: big,
        stroke: bench.strokeOn(bench[big]!, seed: 1),
        x: 48,
        z: 48,
        gesture: Object(),
        label: 'Raise big',
      );
      history.run(step);
      final patch = step.patch!;
      expect(patch.isEmpty, isFalse);
      // A tile's heights before and after, and nothing else.
      expect(patch.byteCount, 32 * 32 * 4 * 2);
    });

    test('two drags are two steps', () {
      final (:bench, :history, :open, said: _) = rig();
      stroke(bench, history, open);
      stroke(bench, history, open);
      history.undo();
      expect(history.canUndo, isTrue);
      history.undo();
      expect(history.canUndo, isFalse);
    });
  });

  group('settings and regions', () {
    test('a slider dragged is one settings step', () {
      final (:bench, :history, :open, said: _) = rig();
      final was = open.terrain.blendSharpness;
      for (final value in const [0.6, 0.4, 0.3]) {
        bench.edit(
          open,
          'Set hills blend',
          (settings) => (
            sets: settings.sets,
            autoCover: settings.autoCover,
            blendSharpness: value,
          ),
          gesture: 'blend',
        );
      }
      history.seal();
      expect(open.terrain.blendSharpness, 0.3);

      history.undo();
      expect(open.terrain.blendSharpness, was);
      expect(history.canUndo, isFalse);
    });

    test('changing the sets asks the renderer for pictures again', () {
      final (:bench, history: _, :open, said: _) = rig();
      final before = bench.picturesRevision;
      bench.edit(
        open,
        'Add a set to hills',
        (settings) => (
          sets: [
            ...settings.sets,
            TerrainSet(name: 'Sand'),
          ],
          autoCover: settings.autoCover,
          blendSharpness: settings.blendSharpness,
        ),
      );
      expect(open.terrain.sets.last.name, 'Sand');
      expect(bench.picturesRevision, greaterThan(before));
    });

    test('taking a region away and undoing it puts back the same ground, '
        'and its file follows', () {
      final (:bench, :history, :open, said: _) = rig();
      bench.brush = const Brush(size: 8, strength: 1);
      stroke(bench, history, open);
      const key = RegionKey(0, 0);
      final region = open.terrain.regionAt(key)!;
      final shape = [...region.heights];
      final path = p.join(p.dirname(open.path), key.fileName);
      expect(bench.save(open), isNull);
      expect(File(path).existsSync(), isTrue);

      history
        ..run(
          TerrainRegionEdit(
            bench: bench,
            file: open.file,
            region: region,
            adding: false,
            label: 'Take ground from hills',
          ),
        )
        ..seal();
      expect(open.terrain.regionAt(key), isNull);
      expect(bench.save(open), isNull);
      expect(File(path).existsSync(), isFalse);

      history.undo();
      expect(open.terrain.regionAt(key), same(region));
      expect(region.heights, shape);
      expect(bench.save(open), isNull);
      expect(File(path).existsSync(), isTrue);
    });
  });

  group('the brush in a view', () {
    // Looking down at the origin from above, the way a new view does.
    final projection = ViewportProjection(
      camera: OrbitCamera(),
      size: const Size(800, 600),
    );
    const centre = Offset(400, 300);

    ViewportGesture at(ViewportPhase phase, Offset where, {bool add = false}) =>
        ViewportGesture(phase, where, add: add, projection: projection);

    test('a drag over the ground is one stroke, named for the tool', () {
      final (:bench, :history, :open, said: _) = rig();
      final input = TerrainBrushInput(
        bench: bench,
        history: history,
        target: () => open,
      );

      expect(input.handle(at(ViewportPhase.dragStart, centre)), isTrue);
      expect(
        input.handle(
          at(ViewportPhase.dragUpdate, centre + const Offset(40, 0)),
        ),
        isTrue,
      );
      expect(
        input.handle(at(ViewportPhase.dragEnd, centre + const Offset(40, 0))),
        isTrue,
      );
      expect(history.undoLabel, 'Raise hills');
      expect(heights(open.terrain).any((height) => height > 0), isTrue);

      history.undo();
      expect(history.canUndo, isFalse);
    });

    test('with the modifier held the tool turns round', () {
      final (:bench, :history, :open, said: _) = rig();
      final input = TerrainBrushInput(
        bench: bench,
        history: history,
        target: () => open,
      );
      input
        ..handle(at(ViewportPhase.dragStart, centre, add: true))
        ..handle(at(ViewportPhase.dragEnd, centre, add: true));
      expect(heights(open.terrain).any((height) => height < 0), isTrue);
      expect(heights(open.terrain).any((height) => height > 0), isFalse);
    });

    test('the sky, or no terrain at all, is left to the view', () {
      final (:bench, :history, :open, said: _) = rig();
      final input = TerrainBrushInput(
        bench: bench,
        history: history,
        target: () => open,
      );
      expect(
        input.handle(at(ViewportPhase.tap, const Offset(400, 0))),
        isFalse,
      );
      expect(history.canUndo, isFalse);

      final none = TerrainBrushInput(
        bench: bench,
        history: history,
        target: () => null,
      );
      expect(none.handle(at(ViewportPhase.tap, centre)), isFalse);
      expect(history.canUndo, isFalse);
    });

    test('hovering over the ground shows where the brush is', () {
      final (:bench, :history, :open, said: _) = rig();
      final input = TerrainBrushInput(
        bench: bench,
        history: history,
        target: () => open,
      );
      expect(input.handle(at(ViewportPhase.hover, centre)), isTrue);
      expect(bench.hover.value, isNotNull);
      input.handle(at(ViewportPhase.leave, centre));
      expect(bench.hover.value, isNull);
    });
  });

  group('drawing', () {
    test('one ground per file however many objects name it, with the '
        'shadows the object asks for', () {
      final (:bench, history: _, open: _, :said) = rig();
      final terrains = bench.renderFor([
        ground('a', castShadows: false),
        ground('b'),
        SceneObject(id: 'box', name: 'Box', kind: ObjectKind.mesh),
      ]);
      expect(terrains, hasLength(1));
      expect(terrains.single.regions, hasLength(4));
      expect(terrains.single.castShadows, isFalse);
      expect(terrains.single.receiveShadows, isTrue);
      expect(said, isEmpty);
    });

    test('a file that cannot be read draws nothing and is said', () {
      final (:bench, history: _, open: _, :said) = rig();
      expect(
        bench.renderFor([ground('lost', file: 'terrain/none/none.oterrain')]),
        isEmpty,
      );
      expect(said, hasLength(1));
    });

    test('a stroke changes the revision the renderer uploads by', () {
      final (:bench, :history, :open, said: _) = rig();
      int revisions() => [
        for (final region in bench.renderFor([ground('a')]).single.regions)
          region.revision,
      ].fold(0, (sum, revision) => sum + revision);

      final before = revisions();
      stroke(bench, history, open);
      expect(revisions(), greaterThan(before));
    });

    test('a scene hands over the ground it shows, and the shared set\'s', () {
      final (:bench, history: _, open: _, said: _) = rig();
      const other = 'terrain/other/other.oterrain';
      expect(
        bench.create(
          other,
          Terrain(regionSize: 32)..addRegion(const RegionKey(0, 0)),
        ),
        isNull,
      );
      final camera = OrbitCamera().toRenderCamera();

      final scene = EditorScene([
        ground('here'),
        ground('hidden', visible: false, file: other),
      ]);
      expect(
        scene.toRenderScene(camera, terrainOf: bench.renderFor).terrain,
        hasLength(1),
        reason: 'a hidden object puts no ground in the view',
      );

      final shared = EditorScene([ground('there', file: other)]);
      expect(
        scene
            .toRenderScene(camera, shared: shared, terrainOf: bench.renderFor)
            .terrain,
        hasLength(2),
      );

      expect(
        scene.toRenderScene(camera).terrain,
        isEmpty,
        reason: 'without a bench there is nothing to draw',
      );
    });

    test('what is scattered over it is drawn as blocks, once per file', () {
      final (:bench, :history, :open, said: _) = rig();
      open.terrain.scatter.addAll(const [
        ScatterLayer(name: 'grass', density: 1),
        ScatterLayer(name: 'trees', seed: 2, density: 0.1, mesh: 'tree.glb'),
      ]);

      final drawn = bench.scatterFor([ground('a'), ground('b')]);
      // A population a region for the grass; the trees are models, which
      // the editor does not draw yet.
      expect(drawn, hasLength(4));
      expect(drawn.map((p) => p.key).toSet(), hasLength(4));
      expect(drawn.every((p) => p.transforms.isNotEmpty), isTrue);

      final again = bench.scatterFor([ground('a')]);
      for (var i = 0; i < drawn.length; i++) {
        expect(again[i], same(drawn[i]), reason: 'nothing moved');
      }

      stroke(bench, history, open);
      final after = bench.scatterFor([ground('a')]);
      expect(
        [for (final p in after) p.revision],
        isNot([for (final p in drawn) p.revision]),
        reason: 'the grass is placed again where the brush raised it',
      );

      final camera = OrbitCamera().toRenderCamera();
      final scene = EditorScene([ground('here')]);
      expect(
        scene.toRenderScene(camera, scatterOf: bench.scatterFor).populations,
        hasLength(4),
      );
      expect(scene.toRenderScene(camera).populations, isEmpty);
    });

    test('ground with nothing scattered draws nothing over it', () {
      final (:bench, history: _, open: _, said: _) = rig();
      expect(bench.scatterFor([ground('a')]), isEmpty);
    });
  });
}

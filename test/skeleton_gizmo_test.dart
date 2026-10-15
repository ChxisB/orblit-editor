import 'dart:convert';
import 'dart:io';
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart' hide Key;
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/model_skeletons.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/skeleton_gizmo.dart';
import 'package:orblit_editor/src/editor/skeleton_view.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/skinned_glb.dart';

// A model's bones drawn over the scene: read from the file the model came
// from, posed by the open clip, and ghosted either side of the playhead.

/// A three-bone spine standing up the Y axis, as a glTF document.
Map<String, Object?> spineDocument({List<Map<String, Object?>>? buffers}) => {
  'asset': {'version': '2.0'},
  'scene': 0,
  'scenes': [
    {
      'nodes': [0],
    },
  ],
  'nodes': [
    {
      'name': 'hips',
      'translation': [0, 0, 0],
      'children': [1],
    },
    {
      'name': 'spine',
      'translation': [0, 1, 0],
      'children': [2],
    },
    {
      'name': 'head',
      'translation': [0, 1, 0],
    },
  ],
  'skins': [
    {
      'joints': [0, 1, 2],
    },
  ],
  'buffers': ?buffers,
};

SceneObject model(String asset, {String id = 'man'}) =>
    SceneObject(id: id, name: 'Man', kind: ObjectKind.mesh, meshAsset: asset);

void main() {
  late Directory root;

  setUp(() => root = Directory.systemTemp.createTempSync('orblit_skeleton'));
  tearDown(() => root.deleteSync(recursive: true));

  group('a model\'s skeleton', () {
    test('is read from a .glb', () {
      File('${root.path}/man.glb').writeAsBytesSync(glb(spineDocument()));
      final skeleton = ModelSkeletons(root.path).of(model('man.glb'));
      expect(skeleton!.names, ['hips', 'spine', 'head']);
      expect(skeleton.originOf(2).y, closeTo(2, 1e-9));
    });

    test('is read from a .gltf with its buffer beside it', () {
      File('${root.path}/beside.bin').writeAsBytesSync(Uint8List(4));
      File('${root.path}/man.gltf').writeAsStringSync(
        jsonEncode(
          spineDocument(
            buffers: [
              {'uri': 'beside.bin', 'byteLength': 4},
            ],
          ),
        ),
      );
      expect(ModelSkeletons(root.path).of(model('man.gltf')), isNotNull);
    });

    test('is null for a file with no skin, no file, and no mesh asset', () {
      File('${root.path}/plain.glb').writeAsBytesSync(
        glb({
          'asset': {'version': '2.0'},
          'nodes': [<String, Object?>{}],
        }),
      );
      final skeletons = ModelSkeletons(root.path);
      expect(skeletons.of(model('plain.glb')), isNull);
      expect(skeletons.of(model('missing.glb')), isNull);
      expect(
        skeletons.of(SceneObject(id: 'x', name: 'X', kind: ObjectKind.group)),
        isNull,
      );
    });

    test('is null for a file that is not glTF', () {
      File('${root.path}/junk.glb').writeAsBytesSync(Uint8List(32));
      expect(ModelSkeletons(root.path).of(model('junk.glb')), isNull);
    });

    test('is read once, and again after the file is forgotten', () {
      final skeletons = ModelSkeletons(root.path);
      expect(skeletons.of(model('man.glb')), isNull);
      File('${root.path}/man.glb').writeAsBytesSync(glb(spineDocument()));
      expect(skeletons.of(model('man.glb')), isNull);
      skeletons.forget('man.glb');
      expect(skeletons.of(model('man.glb')), isNotNull);
    });
  });

  group('the skeleton gizmo', () {
    const size = Size(800, 600);

    late EditorScene scene;
    late ClipBench bench;
    late ModelSkeletons models;
    late ValueNotifier<SkeletonSettings> settings;

    setUp(() {
      File('${root.path}/man.glb').writeAsBytesSync(glb(spineDocument()));
      scene = EditorScene([
        model('man.glb'),
        SceneObject(id: 'rock', name: 'Rock', kind: ObjectKind.mesh),
      ]);
      final workspace = Workspace(root.path)
        ..add(SceneEntry(id: 'a', name: 'A', scene: scene));
      bench = ClipBench(history: History(workspace), scene: () => scene);
      models = ModelSkeletons(root.path);
      settings = ValueNotifier(const SkeletonSettings(bones: true));
    });

    GizmoTarget targetOf(Set<String> selected) => GizmoTarget(
      object: scene[selected.first]!,
      scene: scene,
      selected: selected,
      projection: ViewportProjection(camera: OrbitCamera(), size: size),
    );

    GizmoType gizmo() =>
        skeletonGizmo(bench: bench, models: models, settings: settings);

    Future<void> show(WidgetTester tester) => tester.pumpWidget(
      Directionality(
        textDirection: TextDirection.ltr,
        child: SizedBox.fromSize(
          size: size,
          child: Stack(
            children: [
              gizmo().overlay!(targetOf({'man'})),
            ],
          ),
        ),
      ),
    );

    /// Where the first path stroked in [colour] lands on screen.
    Rect boundsIn(WidgetTester tester, Color colour) {
      late Rect bounds;
      expect(
        find.byType(CustomPaint),
        paints..something((method, arguments) {
          if (method != #drawPath) return false;
          // Compared as bits: a colour read back off a paint is not exactly
          // the float colour it was made from.
          final stroked = (arguments[1] as Paint).color.toARGB32();
          if (stroked != colour.toARGB32()) return false;
          bounds = (arguments[0] as Path).getBounds();
          return true;
        }),
        reason: 'nothing was drawn in $colour',
      );
      return bounds;
    }

    /// A quarter turn of the spine over one second, which lays the head out
    /// to the side of the hips by the end of it.
    void bend() {
      bench
        ..openClip(
          '/project/bend.oclip',
          ClipDocument(
            name: 'bend',
            duration: 1,
            rate: 10,
            channels: [
              ClipChannel<Quaternion>(
                target: '',
                bone: 'spine',
                property: 'rotation',
                kind: ChannelKind.rotation,
                keys: [
                  Key(0, Quaternion.identity(), hold: Hold.linear),
                  Key(1, Quaternion.axisAngle(Vector3(0, 0, 1), math.pi / 2)),
                ],
              ),
            ],
          ),
        )
        ..owner = 'man';
    }

    final posed = OrblitColors.axisZ;
    final earlier = OrblitColors.axisX.withAlpha(190);
    final later = OrblitColors.axisY.withAlpha(190);

    test('is there for a selection with a skinned model, and only then', () {
      expect(gizmo().appliesTo(targetOf({'man'})), isTrue);
      expect(gizmo().appliesTo(targetOf({'rock'})), isFalse);
      expect(gizmo().appliesTo(targetOf({'rock', 'man'})), isTrue);
    });

    testWidgets('draws nothing until bones are turned on', (tester) async {
      settings.value = const SkeletonSettings();
      await show(tester);
      expect(find.byType(CustomPaint), paintsNothing);

      settings.value = const SkeletonSettings(bones: true);
      await tester.pump();
      boundsIn(tester, posed);
    });

    testWidgets('draws the rest pose with no clip, tall and thin', (
      tester,
    ) async {
      await show(tester);
      final rest = boundsIn(tester, posed);
      expect(rest.height, greaterThan(rest.width));
    });

    testWidgets('poses the bones where the clip has them at the playhead', (
      tester,
    ) async {
      bend();
      await show(tester);
      final start = boundsIn(tester, posed);

      bench.at = 1;
      await tester.pump();
      final end = boundsIn(tester, posed);

      // At the end the head is out beside the hips, so it is wider and no
      // taller than it stood.
      expect(end.width, greaterThan(start.width * 2));
      expect(end.height, lessThan(start.height));
    });

    testWidgets('leaves the clip off a model it is not played on', (
      tester,
    ) async {
      await show(tester);
      final rest = boundsIn(tester, posed);

      bend();
      bench
        ..owner = 'rock'
        ..at = 1;
      await tester.pump();

      expect(boundsIn(tester, posed), rest);
    });

    testWidgets('draws ghosts either side of the playhead', (tester) async {
      bend();
      bench.at = 0.5;
      settings.value = const SkeletonSettings(
        bones: true,
        onion: true,
        before: 1,
        after: 1,
        stride: 2,
      );
      await show(tester);

      final before = boundsIn(tester, earlier);
      final after = boundsIn(tester, later);
      final now = boundsIn(tester, posed);
      // Each is a different point on the way round.
      expect(before.width, lessThan(now.width));
      expect(after.width, greaterThan(now.width));
    });

    testWidgets('draws no ghosts with onion skin off', (tester) async {
      bend();
      bench.at = 0.5;
      await show(tester);
      boundsIn(tester, posed);
      expect(
        find.byType(CustomPaint),
        isNot(
          paints..something((method, arguments) {
            return method == #drawPath &&
                const {
                  0x7FF0736A,
                  0x7F7FD083,
                }.contains((arguments[1] as Paint).color.toARGB32());
          }),
        ),
      );
    });
  });
}

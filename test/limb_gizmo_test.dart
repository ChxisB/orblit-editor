import 'dart:io';

import 'package:flutter/material.dart' hide Key;
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/limb_gizmo.dart';
import 'package:orblit_editor/src/editor/model_skeletons.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/skeleton_view.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/viewport_input.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'support/skinned_glb.dart';

// The hands and feet of a posed model, drawn as handles over the view and
// dragged to key the limb where they are put.

void main() {
  const size = Size(800, 600);

  late Directory root;
  late EditorScene scene;
  late ClipBench bench;
  late History history;
  late ModelSkeletons models;
  late ValueNotifier<SkeletonSettings> settings;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_limb_gizmo');
    File('${root.path}/man.glb').writeAsBytesSync(personGlb());
    scene = EditorScene([
      SceneObject(
        id: 'man',
        name: 'Man',
        kind: ObjectKind.mesh,
        meshAsset: 'man.glb',
      ),
      SceneObject(id: 'rock', name: 'Rock', kind: ObjectKind.mesh),
    ]);
    final workspace = Workspace(root.path)
      ..add(SceneEntry(id: 'a', name: 'A', scene: scene));
    history = History(workspace);
    bench = ClipBench(history: history, scene: () => scene)
      ..openClip(
        '/project/pose.oclip',
        ClipDocument(name: 'pose', duration: 1, rate: 10),
      )
      ..owner = 'man';
    models = ModelSkeletons(root.path);
    settings = ValueNotifier(const SkeletonSettings(bones: true));
  });
  tearDown(() => root.deleteSync(recursive: true));

  GizmoTarget targetOf(Set<String> selected) => GizmoTarget(
    object: scene[selected.first]!,
    scene: scene,
    selected: selected,
    projection: ViewportProjection(camera: OrbitCamera(), size: size),
  );

  GizmoType gizmo() =>
      limbGizmo(bench: bench, models: models, settings: settings);

  LimbHandle handle(String name, [Set<String> selected = const {'man'}]) =>
      limbHandles(
        targetOf(selected),
        bench,
        models,
      ).firstWhere((handle) => handle.limb.name == name);

  Offset screenOf(Vector3 world) =>
      targetOf({'man'}).projection.project(world)!;

  bool send(GizmoType type, ViewportPhase phase, Offset at) =>
      type.input!(targetOf({'man'}), ViewportGesture(phase, at));

  /// Where the left hand stands in the clip at the playhead, in the model's
  /// space, which is the world since the model is at the origin.
  Vector3 leftHand() {
    final limb = handle('left arm').limb;
    final skeleton = models.ofAsset('man.glb')!;
    final pose =
        bench.clip!.sampleAt(bench.frame).bones[''] ??
        const <String, BoneLocal>{};
    return skeleton.posedOrigins(pose)[skeleton.indexOf(limb.tip)!];
  }

  group('the handles', () {
    test('are one on each hand and foot of the selected model', () {
      final handles = limbHandles(targetOf({'man'}), bench, models);
      expect(handles.map((h) => h.limb.name), [
        'left arm',
        'right arm',
        'left leg',
        'right leg',
      ]);
      expect(handles.every((h) => h.id == 'man'), isTrue);
    });

    test('stand where the bones put the tip, in the world', () {
      final left = handle('left arm');
      expect(left.tip.x, greaterThan(0.3));
      expect(left.tip.y, closeTo(1.55, 0.05));
    });

    test('follow the pose the clip gives at the playhead', () {
      final before = handle('left arm').tip;
      bench.edit(
        'Turn spine',
        (clip) => clip.copyWith(
          channels: [
            ClipChannel<Quaternion>(
              target: '',
              bone: 'hips',
              property: 'rotation',
              kind: ChannelKind.rotation,
              keys: [Key(0, Quaternion.axisAngle(Vector3(0, 1, 0), 1.0))],
            ),
          ],
        ),
      );
      final after = handle('left arm').tip;
      expect((after - before).length, greaterThan(0.1));
    });

    test('call the model what the clip calls it', () {
      expect(handle('left arm').target, '');
      // Played on something else, the model is a part of the scene the clip
      // names by id, as a field keyed on it would be.
      bench.owner = 'rock';
      expect(handle('left arm').target, 'man');
    });

    test('are not offered while the clip plays', () {
      bench.playing = true;
      expect(limbHandles(targetOf({'man'}), bench, models), isEmpty);
    });

    test('are not offered with no clip open', () {
      bench.close('/project/pose.oclip');
      expect(limbHandles(targetOf({'man'}), bench, models), isEmpty);
    });
  });

  test('are not offered in a cutscene, which cannot move bones', () {
    final cutscene =
        ClipBench(
          history: history,
          scene: () => scene,
          playedOn: PlayedOn.scene,
        )..openCutscene(
          '/project/intro.ocut',
          CutsceneDocument(
            motion: ClipDocument(name: 'intro', duration: 1, rate: 10),
          ),
        );
    expect(limbHandles(targetOf({'man'}), cutscene, models), isEmpty);
  });

  group('the gizmo', () {
    testWidgets('draws a ring on each tip, and nothing with bones off', (
      tester,
    ) async {
      await tester.pumpWidget(
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
      // Each ring is a dark pass under the ember one, as the bones are.
      final rings = paints;
      for (var i = 0; i < 4; i++) {
        rings
          ..circle(color: const Color(0x66000000))
          ..circle(color: OrblitColors.ember);
      }
      expect(find.byType(CustomPaint), rings);

      settings.value = const SkeletonSettings();
      await tester.pump();
      expect(find.byType(CustomPaint), paintsNothing);
    });

    test('applies to a selection with a skinned model, and only then', () {
      expect(gizmo().appliesTo(targetOf({'man'})), isTrue);
      expect(gizmo().appliesTo(targetOf({'rock'})), isFalse);
    });

    test('takes a drag that starts on a hand, and not one beside it', () {
      final type = gizmo();
      final hand = screenOf(handle('left arm').tip);
      expect(
        send(type, ViewportPhase.dragStart, hand + const Offset(40, 0)),
        isFalse,
      );
      expect(
        send(type, ViewportPhase.dragStart, hand + const Offset(3, 2)),
        isTrue,
      );
      send(type, ViewportPhase.dragCancel, hand);
    });

    test('takes nothing while bones are off', () {
      settings.value = const SkeletonSettings();
      final hand = screenOf(handle('left arm').tip);
      expect(send(gizmo(), ViewportPhase.dragStart, hand), isFalse);
    });

    test('takes a tap on a handle, so the selection is kept', () {
      final hand = screenOf(handle('left arm').tip);
      expect(send(gizmo(), ViewportPhase.tap, hand), isTrue);
    });

    test('leaves hovering to the stages behind it', () {
      final hand = screenOf(handle('left arm').tip);
      expect(send(gizmo(), ViewportPhase.hover, hand), isFalse);
    });

    test('drags a hand to where the pointer goes, keyed at the playhead', () {
      final type = gizmo();
      final start = handle('left arm').tip;
      final projection = targetOf({'man'}).projection;
      final goal = start + Vector3(-0.15, 0.1, 0.1);
      final to = screenOf(goal);
      // The pointer moves in the plane through the hand facing the camera,
      // so that is where the hand is brought to.
      final meets = projection
          .rayThrough(to)
          .meets(point: start, normal: projection.forward)!;

      bench.at = 0.5;
      expect(send(type, ViewportPhase.dragStart, screenOf(start)), isTrue);
      send(type, ViewportPhase.dragUpdate, to);
      send(type, ViewportPhase.dragEnd, to);

      expect((leftHand() - meets).length, lessThan(1e-3));
      final keyed = bench.clip!.channels.where((c) => c.bone != null);
      expect(keyed, hasLength(2));
      expect(keyed.every((c) => c.keys.single.at == 0.5), isTrue);
    });

    test('keeps a whole drag as one step, and undo takes it back', () {
      final type = gizmo();
      final start = handle('left arm').tip;
      expect(send(type, ViewportPhase.dragStart, screenOf(start)), isTrue);
      for (final step in [1, 2, 3]) {
        final to = screenOf(start + Vector3(-0.03 * step, 0.02 * step, 0));
        send(type, ViewportPhase.dragUpdate, to);
      }
      send(type, ViewportPhase.dragEnd, screenOf(start));

      expect(history.labels, ['Pose left arm']);
      history.undo();
      expect(bench.clip!.channels, isEmpty);
      history.redo();
      expect(bench.clip!.channels, hasLength(2));
    });

    test('does not jump when it is grabbed off the middle of the ring', () {
      final type = gizmo();
      final start = handle('left arm').tip;
      final grab = screenOf(start) + const Offset(6, -5);
      send(type, ViewportPhase.dragStart, grab);
      send(type, ViewportPhase.dragUpdate, grab);
      send(type, ViewportPhase.dragEnd, grab);
      expect((leftHand() - start).length, lessThan(1e-3));
    });

    test('ends a drag that is cancelled, keeping what it keyed', () {
      final type = gizmo();
      final start = handle('left arm').tip;
      send(type, ViewportPhase.dragStart, screenOf(start));
      send(
        type,
        ViewportPhase.dragUpdate,
        screenOf(start + Vector3(-0.1, 0.1, 0)),
      );
      send(type, ViewportPhase.dragCancel, screenOf(start));
      expect(history.labels, ['Pose left arm']);
      // The next drag is a new step and not a continuation.
      expect(send(type, ViewportPhase.dragStart, screenOf(leftHand())), isTrue);
      send(type, ViewportPhase.dragUpdate, screenOf(start));
      send(type, ViewportPhase.dragEnd, screenOf(start));
      expect(history.labels, ['Pose left arm', 'Pose left arm']);
    });
  });
}

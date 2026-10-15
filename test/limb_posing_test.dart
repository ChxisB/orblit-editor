import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/limb_posing.dart';
import 'package:orblit_editor/src/editor/model_skeletons.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

import 'support/skinned_glb.dart';

// A limb reached for with two keyed turns: which limbs a model has, and the
// clip that puts the tip of one where it was asked.

SceneObject man() => SceneObject(
  id: 'man',
  name: 'Man',
  kind: ObjectKind.mesh,
  meshAsset: 'man.glb',
);

ClipDocument emptyClip() => ClipDocument(name: 'pose', duration: 1, rate: 10);

void main() {
  late Directory root;
  late RestSkeleton skeleton;

  setUp(() {
    root = Directory.systemTemp.createTempSync('orblit_limb');
    File('${root.path}/man.glb').writeAsBytesSync(personGlb());
    skeleton = ModelSkeletons(root.path).of(man())!;
  });
  tearDown(() => root.deleteSync(recursive: true));

  Vector3 tipOf(ClipDocument clip, Limb limb, double at) {
    final pose = clip.sampleAt(at).bones[''] ?? const <String, BoneLocal>{};
    return skeleton.posedOrigins(pose)[skeleton.indexOf(limb.tip)!];
  }

  group('limbsOf', () {
    test('finds the arms and the legs of a person', () {
      final limbs = limbsOf(skeleton);
      expect(limbs.map((limb) => limb.name), [
        'left arm',
        'right arm',
        'left leg',
        'right leg',
      ]);
    });

    test('names three bones that hang from one another', () {
      for (final limb in limbsOf(skeleton)) {
        final root = skeleton.indexOf(limb.root)!;
        final mid = skeleton.indexOf(limb.mid)!;
        final tip = skeleton.indexOf(limb.tip)!;
        expect(skeleton.parents[mid], root, reason: limb.name);
        expect(skeleton.parents[tip], mid, reason: limb.name);
      }
    });

    test('puts the hand at the end of the arm, and the foot of the leg', () {
      final limbs = {for (final limb in limbsOf(skeleton)) limb.name: limb};
      expect(limbs['left arm']!.tip, contains('hand'));
      expect(limbs['left leg']!.tip, contains('foot'));
      expect(
        skeleton.originOf(skeleton.indexOf(limbs['left arm']!.tip)!).x,
        greaterThan(0),
      );
    });

    test('is the same answer for the same skeleton', () {
      expect(identical(limbsOf(skeleton), limbsOf(skeleton)), isTrue);
    });

    test('has none for a skeleton that is not a person', () {
      File('${root.path}/spine.glb')
          .writeAsBytesSync(chainGlb(['hips', 'spine', 'head']));
      final spine = ModelSkeletons(root.path).ofAsset('spine.glb')!;
      expect(limbsOf(spine), isEmpty);
    });
  });

  group('withLimbReach', () {
    late Limb arm;

    setUp(
      () => arm = limbsOf(skeleton).firstWhere((l) => l.name == 'left arm'),
    );

    test('puts the tip of the limb on a point it can reach', () {
      final rest = tipOf(emptyClip(), arm, 0);
      final point = rest + Vector3(-0.1, 0.15, 0.2);
      final clip = withLimbReach(
        emptyClip(),
        skeleton,
        target: '',
        limb: arm,
        point: point,
        at: 0.5,
      );
      final tip = tipOf(clip, arm, 0.5);
      expect((tip - point).length, lessThan(1e-4));
    });

    test('keys the turn of the two bones above the tip, and no more', () {
      final clip = withLimbReach(
        emptyClip(),
        skeleton,
        target: '',
        limb: arm,
        point: tipOf(emptyClip(), arm, 0) + Vector3(-0.1, 0.1, 0.1),
        at: 0.5,
      );
      expect(
        {for (final c in clip.channels) '${c.bone}.${c.property}'},
        {'${arm.root}.rotation', '${arm.mid}.rotation'},
      );
      for (final channel in clip.channels) {
        expect(channel.keys.single.at, 0.5);
      }
    });

    test('changes nothing else of the clip', () {
      final other = ClipChannel<Quaternion>(
        target: '',
        bone: 'spine',
        property: 'rotation',
        kind: ChannelKind.rotation,
        keys: [Key(0, Quaternion.identity())],
      );
      final before = ClipDocument(
        name: 'pose',
        duration: 1,
        rate: 10,
        channels: [other],
      );
      final clip = withLimbReach(
        before,
        skeleton,
        target: '',
        limb: arm,
        point: tipOf(before, arm, 0) + Vector3(-0.1, 0.1, 0.1),
        at: 0.5,
      );
      expect(clip.channels.first, same(other));
      expect(clip.channels, hasLength(3));
    });

    test('works from the pose the clip already gives', () {
      final lean = ClipChannel<Quaternion>(
        target: '',
        bone: 'spine',
        property: 'rotation',
        kind: ChannelKind.rotation,
        keys: [Key(0, Quaternion.axisAngle(Vector3(1, 0, 0), 0.5))],
      );
      final before = ClipDocument(
        name: 'pose',
        duration: 1,
        rate: 10,
        channels: [lean],
      );
      final point = tipOf(before, arm, 0) + Vector3(-0.15, -0.1, 0.1);
      final clip = withLimbReach(
        before,
        skeleton,
        target: '',
        limb: arm,
        point: point,
        at: 0,
      );
      expect((tipOf(clip, arm, 0) - point).length, lessThan(1e-4));
    });

    test('reaches again from what an earlier reach keyed', () {
      final first = withLimbReach(
        emptyClip(),
        skeleton,
        target: '',
        limb: arm,
        point: tipOf(emptyClip(), arm, 0) + Vector3(-0.15, 0.2, 0.1),
        at: 0,
      );
      final point = tipOf(first, arm, 0) + Vector3(0.05, -0.1, 0.05);
      final second = withLimbReach(
        first,
        skeleton,
        target: '',
        limb: arm,
        point: point,
        at: 0,
      );
      expect(second.channels, hasLength(2));
      expect((tipOf(second, arm, 0) - point).length, lessThan(1e-4));
    });

    test('straightens the limb towards a point out of reach', () {
      final far = Vector3(5, 1.4, 0);
      final clip = withLimbReach(
        emptyClip(),
        skeleton,
        target: '',
        limb: arm,
        point: far,
        at: 0,
      );
      final pose = clip.sampleAt(0).bones[''] ?? const <String, BoneLocal>{};
      final origins = skeleton.posedOrigins(pose);
      final shoulder = origins[skeleton.indexOf(arm.root)!];
      final tip = tipOf(clip, arm, 0);
      final toward = (far - shoulder).normalized();
      expect((tip - shoulder).normalized().dot(toward), closeTo(1, 1e-3));
    });
  });
}

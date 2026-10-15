import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip_edits.dart';

/// The three bones of an arm or a leg, parent to child, by name.
typedef Limb = ({String name, String root, String mid, String tip});

const _parts = [
  (
    'left arm',
    HumanoidBone.leftUpperArm,
    HumanoidBone.leftLowerArm,
    HumanoidBone.leftHand,
  ),
  (
    'right arm',
    HumanoidBone.rightUpperArm,
    HumanoidBone.rightLowerArm,
    HumanoidBone.rightHand,
  ),
  (
    'left leg',
    HumanoidBone.leftUpperLeg,
    HumanoidBone.leftLowerLeg,
    HumanoidBone.leftFoot,
  ),
  (
    'right leg',
    HumanoidBone.rightUpperLeg,
    HumanoidBone.rightLowerLeg,
    HumanoidBone.rightFoot,
  ),
];

/// Held by the skeleton it was worked out from, so a skeleton read once is
/// searched once however often the view is drawn.
final _found = Expando<List<Limb>>('limbs');

/// The arms and legs of [skeleton] that two turns can reach with.
///
/// Found as the parts of a person are, so a model that is not one has none,
/// and an arm or leg with more bones than the finder expects is not placed.
/// A limb whose bones do not hang one from the next is left out as well,
/// because the turn of one bone could not then carry the next.
List<Limb> limbsOf(RestSkeleton skeleton) =>
    _found[skeleton] ??= _search(skeleton);

List<Limb> _search(RestSkeleton skeleton) {
  final person = humanoidOf(skeleton);
  final limbs = <Limb>[];
  for (final (name, root, mid, tip) in _parts) {
    final bones = (person[root], person[mid], person[tip]);
    if (bones case (final root?, final mid?, final tip?)) {
      final hangs =
          skeleton.parents[skeleton.indexOf(mid)!] == skeleton.indexOf(root) &&
          skeleton.parents[skeleton.indexOf(tip)!] == skeleton.indexOf(mid);
      if (hangs) limbs.add((name: name, root: root, mid: mid, tip: tip));
    }
  }
  return limbs;
}

/// [clip] with the two turns that put the tip of [limb] on [point] keyed at
/// [at].
///
/// [target] is what the clip calls the model, and [point] is in the model's
/// own space. The turns are worked out from the pose the clip gives at [at],
/// so a bone the clip does not move stands where it rests. Only rotations
/// are keyed: the limb is turned and never stretched.
///
/// Baked as plain turns on purpose. The file stays an ordinary clip that
/// plays anywhere a clip does, and nothing has to solve a limb at run time.
ClipDocument withLimbReach(
  ClipDocument clip,
  RestSkeleton skeleton, {
  required String target,
  required Limb limb,
  required Vector3 point,
  required double at,
}) {
  final pose = clip.sampleAt(at).bones[target] ?? const <String, BoneLocal>{};
  final reach = reachLimb(
    skeleton,
    pose,
    root: limb.root,
    mid: limb.mid,
    tip: limb.tip,
    target: point,
  );
  ChannelAddress turnOf(String bone) =>
      (target: target, bone: bone, property: 'rotation');
  final upper = keyed(
    clip,
    turnOf(limb.root),
    ChannelKind.rotation,
    at,
    reach.root,
  );
  return keyed(upper, turnOf(limb.mid), ChannelKind.rotation, at, reach.mid);
}

import 'package:flutter/foundation.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

/// What the view shows of a model's skeleton, and of the poses a clip takes
/// either side of the playhead.
///
/// Held by the editor and not by a clip: it is how somebody looks at their
/// work, not part of the work, so it is never saved and never undone.
@immutable
final class SkeletonSettings {
  const SkeletonSettings({
    this.bones = false,
    this.onion = false,
    this.before = 2,
    this.after = 2,
    this.stride = 2,
  });

  /// Draws the bones of a selected model, at rest or in the clip's pose.
  final bool bones;

  /// Draws the pose at moments either side of the playhead as well. Only
  /// shown with [bones], since there is nothing to ghost without them.
  final bool onion;

  /// How many ghosts come from before the playhead.
  final int before;

  /// How many come from after it.
  final int after;

  /// Frames between one ghost and the next.
  final int stride;

  SkeletonSettings copyWith({
    bool? bones,
    bool? onion,
    int? before,
    int? after,
    int? stride,
  }) => SkeletonSettings(
    bones: bones ?? this.bones,
    onion: onion ?? this.onion,
    before: before ?? this.before,
    after: after ?? this.after,
    stride: stride ?? this.stride,
  );

  @override
  bool operator ==(Object other) =>
      other is SkeletonSettings &&
      other.bones == bones &&
      other.onion == onion &&
      other.before == before &&
      other.after == after &&
      other.stride == stride;

  @override
  int get hashCode => Object.hash(bones, onion, before, after, stride);
}

/// One ghost: when it is in the clip, and which side of the playhead.
typedef OnionMoment = ({double at, bool earlier});

/// The moments of [clip] that are ghosted round [at], nearest first on each
/// side.
///
/// A looping clip wraps, so the frame before its first is its last. A clip
/// that holds, bounces or lets go has nothing before its start and nothing
/// after its end, so a ghost that falls off either end is left out rather
/// than piled on the first or last frame.
List<OnionMoment> onionMoments(
  ClipDocument clip,
  double at,
  SkeletonSettings settings,
) {
  if (!settings.onion || clip.rate <= 0 || clip.duration <= 0) return const [];
  final step = settings.stride.clamp(1, 1000) / clip.rate;
  final loops = clip.whenDone == WhenDone.loop;

  double? place(double moment) {
    if (loops) {
      final wrapped = moment % clip.duration;
      return wrapped < 0 ? wrapped + clip.duration : wrapped;
    }
    return moment < 0 || moment > clip.duration ? null : moment;
  }

  final out = <OnionMoment>[];
  void side(int count, double sign, {required bool earlier}) {
    for (var i = 1; i <= count; i++) {
      final moment = place(at + sign * i * step);
      // A short loop comes back to the playhead itself, and to the same
      // moment from both sides.
      if (moment == null || (moment - at).abs() < 1e-9) continue;
      if (out.any((one) => (one.at - moment).abs() < 1e-9)) continue;
      out.add((at: moment, earlier: earlier));
    }
  }

  side(settings.before, -1, earlier: true);
  side(settings.after, 1, earlier: false);
  return out;
}

/// Every bone of [skeleton] that has a parent, as the pair of points it is
/// drawn between, in the model's own space.
///
/// [pose] is the clip's say over some of the bones, by name. A root has no
/// line, since a bone is drawn from the joint it hangs from to its own.
List<(Vector3, Vector3)> boneLines(
  RestSkeleton skeleton,
  Map<String, BoneLocal> pose,
) {
  final origins = skeleton.posedOrigins(pose);
  return [
    for (var bone = 0; bone < skeleton.length; bone++)
      if (skeleton.parents[bone] >= 0)
        (origins[skeleton.parents[bone]], origins[bone]),
  ];
}

import 'package:flutter/foundation.dart' show ValueListenable;
import 'package:flutter/widgets.dart';
import 'package:orblit_motion/orblit_motion.dart' show BoneLocal, RestSkeleton;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import 'clip_bench.dart';
import 'gizmo.dart' show ViewportProjection;
import 'gizmo_registry.dart';
import 'limb_posing.dart';
import 'model_skeletons.dart';
import 'skeleton_view.dart';
import 'viewport_input.dart';

/// How near a handle the pointer has to be to take it, in pixels.
const double _grab = 12;

/// The hand or foot of a selected model that the open clip can be keyed
/// through.
final class LimbHandle {
  const LimbHandle({
    required this.id,
    required this.target,
    required this.skeleton,
    required this.limb,
    required this.world,
    required this.tip,
  });

  /// The scene object the model is, and what the clip calls it.
  final String id;
  final String target;

  final RestSkeleton skeleton;
  final Limb limb;

  /// From the model's own space to the world.
  final Matrix4 world;

  /// Where the end of the limb stands in the world.
  final Vector3 tip;
}

/// Every handle the open clip gives the selection at the playhead.
///
/// Empty while the clip plays, since a pose that is moving by itself is not
/// one to hold a hand against.
List<LimbHandle> limbHandles(
  GizmoTarget target,
  ClipBench bench,
  ModelSkeletons models,
) {
  final scope = bench.scopeIn(target.scene);
  final clip = bench.clip;
  if (scope == null || clip == null || bench.playing) return const [];
  final frame = clip.sampleAt(bench.frame);
  final out = <LimbHandle>[];
  for (final id in target.selected) {
    final object = target.scene[id];
    final skeleton = object == null ? null : models.of(object);
    final named = scope.targetOf(id);
    if (skeleton == null || named == null) continue;
    final origins = skeleton.posedOrigins(
      frame.bones[named] ?? const <String, BoneLocal>{},
    );
    final world = target.scene.worldOf(id);
    for (final limb in limbsOf(skeleton)) {
      out.add(
        LimbHandle(
          id: id,
          target: named,
          skeleton: skeleton,
          limb: limb,
          world: world,
          tip: world.transformed3(origins[skeleton.indexOf(limb.tip)!]),
        ),
      );
    }
  }
  return out;
}

/// Handles on the hands and feet of a selected model, which put the limb
/// where they are dragged and key it there.
///
/// Shown with the view's bones, over the pose the clip gives at the
/// playhead. A drag keys the rotations of the two bones above the handle at
/// the playhead, as one step of undo, and leaves everything else of the pose
/// alone. A limb that cannot reach straightens towards the pointer.
///
/// Registered by the shell through the view's gizmo registry, taking the
/// pointer after the transform handles and before the selection.
GizmoType limbGizmo({
  required ClipBench bench,
  required ModelSkeletons models,
  required ValueListenable<SkeletonSettings> settings,
}) {
  final poser = _LimbDrag(bench: bench, models: models, settings: settings);
  return GizmoType(
    name: 'limb',
    appliesTo: (target) => target.selected.any((id) {
      final object = target.scene[id];
      return object != null && models.of(object) != null;
    }),
    overlay: (target) => Positioned.fill(
      child: IgnorePointer(
        child: ListenableBuilder(
          listenable: Listenable.merge([bench, settings]),
          builder: (_, _) => CustomPaint(
            painter: _HandlePainter(
              projection: target.projection,
              handles: settings.value.bones
                  ? limbHandles(target, bench, models)
                  : const [],
            ),
          ),
        ),
      ),
    ),
    input: poser.input,
  );
}

/// What a drag on a handle remembers between one move and the next.
final class _Hold {
  const _Hold({
    required this.handle,
    required this.gesture,
    required this.toModel,
    required this.plane,
    required this.grabbed,
  });

  final LimbHandle handle;

  /// Ties the edits of one drag into one step of undo.
  final Object gesture;

  /// From the world back to the model's own space.
  final Matrix4 toModel;

  /// A point of the plane the pointer is held to: through the tip, facing
  /// the camera.
  final Vector3 plane;

  /// How far the tip was from the point first grabbed, so it does not jump
  /// to the pointer.
  final Vector3 grabbed;
}

final class _LimbDrag {
  _LimbDrag({
    required this.bench,
    required this.models,
    required this.settings,
  });

  final ClipBench bench;
  final ModelSkeletons models;
  final ValueListenable<SkeletonSettings> settings;

  _Hold? _hold;

  bool input(GizmoTarget target, ViewportGesture gesture) {
    if (_hold != null) return _carry(target.projection, gesture);
    if (!settings.value.bones) return false;
    final handle = _under(target, gesture.at);
    if (handle == null) return false;
    switch (gesture.phase) {
      case ViewportPhase.dragStart:
        _pick(target.projection, handle, gesture.at);
        // A tap is taken too, so a click that misses by a pixel does not
        // clear the selection the handle belongs to.
        return true;
      case ViewportPhase.tap:
        return true;
      case ViewportPhase.hover ||
          ViewportPhase.leave ||
          ViewportPhase.dragUpdate ||
          ViewportPhase.dragEnd ||
          ViewportPhase.dragCancel:
        return false;
    }
  }

  LimbHandle? _under(GizmoTarget target, Offset at) {
    LimbHandle? nearest;
    var best = _grab;
    for (final handle in limbHandles(target, bench, models)) {
      final screen = target.projection.project(handle.tip);
      if (screen == null) continue;
      final distance = (screen - at).distance;
      if (distance > best) continue;
      best = distance;
      nearest = handle;
    }
    return nearest;
  }

  void _pick(ViewportProjection projection, LimbHandle handle, Offset at) {
    final forward = projection.forward;
    final first = projection
        .rayThrough(at)
        .meets(point: handle.tip, normal: forward);
    _hold = _Hold(
      handle: handle,
      gesture: Object(),
      toModel: Matrix4.inverted(handle.world),
      plane: handle.tip,
      grabbed: first == null ? Vector3.zero() : handle.tip - first,
    );
  }

  /// The rest of a drag, which this gizmo keeps whatever is under the pointer.
  bool _carry(ViewportProjection projection, ViewportGesture gesture) {
    final hold = _hold!;
    switch (gesture.phase) {
      case ViewportPhase.dragUpdate:
        _reach(projection, hold, gesture.at);
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        // A cancelled drag keeps what it keyed. It is one step, and undo is
        // the way back.
        _hold = null;
      case ViewportPhase.hover ||
          ViewportPhase.leave ||
          ViewportPhase.tap ||
          ViewportPhase.dragStart:
        break;
    }
    return true;
  }

  void _reach(ViewportProjection projection, _Hold hold, Offset at) {
    final hit = projection
        .rayThrough(at)
        .meets(point: hold.plane, normal: projection.forward);
    if (hit == null) return;
    final point = hold.toModel.transformed3(hit + hold.grabbed);
    final handle = hold.handle;
    final frame = bench.frame;
    bench.edit(
      'Pose ${handle.limb.name}',
      (clip) => withLimbReach(
        clip,
        handle.skeleton,
        target: handle.target,
        limb: handle.limb,
        point: point,
        at: frame,
      ),
      gesture: hold.gesture,
    );
  }
}

final class _HandlePainter extends CustomPainter {
  _HandlePainter({required this.projection, required this.handles});

  final ViewportProjection projection;
  final List<LimbHandle> handles;

  @override
  void paint(Canvas canvas, Size size) {
    for (final handle in handles) {
      final at = projection.project(handle.tip);
      if (at == null) continue;
      canvas
        ..drawCircle(
          at,
          7,
          Paint()
            ..style = PaintingStyle.stroke
            ..strokeWidth = 3
            ..color = const Color(0x66000000),
        )
        ..drawCircle(
          at,
          7,
          Paint()
            ..style = PaintingStyle.stroke
            ..strokeWidth = 1.5
            ..color = OrblitColors.ember,
        );
    }
  }

  @override
  bool shouldRepaint(covariant _HandlePainter old) => true;
}

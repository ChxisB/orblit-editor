import 'package:flutter/foundation.dart' show ValueListenable;
import 'package:flutter/widgets.dart';
import 'package:orblit_motion/orblit_motion.dart' show BoneLocal, ClipFrame;

import '../theme/orblit_theme.dart';
import 'clip_bench.dart';
import 'gizmo_registry.dart';
import 'model_skeletons.dart';
import 'skeleton_view.dart';
import 'world_lines.dart';

/// Draws the bones of every selected model, in the pose the open clip gives
/// them at the playhead, and the poses either side of it as onion skin.
///
/// The renderer poses a skinned model on its own and tells the editor
/// nothing, so this is a second reading of the same two things it works
/// from: the skeleton in the model's file and the clip at the playhead.
/// Drawn over the scene and under the view's chips, and never taking the
/// pointer. A bone you cannot see is a rotation you key by guessing.
///
/// A clip only reaches the model it is played on and the parts of it the
/// clip names. Any other selected model is drawn at rest.
///
/// Registered by the shell through the view's gizmo registry. It applies to
/// any selection with a model that has a skeleton, and draws nothing while
/// [settings] has bones off, so toggling it never asks the view to rebuild.
GizmoType skeletonGizmo({
  required ClipBench bench,
  required ModelSkeletons models,
  required ValueListenable<SkeletonSettings> settings,
}) => GizmoType(
  name: 'skeleton',
  appliesTo: (target) => target.selected.any((id) {
    final object = target.scene[id];
    return object != null && models.of(object) != null;
  }),
  overlay: (target) => Positioned.fill(
    child: IgnorePointer(
      child: ListenableBuilder(
        listenable: Listenable.merge([bench, settings]),
        builder: (_, _) => CustomPaint(
          painter: _SkeletonPainter(
            target: target,
            models: models,
            bench: bench,
            settings: settings.value,
          ),
        ),
      ),
    ),
  ),
);

/// One pose to draw, and how.
typedef _Ghost = ({ClipFrame? frame, Color colour, bool current});

class _SkeletonPainter extends CustomPainter {
  _SkeletonPainter({
    required this.target,
    required this.models,
    required this.bench,
    required this.settings,
  });

  final GizmoTarget target;
  final ModelSkeletons models;
  final ClipBench bench;
  final SkeletonSettings settings;

  @override
  void paint(Canvas canvas, Size size) {
    if (!settings.bones) return;
    final scope = bench.scopeIn(target.scene);
    final ghosts = _ghosts(scope != null);
    for (final id in target.selected) {
      final object = target.scene[id];
      final skeleton = object == null ? null : models.of(object);
      if (skeleton == null) continue;
      final world = target.scene.worldOf(id);
      final named = scope?.targetOf(id);
      for (final ghost in ghosts) {
        final pose = named == null
            ? const <String, BoneLocal>{}
            : ghost.frame?.bones[named] ?? const <String, BoneLocal>{};
        // A ghost of a pose the clip does not move would only sit under
        // the current one.
        if (!ghost.current && pose.isEmpty) continue;
        final lines = WorldLines(target.projection);
        for (final (from, to) in boneLines(skeleton, pose)) {
          lines.line(world.transformed3(from), world.transformed3(to));
        }
        if (ghost.current) {
          lines.paint(canvas, ghost.colour);
        } else {
          canvas.drawPath(
            lines.path,
            Paint()
              ..style = PaintingStyle.stroke
              ..strokeWidth = 1
              ..color = ghost.colour,
          );
        }
      }
    }
  }

  /// The poses to draw, ghosts first so the current one is on top.
  List<_Ghost> _ghosts(bool played) {
    final clip = bench.clip;
    if (clip == null || !played) {
      return [(frame: null, colour: OrblitColors.axisZ, current: true)];
    }
    var earlier = 0;
    var later = 0;
    return [
      for (final moment in onionMoments(clip, bench.at, settings))
        (
          frame: clip.sampleAt(moment.at),
          colour: _tint(moment.earlier, moment.earlier ? ++earlier : ++later),
          current: false,
        ),
      (
        frame: clip.sampleAt(bench.at),
        colour: OrblitColors.axisZ,
        current: true,
      ),
    ];
  }

  /// Earlier ghosts are red and later ones green, each fainter the further
  /// it is from the playhead.
  static Color _tint(bool earlier, int rank) {
    final alpha = (190 - 45 * (rank - 1)).clamp(50, 190);
    return (earlier ? OrblitColors.axisX : OrblitColors.axisY).withAlpha(alpha);
  }

  @override
  bool shouldRepaint(covariant _SkeletonPainter old) => true;
}

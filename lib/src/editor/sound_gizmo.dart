import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:vector_math/vector_math_64.dart';

import '../theme/orblit_theme.dart';
import 'gizmo_registry.dart';
import 'sound_section.dart';
import 'world_lines.dart';

/// Draws how far each selected sound source carries: a ring set at the
/// distance it is at full volume and another at the distance it falls silent.
///
/// A sound heard from the wrong place is a range nobody could see. The rings
/// are drawn about the world's axes and not the object's, because a range is
/// a distance and does not turn or stretch with the object. It only draws
/// and never takes the pointer.
GizmoType soundGizmo() => GizmoType(
  name: 'sound',
  appliesTo: (target) => target.selected.any((id) {
    final object = target.scene[id];
    return object != null && soundSourceOf(object) != null;
  }),
  overlay: (target) => Positioned.fill(
    child: IgnorePointer(child: CustomPaint(painter: _SoundPainter(target))),
  ),
);

class _SoundPainter extends CustomPainter {
  _SoundPainter(this.target);

  final GizmoTarget target;

  @override
  void paint(Canvas canvas, Size size) {
    final near = WorldLines(target.projection);
    final far = WorldLines(target.projection);
    for (final id in target.selected) {
      final object = target.scene[id];
      final source = object == null ? null : soundSourceOf(object);
      if (source == null) continue;
      final centre = target.scene.worldOf(id).getTranslation();
      _rings(near, centre, source.near);
      _rings(far, centre, source.far);
    }
    far.paint(canvas, OrblitColors.axisZ);
    near.paint(canvas, OrblitColors.inkMid);
  }

  static void _rings(WorldLines lines, Vector3 centre, double radius) {
    const full = 2 * math.pi;
    final x = Vector3(1, 0, 0);
    final y = Vector3(0, 1, 0);
    final z = Vector3(0, 0, 1);
    lines
      ..arc(centre, x, y, radius, 0, full)
      ..arc(centre, y, z, radius, 0, full)
      ..arc(centre, z, x, radius, 0, full);
  }

  @override
  bool shouldRepaint(covariant _SoundPainter old) => true;
}

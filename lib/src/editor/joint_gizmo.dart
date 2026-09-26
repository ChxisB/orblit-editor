import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart';

import '../theme/orblit_theme.dart';
import 'gizmo_registry.dart';
import 'joint_section.dart';
import 'scene.dart';
import 'world_lines.dart';

/// Draws every selected joint: its frame, what it joins, and its limits.
///
/// A joint you cannot see is a joint you tune by guessing. Which way a hinge
/// turns is its entity's x axis, how far it goes is an arc, and which two
/// bodies it holds is decided by where the entity sits in the tree, so all
/// three are drawn rather than left to be found out when the scene runs.
///
/// The frame is drawn with x longer than y and z, because x is the axis a
/// hinge turns about, a slider runs along and a cone opens round. Limits are
/// drawn from where things stand in the scene, since that is where every
/// limit is measured from: a hinge's arc starts at nought on the entity's y
/// axis and turns towards z. In amber, so it reads apart from the green of
/// the bodies it joins when both are selected.
///
/// Registered by the shell through the view's gizmo registry. It only draws:
/// the handles that place a joint are its entity's own.
GizmoType jointGizmo() => GizmoType(
  name: 'joint',
  appliesTo: (target) => target.selected.any((id) {
    final object = target.scene[id];
    return object != null && jointOf(object) != null;
  }),
  overlay: (target) => Positioned.fill(
    child: IgnorePointer(child: CustomPaint(painter: _JointPainter(target))),
  ),
);

class _JointPainter extends CustomPainter {
  _JointPainter(this.target);

  final GizmoTarget target;

  @override
  void paint(Canvas canvas, Size size) {
    final lines = _JointLines(target.projection);
    for (final id in target.selected) {
      final object = target.scene[id];
      if (object == null) continue;
      final joint = jointOf(object);
      if (joint != null) lines.joint(joint, id, target.scene);
    }
    lines.paint(canvas, OrblitColors.warn);
  }

  @override
  bool shouldRepaint(covariant _JointPainter old) => true;
}

/// A joint as lines in the world.
class _JointLines extends WorldLines {
  _JointLines(super.projection);

  void joint(doc.JointComponent joint, String id, EditorScene scene) {
    final world = scene.worldOf(id);
    final at = Vector3.zero();
    final turn = Quaternion.identity();
    world.decompose(at, turn, Vector3.zero());

    // The entity's own axes without its scale, as the physics takes them:
    // the columns of its rotation, not `Quaternion.rotated`, which turns the
    // other way.
    final turning = turn.asRotationMatrix();
    final x = turning.getColumn(0);
    final y = turning.getColumn(1);
    final z = turning.getColumn(2);

    // As big on screen wherever the camera is, as the handles are: a frame
    // and its arcs have no size in the world to be drawn at.
    final reach = projection.handleLength(at) * 0.6;

    line(at, at + x * reach);
    line(at, at + y * (reach * 0.35));
    line(at, at + z * (reach * 0.35));

    final ends = jointEndsIn(scene, id);
    final body = ends.body == null ? null : bodyMiddleIn(scene, ends.body!);
    final holder = ends.holder == null
        ? null
        : bodyMiddleIn(scene, ends.holder!);
    if (body != null) line(at, body);
    if (holder != null) line(at, holder);

    final limits = joint.limits;
    switch (joint.kind) {
      case doc.JointKind.fixed:
        break;
      case doc.JointKind.point:
        _ball(at, x, y, z, reach * 0.2);
      case doc.JointKind.hinge:
        _turn(at, x, y, z, limits[doc.JointAxis.aboutX], reach * 0.5);
      case doc.JointKind.slider:
        _travel(at, x, y, limits[doc.JointAxis.alongX], reach);
      case doc.JointKind.distance:
        final towards = body == null || (body - at).length < 1e-6
            ? x
            : (body - at).normalized();
        final range = limits[doc.JointAxis.alongX];
        if (range != null) _travel(at, towards, _across(towards), range, reach);
      case doc.JointKind.cone:
        _cone(at, x, y, z, joint.swing, reach);
        final twist = limits[doc.JointAxis.aboutX];
        if (twist != null) _turn(at, x, y, z, twist, reach * 0.3);
      case doc.JointKind.sixAxis:
        final axes = [x, y, z];
        for (var i = 0; i < 3; i++) {
          final along = limits[doc.JointAxis.values[i]];
          if (along != null) {
            _travel(at, axes[i], axes[(i + 1) % 3], along, reach);
          }
          final about = limits[doc.JointAxis.values[i + 3]];
          if (about != null) {
            _turn(
              at,
              axes[i],
              axes[(i + 1) % 3],
              axes[(i + 2) % 3],
              about,
              reach * 0.5,
            );
          }
        }
    }
  }

  /// How far a joint turns about [axis]: a whole ring when it is free, an
  /// arc with a spoke at each end when it is a range, and one spoke where it
  /// is locked. Angles run from [u] towards [v], which is the right-hand
  /// sense about [axis].
  void _turn(
    Vector3 at,
    Vector3 axis,
    Vector3 u,
    Vector3 v,
    doc.JointRange? range,
    double radius,
  ) {
    if (range == null) {
      arc(at, u, v, radius, 0, 2 * math.pi);
      return;
    }
    final low = radians(range.low);
    final high = radians(range.high);
    Vector3 spoke(double angle) =>
        at + u * (radius * math.cos(angle)) + v * (radius * math.sin(angle));
    line(at, spoke(low));
    if (range.locked) return;
    line(at, spoke(high));
    arc(at, u, v, radius, low, high);
  }

  /// How far a joint travels along [axis]: a track through the point when it
  /// is free, and the stretch between its ends, with a bar across each end,
  /// when it is limited. Metres in the world, not a size on screen, because
  /// that is what a slider's travel is.
  void _travel(
    Vector3 at,
    Vector3 axis,
    Vector3 across,
    doc.JointRange? range,
    double reach,
  ) {
    if (range == null) {
      line(at - axis * reach, at + axis * reach);
      return;
    }
    final bar = across * (reach * 0.12);
    final low = at + axis * range.low;
    final high = at + axis * range.high;
    line(low, high);
    line(low - bar, low + bar);
    if (!range.locked) line(high - bar, high + bar);
  }

  /// The cone a body's x axis is kept inside: a ring where it opens out to
  /// [swing] degrees, and four lines from the point to it.
  void _cone(
    Vector3 at,
    Vector3 x,
    Vector3 y,
    Vector3 z,
    double swing,
    double reach,
  ) {
    final angle = radians(swing.clamp(0, 180).toDouble());
    final rim = at + x * (reach * math.cos(angle));
    final radius = reach * math.sin(angle);
    arc(rim, y, z, radius, 0, 2 * math.pi);
    for (final side in [y, -y, z, -z]) {
      line(at, rim + side * radius);
    }
  }

  void _ball(Vector3 at, Vector3 x, Vector3 y, Vector3 z, double radius) {
    arc(at, x, y, radius, 0, 2 * math.pi);
    arc(at, y, z, radius, 0, 2 * math.pi);
    arc(at, z, x, radius, 0, 2 * math.pi);
  }

  /// Some direction square to [axis].
  static Vector3 _across(Vector3 axis) {
    final other = axis.x.abs() < 0.9 ? Vector3(1, 0, 0) : Vector3(0, 1, 0);
    return axis.cross(other)..normalize();
  }
}

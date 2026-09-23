import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart';

import '../theme/orblit_theme.dart';
import 'body_section.dart';
import 'gizmo.dart';
import 'gizmo_registry.dart';

/// Draws the shape the physics sees for every selected object with a body.
///
/// Seeing the collider is how collider bugs get found: a crate that floats a
/// hand above the floor is a body fitted to the wrong mesh, and a ball that
/// rolls off a ledge it should rest on is a radius nobody could see. Drawn
/// the way the simulation reads it — scaled with the object, a ball by its
/// largest scale and a capsule by its sideways and upright ones — so what is
/// on screen is what will fall, not what the inspector's numbers say before
/// the scale is applied.
///
/// Registered by the shell through the view's gizmo registry. It only draws
/// and never takes the pointer: the handles that move a body are the
/// object's own.
GizmoType bodyGizmo() => GizmoType(
  name: 'body',
  appliesTo: (target) => target.selected.any((id) {
    final object = target.scene[id];
    return object != null && physicsBodyOf(object) != null;
  }),
  overlay: (target) => Positioned.fill(
    child: IgnorePointer(child: CustomPaint(painter: _BodyPainter(target))),
  ),
);

/// The wireframe of each selected body, projected into the view.
class _BodyPainter extends CustomPainter {
  _BodyPainter(this.target);

  final GizmoTarget target;

  @override
  void paint(Canvas canvas, Size size) {
    final outline = _Outline(target.projection);
    for (final id in target.selected) {
      final object = target.scene[id];
      if (object == null) continue;
      final body = physicsBodyOf(object);
      if (body != null) outline.body(body, target.scene.worldOf(id));
    }

    canvas
      // A dark pass under the bright one, as the selection outline has, so the
      // wireframe reads against a pale floor as well as a dark sky.
      ..drawPath(
        outline.path,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 3
          ..color = const Color(0x66000000),
      )
      ..drawPath(
        outline.path,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.25
          ..color = OrblitColors.good,
      );
  }

  @override
  bool shouldRepaint(covariant _BodyPainter old) => true;
}

/// Lines in the world, gathered as a path in the view's pixels.
class _Outline {
  _Outline(this.projection);

  final ViewportProjection projection;
  final path = Path();

  /// Segments to a full circle: round at any size a body is drawn at, and few
  /// enough that a hundred selected balls cost nothing.
  static const int _segments = 32;

  /// [body] laid out the way the simulation places it: turned with its
  /// object, at the object's point for [doc.BodyComponent.centre], and sized
  /// by the object's scale.
  void body(doc.BodyComponent body, Matrix4 world) {
    final turn = Quaternion.identity();
    final scale = Vector3.zero();
    world.decompose(Vector3.zero(), turn, scale);
    scale.absolute();
    final centre = world.transformed3(body.centre);

    // The object's own axes in the world, without its scale: the columns of
    // its rotation. Not `Quaternion.rotated`, which turns by the inverse and
    // would draw the body turned the other way from its model.
    final turning = turn.asRotationMatrix();
    final x = turning.getColumn(0);
    final y = turning.getColumn(1);
    final z = turning.getColumn(2);

    switch (body.shape) {
      case doc.BodyShape.box:
        final half = (body.size.clone()..multiply(scale))
          ..absolute()
          ..scale(0.5);
        _box(centre, x * half.x, y * half.y, z * half.z);
      case doc.BodyShape.sphere:
        final radius =
            body.radius * math.max(scale.x, math.max(scale.y, scale.z));
        _ball(centre, x, y, z, radius);
      case doc.BodyShape.capsule:
        final radius = body.radius * math.max(scale.x, scale.z);
        final straight = body.height * scale.y / 2 - radius;
        // All ends and no middle, which the simulation treats as a ball.
        if (straight <= 0) {
          _ball(centre, x, y, z, radius);
        } else {
          _capsule(centre, x, y, z, radius, straight);
        }
      case doc.BodyShape.plane:
        _ground(centre, x, y, z);
    }
  }

  void _box(Vector3 centre, Vector3 x, Vector3 y, Vector3 z) {
    Vector3 corner(int i) =>
        centre +
        x * (i & 1 == 0 ? -1.0 : 1.0) +
        y * (i & 2 == 0 ? -1.0 : 1.0) +
        z * (i & 4 == 0 ? -1.0 : 1.0);

    // Two corners share an edge when they differ along exactly one axis.
    for (var i = 0; i < 8; i++) {
      for (final axis in const [1, 2, 4]) {
        if (i & axis == 0) _line(corner(i), corner(i | axis));
      }
    }
  }

  /// A ring about each of the object's axes, so a ball's turn shows as well
  /// as its size.
  void _ball(Vector3 centre, Vector3 x, Vector3 y, Vector3 z, double radius) {
    _arc(centre, x, y, radius, 0, 2 * math.pi);
    _arc(centre, y, z, radius, 0, 2 * math.pi);
    _arc(centre, z, x, radius, 0, 2 * math.pi);
  }

  /// A ring at each end of the straight part, four lines joining them, and a
  /// dome over each end drawn as two half rings across each other.
  void _capsule(
    Vector3 centre,
    Vector3 x,
    Vector3 y,
    Vector3 z,
    double radius,
    double straight,
  ) {
    final top = centre + y * straight;
    final bottom = centre - y * straight;
    _arc(top, x, z, radius, 0, 2 * math.pi);
    _arc(bottom, x, z, radius, 0, 2 * math.pi);
    for (final side in [x, -x, z, -z]) {
      _line(top + side * radius, bottom + side * radius);
    }
    for (final across in [x, z]) {
      _arc(top, across, y, radius, 0, math.pi);
      _arc(bottom, across, y, radius, math.pi, 2 * math.pi);
    }
  }

  /// A square of the surface round the point it goes through, with a line
  /// standing up from it to say which side is open.
  ///
  /// Sized to look the same wherever the camera is, as the handles are,
  /// because ground has no size of its own to draw.
  void _ground(Vector3 centre, Vector3 x, Vector3 y, Vector3 z) {
    final reach = projection.handleLength(centre);
    const lines = 2;
    for (var i = -lines; i <= lines; i++) {
      final along = i / lines * reach;
      _line(centre + x * along - z * reach, centre + x * along + z * reach);
      _line(centre + z * along - x * reach, centre + z * along + x * reach);
    }
    _line(centre, centre + y * (reach / 2));
  }

  /// Part of a circle about [centre] in the plane of [u] and [v], from angle
  /// [from] to [to] measured from [u] towards [v].
  void _arc(
    Vector3 centre,
    Vector3 u,
    Vector3 v,
    double radius,
    double from,
    double to,
  ) {
    final steps = math.max(
      2,
      (_segments * (to - from).abs() / (2 * math.pi)).ceil(),
    );
    Vector3 at(int i) {
      final angle = from + (to - from) * i / steps;
      return centre +
          u * (radius * math.cos(angle)) +
          v * (radius * math.sin(angle));
    }

    for (var i = 0; i < steps; i++) {
      _line(at(i), at(i + 1));
    }
  }

  void _line(Vector3 a, Vector3 b) {
    final from = projection.project(a);
    final to = projection.project(b);
    // With one end behind the eye the line would be flipped across the view,
    // through somewhere the body is not.
    if (from == null || to == null) return;
    path
      ..moveTo(from.dx, from.dy)
      ..lineTo(to.dx, to.dy);
  }
}

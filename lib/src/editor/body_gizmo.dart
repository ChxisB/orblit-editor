import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart';

import '../theme/orblit_theme.dart';
import 'body_section.dart';
import 'convex_outline.dart';
import 'gizmo_registry.dart';
import 'world_lines.dart';

/// Draws the shape the physics sees for every selected object with a body.
///
/// Seeing the collider is how collider bugs get found: a crate that floats a
/// hand above the floor is a body fitted to the wrong mesh, and a ball that
/// rolls off a ledge it should rest on is a radius nobody could see. Drawn
/// the way the simulation reads it. Scaled with the object: a ball by its
/// largest scale, a capsule or cylinder by its sideways and upright ones and a
/// hull by each of its three. What is on screen is what will fall, not what
/// the inspector's numbers say before the scale is applied.
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
    final solid = _Outline(target.projection);
    // A place is warm, so a trigger or a zone is not taken for a wall.
    final places = _Outline(target.projection);
    for (final id in target.selected) {
      final object = target.scene[id];
      if (object == null) continue;
      final body = physicsBodyOf(object);
      if (body == null) continue;
      (isPlace(object) ? places : solid).body(body, target.scene.worldOf(id));
    }

    solid.paint(canvas, OrblitColors.good);
    places.paint(canvas, OrblitColors.warn);
  }

  @override
  bool shouldRepaint(covariant _BodyPainter old) => true;
}

/// The object's three axes in the world, as unit vectors or already stretched
/// to the size of what is drawn along them.
typedef _Axes = ({Vector3 x, Vector3 y, Vector3 z});

/// A body's shape as lines in the world.
class _Outline extends WorldLines {
  _Outline(super.projection);

  /// [body] laid out the way the simulation places it: turned with its
  /// object, at the object's point for [doc.BodyComponent.centre], and sized
  /// by the object's scale.
  void body(doc.BodyComponent body, Matrix4 world) {
    final turn = Quaternion.identity();
    final scale = Vector3.zero();
    world.decompose(Vector3.zero(), turn, scale);
    scale.absolute();

    // The object's own axes in the world, without its scale: the columns of
    // its rotation. Not `Quaternion.rotated`, which turns by the inverse and
    // would draw the body turned the other way from its model.
    final turning = turn.asRotationMatrix();
    final axes = (
      x: turning.getColumn(0),
      y: turning.getColumn(1),
      z: turning.getColumn(2),
    );
    final centre = world.transformed3(body.centre);
    if (body.shape == doc.BodyShape.compound ||
        body.shapeScale != Vector3.all(1)) {
      scale.multiply(body.shapeScale);
      _shape(body, centre, _stretched(axes, scale), Vector3.all(1));
      return;
    }
    _shape(body, centre, axes, scale);
  }

  void _shape(
    doc.BodyComponent body,
    Vector3 centre,
    _Axes axes,
    Vector3 scale,
  ) {
    switch (body.shape) {
      case doc.BodyShape.compound:
        for (final part in body.parts) {
          _part(part, centre, axes);
        }
      case doc.BodyShape.box:
        final half = (body.size.clone()..multiply(scale))
          ..absolute()
          ..scale(0.5);
        _box(centre, _stretched(axes, half));
      case doc.BodyShape.sphere:
        final radius =
            body.radius * math.max(scale.x, math.max(scale.y, scale.z));
        _ball(centre, axes, radius);
      case doc.BodyShape.capsule:
        final radius = body.radius * math.max(scale.x, scale.z);
        final straight = body.height * scale.y / 2 - radius;
        // All ends and no middle, which the simulation treats as a ball.
        if (straight <= 0) {
          _ball(centre, axes, radius);
        } else {
          _capsule(centre, axes, radius, straight);
        }
      case doc.BodyShape.cylinder:
        final radius = body.radius * math.max(scale.x, scale.z);
        _tube(centre, axes, radius, body.height * scale.y / 2);
      case doc.BodyShape.hull:
        _hull(centre, _stretched(axes, scale), body.hull);
      case doc.BodyShape.plane:
        _ground(centre, axes);
    }
  }

  void _part(doc.BodyPart part, Vector3 centre, _Axes axes) {
    if (part.shape == doc.BodyShape.compound ||
        part.shape == doc.BodyShape.plane) {
      return;
    }
    Vector3 mapped(Vector3 p) => axes.x * p.x + axes.y * p.y + axes.z * p.z;
    final rotation = part.rotation.clone()..normalize();
    final turn = rotation.asRotationMatrix();
    final ownAxes = _stretched((
      x: mapped(turn.getColumn(0)),
      y: mapped(turn.getColumn(1)),
      z: mapped(turn.getColumn(2)),
    ), part.scale);
    _shape(
      doc.BodyComponent(
        shape: part.shape,
        size: part.size,
        radius: part.radius,
        height: part.height,
        hull: part.hull,
      ),
      centre + mapped(part.centre),
      ownAxes,
      Vector3.all(1),
    );
  }

  /// [axes] each made as long as the matching part of [by].
  _Axes _stretched(_Axes axes, Vector3 by) =>
      (x: axes.x * by.x, y: axes.y * by.y, z: axes.z * by.z);

  void _box(Vector3 centre, _Axes half) {
    Vector3 corner(int i) =>
        centre +
        half.x * (i & 1 == 0 ? -1.0 : 1.0) +
        half.y * (i & 2 == 0 ? -1.0 : 1.0) +
        half.z * (i & 4 == 0 ? -1.0 : 1.0);

    // Two corners share an edge when they differ along exactly one axis.
    for (var i = 0; i < 8; i++) {
      for (final axis in const [1, 2, 4]) {
        if (i & axis == 0) line(corner(i), corner(i | axis));
      }
    }
  }

  /// A ring about each of the object's axes, so a ball's turn shows as well
  /// as its size.
  void _ball(Vector3 centre, _Axes axes, double radius) {
    arc(centre, axes.x, axes.y, radius, 0, 2 * math.pi);
    arc(centre, axes.y, axes.z, radius, 0, 2 * math.pi);
    arc(centre, axes.z, axes.x, radius, 0, 2 * math.pi);
  }

  /// A ring at each end of the straight part, four lines joining them, and a
  /// dome over each end drawn as two half rings across each other.
  void _capsule(Vector3 centre, _Axes axes, double radius, double straight) {
    _tube(centre, axes, radius, straight);
    final top = centre + axes.y * straight;
    final bottom = centre - axes.y * straight;
    for (final across in [axes.x, axes.z]) {
      arc(top, across, axes.y, radius, 0, math.pi);
      arc(bottom, across, axes.y, radius, math.pi, 2 * math.pi);
    }
  }

  /// A ring at each end, [half] up and down from [centre], and four lines
  /// joining them.
  void _tube(Vector3 centre, _Axes axes, double radius, double half) {
    final top = centre + axes.y * half;
    final bottom = centre - axes.y * half;
    arc(top, axes.x, axes.z, radius, 0, 2 * math.pi);
    arc(bottom, axes.x, axes.z, radius, 0, 2 * math.pi);
    for (final side in [axes.x, -axes.x, axes.z, -axes.z]) {
      line(top + side * radius, bottom + side * radius);
    }
  }

  /// The edges of the solid round [corners], which are written flat and
  /// measured from [centre]. Nothing for points that enclose no volume, which
  /// is a body that does nothing.
  void _hull(Vector3 centre, _Axes axes, List<double> corners) {
    final outline = ConvexOutline.of(corners);
    Vector3 at(int i) {
      final p = outline.corners[i];
      return centre + axes.x * p.x + axes.y * p.y + axes.z * p.z;
    }

    for (final (a, b) in outline.edges) {
      line(at(a), at(b));
    }
  }

  /// A square of the surface round the point it goes through, with a line
  /// standing up from it to say which side is open.
  ///
  /// Sized to look the same wherever the camera is, as the handles are,
  /// because ground has no size of its own to draw.
  void _ground(Vector3 centre, _Axes axes) {
    final reach = projection.handleLength(centre);
    const lines = 2;
    for (var i = -lines; i <= lines; i++) {
      final along = i / lines * reach;
      line(
        centre + axes.x * along - axes.z * reach,
        centre + axes.x * along + axes.z * reach,
      );
      line(
        centre + axes.z * along - axes.x * reach,
        centre + axes.z * along + axes.x * reach,
      );
    }
    line(centre, centre + axes.y * (reach / 2));
  }
}

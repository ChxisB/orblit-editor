import 'dart:math' as math;

import 'package:flutter/widgets.dart';
import 'package:vector_math/vector_math_64.dart';

import 'gizmo.dart';

/// Lines in the world, gathered as a path in the view's pixels.
///
/// What a wireframe gizmo draws with: the body gizmo its shapes, the joint
/// gizmo its frames and limits. Held in one place so both project, clip and
/// round a circle the same way.
class WorldLines {
  WorldLines(this.projection);

  final ViewportProjection projection;
  final path = Path();

  /// Segments to a full circle: round at any size a gizmo is drawn at, and
  /// few enough that a hundred selected balls cost nothing.
  static const int segments = 32;

  /// Part of a circle about [centre] in the plane of [u] and [v], from angle
  /// [from] to [to] measured from [u] towards [v].
  void arc(
    Vector3 centre,
    Vector3 u,
    Vector3 v,
    double radius,
    double from,
    double to,
  ) {
    final steps = math.max(
      2,
      (segments * (to - from).abs() / (2 * math.pi)).ceil(),
    );
    Vector3 at(int i) {
      final angle = from + (to - from) * i / steps;
      return centre +
          u * (radius * math.cos(angle)) +
          v * (radius * math.sin(angle));
    }

    for (var i = 0; i < steps; i++) {
      line(at(i), at(i + 1));
    }
  }

  void line(Vector3 a, Vector3 b) {
    final from = projection.project(a);
    final to = projection.project(b);
    // With one end behind the eye the line would be flipped across the view,
    // through somewhere the thing drawn is not.
    if (from == null || to == null) return;
    path
      ..moveTo(from.dx, from.dy)
      ..lineTo(to.dx, to.dy);
  }

  /// Strokes the lines in [colour] over a dark pass, as the selection outline
  /// is drawn, so they read against a pale floor as well as a dark sky.
  void paint(Canvas canvas, Color colour) {
    canvas
      ..drawPath(
        path,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 3
          ..color = const Color(0x66000000),
      )
      ..drawPath(
        path,
        Paint()
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.25
          ..color = colour,
      );
  }
}

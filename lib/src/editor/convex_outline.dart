import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

/// The convex solid round a set of points, as the corners that stand out and
/// the edges between them.
///
/// What a hull body is drawn as, and what a hull is cut down to when it is
/// taken from a mesh. The simulation keeps at most 255 corners and this keeps
/// every one, so a very detailed set is drawn a little larger than it is
/// simulated.
final class ConvexOutline {
  const ConvexOutline._(this.corners, this.edges);

  /// What points that enclose no volume make.
  static const none = ConvexOutline._([], []);

  final List<Vector3> corners;

  /// Each edge as two positions in [corners]. A flat face is bounded by its
  /// edges only: the diagonals that split it into triangles are left out.
  final List<(int, int)> edges;

  /// Whether the points enclose a volume. Fewer than four, or all in one plane
  /// or on one line, do not.
  bool get hasVolume => corners.isNotEmpty;

  /// The outline round [points].
  static ConvexOutline around(List<Vector3> points) {
    final solid = points.length < 4 ? null : _Solid.start(points);
    if (solid == null) return none;
    for (var i = 0; i < points.length; i++) {
      solid.take(i);
    }
    return solid.outline();
  }

  /// The outline round [flat], points written x, y, z one after another, or
  /// none when it is not a whole number of finite points.
  ///
  /// Worked out once for each list, found by identity. A body's list is never
  /// changed, and a painter asks for it every frame.
  static ConvexOutline of(List<double> flat) =>
      _made[flat] ??= around(_pointsOf(flat));

  static final _made = Expando<ConvexOutline>();
}

List<Vector3> _pointsOf(List<double> flat) {
  if (flat.length % 3 != 0 || !flat.every((value) => value.isFinite)) {
    return const [];
  }
  return [
    for (var i = 0; i < flat.length; i += 3)
      Vector3(flat[i], flat[i + 1], flat[i + 2]),
  ];
}

/// The largest span of [points] along any axis.
double _extent(List<Vector3> points) {
  final low = points.first.clone();
  final high = low.clone();
  for (final point in points) {
    Vector3.min(low, point, low);
    Vector3.max(high, point, high);
  }
  final span = high - low;
  return math.max(span.x, math.max(span.y, span.z));
}

/// The index of the point that scores highest, and its score.
(int, double) _farthest(
  List<Vector3> points,
  double Function(Vector3 point) score,
) {
  var best = 0;
  var most = -1.0;
  for (var i = 0; i < points.length; i++) {
    final value = score(points[i]);
    if (value > most) {
      most = value;
      best = i;
    }
  }
  return (best, most);
}

/// Four points that are not on one plane, spread wide, or null when there are
/// none to find. The first solid the rest are added to.
List<int>? _seedOf(List<Vector3> points, double eps) {
  var first = 0;
  for (var i = 1; i < points.length; i++) {
    if (points[i].x < points[first].x) first = i;
  }
  final from = points[first];

  final (second, apart) = _farthest(points, (p) => p.distanceTo(from));
  if (apart <= eps) return null;
  final along = (points[second] - from).normalized();

  final (third, off) = _farthest(points, (p) => (p - from).cross(along).length);
  if (off <= eps) return null;
  final flat = along.cross(points[third] - from).normalized();

  final (fourth, high) = _farthest(points, (p) => flat.dot(p - from).abs());
  return high <= eps ? null : [first, second, third, fourth];
}

/// A triangle of the solid, with the plane it lies in.
final class _Face {
  _Face(this.a, this.b, this.c, this.normal, this.offset);

  /// The face on three points in the order given, which looks at whoever sees
  /// them wound counter-clockwise.
  factory _Face.through(List<Vector3> points, int a, int b, int c) {
    final normal = (points[b] - points[a]).cross(points[c] - points[a])
      ..normalize();
    return _Face(a, b, c, normal, normal.dot(points[a]));
  }

  /// The face on [corners], ordered so that it looks away from [inside].
  factory _Face.outward(
    List<Vector3> points,
    List<int> corners,
    Vector3 inside,
  ) {
    final [a, b, c] = corners;
    final face = _Face.through(points, a, b, c);
    return face.above(inside) > 0 ? _Face.through(points, a, c, b) : face;
  }

  final int a;
  final int b;
  final int c;

  /// A unit vector pointing out of the solid.
  final Vector3 normal;
  final double offset;

  /// How far [point] is out from the plane, which is negative behind it.
  double above(Vector3 point) => normal.dot(point) - offset;

  /// The three edges, each in the direction the face winds.
  List<(int, int)> get edges => [(a, b), (b, c), (c, a)];
}

/// A convex solid grown one point at a time: each point outside it takes the
/// faces it can see away and joins the edge they leave to itself.
final class _Solid {
  _Solid._({
    required this.points,
    required this.seed,
    required this.eps,
    required this.inside,
    required this.faces,
  });

  /// A tetrahedron on four well-spread points, or null for points that
  /// enclose no volume.
  static _Solid? start(List<Vector3> points) {
    // Within this of a plane counts as on it.
    final eps = _extent(points) * 1e-6;
    final seed = _seedOf(points, eps);
    if (seed == null) return null;

    final inside =
        (points[seed[0]] + points[seed[1]] + points[seed[2]] + points[seed[3]])
          ..scale(0.25);
    return _Solid._(
      points: points,
      seed: seed,
      eps: eps,
      inside: inside,
      faces: [
        for (final corners in [
          [seed[0], seed[1], seed[2]],
          [seed[0], seed[1], seed[3]],
          [seed[0], seed[2], seed[3]],
          [seed[1], seed[2], seed[3]],
        ])
          _Face.outward(points, corners, inside),
      ],
    );
  }

  final List<Vector3> points;
  final List<int> seed;
  final double eps;

  /// A point inside the solid for good: the middle of the first tetrahedron.
  final Vector3 inside;
  final List<_Face> faces;

  /// Adds the point at [index], if it is outside the solid.
  void take(int index) {
    if (seed.contains(index)) return;
    final point = points[index];
    final seen = {
      for (final face in faces)
        if (face.above(point) > eps) face,
    };
    if (seen.isEmpty) return;

    final leaving = {for (final face in seen) ...face.edges};
    faces.removeWhere(seen.contains);
    // An edge is on the rim of what was seen when no seen face runs it back
    // the other way.
    for (final (a, b) in leaving) {
      if (!leaving.contains((b, a))) {
        faces.add(_Face.through(points, a, b, index));
      }
    }
  }

  /// The corners and edges where two faces meet at an angle.
  ConvexOutline outline() {
    final sharp = <(int, int)>[];
    final beside = <(int, int), _Face>{};
    for (final face in faces) {
      for (final (a, b) in face.edges) {
        final edge = a < b ? (a, b) : (b, a);
        final other = beside.remove(edge);
        if (other == null) {
          beside[edge] = face;
        } else if (other.normal.dot(face.normal) < 1 - 1e-9) {
          sharp.add(edge);
        }
      }
    }

    final where = <int, int>{};
    final corners = <Vector3>[];
    int cornerOf(int point) => where.putIfAbsent(point, () {
      corners.add(points[point]);
      return corners.length - 1;
    });
    final edges = [for (final (a, b) in sharp) (cornerOf(a), cornerOf(b))];
    return ConvexOutline._(corners, edges);
  }
}

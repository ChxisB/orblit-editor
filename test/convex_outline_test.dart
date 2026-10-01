import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/convex_outline.dart';

// The solid round a set of points, drawn as its corners and the edges between
// them, with the diagonals that only split a flat face left out.

List<double> cube() => [
  for (final x in [-1.0, 1.0])
    for (final y in [-1.0, 1.0])
      for (final z in [-1.0, 1.0]) ...[x, y, z],
];

/// The centre of each face of the cube, which are inside the solid's faces.
const faceCentres = <double>[
  ...[1, 0, 0],
  ...[-1, 0, 0],
  ...[0, 1, 0],
  ...[0, -1, 0],
  ...[0, 0, 1],
  ...[0, 0, -1],
];

void main() {
  group('the outline of points', () {
    test('a cube has eight corners and twelve edges', () {
      final outline = ConvexOutline.of(cube());

      expect(outline.hasVolume, isTrue);
      expect(outline.corners, hasLength(8));
      expect(outline.edges, hasLength(12));
    });

    test('a tetrahedron has four corners and six edges', () {
      final outline = ConvexOutline.of([0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]);

      expect(outline.corners, hasLength(4));
      expect(outline.edges, hasLength(6));
    });

    test('an octahedron has six corners and twelve edges', () {
      final outline = ConvexOutline.of([
        ...[1, 0, 0],
        ...[-1, 0, 0],
        ...[0, 1, 0],
        ...[0, -1, 0],
        ...[0, 0, 1],
        ...[0, 0, -1],
      ]);

      expect(outline.corners, hasLength(6));
      expect(outline.edges, hasLength(12));
    });

    test('a square pyramid leaves out the diagonal of its flat base', () {
      final outline = ConvexOutline.of([
        ...[-1, 0, -1],
        ...[1, 0, -1],
        ...[1, 0, 1],
        ...[-1, 0, 1],
        ...[0, 1, 0],
      ]);

      expect(outline.corners, hasLength(5));
      expect(outline.edges, hasLength(8));
    });

    test('points inside it, or in the middle of a face, add nothing', () {
      final outline = ConvexOutline.of([...cube(), ...faceCentres, 0, 0, 0]);

      expect(outline.corners, hasLength(8));
      expect(outline.edges, hasLength(12));
    });

    test('are the same whichever order they come in', () {
      // The centres first: each is a corner of the solid until the cube's own
      // corners arrive and cover it.
      final outline = ConvexOutline.of([0, 0, 0, ...faceCentres, ...cube()]);

      expect(outline.corners, hasLength(8));
      expect(outline.edges, hasLength(12));
    });

    test('every edge joins two of its corners', () {
      final outline = ConvexOutline.of(cube());

      for (final (a, b) in outline.edges) {
        final apart = (outline.corners[a] - outline.corners[b]).length;
        // Along a side of the cube, not across a face or through it.
        expect(apart, closeTo(2, 1e-9));
      }
    });
  });

  group('points that enclose nothing', () {
    test('make no outline', () {
      for (final flat in <List<double>>[
        // Fewer than four.
        [0, 0, 0, 1, 0, 0, 0, 1, 0],
        // All in one plane.
        [0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0, 1],
        // All on one line.
        [0, 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3],
        // All one point.
        [1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1],
        // Not a whole number of points.
        [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0],
        // Not a number.
        [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, double.nan],
        const [],
      ]) {
        final outline = ConvexOutline.of(flat);
        expect(outline.hasVolume, isFalse, reason: '$flat');
        expect(outline.edges, isEmpty);
      }
    });
  });

  group('asking again', () {
    test('for the same list is not working it out again', () {
      final points = cube();

      expect(ConvexOutline.of(points), same(ConvexOutline.of(points)));
    });
  });
}

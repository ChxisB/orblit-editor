part of 'viewport.dart';

// What the viewport draws over the render: the ground plane, and the
// outline and box that say what is selected.

/// A faint ground grid, so an empty viewport reads as a space rather than a
/// panel that failed to load.
class _GridPainter extends CustomPainter {
  const _GridPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = OrblitColors.line.withValues(alpha: 0.35)
      ..strokeWidth = 1;
    const spacing = 32.0;

    for (var x = spacing; x < size.width; x += spacing) {
      canvas.drawLine(Offset(x, 0), Offset(x, size.height), paint);
    }
    for (var y = spacing; y < size.height; y += spacing) {
      canvas.drawLine(Offset(0, y), Offset(size.width, y), paint);
    }
  }

  @override
  bool shouldRepaint(covariant _GridPainter oldDelegate) => false;
}

/// Draws a box round the selected object.
class _SelectionPainter extends CustomPainter {
  const _SelectionPainter({
    required this.workspace,
    required this.selected,
    required this.camera,
    this.models,
  });

  final Workspace workspace;
  final Set<String> selected;
  final OrbitCamera camera;

  /// How big an imported model says it is, for the objects whose geometry
  /// the editor does not hold.
  final ModelBounds? models;

  /// Past this many edges an outline is a smear rather than a shape, so the
  /// box is drawn instead. Nothing the editor builds comes close; an imported
  /// model would, if the editor ever held its geometry.
  static const int _tooManyEdges = 3000;

  /// The eight corners of a box.
  static List<Vector3> _cornersOf(({Vector3 min, Vector3 max}) box) => [
    for (final x in [box.min.x, box.max.x])
      for (final y in [box.min.y, box.max.y])
        for (final z in [box.min.z, box.max.z]) Vector3(x, y, z),
  ];

  /// Pairs of corner indices making the twelve edges.
  static const _edges = [
    [0, 1],
    [1, 3],
    [3, 2],
    [2, 0],
    [4, 5],
    [5, 7],
    [7, 6],
    [6, 4],
    [0, 4],
    [1, 5],
    [2, 6],
    [3, 7],
  ];

  @override
  void paint(Canvas canvas, Size size) {
    if (selected.isEmpty || size.isEmpty) return;

    final scene = workspace.loaded?.scene;
    if (scene == null) return;

    final view = camera.toRenderCamera();
    final viewMatrix = makeViewMatrix(
      view.position,
      view.target,
      Vector3(0, 1, 0),
    );
    final projection = makePerspectiveMatrix(
      radians(view.fieldOfView),
      size.width / size.height,
      0.1,
      1000,
    );
    final viewProjection = projection.multiplied(viewMatrix);
    final path = Path();

    for (final id in selected) {
      final object = scene[id];
      if (object == null || !object.isDrawable) continue;

      if (object.boundary.isNothing) continue;
      final clip = viewProjection.multiplied(scene.worldOf(id));

      Offset? at(Vector3 world) {
        final projected = clip.transform(Vector4(world.x, world.y, world.z, 1));
        // Behind the camera: the perspective divide flips the point to the
        // opposite side of the screen, which would draw a line across the
        // whole viewport. Skipped rather than drawn wrong.
        return projected.w <= 1e-6
            ? null
            : Offset(
                (projected.x / projected.w * 0.5 + 0.5) * size.width,
                (1 - (projected.y / projected.w * 0.5 + 0.5)) * size.height,
              );
      }

      // The boundary itself, drawn as what it is. A box round a drawn room
      // says nothing true about where its walls are, and the whole point of a
      // mesh boundary is that somebody can see it follows the shape.
      final shell = object.boundaryMesh;
      final edges = object.boundaryEdges;
      if (shell != null && edges.length <= _tooManyEdges) {
        for (final edge in edges) {
          final a = at(shell.positions[edge.$1]);
          final b = at(shell.positions[edge.$2]);
          if (a == null || b == null) continue;
          path
            ..moveTo(a.dx, a.dy)
            ..lineTo(b.dx, b.dy);
        }
        continue;
      }

      // A box: either because that is what was asked for, or because the
      // shape has more edges than anybody could read as an outline.
      final corners = _cornersOf(
        object.boundary.boxFrom(
          object.localBounds(reported: models?.of(object)),
        ),
      );
      final points = [for (final corner in corners) at(corner)];
      if (points.any((one) => one == null)) continue;

      for (final edge in _edges) {
        path
          ..moveTo(points[edge[0]]!.dx, points[edge[0]]!.dy)
          ..lineTo(points[edge[1]]!.dx, points[edge[1]]!.dy);
      }
    }

    if (path.getBounds().isEmpty) return;

    canvas
      // A dark pass under the bright one, so the outline reads against a pale
      // surface as well as a dark one.
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
          ..strokeWidth = 1.5
          ..color = OrblitColors.ember,
      );
  }

  @override
  bool shouldRepaint(covariant _SelectionPainter old) => true;
}

import 'dart:math' as math;

import 'package:flutter/widgets.dart';

import '../theme/orblit_theme.dart';

/// The engine's mark: a small body in its own colour with a ring tipped round
/// it. Drawn rather than loaded, so it stays sharp at the size of an icon.
final class OrblitMark extends StatelessWidget {
  const OrblitMark({super.key, this.size = 22});

  final double size;

  @override
  Widget build(BuildContext context) =>
      CustomPaint(size: Size.square(size), painter: const _MarkPainter());
}

final class _MarkPainter extends CustomPainter {
  const _MarkPainter();

  // The mark is drawn on a 24 unit square and scaled to fit.
  static const _unit = 24.0;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / _unit);
    canvas.translate(_unit / 2, _unit / 2);

    canvas.save();
    canvas.rotate(-24 * math.pi / 180);
    canvas.drawOval(
      Rect.fromCenter(center: Offset.zero, width: 21, height: 8),
      Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5
        ..color = OrblitColors.ink,
    );
    canvas.restore();

    canvas.drawCircle(Offset.zero, 5, Paint()..color = OrblitColors.ember);
  }

  @override
  bool shouldRepaint(_MarkPainter oldDelegate) => false;
}

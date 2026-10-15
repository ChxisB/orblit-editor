import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_motion/orblit_motion.dart';

import '../theme/orblit_theme.dart';
import 'blend_bench.dart';
import 'blend_model.dart';

// The graph itself: a box for each state, an arrow for each pair of states a
// change joins, and a handle on each box to drag to another to join them.

const double _nodeWidth = 156;
const double _nodeHeight = 52;
const double _gapX = 92;
const double _gapY = 40;
const double _margin = 28;

/// Where something is dragged from to make a change: a state, or null for
/// the box that stands for every state.
final class _Link {
  const _Link(this.from);

  final String? from;
}

/// One curve, from where it leaves a box to where it arrives at another.
final class _Curve {
  const _Curve(this.start, this.pullStart, this.pullEnd, this.end);

  final Offset start;
  final Offset pullStart;
  final Offset pullEnd;
  final Offset end;

  Offset at(double t) {
    final u = 1 - t;
    return start * (u * u * u) +
        pullStart * (3 * u * u * t) +
        pullEnd * (3 * u * t * t) +
        end * (t * t * t);
  }

  Path get path => Path()
    ..moveTo(start.dx, start.dy)
    ..cubicTo(
      pullStart.dx,
      pullStart.dy,
      pullEnd.dx,
      pullEnd.dy,
      end.dx,
      end.dy,
    );

  /// The way the curve is heading as it arrives.
  double get heading {
    final along = end - pullEnd;
    return along.distanceSquared == 0 ? 0 : math.atan2(along.dy, along.dx);
  }
}

/// Forwards across the gap between columns, and underneath both boxes when
/// the change goes back or stays in a column, so it never crosses a box.
_Curve _curveBetween(Rect from, Rect to) {
  if (to.left > from.right) {
    final a = from.centerRight;
    final b = to.centerLeft;
    final pull = (b.dx - a.dx) / 2;
    return _Curve(a, a + Offset(pull, 0), b - Offset(pull, 0), b);
  }
  final a = from.bottomCenter;
  final b = to.bottomCenter;
  final low = math.max(a.dy, b.dy) + _gapY * 0.9;
  return _Curve(a, Offset(a.dx, low), Offset(b.dx, low), b);
}

/// One arrow, and the changes it stands for in order of precedence.
final class _Arrow {
  const _Arrow({required this.from, required this.to, required this.changes});

  final Rect from;
  final Rect to;
  final List<int> changes;

  bool get isLoop => from == to;

  _Curve get curve => _curveBetween(from, to);
}

/// Where everything in a blend is drawn, in pixels from the corner of the
/// canvas.
final class _Sketch {
  _Sketch._({
    required this.states,
    required this.anywhere,
    required this.arrows,
    required this.size,
  });

  factory _Sketch.of(BlendDocument blend) {
    final cells = layoutBlend(blend);
    final fromAnywhere = changesFromAnywhere(blend);
    // The box for every state takes the first column, so what is started
    // from it is read left to right like everything else.
    final shift = fromAnywhere.isEmpty ? 0 : 1;
    Rect cell(int column, int row) => Rect.fromLTWH(
      _margin + (column + shift) * (_nodeWidth + _gapX),
      _margin + row * (_nodeHeight + _gapY),
      _nodeWidth,
      _nodeHeight,
    );
    final states = {
      for (final MapEntry(:key, :value) in cells.entries)
        key: cell(value.column, value.row),
    };
    final hub = fromAnywhere.isEmpty ? null : cell(-1, 0);
    final arrows = <_Arrow>[
      for (final edge in blendEdges(blend))
        if (states[edge.from] case final from?)
          if (states[edge.to] case final to?)
            _Arrow(from: from, to: to, changes: edge.changes),
      if (hub != null)
        for (final MapEntry(:key, :value) in fromAnywhere.entries)
          if (states[key] case final to?)
            _Arrow(from: hub, to: to, changes: value),
    ];
    final boxes = [...states.values, ?hub];
    return _Sketch._(
      states: states,
      anywhere: hub,
      arrows: arrows,
      size: Size(
        boxes.map((box) => box.right).fold(0.0, math.max) + _margin,
        boxes.map((box) => box.bottom).fold(0.0, math.max) + _margin + _gapY,
      ),
    );
  }

  final Map<String, Rect> states;

  /// The box standing for every state, or null when no change is from there.
  final Rect? anywhere;

  final List<_Arrow> arrows;

  final Size size;
}

/// The graph of [blend], scrolling both ways.
///
/// [onConnect] is told what was dragged from, which is null for the box
/// that stands for every state, and what it was dropped on.
class BlendCanvas extends StatelessWidget {
  const BlendCanvas({
    super.key,
    required this.blend,
    required this.pick,
    required this.onPick,
    required this.onConnect,
  });

  final BlendDocument blend;
  final BlendPick pick;
  final ValueChanged<BlendPick> onPick;
  final void Function(String? from, String to) onConnect;

  @override
  Widget build(BuildContext context) {
    final sketch = _Sketch.of(blend);
    return GestureDetector(
      behavior: HitTestBehavior.opaque,
      onTap: () => onPick(const NothingPicked()),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: SingleChildScrollView(
          child: SizedBox(
            width: sketch.size.width,
            height: sketch.size.height,
            child: _Drawing(
              blend: blend,
              sketch: sketch,
              pick: pick,
              onPick: onPick,
              onConnect: onConnect,
            ),
          ),
        ),
      ),
    );
  }
}

final class _Drawing extends StatelessWidget {
  const _Drawing({
    required this.blend,
    required this.sketch,
    required this.pick,
    required this.onPick,
    required this.onConnect,
  });

  final BlendDocument blend;
  final _Sketch sketch;
  final BlendPick pick;
  final ValueChanged<BlendPick> onPick;
  final void Function(String? from, String to) onConnect;

  @override
  Widget build(BuildContext context) {
    final picked = switch (pick) {
      ChangePicked(:final index) => index,
      _ => -1,
    };
    return Stack(
      children: [
        Positioned.fill(
          child: CustomPaint(
            painter: _ArrowPainter(sketch.arrows, picked: picked),
          ),
        ),
        for (final arrow in sketch.arrows)
          _ArrowLabel(
            arrow: arrow,
            blend: blend,
            picked: arrow.changes.contains(picked),
            onTap: () => onPick(ChangePicked(_after(arrow.changes, picked))),
          ),
        if (sketch.anywhere case final hub?)
          Positioned.fromRect(rect: hub, child: const _AnyState()),
        for (final MapEntry(:key, :value) in sketch.states.entries)
          Positioned.fromRect(
            rect: value,
            child: _StateBox(
              name: key,
              plays: describeSource(
                blend.writtenStates.firstWhere((s) => s.name == key).plays,
              ),
              isStart: blend.writtenStart == key,
              picked: pick == StatePicked(key),
              onTap: () => onPick(StatePicked(key)),
              onConnect: onConnect,
            ),
          ),
      ],
    );
  }
}

/// The change after [picked] among those an arrow stands for, or the first
/// when [picked] is not one of them, so tapping an arrow again steps along
/// the changes that share it.
int _after(List<int> changes, int picked) {
  final at = changes.indexOf(picked);
  return changes[(at + 1) % changes.length];
}

class _ArrowPainter extends CustomPainter {
  const _ArrowPainter(this.arrows, {required this.picked});

  final List<_Arrow> arrows;
  final int picked;

  @override
  void paint(Canvas canvas, Size size) {
    for (final arrow in arrows) {
      if (arrow.isLoop) continue;
      final curve = arrow.curve;
      final colour = arrow.changes.contains(picked)
          ? OrblitColors.ember
          : OrblitColors.inkDim;
      final stroke = Paint()
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5
        ..color = colour;
      canvas.drawPath(curve.path, stroke);
      _head(canvas, curve, colour);
    }
  }

  void _head(Canvas canvas, _Curve curve, Color colour) {
    const length = 8.0;
    const spread = 0.45;
    final heading = curve.heading;
    final tip = curve.end;
    final head = Path()
      ..moveTo(tip.dx, tip.dy)
      ..lineTo(
        tip.dx - length * math.cos(heading - spread),
        tip.dy - length * math.sin(heading - spread),
      )
      ..lineTo(
        tip.dx - length * math.cos(heading + spread),
        tip.dy - length * math.sin(heading + spread),
      )
      ..close();
    canvas.drawPath(head, Paint()..color = colour);
  }

  @override
  bool shouldRepaint(_ArrowPainter old) =>
      old.arrows != arrows || old.picked != picked;
}

/// What an arrow says: the first change's condition, and how many more
/// share the arrow.
final class _ArrowLabel extends StatelessWidget {
  const _ArrowLabel({
    required this.arrow,
    required this.blend,
    required this.picked,
    required this.onTap,
  });

  final _Arrow arrow;
  final BlendDocument blend;
  final bool picked;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final first = arrow.changes.first;
    final change = blend.writtenChanges[first];
    final more = arrow.changes.length - 1;
    final anchor = arrow.isLoop
        ? arrow.from.bottomLeft + const Offset(0, 14)
        : arrow.curve.at(0.5);
    return Positioned(
      left: anchor.dx,
      top: anchor.dy,
      child: FractionalTranslation(
        translation: arrow.isLoop
            ? const Offset(0, -0.5)
            : const Offset(-0.5, -0.5),
        child: GestureDetector(
          key: ValueKey('change/$first'),
          onTap: onTap,
          child: Container(
            constraints: const BoxConstraints(maxWidth: 190),
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            decoration: BoxDecoration(
              color: picked ? OrblitColors.emberWash : OrblitColors.raised,
              border: Border.all(
                color: picked ? OrblitColors.ember : OrblitColors.line,
              ),
              borderRadius: BorderRadius.circular(Radii.control),
            ),
            child: Text(
              more == 0
                  ? describeCondition(change.when)
                  : '${describeCondition(change.when)} +$more',
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.caption.copyWith(
                color: picked ? OrblitColors.ink : OrblitColors.inkMid,
              ),
            ),
          ),
        ),
      ),
    );
  }
}

/// A box that something can be dragged from and, for a state, onto.
final class _StateBox extends StatelessWidget {
  const _StateBox({
    required this.name,
    required this.plays,
    required this.isStart,
    required this.picked,
    required this.onTap,
    required this.onConnect,
  });

  final String name;
  final String plays;
  final bool isStart;
  final bool picked;
  final VoidCallback onTap;
  final void Function(String? from, String to) onConnect;

  @override
  Widget build(BuildContext context) => DragTarget<_Link>(
    onAcceptWithDetails: (details) => onConnect(details.data.from, name),
    builder: (context, over, _) => GestureDetector(
      key: ValueKey('state/$name'),
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.only(left: 10, right: 4),
        decoration: BoxDecoration(
          color: picked ? OrblitColors.emberWash : OrblitColors.raised,
          border: Border.all(
            color: over.isNotEmpty || picked
                ? OrblitColors.ember
                : OrblitColors.line,
            width: over.isNotEmpty ? 2 : 1,
          ),
          borderRadius: BorderRadius.circular(Radii.card),
        ),
        child: Row(
          children: [
            Expanded(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      if (isStart)
                        const Padding(
                          padding: EdgeInsets.only(right: 4),
                          child: Icon(
                            Icons.flag,
                            size: 12,
                            color: OrblitColors.ember,
                          ),
                        ),
                      Expanded(
                        child: Text(
                          name,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: OrblitText.label.copyWith(
                            color: OrblitColors.ink,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ),
                    ],
                  ),
                  Text(
                    plays,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: OrblitText.caption,
                  ),
                ],
              ),
            ),
            _Handle(link: _Link(name), keyed: 'link/$name'),
          ],
        ),
      ),
    ),
  );
}

/// The box for what a change from anywhere leaves: it is not a state, and
/// nothing can be dropped on it.
final class _AnyState extends StatelessWidget {
  const _AnyState();

  @override
  Widget build(BuildContext context) => Container(
    key: const ValueKey('anywhere'),
    padding: const EdgeInsets.only(left: 10, right: 4),
    decoration: BoxDecoration(
      border: Border.all(color: OrblitColors.line),
      borderRadius: BorderRadius.circular(Radii.card),
    ),
    child: const Row(
      children: [
        Expanded(child: Text('Any state', style: OrblitText.caption)),
        _Handle(link: _Link(null), keyed: 'link/anywhere'),
      ],
    ),
  );
}

/// What a change is dragged out of.
final class _Handle extends StatelessWidget {
  const _Handle({required this.link, required this.keyed});

  final _Link link;
  final String keyed;

  static const _dot = Icon(Icons.circle, size: 14, color: OrblitColors.ember);

  @override
  Widget build(BuildContext context) => Tooltip(
    message: 'Drag onto a state to make a change to it',
    child: Draggable<_Link>(
      key: ValueKey(keyed),
      data: link,
      feedback: const Material(color: Colors.transparent, child: _dot),
      child: const Padding(
        padding: EdgeInsets.all(6),
        child: Icon(Icons.trending_flat, size: 16, color: OrblitColors.inkMid),
      ),
    ),
  );
}

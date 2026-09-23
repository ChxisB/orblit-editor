import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:orblit_motion/orblit_motion.dart' hide Key;

import '../theme/orblit_theme.dart';
import 'clip_bench.dart';
import 'clip_edits.dart';
import 'timeline_sheet.dart';

// One channel drawn as the numbers it moves through. The dope sheet says
// when; this says how much, and how it gets there.

/// How far out from its key a handle is drawn, whatever its slope.
const double _handleLength = 40;

/// How close a pointer has to be to a key or a handle to take it.
const double _reach = 6;

/// X, Y and Z, tinted the way the inspector tints them.
const _partColours = [Color(0xFFD9634F), Color(0xFF7FB069), Color(0xFF5B8DD9)];

/// The channel the bench says to draw, as one line a number it moves.
///
/// A key can be dragged in time and in value, and the picked keys grow
/// handles that bend the curve through them. The two handles of a key turn
/// together unless Alt is held, which breaks them into a corner. Shift keeps
/// a key's drag to whichever way it went furthest.
class CurveView extends StatefulWidget {
  const CurveView({super.key, required this.bench, this.onFocus});

  final ClipBench bench;
  final VoidCallback? onFocus;

  @override
  State<CurveView> createState() => _CurveViewState();
}

/// Where a curve is drawn: time across, value up.
class _Frame {
  _Frame(this.axis, this.low, this.high, this.height);

  final TimeAxis axis;
  final double low;
  final double high;
  final double height;

  static const double pad = 14;

  double get perUnit => (height - pad * 2) / (high - low);

  double yOf(double value) => pad + (high - value) * perUnit;

  double valueOf(double y) => high - (y - pad) / perUnit;

  Offset at(double time, double value) => Offset(axis.xOf(time), yOf(value));
}

typedef _Grip = ({int index, int part, KeySide? side});

class _CurveViewState extends State<CurveView> {
  /// The values the view spans, held still while something is dragged so the
  /// curve does not rescale under the pointer.
  ({double low, double high})? _held;

  ({
    ClipDocument start,
    _Grip grip,
    Set<KeyRef> keys,
    Offset from,
    Object gesture,
    bool moved,
  })?
  _drag;

  ClipBench get _bench => widget.bench;

  ClipChannel<Object>? get _channel {
    final clip = _bench.clip;
    final address = _bench.curve;
    if (clip == null || address == null) return null;
    return channelAt(clip, address);
  }

  static ({double low, double high}) _rangeOf(
    ClipChannel<Object> channel,
    TimeAxis axis,
  ) {
    final parts = curvesOf(channel.kind);
    var low = double.infinity;
    var high = double.negativeInfinity;
    void take(Object value) {
      for (var part = 0; part < parts; part++) {
        final number = partOf(value, part);
        low = math.min(low, number);
        high = math.max(high, number);
      }
    }

    for (final key in channel.keys) {
      take(key.value);
    }
    const samples = 64;
    for (var i = 0; i <= samples; i++) {
      take(channel.valueAt(axis.span * i / samples));
    }
    if (!low.isFinite || !high.isFinite) return (low: -1, high: 1);
    var spread = high - low;
    if (spread < 1e-6) {
      spread = math.max(low.abs() * 0.2, 1);
      low -= spread / 2;
      high += spread / 2;
    }
    return (low: low - spread * 0.1, high: high + spread * 0.1);
  }

  /// The picked keys of [channel], by place.
  List<int> _pickedIn(ClipChannel<Object> channel) {
    final address = addressOf(channel);
    return [
      for (final ref in _bench.selection)
        if (ref.channel == address && ref.index < channel.keys.length)
          ref.index,
    ];
  }

  static Offset _handle(
    _Frame frame,
    ClipChannel<Object> channel,
    int index,
    int part,
    KeySide side,
  ) {
    final key = channel.keys[index];
    final arriving = side == KeySide.arriving;
    final slope = partOf(
      (arriving ? key.slopeIn : key.slopeOut) ?? channel.curve.slopeAt(index),
      part,
    );
    final origin = frame.at(key.at, partOf(key.value, part));
    // One second along the slope, on screen, made the length of a handle.
    final along = Offset(frame.axis.perSecond, -slope * frame.perUnit);
    final unit = along.distance == 0
        ? const Offset(1, 0)
        : along / along.distance;
    return origin + unit * (arriving ? -_handleLength : _handleLength);
  }

  _Grip? _hit(_Frame frame, ClipChannel<Object> channel, Offset at) {
    final parts = curvesOf(channel.kind);
    var best = _reach;
    _Grip? found;
    void consider(Offset point, _Grip grip) {
      final distance = (point - at).distance;
      if (distance <= best) {
        best = distance;
        found = grip;
      }
    }

    // Handles first: they sit over curves, and a key under a handle can
    // still be taken by its middle.
    final last = channel.keys.length - 1;
    for (final index in _pickedIn(channel)) {
      for (var part = 0; part < parts; part++) {
        if (index > 0) {
          consider(
            _handle(frame, channel, index, part, KeySide.arriving),
            (index: index, part: part, side: KeySide.arriving),
          );
        }
        if (index < last) {
          consider(
            _handle(frame, channel, index, part, KeySide.leaving),
            (index: index, part: part, side: KeySide.leaving),
          );
        }
      }
    }
    if (found != null) return found;
    for (final (index, key) in channel.keys.indexed) {
      for (var part = 0; part < parts; part++) {
        consider(
          frame.at(key.at, partOf(key.value, part)),
          (index: index, part: part, side: null),
        );
      }
    }
    return found;
  }

  void _tap(_Frame frame, ClipChannel<Object> channel, Offset at) {
    widget.onFocus?.call();
    final grip = _hit(frame, channel, at);
    final adding = HardwareKeyboard.instance.isShiftPressed;
    if (grip == null) {
      if (!adding) _bench.selection = const {};
      return;
    }
    if (grip.side != null) return;
    final ref = (channel: addressOf(channel), index: grip.index);
    _bench.selection = !adding
        ? {ref}
        : _bench.selection.contains(ref)
        ? _bench.selection.difference({ref})
        : _bench.selection.union({ref});
  }

  void _start(_Frame frame, ClipChannel<Object> channel, Offset at) {
    widget.onFocus?.call();
    final clip = _bench.clip;
    final grip = _hit(frame, channel, at);
    if (clip == null || grip == null) return;
    final ref = (channel: addressOf(channel), index: grip.index);
    if (grip.side == null && !_bench.selection.contains(ref)) {
      _bench.selection = HardwareKeyboard.instance.isShiftPressed
          ? _bench.selection.union({ref})
          : {ref};
    }
    setState(() {
      _held = (low: frame.low, high: frame.high);
      _drag = (
        start: clip,
        grip: grip,
        keys: _bench.selection,
        from: at,
        gesture: Object(),
        moved: false,
      );
    });
  }

  void _update(_Frame frame, ClipChannel<Object> channel, Offset at) {
    final drag = _drag;
    if (drag == null || frame.axis.perSecond == 0) return;
    final address = addressOf(channel);
    final grip = drag.grip;

    if (grip.side case final side?) {
      final ref = (channel: address, index: grip.index);
      final key = keyOf(drag.start, ref);
      if (key == null) return;
      // A handle cannot cross over its key: past it, a slope would point
      // the curve back in time.
      final least = 2 / frame.axis.perSecond;
      var seconds = frame.axis.atOf(at.dx) - key.at;
      seconds = side == KeySide.arriving
          ? math.min(seconds, -least)
          : math.max(seconds, least);
      final rise = frame.valueOf(at.dy) - partOf(key.value, grip.part);
      _push(
        drag,
        'Bend curve',
        withSlope(
          drag.start,
          ref,
          side: side,
          part: grip.part,
          slope: rise / seconds,
          broken: HardwareKeyboard.instance.isAltPressed,
        ),
        drag.keys,
      );
      return;
    }

    var travel = at - drag.from;
    if (HardwareKeyboard.instance.isShiftPressed) {
      travel = travel.dx.abs() >= travel.dy.abs()
          ? Offset(travel.dx, 0)
          : Offset(0, travel.dy);
    }
    final rise = -travel.dy / frame.perUnit;
    var valued = drag.start;
    if (rise != 0) {
      for (final ref in drag.keys) {
        if (ref.channel != address) continue;
        final key = keyOf(drag.start, ref);
        if (key == null) continue;
        valued = withKeyValue(
          valued,
          ref,
          withPart(key.value, grip.part, partOf(key.value, grip.part) + rise),
        );
      }
    }
    final moved = movedKeys(
      valued,
      drag.keys,
      travel.dx / frame.axis.perSecond,
    );
    _push(drag, 'Move keys', moved.clip, moved.keys);
  }

  void _push(
    ({
      ClipDocument start,
      _Grip grip,
      Set<KeyRef> keys,
      Offset from,
      Object gesture,
      bool moved,
    })
    drag,
    String label,
    ClipDocument to,
    Set<KeyRef> keys,
  ) {
    if (identical(to, drag.start) && !drag.moved) return;
    _drag = (
      start: drag.start,
      grip: drag.grip,
      keys: drag.keys,
      from: drag.from,
      gesture: drag.gesture,
      moved: true,
    );
    _bench.edit(
      label,
      (_) => to,
      keys: keys,
      gesture: drag.gesture,
      onlyMoves: true,
    );
  }

  void _end() {
    if (_drag?.moved ?? false) _bench.history.seal();
    setState(() {
      _drag = null;
      _held = null;
    });
  }

  @override
  Widget build(BuildContext context) {
    final clip = _bench.clip;
    final channel = _channel;
    if (clip == null || channel == null || channel.keys.isEmpty) {
      return const _Note(
        'Choose a number or a position on the left to draw its curve.',
      );
    }
    if (curvesOf(channel.kind) == 0) {
      return const _Note(
        'A turn or a flag has no curve to draw; its keys are edited on '
        'the sheet.',
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final axis = TimeAxis(clip, width: constraints.maxWidth);
        final range = _held ?? _rangeOf(channel, axis);
        final frame = _Frame(
          axis,
          range.low,
          range.high,
          constraints.maxHeight,
        );
        return GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapUp: (details) => _tap(frame, channel, details.localPosition),
          onPanStart: (details) =>
              _start(frame, channel, details.localPosition),
          onPanUpdate: (details) =>
              _update(frame, channel, details.localPosition),
          onPanEnd: (_) => _end(),
          onPanCancel: _end,
          child: CustomPaint(
            size: Size(constraints.maxWidth, constraints.maxHeight),
            painter: _CurvePainter(
              frame: frame,
              channel: channel,
              rate: clip.rate,
              at: _bench.at,
              picked: _pickedIn(channel),
            ),
          ),
        );
      },
    );
  }
}

class _Note extends StatelessWidget {
  const _Note(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Text(
          text,
          style: OrblitText.caption,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

class _CurvePainter extends CustomPainter {
  _CurvePainter({
    required this.frame,
    required this.channel,
    required this.rate,
    required this.at,
    required this.picked,
  });

  final _Frame frame;
  final ClipChannel<Object> channel;
  final double rate;
  final double at;
  final List<int> picked;

  @override
  void paint(Canvas canvas, Size size) {
    final axis = frame.axis;
    final past = axis.xOf(axis.duration);
    if (past < size.width) {
      canvas.drawRect(
        Rect.fromLTRB(past, 0, size.width, size.height),
        Paint()..color = OrblitColors.ground.withValues(alpha: 0.6),
      );
    }
    _grid(canvas, size);

    final parts = curvesOf(channel.kind);
    for (var part = 0; part < parts; part++) {
      final colour = parts == 1 ? OrblitColors.ember : _partColours[part];
      double yAt(double x) =>
          frame.yOf(partOf(channel.valueAt(axis.atOf(x)), part));
      final start = axis.xOf(0);
      final end = math.min(size.width, axis.xOf(axis.span));
      final path = Path()..moveTo(start, yAt(start));
      for (var x = start + 2; x <= end + 1; x += 2) {
        path.lineTo(x, yAt(x));
      }
      canvas.drawPath(
        path,
        Paint()
          ..color = colour
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.5,
      );
    }

    final last = channel.keys.length - 1;
    for (final index in picked) {
      for (var part = 0; part < parts; part++) {
        final key = channel.keys[index];
        final origin = frame.at(key.at, partOf(key.value, part));
        for (final side in KeySide.values) {
          if (side == KeySide.arriving && index == 0) continue;
          if (side == KeySide.leaving && index == last) continue;
          final end = _CurveViewState._handle(
            frame,
            channel,
            index,
            part,
            side,
          );
          canvas
            ..drawLine(origin, end, Paint()..color = OrblitColors.inkMid)
            ..drawCircle(end, 3, Paint()..color = OrblitColors.ink);
        }
      }
    }

    for (final (index, key) in channel.keys.indexed) {
      final chosen = picked.contains(index);
      for (var part = 0; part < parts; part++) {
        final point = frame.at(key.at, partOf(key.value, part));
        canvas
          ..drawCircle(
            point,
            chosen ? 4.5 : 3.5,
            Paint()..color = chosen ? OrblitColors.ember : OrblitColors.ink,
          )
          ..drawCircle(
            point,
            chosen ? 4.5 : 3.5,
            Paint()
              ..color = OrblitColors.ground
              ..style = PaintingStyle.stroke,
          );
      }
    }

    final x = axis.xOf(at).roundToDouble() + 0.5;
    canvas.drawLine(
      Offset(x, 0),
      Offset(x, size.height),
      Paint()
        ..color = OrblitColors.ember
        ..strokeWidth = 1.5,
    );

    if (parts > 1) _legend(canvas, size, parts);
  }

  /// A line every second and a line at every round value, with the values
  /// written against the left edge.
  void _grid(Canvas canvas, Size size) {
    final axis = frame.axis;
    final firm = Paint()..color = OrblitColors.line;
    final soft = Paint()..color = OrblitColors.lineSoft;
    if (rate > 0) {
      for (var second = 0; second <= axis.span; second++) {
        final x = axis.xOf(second.toDouble()).roundToDouble() + 0.5;
        canvas.drawLine(Offset(x, 0), Offset(x, size.height), firm);
      }
    }

    final step = _niceStep((frame.high - frame.low) / (size.height / 36));
    if (step <= 0 || !step.isFinite) return;
    for (
      var value = (frame.low / step).ceil() * step;
      value <= frame.high;
      value += step
    ) {
      final y = frame.yOf(value).roundToDouble() + 0.5;
      canvas.drawLine(
        Offset(0, y),
        Offset(size.width, y),
        value.abs() < step / 2 ? firm : soft,
      );
      final text = TextPainter(
        text: TextSpan(
          text: _valueLabel(value, step),
          style: OrblitText.caption.copyWith(fontSize: 10),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      text.paint(canvas, Offset(3, y - text.height - 1));
    }
  }

  void _legend(Canvas canvas, Size size, int parts) {
    var x = size.width - Space.sm;
    for (var part = parts - 1; part >= 0; part--) {
      final text = TextPainter(
        text: TextSpan(
          text: const ['X', 'Y', 'Z'][part],
          style: OrblitText.caption.copyWith(
            fontSize: 10,
            color: _partColours[part],
            fontWeight: FontWeight.w600,
          ),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      x -= text.width;
      text.paint(canvas, Offset(x, Space.xs));
      x -= Space.xs;
    }
  }

  /// The round number nearest above [rough]: 1, 2 or 5 of a power of ten.
  static double _niceStep(double rough) {
    if (rough <= 0 || !rough.isFinite) return 0;
    final power = math.pow(10, (math.log(rough) / math.ln10).floor());
    for (final times in const [1, 2, 5, 10]) {
      if (times * power >= rough) return (times * power).toDouble();
    }
    return (10 * power).toDouble();
  }

  static String _valueLabel(double value, double step) {
    final decimals = step >= 1 ? 0 : (-math.log(step) / math.ln10).ceil();
    final text = value.toStringAsFixed(decimals.clamp(0, 6));
    return text == '-0' ? '0' : text;
  }

  @override
  bool shouldRepaint(_CurvePainter old) => true;
}

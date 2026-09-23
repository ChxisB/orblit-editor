import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:orblit_motion/orblit_motion.dart' hide Key;

import '../theme/orblit_theme.dart';
import 'clip_bench.dart';
import 'clip_edits.dart';

// The timeline's ruler and dope sheet: time across, one row a channel, and
// every key a diamond on its row. What a key is worth is the curve view's
// business; here it is only where and when.

/// How tall a row of the timeline is, on the track list and the sheet alike.
const double rowHeight = 22;

/// Where a moment of a clip is drawn across a strip [width] wide.
///
/// The whole clip always fits: a clip is seconds long, not minutes, and one
/// that fits is one nobody has to scroll to find the end of. Keys left past
/// the end by a clip made shorter stretch it, so they can still be seen and
/// dragged back.
class TimeAxis {
  TimeAxis(ClipDocument clip, {required this.width})
    : duration = clip.duration,
      span = math.max(clip.duration, _lastKeyOf(clip));

  final double duration;

  /// From the start to whichever is later, the end or the last key.
  final double span;

  final double width;

  /// Room at each end, so a key on the first or last frame is not cut in half.
  static const double margin = 10;

  double get perSecond =>
      span <= 0 ? 0 : math.max(width - margin * 2, 1) / span;

  double xOf(double at) => margin + at * perSecond;

  double atOf(double x) =>
      perSecond == 0 ? 0 : ((x - margin) / perSecond).clamp(0.0, span);

  static double _lastKeyOf(ClipDocument clip) {
    var last = 0.0;
    for (final channel in clip.channels) {
      if (channel.keys.isNotEmpty) last = math.max(last, channel.keys.last.at);
    }
    return last;
  }
}

/// One line of the timeline: what a clip moves, or one channel of it.
class TimelineRow {
  TimelineRow.heading(this.target) : channel = null;

  TimelineRow.channel(ClipChannel<Object> this.channel)
    : target = channel.target;

  final String target;

  /// Null on a heading.
  final ClipChannel<Object>? channel;

  bool get isHeading => channel == null;
}

/// [clip]'s rows: each thing it moves, in the order it first moves it, with
/// its channels under it.
List<TimelineRow> rowsOf(ClipDocument clip) {
  final byTarget = <String, List<ClipChannel<Object>>>{};
  for (final channel in clip.channels) {
    (byTarget[channel.target] ??= []).add(channel);
  }
  return [
    for (final MapEntry(key: target, value: channels) in byTarget.entries) ...[
      TimelineRow.heading(target),
      for (final channel in channels) TimelineRow.channel(channel),
    ],
  ];
}

/// How many frames apart lines can be drawn at [perSecond] pixels a second
/// without coming closer than [gap] pixels.
int frameStep(double perSecond, double rate, {double gap = 8}) {
  if (rate <= 0 || perSecond <= 0) return 1;
  const steps = [1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 1200, 3000];
  for (final step in steps) {
    if (step / rate * perSecond >= gap) return step;
  }
  return steps.last;
}

/// Seconds as few figures as say them: 1, 0.5, 0.25.
String secondsLabel(double seconds) {
  final text = seconds.toStringAsFixed(2);
  return text.contains('.')
      ? text.replaceFirst(RegExp(r'0+$'), '').replaceFirst(RegExp(r'\.$'), '')
      : text;
}

/// The ruler over the sheet, which is where the playhead is dragged.
///
/// Scrubbing stops playback and puts the playhead on a frame: a key goes
/// where the playhead is, and one between frames would be a key nobody could
/// land on again.
class TimelineRuler extends StatelessWidget {
  const TimelineRuler({super.key, required this.bench, this.onFocus});

  final ClipBench bench;

  /// Called on any touch, so the panel's keys go to the panel.
  final VoidCallback? onFocus;

  @override
  Widget build(BuildContext context) {
    final clip = bench.clip;
    if (clip == null) return const SizedBox.shrink();
    return LayoutBuilder(
      builder: (context, constraints) {
        final axis = TimeAxis(clip, width: constraints.maxWidth);
        void scrub(double x) {
          onFocus?.call();
          bench
            ..playing = false
            ..at = snapped(axis.atOf(x), clip.rate);
        }

        return GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapDown: (details) => scrub(details.localPosition.dx),
          onHorizontalDragStart: (details) => scrub(details.localPosition.dx),
          onHorizontalDragUpdate: (details) =>
              scrub(details.localPosition.dx),
          child: CustomPaint(
            size: Size(constraints.maxWidth, constraints.maxHeight),
            painter: _RulerPainter(axis: axis, rate: clip.rate, at: bench.at),
          ),
        );
      },
    );
  }
}

class _RulerPainter extends CustomPainter {
  _RulerPainter({required this.axis, required this.rate, required this.at});

  final TimeAxis axis;
  final double rate;
  final double at;

  @override
  void paint(Canvas canvas, Size size) {
    final past = axis.xOf(axis.duration);
    if (past < size.width) {
      canvas.drawRect(
        Rect.fromLTRB(past, 0, size.width, size.height),
        Paint()..color = OrblitColors.ground,
      );
    }

    final tick = frameStep(axis.perSecond, rate, gap: 6);
    final label = frameStep(axis.perSecond, rate, gap: 48);
    final line = Paint()
      ..color = OrblitColors.line
      ..strokeWidth = 1;
    final frames = rate <= 0 ? 0 : (axis.span * rate).floor();
    for (var frame = 0; frame <= frames; frame += tick) {
      final x = axis.xOf(frame / rate).roundToDouble() + 0.5;
      final long = frame % label == 0;
      canvas.drawLine(
        Offset(x, size.height - (long ? 9 : 4)),
        Offset(x, size.height),
        line,
      );
      if (!long) continue;
      final text = TextPainter(
        text: TextSpan(
          text: secondsLabel(frame / rate),
          style: OrblitText.caption.copyWith(fontSize: 10),
        ),
        textDirection: TextDirection.ltr,
      )..layout();
      text.paint(canvas, Offset(x + 3, 2));
    }

    canvas.drawLine(
      Offset(0, size.height - 0.5),
      Offset(size.width, size.height - 0.5),
      Paint()..color = OrblitColors.lineSoft,
    );
    _playhead(canvas, size, axis.xOf(at));
  }

  static void _playhead(Canvas canvas, Size size, double x) {
    final paint = Paint()..color = OrblitColors.ember;
    canvas
      ..drawPath(
        Path()
          ..moveTo(x - 5, size.height - 10)
          ..lineTo(x + 5, size.height - 10)
          ..lineTo(x, size.height - 4)
          ..close(),
        paint,
      )
      ..drawLine(
        Offset(x, size.height - 5),
        Offset(x, size.height),
        paint..strokeWidth = 1.5,
      );
  }

  @override
  bool shouldRepaint(_RulerPainter old) =>
      old.at != at ||
      old.rate != rate ||
      old.axis.span != axis.span ||
      old.axis.duration != axis.duration ||
      old.axis.width != axis.width;
}

/// The keys of every channel of the shown clip, one row each.
///
/// A click picks a key, shift adds to what is picked, and a drag from empty
/// space picks everything it boxes. Dragging a key drags every picked key,
/// frame by frame, as one step to undo. A heading row shows a diamond
/// wherever any channel under it has a key, and picking one picks them all.
class DopeSheet extends StatefulWidget {
  const DopeSheet({
    super.key,
    required this.bench,
    required this.rows,
    required this.height,
    this.onFocus,
  });

  final ClipBench bench;
  final List<TimelineRow> rows;

  /// At least as tall as the rows, so empty space below them still takes a
  /// click or a box.
  final double height;

  final VoidCallback? onFocus;

  @override
  State<DopeSheet> createState() => _DopeSheetState();
}

class _DopeSheetState extends State<DopeSheet> {
  /// The drag in progress, if it is one of keys.
  ({
    ClipDocument start,
    Set<KeyRef> keys,
    double from,
    Object gesture,
    bool moved,
  })?
  _dragging;

  /// The box in progress, and what was picked before it.
  ({Offset from, Offset to, Set<KeyRef> before})? _box;

  ClipBench get _bench => widget.bench;

  /// The keys under [at], or an empty set.
  Set<KeyRef> _hit(TimeAxis axis, Offset at) {
    final index = (at.dy / rowHeight).floor();
    if (index < 0 || index >= widget.rows.length) return const {};
    final row = widget.rows[index];

    var best = 7.0;
    double? time;
    for (final channel in _channelsOf(row)) {
      for (final key in channel.keys) {
        final distance = (axis.xOf(key.at) - at.dx).abs();
        if (distance <= best) {
          best = distance;
          time = key.at;
        }
      }
    }
    if (time == null) return const {};
    return {
      for (final channel in _channelsOf(row))
        for (final (place, key) in channel.keys.indexed)
          if ((key.at - time).abs() < sameMoment)
            (channel: addressOf(channel), index: place),
    };
  }

  /// The channels a row stands for: its own, or all of a heading's.
  Iterable<ClipChannel<Object>> _channelsOf(TimelineRow row) {
    final channel = row.channel;
    if (channel != null) return [channel];
    return [
      for (final one in widget.rows)
        if (!one.isHeading && one.target == row.target) one.channel!,
    ];
  }

  Set<KeyRef> _boxed(TimeAxis axis, Rect box) {
    final picked = <KeyRef>{};
    for (final (index, row) in widget.rows.indexed) {
      final top = index * rowHeight;
      if (top + rowHeight < box.top || top > box.bottom) continue;
      for (final channel in _channelsOf(row)) {
        for (final (place, key) in channel.keys.indexed) {
          final x = axis.xOf(key.at);
          if (x >= box.left && x <= box.right) {
            picked.add((channel: addressOf(channel), index: place));
          }
        }
      }
    }
    return picked;
  }

  void _tap(TimeAxis axis, Offset at) {
    widget.onFocus?.call();
    final hit = _hit(axis, at);
    final adding = HardwareKeyboard.instance.isShiftPressed;
    if (hit.isEmpty) {
      if (!adding) _bench.selection = const {};
      return;
    }
    if (adding) {
      final all = _bench.selection.containsAll(hit);
      _bench.selection = all
          ? _bench.selection.difference(hit)
          : _bench.selection.union(hit);
    } else {
      _bench.selection = hit;
    }
    if (hit.length == 1) _bench.curve = hit.single.channel;
  }

  void _start(TimeAxis axis, Offset at) {
    widget.onFocus?.call();
    final clip = _bench.clip;
    if (clip == null) return;
    final hit = _hit(axis, at);
    if (hit.isEmpty) {
      _box = (
        from: at,
        to: at,
        before: HardwareKeyboard.instance.isShiftPressed
            ? _bench.selection
            : const {},
      );
      setState(() {});
      return;
    }
    if (!_bench.selection.containsAll(hit)) {
      _bench.selection = HardwareKeyboard.instance.isShiftPressed
          ? _bench.selection.union(hit)
          : hit;
    }
    _dragging = (
      start: clip,
      keys: _bench.selection,
      from: at.dx,
      gesture: Object(),
      moved: false,
    );
  }

  void _update(TimeAxis axis, Offset at) {
    if (_box case final box?) {
      _box = (from: box.from, to: at, before: box.before);
      _bench.selection = box.before.union(
        _boxed(axis, Rect.fromPoints(box.from, at)),
      );
      setState(() {});
      return;
    }
    final drag = _dragging;
    if (drag == null || axis.perSecond == 0) return;
    final moved = movedKeys(
      drag.start,
      drag.keys,
      (at.dx - drag.from) / axis.perSecond,
    );
    // Still on the frame it started on: nothing to undo yet.
    if (identical(moved.clip, drag.start) && !drag.moved) return;
    _dragging = (
      start: drag.start,
      keys: drag.keys,
      from: drag.from,
      gesture: drag.gesture,
      moved: true,
    );
    _bench.edit(
      'Move keys',
      (_) => moved.clip,
      keys: identical(moved.clip, drag.start) ? drag.keys : moved.keys,
      gesture: drag.gesture,
      onlyMoves: true,
    );
  }

  void _end() {
    if (_box != null) setState(() => _box = null);
    if (_dragging?.moved ?? false) _bench.history.seal();
    _dragging = null;
  }

  @override
  Widget build(BuildContext context) {
    final clip = _bench.clip;
    if (clip == null) return const SizedBox.shrink();
    return LayoutBuilder(
      builder: (context, constraints) {
        final axis = TimeAxis(clip, width: constraints.maxWidth);
        return GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapUp: (details) => _tap(axis, details.localPosition),
          onPanStart: (details) => _start(axis, details.localPosition),
          onPanUpdate: (details) => _update(axis, details.localPosition),
          onPanEnd: (_) => _end(),
          onPanCancel: _end,
          child: CustomPaint(
            size: Size(constraints.maxWidth, widget.height),
            painter: _SheetPainter(
              axis: axis,
              rate: clip.rate,
              rows: widget.rows,
              at: _bench.at,
              selection: _bench.selection,
              box: switch (_box) {
                final box? => Rect.fromPoints(box.from, box.to),
                null => null,
              },
            ),
          ),
        );
      },
    );
  }
}

class _SheetPainter extends CustomPainter {
  _SheetPainter({
    required this.axis,
    required this.rate,
    required this.rows,
    required this.at,
    required this.selection,
    required this.box,
  });

  final TimeAxis axis;
  final double rate;
  final List<TimelineRow> rows;
  final double at;
  final Set<KeyRef> selection;
  final Rect? box;

  @override
  void paint(Canvas canvas, Size size) {
    for (final (index, row) in rows.indexed) {
      final top = index * rowHeight;
      if (row.isHeading) {
        canvas.drawRect(
          Rect.fromLTWH(0, top, size.width, rowHeight),
          Paint()..color = OrblitColors.raised,
        );
      }
      canvas.drawLine(
        Offset(0, top + rowHeight - 0.5),
        Offset(size.width, top + rowHeight - 0.5),
        Paint()..color = OrblitColors.lineSoft,
      );
    }

    final past = axis.xOf(axis.duration);
    if (past < size.width) {
      canvas.drawRect(
        Rect.fromLTRB(past, 0, size.width, size.height),
        Paint()..color = OrblitColors.ground.withValues(alpha: 0.6),
      );
    }

    // A line a frame where there is room, and a stronger one a second.
    if (rate > 0) {
      final step = frameStep(axis.perSecond, rate);
      final perSecond = rate.round();
      final soft = Paint()..color = OrblitColors.lineSoft;
      final firm = Paint()..color = OrblitColors.line;
      for (var frame = 0; frame <= axis.span * rate; frame += step) {
        final x = axis.xOf(frame / rate).roundToDouble() + 0.5;
        final second = perSecond > 0 && frame % perSecond == 0;
        canvas.drawLine(
          Offset(x, 0),
          Offset(x, size.height),
          second ? firm : soft,
        );
      }
    }

    for (final (index, row) in rows.indexed) {
      final middle = index * rowHeight + rowHeight / 2;
      if (row.channel case final channel?) {
        final address = addressOf(channel);
        for (final (place, key) in channel.keys.indexed) {
          _diamond(
            canvas,
            Offset(axis.xOf(key.at), middle),
            selection.contains((channel: address, index: place)),
            size: 4.5,
          );
        }
      } else {
        _summary(canvas, row.target, middle);
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

    if (box case final box?) {
      canvas
        ..drawRect(box, Paint()..color = OrblitColors.emberWash)
        ..drawRect(
          box,
          Paint()
            ..color = OrblitColors.ember
            ..style = PaintingStyle.stroke,
        );
    }
  }

  /// A diamond wherever any channel of [target] has a key, picked when every
  /// key there is.
  void _summary(Canvas canvas, String target, double middle) {
    final moments = <double, bool>{};
    for (final row in rows) {
      final channel = row.channel;
      if (channel == null || channel.target != target) continue;
      final address = addressOf(channel);
      for (final (place, key) in channel.keys.indexed) {
        final picked = selection.contains((channel: address, index: place));
        final same = moments.keys.where(
          (at) => (at - key.at).abs() < sameMoment,
        );
        final at = same.isEmpty ? key.at : same.first;
        moments[at] = (moments[at] ?? true) && picked;
      }
    }
    for (final MapEntry(key: at, value: picked) in moments.entries) {
      _diamond(canvas, Offset(axis.xOf(at), middle), picked, size: 3.5);
    }
  }

  static void _diamond(
    Canvas canvas,
    Offset at,
    bool picked, {
    required double size,
  }) {
    final path = Path()
      ..moveTo(at.dx, at.dy - size)
      ..lineTo(at.dx + size, at.dy)
      ..lineTo(at.dx, at.dy + size)
      ..lineTo(at.dx - size, at.dy)
      ..close();
    canvas
      ..drawPath(
        path,
        Paint()..color = picked ? OrblitColors.ember : OrblitColors.inkMid,
      )
      ..drawPath(
        path,
        Paint()
          ..color = OrblitColors.ground
          ..style = PaintingStyle.stroke,
      );
  }

  @override
  bool shouldRepaint(_SheetPainter old) =>
      old.at != at ||
      old.rate != rate ||
      !identical(old.rows, rows) ||
      !identical(old.selection, selection) ||
      old.box != box ||
      old.axis.span != axis.span ||
      old.axis.width != axis.width;
}

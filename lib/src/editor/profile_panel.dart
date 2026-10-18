import 'dart:async';

import 'package:flutter/material.dart';
import 'package:orblit_profile/orblit_profile.dart';

import '../theme/orblit_theme.dart';

/// Where a frame went, read from the engine's [Profiler].
///
/// Frame times here are the gaps between drawn frames. An editor that is
/// sitting still draws none, so a long gap is rest and not slowness. Drag in
/// the scene or play the game for steady figures.
class ProfilePanel extends StatefulWidget {
  const ProfilePanel({
    super.key,
    required this.profiler,
    this.every = const Duration(milliseconds: 500),
  });

  final Profiler profiler;

  /// How often the figures are read again. Not every frame: a panel that
  /// rebuilt on each one would add to the cost it is there to show.
  final Duration every;

  @override
  State<ProfilePanel> createState() => _ProfilePanelState();
}

class _ProfilePanelState extends State<ProfilePanel> {
  late Map<String, Object?> _shot = widget.profiler.snapshot();
  Timer? _timer;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(widget.every, (_) {
      setState(() => _shot = widget.profiler.snapshot());
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final frames = _shot['frames'] as Map<String, Object?>?;
    if (frames == null) {
      return const Center(
        child: Text('Profiling is off.', style: OrblitText.caption),
      );
    }
    final spans = (_shot['spans']! as Map<String, Object?>).entries.toList()
      ..sort((a, b) => _mean(b.value).compareTo(_mean(a.value)));
    final gauges = (_shot['gauges']! as Map<String, Object?>).entries;
    final counts = (_shot['counts']! as Map<String, Object?>).entries;
    return Container(
      color: OrblitColors.surface,
      child: ListView(
        padding: const EdgeInsets.all(Space.sm),
        children: [
          _Line('Frames', _framesText(frames)),
          for (final MapEntry(:key, :value) in spans)
            _Line(key, _spanText(value! as Map<String, Object?>)),
          for (final MapEntry(:key, :value) in gauges) _Line(key, '$value'),
          for (final MapEntry(:key, :value) in counts) _Line(key, '$value'),
          _Line('Memory', _heapText(_shot['heap']! as Map<String, Object?>)),
        ],
      ),
    );
  }
}

double _mean(Object? span) =>
    (span! as Map<String, Object?>)['meanMs']! as double;

String _ms(Object? value) => '${(value! as double).toStringAsFixed(1)} ms';

String _framesText(Map<String, Object?> frames) {
  final fps = frames['fps'] as double?;
  if (fps == null) return 'none yet';
  return '${fps.round()} fps · mean ${_ms(frames['meanMs'])} · '
      'p95 ${_ms(frames['p95Ms'])} · max ${_ms(frames['maxMs'])}';
}

String _spanText(Map<String, Object?> span) =>
    'mean ${_ms(span['meanMs'])} · max ${_ms(span['maxMs'])}';

String _megabytes(num bytes) => (bytes / 1048576).toStringAsFixed(1);

String _heapText(Map<String, Object?> heap) {
  final samples = heap['samples']! as List<Object?>;
  if (samples.isEmpty) return 'not read';
  final bytes = (samples.last! as List<Object?>)[1]! as double;
  final slope = heap['bytesPerMinute'] as double?;
  final rate = slope == null
      ? ''
      : ' · ${slope >= 0 ? '+' : '−'}${_megabytes(slope.abs())} MB/min';
  return '${_megabytes(bytes)} MB$rate';
}

/// One figure and what it is.
class _Line extends StatelessWidget {
  const _Line(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 2),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 96,
          child: Text(
            label,
            style: OrblitText.caption,
            overflow: TextOverflow.ellipsis,
          ),
        ),
        Expanded(child: Text(value, style: OrblitText.monoValue)),
      ],
    ),
  );
}

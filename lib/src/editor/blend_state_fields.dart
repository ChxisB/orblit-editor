import 'package:flutter/material.dart';
import 'package:orblit_motion/orblit_motion.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'blend_bench.dart';
import 'blend_fields.dart';
import 'blend_model.dart';
import 'inspector.dart' show FieldRow, PickRow;

/// The fields of one state: what it plays, how, and the changes that leave
/// or enter it.
final class BlendStateFields extends StatelessWidget {
  const BlendStateFields({
    super.key,
    required this.blend,
    required this.name,
    required this.edits,
    required this.clips,
  });

  final BlendDocument blend;
  final String name;
  final BlendEdits edits;
  final List<String> clips;

  @override
  Widget build(BuildContext context) {
    final state = blend.writtenStates
        .where((one) => one.name == name)
        .firstOrNull;
    if (state == null) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OrblitSection(
          title: 'State',
          child: _StateIdentity(blend: blend, name: name, edits: edits),
        ),
        OrblitSection(
          title: 'Plays',
          child: _StatePlays(
            blend: blend,
            state: state,
            edits: edits,
            clips: clips,
          ),
        ),
        OrblitSection(
          title: 'Playback',
          child: _StatePlayback(state: state, edits: edits),
        ),
        OrblitSection(
          title: 'Changes',
          child: _StateChanges(blend: blend, name: name, edits: edits),
        ),
      ],
    );
  }
}

final class _StateIdentity extends StatelessWidget {
  const _StateIdentity({
    required this.blend,
    required this.name,
    required this.edits,
  });

  final BlendDocument blend;
  final String name;
  final BlendEdits edits;

  @override
  Widget build(BuildContext context) {
    final isStart = blend.writtenStart == name;
    return Column(
      children: [
        FieldRow(
          label: 'Name',
          child: ValueField(
            value: name,
            onChanged: (text) => edits.typed(
              'Rename state',
              (blend) => blend.withStateRenamed(name, text.trim()),
              pick: StatePicked(text.trim()),
            ),
            onDone: edits.done,
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(vertical: Space.xs),
          child: OrblitButtonRow(
            buttons: [
              OrblitButton(
                expand: true,
                label: isStart ? 'Starts here' : 'Start here',
                tone: ButtonTone.quiet,
                onPressed: isStart
                    ? null
                    : () => edits.run(
                        'Set start state',
                        (b) => b.withStart(name),
                      ),
              ),
              OrblitButton(
                expand: true,
                label: 'Delete',
                tone: ButtonTone.quiet,
                onPressed: blend.writtenStates.length < 2
                    ? null
                    : () => edits.run(
                        'Delete state',
                        (b) => b.withStateRemoved(name),
                        pick: const NothingPicked(),
                      ),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

/// What a state plays: one clip, a line of clips along an input, or
/// something the editor reads and leaves to the file.
final class _StatePlays extends StatelessWidget {
  const _StatePlays({
    required this.blend,
    required this.state,
    required this.edits,
    required this.clips,
  });

  final BlendDocument blend;
  final BlendState state;
  final BlendEdits edits;
  final List<String> clips;

  void _plays(String label, BlendSource plays, {bool typed = false}) {
    BlendDocument change(BlendDocument b) =>
        b.withState(state.name, (s) => s.copyWith(plays: plays));
    if (typed) {
      edits.typed(label, change);
    } else {
      edits.run(label, change);
    }
  }

  @override
  Widget build(BuildContext context) {
    final plays = state.plays;
    if (!canEditSource(plays)) {
      return Padding(
        padding: const EdgeInsets.only(bottom: Space.sm),
        child: Text(
          '${describeSource(plays)}. This one is edited in the file.',
          style: OrblitText.caption,
        ),
      );
    }
    final line = lineOfClips(plays);
    return Column(
      children: [
        PickRow<bool>(
          label: 'Plays',
          shown: line == null ? 'One clip' : 'A mix along an input',
          options: const {false: 'One clip', true: 'A mix along an input'},
          onSelect: (mix) {
            if (mix == (line != null)) return;
            _switch(mix, plays);
          },
        ),
        if (plays is BlendClip)
          BlendNameRow(
            label: 'Clip',
            value: plays.clip,
            choices: clips,
            onChanged: (name) {
              if (name.isNotEmpty) {
                _plays('Change clip', BlendClip(name), typed: true);
              }
            },
            onDone: edits.done,
          ),
        if (line != null)
          _Line(
            line: line,
            inputs: blend.inputs.keys.toList(),
            clips: clips,
            edits: edits,
            onLine: (next, label) => _plays(label, next, typed: true),
          ),
      ],
    );
  }

  void _switch(bool mix, BlendSource plays) {
    if (!mix) {
      edits.run('Play one clip', (b) {
        return b.withState(
          state.name,
          (s) => s.copyWith(plays: BlendClip(firstClipOf(plays))),
        );
      });
      return;
    }
    edits.run('Mix clips', (b) {
      final input = b.inputs.keys.firstOrNull ?? 'speed';
      return b
          .withInputAdded(input)
          .withState(
            state.name,
            (s) => s.copyWith(plays: lineFrom(firstClipOf(plays), input)),
          );
    });
  }
}

/// The points of a line of clips: where along the input each is reached
/// and what plays there.
class _Line extends StatefulWidget {
  const _Line({
    required this.line,
    required this.inputs,
    required this.clips,
    required this.edits,
    required this.onLine,
  });

  final BlendLine line;
  final List<String> inputs;
  final List<String> clips;
  final BlendEdits edits;

  /// Told of the line a change makes, and what to call the step.
  final void Function(BlendLine line, String label) onLine;

  @override
  State<_Line> createState() => _LineState();
}

class _LineState extends State<_Line> {
  /// What each row is keyed by, handed on from a point to the one an edit
  /// makes of it. Typing a position can put a point past another, and the
  /// row being typed in has to move with it rather than hand its focus to
  /// the point that took its place.
  final Expando<Object> _rows = Expando();

  Object _rowOf(LinePoint point) => _rows[point] ??= Object();

  void _change(int index, {double? at, String? clip}) {
    final old = widget.line.points[index];
    final next = linePointChanged(widget.line, index, at: at, clip: clip);
    for (final point in next.points) {
      if (!widget.line.points.any((one) => identical(one, point))) {
        _rows[point] = _rowOf(old);
      }
    }
    widget.onLine(next, 'Move a point');
  }

  @override
  Widget build(BuildContext context) {
    final line = widget.line;
    return Column(
      children: [
        BlendNameRow(
          label: 'Along',
          value: line.input,
          choices: widget.inputs,
          onChanged: (name) {
            if (name.isNotEmpty) {
              widget.onLine(lineAlong(line, name), 'Change input');
            }
          },
          onDone: widget.edits.done,
        ),
        for (final (index, point) in line.points.indexed)
          _PointRow(
            key: ObjectKey(_rowOf(point)),
            point: point,
            clips: widget.clips,
            canRemove: line.points.length > 1,
            onAt: (at) => _change(index, at: at),
            onClip: (clip) => _change(index, clip: clip),
            onRemove: () =>
                widget.onLine(linePointRemoved(line, index), 'Remove a point'),
            onDone: widget.edits.done,
          ),
        Align(
          alignment: Alignment.centerLeft,
          child: OrblitButton(
            label: 'Add a point',
            tone: ButtonTone.quiet,
            onPressed: () => widget.onLine(linePointAdded(line), 'Add a point'),
          ),
        ),
      ],
    );
  }
}

final class _PointRow extends StatelessWidget {
  const _PointRow({
    super.key,
    required this.point,
    required this.clips,
    required this.canRemove,
    required this.onAt,
    required this.onClip,
    required this.onRemove,
    required this.onDone,
  });

  final LinePoint point;
  final List<String> clips;
  final bool canRemove;
  final ValueChanged<double> onAt;
  final ValueChanged<String> onClip;
  final VoidCallback onRemove;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) {
    final plays = point.plays;
    return Container(
      margin: const EdgeInsets.only(top: Space.xs),
      padding: const EdgeInsets.only(left: Space.sm),
      decoration: const BoxDecoration(
        border: Border(left: BorderSide(color: OrblitColors.line)),
      ),
      child: Column(
        children: [
          BlendNumberRow(
            label: 'At',
            value: point.at,
            onChanged: onAt,
            onDone: onDone,
            trailing: canRemove
                ? BlendIconAction(
                    icon: Icons.close,
                    tooltip: 'Take this point out',
                    onTap: onRemove,
                  )
                : null,
          ),
          BlendNameRow(
            label: 'Clip',
            value: plays is BlendClip ? plays.clip : '',
            choices: clips,
            onChanged: (name) {
              if (name.isNotEmpty) onClip(name);
            },
            onDone: onDone,
          ),
        ],
      ),
    );
  }
}

String _whenDoneName(WhenDone? done) => switch (done) {
  null => "The clip's own",
  WhenDone.hold => 'Hold the last frame',
  WhenDone.loop => 'Loop',
  WhenDone.bounce => 'Bounce',
  WhenDone.release => 'Let go',
};

final class _StatePlayback extends StatelessWidget {
  const _StatePlayback({required this.state, required this.edits});

  final BlendState state;
  final BlendEdits edits;

  @override
  Widget build(BuildContext context) {
    final name = state.name;
    return Column(
      children: [
        BlendNumberRow(
          label: 'Speed',
          value: state.speed,
          unit: 'x',
          onChanged: (speed) => edits.typed(
            'Change speed',
            (b) => b.withState(name, (s) => s.copyWith(speed: speed)),
          ),
          onDone: edits.done,
        ),
        PickRow<WhenDone?>(
          label: 'At the end',
          shown: _whenDoneName(state.whenDone),
          options: {
            for (final done in <WhenDone?>[null, ...WhenDone.values])
              done: _whenDoneName(done),
          },
          onSelect: (done) => edits.run(
            'Change what happens at the end',
            (b) => b.withState(
              name,
              (s) => done == null
                  ? s.copyWith(clearWhenDone: true)
                  : s.copyWith(whenDone: done),
            ),
          ),
        ),
        BlendNameRow(
          label: 'Sync marks',
          value: state.sync.join(', '),
          choices: const [],
          onChanged: (text) => edits.typed(
            'Change sync marks',
            (b) => b.withState(
              name,
              (s) => s.copyWith(
                sync: [
                  for (final part in text.split(','))
                    if (part.trim().isNotEmpty) part.trim(),
                ],
              ),
            ),
          ),
          onDone: edits.done,
        ),
      ],
    );
  }
}

/// The changes that leave this state or enter it, one row each.
final class _StateChanges extends StatelessWidget {
  const _StateChanges({
    required this.blend,
    required this.name,
    required this.edits,
  });

  final BlendDocument blend;
  final String name;
  final BlendEdits edits;

  @override
  Widget build(BuildContext context) {
    final mine = [
      for (final (index, change) in blend.writtenChanges.indexed)
        if (stateOwning(blend, change.to) == name ||
            (change.from != null && stateOwning(blend, change.from!) == name))
          index,
    ];
    final other = blend.writtenStates
        .map((state) => state.name)
        .firstWhere((one) => one != name, orElse: () => name);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final index in mine)
          _ChangeRow(
            key: ValueKey('row/$index'),
            change: blend.writtenChanges[index],
            onTap: () => edits.bench.pick = ChangePicked(index),
          ),
        Align(
          alignment: Alignment.centerLeft,
          child: OrblitButton(
            label: 'Add a change',
            tone: ButtonTone.quiet,
            // A change to the state it leaves is a loop, which is drawn
            // from the graph; this one goes to another state.
            onPressed: other == name
                ? null
                : () => edits.run(
                    'Add change',
                    (b) => b.withChangeAdded(name, other),
                    pick: ChangePicked(blend.writtenChanges.length),
                  ),
          ),
        ),
      ],
    );
  }
}

final class _ChangeRow extends StatelessWidget {
  const _ChangeRow({super.key, required this.change, required this.onTap});

  final BlendChange change;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => GestureDetector(
    onTap: onTap,
    behavior: HitTestBehavior.opaque,
    child: Padding(
      padding: const EdgeInsets.symmetric(vertical: 5),
      child: Text(
        '${change.from ?? 'Any state'} to ${change.to}, '
        '${describeCondition(change.when)}',
        maxLines: 2,
        overflow: TextOverflow.ellipsis,
        style: OrblitText.label,
      ),
    ),
  );
}

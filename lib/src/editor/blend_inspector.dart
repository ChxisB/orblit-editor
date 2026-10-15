import 'package:flutter/material.dart' hide Easing;
import 'package:orblit_motion/orblit_motion.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import '../widgets/orblit_switch.dart';
import 'blend_bench.dart';
import 'blend_conditions.dart';
import 'blend_fields.dart';
import 'blend_model.dart';
import 'blend_state_fields.dart';
import 'inspector.dart' show FieldRow, PickRow;

/// The fields of whatever is chosen in the graph: the blend itself when
/// nothing is, a state, or a change.
///
/// [clips] answers the names of the project's clips, which a state can
/// play.
class BlendInspector extends StatefulWidget {
  const BlendInspector({super.key, required this.bench, required this.clips});

  final BlendBench bench;
  final List<String> Function() clips;

  @override
  State<BlendInspector> createState() => _BlendInspectorState();
}

class _BlendInspectorState extends State<BlendInspector> {
  late final BlendEdits _edits = BlendEdits(widget.bench, () {
    // A name typed to nothing is not kept, so the box is put back to what
    // the blend says once it is left.
    if (mounted) setState(() {});
  });

  @override
  Widget build(BuildContext context) {
    final blend = widget.bench.blend;
    if (blend == null) return const SizedBox.shrink();
    final clips = widget.clips();
    final pick = widget.bench.pick;
    return ListView(
      padding: const EdgeInsets.only(bottom: Space.lg),
      children: [
        // Keyed by where the choice stands and not by its name, so choosing
        // another state builds its fields afresh, while a rename keeps the
        // box that is being typed in.
        KeyedSubtree(
          key: ValueKey(_standing(blend, pick)),
          child: switch (pick) {
            NothingPicked() => _BlendFields(
              blend: blend,
              edits: _edits,
              clips: clips,
            ),
            StatePicked(:final name) => BlendStateFields(
              blend: blend,
              name: name,
              edits: _edits,
              clips: clips,
            ),
            ChangePicked(:final index) => _ChangeFields(
              blend: blend,
              index: index,
              edits: _edits,
            ),
          },
        ),
      ],
    );
  }

  static (String, int) _standing(BlendDocument blend, BlendPick pick) =>
      switch (pick) {
        NothingPicked() => ('blend', 0),
        StatePicked(:final name) => (
          'state',
          blend.writtenStates.indexWhere((state) => state.name == name),
        ),
        ChangePicked(:final index) => ('change', index),
      };
}

final class _BlendFields extends StatelessWidget {
  const _BlendFields({
    required this.blend,
    required this.edits,
    required this.clips,
  });

  final BlendDocument blend;
  final BlendEdits edits;
  final List<String> clips;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      OrblitSection(
        title: 'Blend',
        child: Column(
          children: [
            FieldRow(
              label: 'Name',
              child: ValueField(
                value: blend.name,
                onChanged: (text) => edits.typed(
                  'Rename blend',
                  (blend) => blend.withName(text.trim()),
                ),
                onDone: edits.done,
              ),
            ),
            PickRow<String>(
              label: 'Starts in',
              shown: blend.writtenStart,
              options: {
                for (final state in blend.writtenStates) state.name: state.name,
              },
              onSelect: (name) =>
                  edits.run('Set start state', (b) => b.withStart(name)),
            ),
            Padding(
              padding: const EdgeInsets.only(bottom: Space.sm),
              child: Text(
                '${blend.writtenStates.length} states and '
                '${blend.writtenChanges.length} changes. Choose a state or '
                'an arrow to edit it, and drag from a state onto another to '
                'join them.',
                style: OrblitText.caption,
              ),
            ),
          ],
        ),
      ),
      OrblitSection(
        title: 'States',
        child: BlendAddRow(
          label: 'New state',
          hint: 'The clip it plays',
          choices: clips,
          onAdd: (clip) {
            final named = clip.isEmpty ? (clips.firstOrNull ?? '') : clip;
            edits.run(
              'Add state',
              (b) => b.withStateAdded(named),
              pick: StatePicked(
                blend.uniqueStateName(named.isEmpty ? 'clip' : named),
              ),
            );
          },
        ),
      ),
      OrblitSection(
        title: 'Inputs',
        child: Column(
          children: [
            for (final MapEntry(:key, :value) in blend.inputs.entries)
              BlendNumberRow(
                key: ValueKey('input/$key'),
                label: key,
                value: value,
                onChanged: (to) =>
                    edits.typed('Set input $key', (b) => b.withInput(key, to)),
                onDone: edits.done,
                trailing: BlendIconAction(
                  icon: Icons.close,
                  tooltip: 'Take out $key',
                  onTap: () => edits.run(
                    'Remove input $key',
                    (b) => b.withInputRemoved(key),
                  ),
                ),
              ),
            BlendAddRow(
              label: 'New input',
              hint: 'A name to read',
              choices: const [],
              onAdd: (name) =>
                  edits.run('Add input', (b) => b.withInputAdded(name)),
            ),
          ],
        ),
      ),
    ],
  );
}

String _shapeName(Easing shape) => switch (shape) {
  Easing.linear => 'Even',
  Easing.in_ => 'Ease in',
  Easing.out => 'Ease out',
  Easing.inOut => 'Ease in and out',
  Easing.smooth => 'Smooth',
  Easing.back => 'Overshoot',
  Easing.bounce => 'Bounce',
};

final class _ChangeFields extends StatelessWidget {
  const _ChangeFields({
    required this.blend,
    required this.index,
    required this.edits,
  });

  final BlendDocument blend;
  final int index;
  final BlendEdits edits;

  BlendDocument _with(
    BlendDocument b,
    BlendChange Function(BlendChange) change,
  ) => b.withChange(index, change);

  @override
  Widget build(BuildContext context) {
    final change = blend.writtenChanges.elementAtOrNull(index);
    if (change == null) return const SizedBox.shrink();
    final states = {
      for (final state in blend.writtenStates) state.name: state.name,
    };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        OrblitSection(
          title: 'Change',
          child: Column(
            children: [
              PickRow<String>(
                label: 'From',
                shown: change.from ?? 'Any state',
                options: {'': 'Any state', ...states},
                onSelect: (from) => edits.run(
                  'Change where it leaves',
                  (b) => _with(
                    b,
                    (c) => from.isEmpty
                        ? c.copyWith(clearFrom: true)
                        : c.copyWith(from: from),
                  ),
                ),
              ),
              PickRow<String>(
                label: 'To',
                shown: change.to,
                options: states,
                onSelect: (to) => edits.run(
                  'Change where it goes',
                  (b) => _with(b, (c) => c.copyWith(to: to)),
                ),
              ),
              _Order(blend: blend, index: index, edits: edits),
            ],
          ),
        ),
        OrblitSection(
          title: 'When',
          child: ConditionEditor(
            condition: change.when,
            inputs: blend.inputs.keys.toList(),
            onChanged: (when) => edits.run(
              'Change condition',
              (b) => _with(b, (c) => c.copyWith(when: when)),
            ),
            onTyped: (when) => edits.typed(
              'Change condition',
              (b) => _with(b, (c) => c.copyWith(when: when)),
            ),
            onDone: edits.done,
          ),
        ),
        OrblitSection(
          title: 'Fade',
          child: _Fade(change: change, edits: edits, apply: _with),
        ),
      ],
    );
  }
}

/// Where a change stands among the others, which decides which is taken
/// when two could be, and the button that takes it out.
final class _Order extends StatelessWidget {
  const _Order({required this.blend, required this.index, required this.edits});

  final BlendDocument blend;
  final int index;
  final BlendEdits edits;

  @override
  Widget build(BuildContext context) {
    final last = blend.writtenChanges.length - 1;
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: Space.xs),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Number ${index + 1} of ${last + 1}. The first that holds is '
            'the one taken.',
            style: OrblitText.caption,
          ),
          const SizedBox(height: Space.xs),
          OrblitButtonRow(
            buttons: [
              OrblitButton(
                expand: true,
                label: 'Earlier',
                tone: ButtonTone.quiet,
                onPressed: index == 0
                    ? null
                    : () => edits.run(
                        'Move change earlier',
                        (b) => b.withChangeMoved(index, -1),
                        pick: ChangePicked(index - 1),
                      ),
              ),
              OrblitButton(
                expand: true,
                label: 'Later',
                tone: ButtonTone.quiet,
                onPressed: index >= last
                    ? null
                    : () => edits.run(
                        'Move change later',
                        (b) => b.withChangeMoved(index, 1),
                        pick: ChangePicked(index + 1),
                      ),
              ),
              OrblitButton(
                expand: true,
                label: 'Delete',
                tone: ButtonTone.quiet,
                onPressed: () => edits.run(
                  'Delete change',
                  (b) => b.withChangeRemoved(index),
                  pick: const NothingPicked(),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

final class _Fade extends StatelessWidget {
  const _Fade({required this.change, required this.edits, required this.apply});

  final BlendChange change;
  final BlendEdits edits;
  final BlendDocument Function(
    BlendDocument blend,
    BlendChange Function(BlendChange change) change,
  )
  apply;

  @override
  Widget build(BuildContext context) => Column(
    children: [
      BlendNumberRow(
        label: 'Length',
        value: change.fade,
        unit: 's',
        onChanged: (seconds) => edits.typed(
          'Change fade',
          (b) => apply(b, (c) => c.copyWith(fade: seconds < 0 ? 0 : seconds)),
        ),
        onDone: edits.done,
      ),
      PickRow<Easing>(
        label: 'Shape',
        shown: _shapeName(change.shape),
        options: {for (final shape in Easing.values) shape: _shapeName(shape)},
        onSelect: (shape) => edits.run(
          'Change fade shape',
          (b) => apply(b, (c) => c.copyWith(shape: shape)),
        ),
      ),
      _SwitchRow(
        label: 'Keep step',
        tip:
            'Start the next state as far through its lap as this one is, '
            'so a walk turning into a run keeps its feet.',
        value: change.inStep,
        onChanged: (on) => edits.run(
          'Change keep step',
          (b) => apply(b, (c) => c.copyWith(inStep: on)),
        ),
      ),
      _SwitchRow(
        label: 'Freeze pose',
        tip:
            'Hold the pose being left while the next clip plays, and fade '
            'from that.',
        value: change.fromPose,
        onChanged: (on) => edits.run(
          'Change freeze pose',
          (b) => apply(b, (c) => c.copyWith(fromPose: on)),
        ),
      ),
    ],
  );
}

final class _SwitchRow extends StatelessWidget {
  const _SwitchRow({
    required this.label,
    required this.tip,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final String tip;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) => FieldRow(
    label: label,
    child: Align(
      alignment: Alignment.centerLeft,
      child: Tooltip(
        message: tip,
        child: OrblitSwitch(label: label, value: value, onChanged: onChanged),
      ),
    ),
  );
}

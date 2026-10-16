import 'package:flutter/material.dart';
import 'package:orblit_motion/orblit_motion.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'blend_fields.dart';
import 'blend_model.dart';
import 'inspector.dart' show PickRow;

/// What a condition is asked, as a choice of test and then what the test
/// reads.
///
/// [onChanged] is told of a choice, and [onTyped] of a number as it is
/// typed, so the caller can tie a typed run into one undo step and seal it
/// on [onDone].
final class ConditionEditor extends StatelessWidget {
  const ConditionEditor({
    super.key,
    required this.condition,
    required this.inputs,
    required this.onChanged,
    required this.onTyped,
    required this.onDone,
  });

  final BlendCondition condition;

  /// The inputs the blend declares, for the menu beside an input's name.
  final List<String> inputs;

  final ValueChanged<BlendCondition> onChanged;
  final ValueChanged<BlendCondition> onTyped;
  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) {
    final kind = conditionKind(condition);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        PickRow<ConditionKind>(
          label: 'Test',
          shown: kind.label,
          options: {for (final one in ConditionKind.values) one: one.label},
          onSelect: (one) => onChanged(conditionOfKind(one, condition)),
        ),
        if (kind.readsInput)
          BlendNameRow(
            label: 'Input',
            value: conditionInput(condition),
            choices: inputs,
            onChanged: (name) => onTyped(conditionReading(condition, name)),
            onDone: onDone,
          ),
        if (kind case ConditionKind.above || ConditionKind.below)
          BlendNumberRow(
            label: 'Value',
            value: conditionNumber(condition),
            onChanged: (value) => onTyped(conditionNumbered(condition, value)),
            onDone: onDone,
          ),
        if (kind == ConditionKind.through)
          BlendNumberRow(
            label: 'Laps',
            value: conditionNumber(condition),
            unit: 'laps',
            onChanged: (value) => onTyped(conditionNumbered(condition, value)),
            onDone: onDone,
          ),
        if (kind.isComposite) _Parts(editor: this, kind: kind),
      ],
    );
  }
}

/// The tests inside an all, an any or a not, each indented under the one
/// they belong to.
final class _Parts extends StatelessWidget {
  const _Parts({required this.editor, required this.kind});

  final ConditionEditor editor;
  final ConditionKind kind;

  @override
  Widget build(BuildContext context) {
    final condition = editor.condition;
    final parts = conditionParts(condition);
    void replace(List<BlendCondition> next) =>
        editor.onChanged(conditionMadeOf(condition, next));
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final (index, part) in parts.indexed)
          Container(
            margin: const EdgeInsets.only(left: Space.sm, top: Space.xs),
            padding: const EdgeInsets.only(left: Space.sm),
            decoration: const BoxDecoration(
              border: Border(left: BorderSide(color: OrblitColors.line)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                ConditionEditor(
                  condition: part,
                  inputs: editor.inputs,
                  onChanged: (to) => replace([...parts]..[index] = to),
                  onTyped: (to) => editor.onTyped(
                    conditionMadeOf(condition, [...parts]..[index] = to),
                  ),
                  onDone: editor.onDone,
                ),
                if (kind != ConditionKind.not)
                  Align(
                    alignment: Alignment.centerRight,
                    child: OrblitButton(
                      label: 'Remove',
                      tone: ButtonTone.quiet,
                      onPressed: () => replace([...parts]..removeAt(index)),
                    ),
                  ),
              ],
            ),
          ),
        if (kind != ConditionKind.not)
          Align(
            alignment: Alignment.centerLeft,
            child: OrblitButton(
              label: 'Add a test',
              tone: ButtonTone.quiet,
              onPressed: () => replace([
                ...parts,
                BlendCondition.on(editor.inputs.firstOrNull ?? 'input'),
              ]),
            ),
          ),
      ],
    );
  }
}

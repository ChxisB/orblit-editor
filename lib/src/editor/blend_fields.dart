import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';

import 'package:orblit_motion/orblit_motion.dart';

import 'blend_bench.dart';
import 'blend_model.dart';
import 'inspector.dart' show FieldRow;

// What every part of the blend inspector is made of.

/// A number typed into a field, in the unit [unit] says when it has one.
///
/// Reports only what reads as a finite number, so a half-typed `-` or `.`
/// waits for the next keystroke instead of being an edit.
final class BlendNumberRow extends StatelessWidget {
  const BlendNumberRow({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
    required this.onDone,
    this.unit,
    this.trailing,
  });

  final String label;
  final double value;
  final ValueChanged<double> onChanged;
  final VoidCallback onDone;
  final String? unit;

  /// Something after the field, such as the button that takes it out. Beats
  /// [unit] when both are given.
  final Widget? trailing;

  @override
  Widget build(BuildContext context) => FieldRow(
    label: label,
    trailing:
        trailing ??
        (unit == null ? null : Text(unit!, style: OrblitText.caption)),
    child: ValueField(
      value: numberText(value),
      mono: true,
      onChanged: (text) {
        final typed = double.tryParse(text.trim());
        if (typed != null && typed.isFinite) onChanged(typed);
      },
      onDone: onDone,
    ),
  );
}

/// A name typed into a field, with a menu of the names there already are
/// beside it, such as the clips of the project or the inputs of the blend.
final class BlendNameRow extends StatelessWidget {
  const BlendNameRow({
    super.key,
    required this.label,
    required this.value,
    required this.choices,
    required this.onChanged,
    required this.onDone,
  });

  final String label;
  final String value;
  final List<String> choices;

  /// Told what is typed or chosen, as it changes.
  final ValueChanged<String> onChanged;

  final VoidCallback onDone;

  @override
  Widget build(BuildContext context) => FieldRow(
    label: label,
    trailing: choices.isEmpty
        ? null
        : PopupMenuButton<String>(
            tooltip: 'Choose one that is there',
            padding: EdgeInsets.zero,
            color: OrblitColors.raised,
            icon: const Icon(
              Icons.arrow_drop_down,
              size: 18,
              color: OrblitColors.inkMid,
            ),
            onSelected: (name) {
              onChanged(name);
              onDone();
            },
            itemBuilder: (context) => [
              for (final name in choices)
                PopupMenuItem(
                  value: name,
                  height: 28,
                  child: Text(name, style: OrblitText.label),
                ),
            ],
          ),
    child: ValueField(
      value: value,
      onChanged: (text) => onChanged(text.trim()),
      onDone: onDone,
    ),
  );
}

/// How the inspector's fields reach the bench: a choice is a step, and a
/// number typed is a run of them that [done] seals into one.
final class BlendEdits {
  BlendEdits(this.bench, this.onDone);

  final BlendBench bench;
  final VoidCallback onDone;

  Object? _typing;

  void run(
    String label,
    BlendDocument Function(BlendDocument blend) change, {
    BlendPick? pick,
  }) => bench.edit(label, change, pick: pick);

  void typed(
    String label,
    BlendDocument Function(BlendDocument blend) change, {
    BlendPick? pick,
  }) => bench.edit(label, change, pick: pick, gesture: _typing ??= Object());

  void done() {
    _typing = null;
    bench.history.seal();
    onDone();
  }
}

/// A name typed beside a button that adds it, with a menu of what there
/// already is.
class BlendAddRow extends StatefulWidget {
  const BlendAddRow({
    super.key,
    required this.label,
    required this.hint,
    required this.choices,
    required this.onAdd,
  });

  final String label;
  final String hint;
  final List<String> choices;
  final ValueChanged<String> onAdd;

  @override
  State<BlendAddRow> createState() => BlendAddRowState();
}

class BlendAddRowState extends State<BlendAddRow> {
  String _text = '';

  /// Changes to put the box back to empty after an add.
  int _round = 0;

  void _add() {
    widget.onAdd(_text.trim());
    setState(() {
      _text = '';
      _round++;
    });
  }

  @override
  Widget build(BuildContext context) => FieldRow(
    label: widget.label,
    trailing: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (widget.choices.isNotEmpty)
          PopupMenuButton<String>(
            tooltip: 'Choose one that is there',
            padding: EdgeInsets.zero,
            color: OrblitColors.raised,
            icon: const Icon(
              Icons.arrow_drop_down,
              size: 18,
              color: OrblitColors.inkMid,
            ),
            onSelected: (name) => setState(() {
              _text = name;
              _round++;
            }),
            itemBuilder: (context) => [
              for (final name in widget.choices)
                PopupMenuItem(
                  value: name,
                  height: 28,
                  child: Text(name, style: OrblitText.label),
                ),
            ],
          ),
        OrblitButton(label: 'Add', onPressed: _add),
      ],
    ),
    child: ValueField(
      key: ValueKey('${widget.label}/$_round'),
      value: _text,
      hint: widget.hint,
      onChanged: (text) => _text = text,
    ),
  );
}

/// A small button that is only an icon.
final class BlendIconAction extends StatelessWidget {
  const BlendIconAction({
    super.key,
    required this.icon,
    required this.tooltip,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => Tooltip(
    message: tooltip,
    child: GestureDetector(
      onTap: onTap,
      behavior: HitTestBehavior.opaque,
      child: Padding(
        padding: const EdgeInsets.all(4),
        child: Icon(icon, size: 14, color: OrblitColors.inkMid),
      ),
    ),
  );
}

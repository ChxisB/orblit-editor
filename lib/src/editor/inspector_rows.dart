part of 'inspector.dart';

// The controls a field is built from. They are public because every
// other panel in the editor lays its fields out with the same ones.

/// A labelled row, so every field lines up on the same column.
class FieldRow extends StatelessWidget {
  const FieldRow({
    super.key,
    required this.label,
    required this.child,
    this.trailing,
  });

  final String label;
  final Widget child;

  /// Something small after the field, such as the button that keys it.
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          SizedBox(
            width: 74,
            child: Text(
              label,
              style: OrblitText.label.copyWith(fontSize: 11.5),
            ),
          ),
          Expanded(child: child),
          if (trailing case final trailing?) ...[
            const SizedBox(width: Space.xs),
            trailing,
          ],
        ],
      ),
    );
  }
}

/// A value with a slider, in real units.
class SliderRow extends StatelessWidget {
  const SliderRow({
    super.key,
    required this.label,
    required this.value,
    required this.min,
    required this.max,
    required this.onChanged,
    this.onSettled,
    this.unit,
    this.decimals = 0,
    this.trailing,
  });

  final String label;
  final double value;
  final double min;
  final double max;
  final ValueChanged<double> onChanged;

  /// Called when the drag finishes, so a run of changes becomes one step.
  final VoidCallback? onSettled;

  final String? unit;
  final int decimals;

  /// See [FieldRow.trailing].
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      trailing: trailing,
      child: Row(
        children: [
          Expanded(
            child: SliderTheme(
              data: SliderTheme.of(context),
              child: Slider(
                value: value.clamp(min, max),
                min: min,
                max: max,
                onChanged: onChanged,
                onChangeEnd: (_) => onSettled?.call(),
              ),
            ),
          ),
          SizedBox(
            width: 56,
            child: Text(
              '${value.toStringAsFixed(decimals)}${unit ?? ''}',
              textAlign: TextAlign.right,
              style: OrblitText.monoValue.copyWith(fontSize: 11.5),
            ),
          ),
        ],
      ),
    );
  }
}

/// One number or a few that belong together, each dragged sideways to change.
///
/// Dragging rather than typing, because most of what an inspector holds is
/// adjusted by feel against the viewport. The whole drag is one undo step when
/// [onChanged] runs a command that merges: it merges while the pointer is
/// down and [onSettled] seals it when the pointer lifts, so undo returns to
/// where the drag started rather than stepping back through every frame.
class DragRow extends StatelessWidget {
  const DragRow({
    super.key,
    required this.label,
    required this.listenable,
    required this.read,
    required this.onChanged,
    required this.onSettled,
    this.step = 0.01,
    this.decimals = 2,
    this.minimum,
    this.trailing,
  });

  final String label;

  /// What says the numbers may have changed. Usually the history, since
  /// every change to a scene goes through it.
  final Listenable listenable;

  /// The numbers as they are now. Asked again at every step of a drag rather
  /// than remembered, because the drag is what is changing them.
  final List<double> Function() read;

  /// Called with every number, one of them moved.
  final ValueChanged<List<double>> onChanged;

  final VoidCallback onSettled;

  /// Units per logical pixel dragged.
  final double step;

  final int decimals;

  /// A floor for each number: a scale cannot be dragged through zero into a
  /// matrix that cannot be inverted, nor a ball into one with no size.
  final double? minimum;

  /// See [FieldRow.trailing].
  final Widget? trailing;

  // X, Y, Z tinted the way every 3D tool tints them, because the convention is
  // older than any of them and reading is faster than remembering.
  static const _axisColours = [
    Color(0xFFD9634F),
    Color(0xFF7FB069),
    Color(0xFF5B8DD9),
  ];

  @override
  Widget build(BuildContext context) {
    // Listening here rather than being rebuilt from above.
    //
    // A drag runs a command a frame, and a row like this is the only part of
    // the inspector it changes — a name is the same name at a different
    // height. Rebuilding the whole inspector to show three numbers was over
    // half the cost of a drag frame, most of it text fields with their own
    // focus, actions and overlays.
    return ListenableBuilder(
      listenable: listenable,
      builder: (context, _) => _row(),
    );
  }

  Widget _row() {
    final values = read();

    return FieldRow(
      label: label,
      trailing: trailing,
      child: Row(
        children: [
          for (var i = 0; i < values.length; i++) ...[
            if (i > 0) const SizedBox(width: Space.xs),
            Expanded(
              child: _NumberField(
                value: values[i],
                accent: values.length == 3
                    ? _axisColours[i]
                    : OrblitColors.inkDim,
                decimals: decimals,
                onDrag: (pixels) {
                  final next = List.of(read());
                  final moved = next[i] + pixels * step;
                  next[i] = minimum == null
                      ? moved
                      : (moved < minimum! ? minimum! : moved);
                  onChanged(next);
                },
                onSettled: onSettled,
              ),
            ),
          ],
        ],
      ),
    );
  }
}

/// One of an object's position, rotation or scale.
class VectorRow extends StatelessWidget {
  const VectorRow({
    super.key,
    required this.label,
    required this.sceneId,
    required this.object,
    required this.field,
    required this.history,
    this.keying,
    this.step = 0.01,
    this.decimals = 2,
    this.minimum,
  });

  final String label;
  final String sceneId;
  final SceneObject object;
  final TransformField field;
  final History history;

  /// What keys it into the clip being edited. Null where there is no
  /// timeline, and then the row has no key button.
  final Keying? keying;

  /// Units per logical pixel dragged.
  final double step;

  final int decimals;

  /// A floor for each component, so scale cannot be dragged through zero into
  /// a matrix that cannot be inverted.
  final double? minimum;

  @override
  Widget build(BuildContext context) {
    final keying = this.keying;
    return DragRow(
      label: label,
      // The clip moves it too, and a pose is not a step on the undo stack.
      listenable: keying == null
          ? history
          : Listenable.merge([history, keying]),
      read: () => field.of(object).storage,
      onChanged: (values) => history.run(
        SetTransform(
          sceneId: sceneId,
          id: object.id,
          field: field,
          name: object.name,
          from: field.of(object),
          to: Vector3.array(values),
        ),
      ),
      onSettled: history.seal,
      step: step,
      decimals: decimals,
      minimum: minimum,
      trailing: keying == null
          ? null
          : KeyButton(
              keying: keying,
              object: object,
              property: 'transform.${field.name}',
            ),
    );
  }
}

/// The diamond after a field that a clip can key.
///
/// Filled where there is a key on the playhead's frame, hollow and lit where
/// the clip moves the field between keys, and hollow and dim where it does
/// not move it yet. Pressing it keys the field as it is now. Not there at all
/// where the clip cannot key the field, so a row does not offer what it
/// cannot do.
class KeyButton extends StatelessWidget {
  const KeyButton({
    super.key,
    required this.keying,
    required this.object,
    required this.property,
  });

  final Keying keying;
  final SceneObject object;

  /// The field as a clip names it, like `transform.position`.
  final String property;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    // Listening for itself: the playhead moving changes the mark and nothing
    // else about the row it is in.
    listenable: keying,
    builder: (context, _) {
      final mark = keying.markFor(object, property);
      if (mark == KeyMark.none) return const SizedBox.shrink();
      return Tooltip(
        message: 'Key this frame',
        child: MouseRegion(
          cursor: SystemMouseCursors.click,
          child: GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: () => keying.key(object, property),
            child: SizedBox(
              width: 16,
              height: 24,
              child: Icon(
                mark == KeyMark.keyed ? Icons.diamond : Icons.diamond_outlined,
                size: 12,
                color: mark == KeyMark.unkeyed
                    ? OrblitColors.inkDim
                    : OrblitColors.ember,
              ),
            ),
          ),
        ),
      );
    },
  );
}

/// One draggable number.
class _NumberField extends StatefulWidget {
  const _NumberField({
    required this.value,
    required this.accent,
    required this.decimals,
    required this.onDrag,
    required this.onSettled,
  });

  final double value;
  final Color accent;
  final int decimals;
  final ValueChanged<double> onDrag;
  final VoidCallback onSettled;

  @override
  State<_NumberField> createState() => _NumberFieldState();
}

class _NumberFieldState extends State<_NumberField> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.resizeLeftRight,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onHorizontalDragUpdate: (details) => widget.onDrag(details.delta.dx),
        onHorizontalDragEnd: (_) => widget.onSettled(),
        onHorizontalDragCancel: widget.onSettled,
        child: Container(
          height: 24,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          decoration: BoxDecoration(
            color: _hovering ? OrblitColors.line : OrblitColors.raised,
            borderRadius: BorderRadius.circular(4),
            border: Border(left: BorderSide(color: widget.accent, width: 2)),
          ),
          alignment: Alignment.centerRight,
          child: Text(
            widget.value.toStringAsFixed(widget.decimals),
            style: OrblitText.monoValue.copyWith(fontSize: 11),
          ),
        ),
      ),
    );
  }
}

/// A short list of options, shown rather than hidden behind a menu.
class ChoiceRow extends StatelessWidget {
  const ChoiceRow({
    super.key,
    required this.label,
    required this.options,
    required this.selected,
    this.onSelect,
  });

  final String label;
  final List<String> options;
  final String selected;

  /// Null where there is nothing to choose — a single-option row is a
  /// statement of fact, not a control.
  final ValueChanged<String>? onSelect;

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      child: Container(
        height: 24,
        padding: const EdgeInsets.all(2),
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(4),
        ),
        child: Row(
          children: [
            for (final option in options)
              Expanded(
                child: GestureDetector(
                  onTap: onSelect == null ? null : () => onSelect!(option),
                  child: Container(
                    alignment: Alignment.center,
                    decoration: BoxDecoration(
                      color: option == selected
                          ? OrblitColors.emberDeep
                          : Colors.transparent,
                      borderRadius: BorderRadius.circular(3),
                    ),
                    child: Text(
                      option,
                      style: OrblitText.label.copyWith(
                        fontSize: 10.5,
                        color: option == selected
                            ? const Color(0xFFFFF0E2)
                            : OrblitColors.inkDim,
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class ColourRow extends StatelessWidget {
  const ColourRow({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final Color value;
  final ValueChanged<Color> onChanged;

  // A short palette rather than a full picker: enough to see a colour change
  // reach the renderer, and a picker is a component in its own right.
  static const _swatches = [
    Color(0xFFFFF3E0),
    Color(0xFFD9634F),
    Color(0xFF7FB069),
    Color(0xFF5B8DD9),
    Color(0xFFE5B84F),
    Color(0xFF3B424C),
  ];

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      child: Row(
        children: [
          for (final swatch in _swatches) ...[
            _Swatch(
              colour: swatch,
              selected: swatch.toARGB32() == value.toARGB32(),
              onTap: () => onChanged(swatch),
            ),
            const SizedBox(width: 3),
          ],
          const SizedBox(width: Space.xs),
          Expanded(
            child: Text(
              '#${value.toARGB32().toRadixString(16).substring(2).toUpperCase()}',
              overflow: TextOverflow.ellipsis,
              style: OrblitText.mono.copyWith(fontSize: 10.5),
            ),
          ),
        ],
      ),
    );
  }
}

class TextRow extends StatelessWidget {
  const TextRow({super.key, required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      child: Container(
        height: 24,
        padding: const EdgeInsets.symmetric(horizontal: Space.sm),
        alignment: Alignment.centerLeft,
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(4),
        ),
        child: Text(value, style: OrblitText.monoValue.copyWith(fontSize: 11)),
      ),
    );
  }
}

class _Swatch extends StatelessWidget {
  const _Swatch({
    required this.colour,
    required this.selected,
    required this.onTap,
  });

  final Color colour;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      child: GestureDetector(
        onTap: onTap,
        child: Container(
          height: 20,
          width: 20,
          decoration: BoxDecoration(
            color: colour,
            borderRadius: BorderRadius.circular(4),
            border: Border.all(
              color: selected ? OrblitColors.ember : OrblitColors.line,
              width: selected ? 2 : 1,
            ),
          ),
        ),
      ),
    );
  }
}

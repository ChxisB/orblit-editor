part of 'inspector.dart';

/// One switch in a row of them, lit while it is on.
class ToggleCell extends StatelessWidget {
  const ToggleCell({
    super.key,
    required this.label,
    required this.on,
    this.onTap,
    this.tooltip,
  });

  final String label;
  final bool on;

  /// Null where there is nothing to switch, and the cell reads as fact.
  final VoidCallback? onTap;

  final String? tooltip;

  @override
  Widget build(BuildContext context) {
    final cell = GestureDetector(
      onTap: onTap,
      child: Container(
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: on ? OrblitColors.emberWash : Colors.transparent,
          borderRadius: BorderRadius.circular(Radii.control - 2),
        ),
        child: Text(
          label,
          style: OrblitText.label.copyWith(
            fontSize: 10.5,
            color: on ? OrblitColors.ember : OrblitColors.inkDim,
          ),
        ),
      ),
    );
    return tooltip == null ? cell : Tooltip(message: tooltip!, child: cell);
  }
}

/// A labelled track holding a row of [ToggleCell]s, each given an equal share.
class ToggleRow extends StatelessWidget {
  const ToggleRow({super.key, required this.label, required this.cells});

  final String label;
  final List<ToggleCell> cells;

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      child: Container(
        height: 28,
        padding: const EdgeInsets.all(2),
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(Radii.control),
        ),
        child: Row(children: [for (final cell in cells) Expanded(child: cell)]),
      ),
    );
  }
}

/// One switch for each physics layer, in rows of eight.
///
/// A layer reads as its number, and as its name when the scene gives it one,
/// in the tooltip: a name would not fit in a cell a twentieth of the panel.
class LayerGrid extends StatelessWidget {
  const LayerGrid({
    super.key,
    required this.label,
    required this.bits,
    required this.names,
    required this.onToggle,
  });

  final String label;

  /// The layers that are on, one bit each, the first layer the lowest.
  final int bits;

  /// What the scene calls each layer, from the first. See
  /// [SceneSettings.layerNames].
  final List<String> names;

  final ValueChanged<int> onToggle;

  static const _across = 8;

  String _tooltip(int index) {
    final name = index < names.length ? names[index] : '';
    return name.isEmpty ? 'Layer ${index + 1}' : '${index + 1}: $name';
  }

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: label,
      child: Container(
        padding: const EdgeInsets.all(2),
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(4),
        ),
        child: Column(
          children: [
            for (var top = 0; top < SceneSettings.layerCount; top += _across)
              SizedBox(
                height: 22,
                child: Row(
                  children: [
                    for (var index = top; index < top + _across; index++)
                      Expanded(
                        child: ToggleCell(
                          label: '${index + 1}',
                          on: bits & (1 << index) != 0,
                          tooltip: _tooltip(index),
                          onTap: () => onToggle(index),
                        ),
                      ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';

/// An on and off switch, ember while it is on.
///
/// [label] is its name for the tooltip and for anything that reads the screen
/// aloud, because the switch has no word of its own.
final class OrblitSwitch extends StatelessWidget {
  const OrblitSwitch({
    super.key,
    required this.label,
    required this.value,
    required this.onChanged,
  });

  final String label;
  final bool value;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: label,
      child: Semantics(
        label: label,
        toggled: value,
        onTap: () => onChanged(!value),
        child: MouseRegion(
          cursor: SystemMouseCursors.click,
          child: GestureDetector(
            onTap: () => onChanged(!value),
            child: Container(
              width: 32,
              height: 18,
              padding: const EdgeInsets.all(2),
              alignment: value ? Alignment.centerRight : Alignment.centerLeft,
              decoration: BoxDecoration(
                color: value ? OrblitColors.ember : OrblitColors.line,
                borderRadius: BorderRadius.circular(9),
              ),
              child: DecoratedBox(
                decoration: BoxDecoration(
                  shape: BoxShape.circle,
                  color: value ? OrblitColors.emberInk : OrblitColors.inkDim,
                ),
                child: const SizedBox.square(dimension: 14),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

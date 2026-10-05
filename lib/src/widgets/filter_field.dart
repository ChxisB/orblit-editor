import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';

/// A short field that narrows a list as it is typed in, with the ember rim
/// the rest of the editor's fields take on focus.
final class FilterField extends StatefulWidget {
  const FilterField({
    super.key,
    required this.controller,
    required this.hint,
    required this.onChanged,
    this.height = 28,
    this.fill = OrblitColors.raised,
    this.radius = Radii.control,
  });

  final TextEditingController controller;

  /// A taller, rounder field with a lighter fill sits on a page rather than in
  /// a panel, so the page can ask for it.
  final double height;
  final Color fill;
  final double radius;

  /// What the field says when it is empty, which is also its name for
  /// anything that reads the screen aloud.
  final String hint;

  final ValueChanged<String> onChanged;

  @override
  State<FilterField> createState() => _FilterFieldState();
}

class _FilterFieldState extends State<FilterField> {
  bool _focused = false;

  @override
  Widget build(BuildContext context) => Focus(
    canRequestFocus: false,
    onFocusChange: (focused) => setState(() => _focused = focused),
    child: Container(
      height: widget.height,
      padding: const EdgeInsets.symmetric(horizontal: 10),
      decoration: BoxDecoration(
        color: widget.fill,
        borderRadius: BorderRadius.circular(widget.radius),
        border: Border.all(
          color: _focused ? OrblitColors.ember : Colors.transparent,
        ),
      ),
      child: Row(
        spacing: Space.sm,
        children: [
          const Icon(Icons.search, size: 14, color: OrblitColors.inkDim),
          Expanded(
            child: TextField(
              controller: widget.controller,
              onChanged: widget.onChanged,
              cursorColor: OrblitColors.ember,
              style: OrblitText.label.copyWith(color: OrblitColors.ink),
              decoration: InputDecoration.collapsed(
                hintText: widget.hint,
                hintStyle: OrblitText.label.copyWith(
                  color: OrblitColors.inkDim,
                ),
              ),
            ),
          ),
        ],
      ),
    ),
  );
}

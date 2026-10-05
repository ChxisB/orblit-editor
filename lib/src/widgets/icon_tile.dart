import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';

/// A square button holding an icon, which lights on hover. It has a tooltip
/// because it has no word.
///
/// A null [onTap] draws it disabled. [active] is for a tool that is on.
final class IconTile extends StatefulWidget {
  const IconTile({
    super.key,
    required this.child,
    required this.tooltip,
    required this.onTap,
    this.size = 32,
    this.iconSize = 17,
    this.radius = Radii.card,
    this.active = false,
  });

  final Widget child;
  final String tooltip;
  final VoidCallback? onTap;
  final double size;
  final double iconSize;
  final double radius;
  final bool active;

  @override
  State<IconTile> createState() => _IconTileState();
}

class _IconTileState extends State<IconTile> {
  bool _hovering = false;

  Color get _fill {
    if (widget.active) return OrblitColors.raised;
    return _hovering ? OrblitColors.hover : Colors.transparent;
  }

  Color get _ink {
    if (widget.onTap == null) return OrblitColors.line;
    if (widget.active) return OrblitColors.ember;
    return _hovering ? OrblitColors.ink : OrblitColors.inkMid;
  }

  @override
  Widget build(BuildContext context) {
    final enabled = widget.onTap != null;
    return Tooltip(
      message: widget.tooltip,
      child: MouseRegion(
        cursor: enabled ? SystemMouseCursors.click : SystemMouseCursors.basic,
        onEnter: (_) => setState(() => _hovering = true),
        onExit: (_) => setState(() => _hovering = false),
        child: GestureDetector(
          onTap: widget.onTap,
          child: Container(
            width: widget.size,
            height: widget.size,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: enabled ? _fill : Colors.transparent,
              borderRadius: BorderRadius.circular(widget.radius),
            ),
            child: IconTheme(
              data: IconThemeData(size: widget.iconSize, color: _ink),
              child: widget.child,
            ),
          ),
        ),
      ),
    );
  }
}

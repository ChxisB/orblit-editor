import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/centred_bar.dart';
import 'dock.dart';

/// Builds the contents of one panel.
typedef PanelBuilder = Widget Function(BuildContext context, DockPanel panel);

/// Builds what a panel puts in the middle of its group's header, if anything.
typedef PanelAccessory = Widget? Function(BuildContext context, DockPanel panel);

/// The editor's panels, arranged as the layout says.
///
/// The layout is data and this draws it. Everything that changes the
/// arrangement — a tab dragged somewhere, a divider moved, a panel closed —
/// goes back out through [onChanged] as a whole new layout, so there is one
/// description of where things are rather than a widget tree that has drifted
/// from the file that was supposed to describe it.
class DockView extends StatelessWidget {
  const DockView({
    super.key,
    required this.layout,
    required this.panel,
    required this.onChanged,
    this.accessory,
  });

  final DockLayout layout;
  final PanelBuilder panel;
  final ValueChanged<DockLayout> onChanged;

  /// Asked for each group's showing panel, so the viewport can keep its
  /// transport in the header it shares with its tabs.
  final PanelAccessory? accessory;

  @override
  Widget build(BuildContext context) => _node(context, layout.root, false);

  Widget _node(BuildContext context, DockNode node, bool folded) =>
      switch (node) {
        DockGroup() => _DockGroupView(
            group: node,
            folded: folded,
            layout: layout,
            panel: panel,
            accessory: accessory,
            onChanged: onChanged,
          ),
        DockSplit() => _DockSplitView(
            split: node,
            layout: layout,
            child: _node,
            onChanged: onChanged,
          ),
      };
}

/// A row or a column of places, with a handle between each pair.
class _DockSplitView extends StatelessWidget {
  const _DockSplitView({
    required this.split,
    required this.layout,
    required this.child,
    required this.onChanged,
  });

  final DockSplit split;
  final DockLayout layout;
  final Widget Function(BuildContext, DockNode, bool folded) child;
  final ValueChanged<DockLayout> onChanged;

  /// The gap between two panels, which is also the thing that drags them.
  static const double _handle = 6;

  @override
  Widget build(BuildContext context) {
    final folded = split.folded;
    // A folded group is as tall as its tabs, and the rest share what is left
    // in the proportions they had, so opening it again gives back the height
    // it had rather than whatever it was squeezed to.
    final open = [
      for (var i = 0; i < split.children.length; i++)
        folded[i] ? 0.0 : split.shares[i],
    ];
    final total = open.fold<double>(0, (sum, share) => sum + share);
    final shares = [
      for (final share in open) total <= 0 ? 0.0 : share / total,
    ];
    final foldedCount = folded.where((fold) => fold).length;

    return LayoutBuilder(
      builder: (context, constraints) {
        final along = split.axis == Axis.horizontal
            ? constraints.maxWidth
            : constraints.maxHeight;
        // The handles take space out of the total before it is shared, or the
        // panels would add up to more than the room and overflow by exactly
        // the number of dividers.
        final room =
            (along -
                    _handle * (split.children.length - 1) -
                    _Tabs.foldedHeight * foldedCount)
                .clamp(0.0, double.infinity);

        final pieces = <Widget>[];
        for (var i = 0; i < split.children.length; i++) {
          final size = folded[i] ? _Tabs.foldedHeight : room * shares[i];
          pieces.add(SizedBox(
            width: split.axis == Axis.horizontal ? size : null,
            height: split.axis == Axis.vertical ? size : null,
            child: child(context, split.children[i], folded[i]),
          ));

          if (i + 1 < split.children.length) {
            pieces.add(_Handle(
              axis: split.axis,
              // Nothing to drag against a folded group: it is the height of
              // its tabs until it is opened.
              locked: layout.locked || folded[i] || folded[i + 1],
              onDrag: (delta) {
                if (room <= 0) return;
                // The two either side share what they had between them, so
                // dragging one divider does not move every other panel.
                final pair = shares[i] + shares[i + 1];
                final moved = shares[i] + delta / room;
                onChanged(layout.resize(split.id, i, moved / pair));
              },
            ));
          }
        }

        return Flex(
          direction: split.axis,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: pieces,
        );
      },
    );
  }
}

/// The bar between two panels.
class _Handle extends StatefulWidget {
  const _Handle({
    required this.axis,
    required this.locked,
    required this.onDrag,
  });

  final Axis axis;
  final bool locked;
  final ValueChanged<double> onDrag;

  @override
  State<_Handle> createState() => _HandleState();
}

class _HandleState extends State<_Handle> {
  bool _hovering = false;
  bool _dragging = false;

  @override
  Widget build(BuildContext context) {
    final lit = _hovering || _dragging;

    // The gap between panels is the ground showing through. Under the cursor
    // a thin ember line runs down the middle of it, so the panels do not
    // change size while one is being dragged.
    final horizontal = widget.axis == Axis.horizontal;
    final bar = SizedBox(
      width: horizontal ? _DockSplitView._handle : null,
      height: horizontal ? null : _DockSplitView._handle,
      child: Center(
        child: SizedBox(
          width: horizontal ? 2 : null,
          height: horizontal ? null : 2,
          child: ColoredBox(
            color: lit && !widget.locked
                ? OrblitColors.ember
                : Colors.transparent,
          ),
        ),
      ),
    );

    // A locked layout still draws the divider — it is the line between two
    // panels either way — but it does not offer to move.
    if (widget.locked) return bar;

    return MouseRegion(
      cursor: widget.axis == Axis.horizontal
          ? SystemMouseCursors.resizeLeftRight
          : SystemMouseCursors.resizeUpDown,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onHorizontalDragStart: widget.axis == Axis.horizontal
            ? (_) => setState(() => _dragging = true)
            : null,
        onHorizontalDragUpdate: widget.axis == Axis.horizontal
            ? (details) => widget.onDrag(details.delta.dx)
            : null,
        onHorizontalDragEnd: widget.axis == Axis.horizontal
            ? (_) => setState(() => _dragging = false)
            : null,
        onVerticalDragStart: widget.axis == Axis.vertical
            ? (_) => setState(() => _dragging = true)
            : null,
        onVerticalDragUpdate: widget.axis == Axis.vertical
            ? (details) => widget.onDrag(details.delta.dy)
            : null,
        onVerticalDragEnd: widget.axis == Axis.vertical
            ? (_) => setState(() => _dragging = false)
            : null,
        child: bar,
      ),
    );
  }
}

/// Panels sharing a space, with the tabs above them.
class _DockGroupView extends StatefulWidget {
  const _DockGroupView({
    required this.group,
    required this.folded,
    required this.layout,
    required this.panel,
    required this.accessory,
    required this.onChanged,
  });

  final DockGroup group;

  /// Whether it is drawn as its tabs alone. The group asks to be folded; the
  /// column it is in decides whether it is.
  final bool folded;

  final DockLayout layout;
  final PanelBuilder panel;
  final PanelAccessory? accessory;
  final ValueChanged<DockLayout> onChanged;

  @override
  State<_DockGroupView> createState() => _DockGroupViewState();
}

class _DockGroupViewState extends State<_DockGroupView> {
  /// Which edge the thing being dragged would land on.
  DockSide? _over;

  /// Which side of this group a point is nearest.
  ///
  /// The middle is a wide target on purpose: dropping a tab beside another one
  /// is the common thing, and a centre so small that every drop makes a new
  /// split is a layout somebody has to keep tidying up.
  static DockSide _sideFor(Offset local, Size size) {
    final x = local.dx / (size.width == 0 ? 1 : size.width);
    final y = local.dy / (size.height == 0 ? 1 : size.height);

    const edge = 0.25;
    final nearest = [
      (DockSide.left, x),
      (DockSide.right, 1 - x),
      (DockSide.top, y),
      (DockSide.bottom, 1 - y),
    ].reduce((a, b) => a.$2 <= b.$2 ? a : b);

    return nearest.$2 < edge ? nearest.$1 : DockSide.centre;
  }

  @override
  Widget build(BuildContext context) {
    final group = widget.group;
    final showing = group.current;

    final body = Container(
      // The ring is drawn by the box itself, so the corner it clips to and
      // the line round it cannot disagree.
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: OrblitColors.surface,
        borderRadius: BorderRadius.circular(Radii.panel),
        border: Border.all(color: OrblitColors.rim),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _Tabs(
            group: group,
            folded: widget.folded,
            layout: widget.layout,
            accessory: showing == null
                ? null
                : widget.accessory?.call(context, showing),
            onChanged: widget.onChanged,
          ),
          // Folded, the panel is not built at all rather than built at no
          // height: a panel squeezed to nothing overflows, and one nobody can
          // see has no reason to be doing its work.
          if (!widget.folded)
            Expanded(
              child: showing == null
                  ? const SizedBox.shrink()
                  // A boundary a panel, so a panel that did not change is not
                  // painted again because a different one did. Every edit
                  // rebuilds the editor from the top — a drag does it sixty
                  // times a second — and without these that is a repaint of
                  // the whole window each time.
                  : RepaintBoundary(
                      child: ClipRect(child: widget.panel(context, showing)),
                    ),
            ),
        ],
      ),
    );

    if (widget.layout.locked) return body;

    return DragTarget<PanelDrag>(
      onWillAcceptWithDetails: (details) => !widget.layout.locked,
      onMove: (details) {
        final box = context.findRenderObject() as RenderBox?;
        if (box == null) return;
        final side = _sideFor(box.globalToLocal(details.offset), box.size);
        if (side != _over) setState(() => _over = side);
      },
      onLeave: (_) => setState(() => _over = null),
      onAcceptWithDetails: (details) {
        final side = _over ?? DockSide.centre;
        setState(() => _over = null);
        widget.onChanged(
          widget.layout.dock(details.data.id, group.id, side),
        );
      },
      builder: (context, candidate, _) => Stack(
        children: [
          Positioned.fill(child: body),
          // Where it would land, drawn as the shape it would take rather than
          // as an arrow: a quarter of the panel highlighted says "here, this
          // size" without anybody having to learn what the arrow meant.
          if (candidate.isNotEmpty && _over != null)
            Positioned.fill(
              child: IgnorePointer(
                child: _DropHint(side: _over!),
              ),
            ),
        ],
      ),
    );
  }
}

/// The shape a dropped panel would take.
class _DropHint extends StatelessWidget {
  const _DropHint({required this.side});

  final DockSide side;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final width = constraints.maxWidth;
        final height = constraints.maxHeight;

        final rect = switch (side) {
          DockSide.left => Rect.fromLTWH(0, 0, width * 0.34, height),
          DockSide.right =>
            Rect.fromLTWH(width * 0.66, 0, width * 0.34, height),
          DockSide.top => Rect.fromLTWH(0, 0, width, height * 0.34),
          DockSide.bottom =>
            Rect.fromLTWH(0, height * 0.66, width, height * 0.34),
          DockSide.centre => Rect.fromLTWH(0, 0, width, height),
        };

        return Stack(
          children: [
            Positioned.fromRect(
              rect: rect,
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: OrblitColors.emberWash,
                  border: Border.all(color: OrblitColors.ember, width: 2),
                ),
              ),
            ),
          ],
        );
      },
    );
  }
}

/// The header above a group.
///
/// A group with one panel shows its name as a title. A group with several
/// shows them as tabs, the one that is showing raised. A panel can also put
/// something in the middle, which stays in the middle of the header until a
/// tab or the fold button would be under it.
class _Tabs extends StatelessWidget {
  const _Tabs({
    required this.group,
    required this.folded,
    required this.layout,
    required this.accessory,
    required this.onChanged,
  });

  final DockGroup group;
  final bool folded;
  final DockLayout layout;
  final Widget? accessory;
  final ValueChanged<DockLayout> onChanged;

  static const double height = 40;

  /// A folded group is nothing but these and the ring drawn round it, which
  /// takes a line off each side.
  static const double foldedHeight = height + 2;

  @override
  Widget build(BuildContext context) {
    final folds = folded || layout.canCollapse(group.id);
    void fold(bool collapsed) =>
        onChanged(layout.collapse(group.id, collapsed: collapsed));

    return SizedBox(
      height: height,
      child: Padding(
        padding: const EdgeInsets.only(left: Space.sm, right: Space.xs),
        child: CentredBar(
          start: ListView(
            scrollDirection: Axis.horizontal,
            shrinkWrap: true,
            children: [
              for (var i = 0; i < group.panels.length; i++)
                _PanelTab(
                  // Found by the panel rather than by its name, which is
                  // also the name of a menu, a button and a heading.
                  key: ValueKey('dock-tab/${group.panels[i].id}'),
                  panel: group.panels[i],
                  alone: group.panels.length == 1,
                  // Folded, nothing is showing, so no tab says it is.
                  selected: i == group.showing && !folded,
                  locked: layout.locked,
                  onTap: () {
                    // The tab that is showing folds its group away and
                    // opens it again, so the console is put away where
                    // it is read rather than from a corner of the panel.
                    if (folds && i == group.showing) {
                      fold(!folded);
                    } else {
                      onChanged(layout.show(group.panels[i].id));
                    }
                  },
                  onClose: () => onChanged(layout.close(group.panels[i].id)),
                ),
            ],
          ),
          middle: folded ? null : accessory,
          end: folds
              ? _FoldButton(folded: folded, onTap: () => fold(!folded))
              : null,
        ),
      ),
    );
  }
}

/// The chevron at the end of a group's tabs that folds it.
class _FoldButton extends StatefulWidget {
  const _FoldButton({required this.folded, required this.onTap});

  final bool folded;
  final VoidCallback onTap;

  @override
  State<_FoldButton> createState() => _FoldButtonState();
}

class _FoldButtonState extends State<_FoldButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    return Tooltip(
      message: widget.folded ? 'Open' : 'Fold away',
      child: MouseRegion(
        cursor: SystemMouseCursors.click,
        onEnter: (_) => setState(() => _hovering = true),
        onExit: (_) => setState(() => _hovering = false),
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTap: widget.onTap,
          child: Container(
            width: 28,
            height: 28,
            decoration: BoxDecoration(
              color: _hovering ? OrblitColors.hover : Colors.transparent,
              borderRadius: BorderRadius.circular(Radii.control),
            ),
            child: Icon(
              widget.folded ? Icons.expand_less : Icons.expand_more,
              size: 16,
              color: _hovering ? OrblitColors.ink : OrblitColors.inkMid,
            ),
          ),
        ),
      ),
    );
  }
}

class _PanelTab extends StatefulWidget {
  const _PanelTab({
    super.key,
    required this.panel,
    required this.alone,
    required this.selected,
    required this.locked,
    required this.onTap,
    required this.onClose,
  });

  final DockPanel panel;

  /// The only panel in its group, so it is the group's title and not a tab
  /// among others.
  final bool alone;

  final bool selected;
  final bool locked;
  final VoidCallback onTap;
  final VoidCallback onClose;

  @override
  State<_PanelTab> createState() => _PanelTabState();
}

class _PanelTabState extends State<_PanelTab> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final panel = widget.panel;
    final alone = widget.alone;
    final colour = widget.selected
        ? OrblitColors.ink
        : (_hovering ? OrblitColors.inkMid : OrblitColors.inkDim);

    final tab = MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Center(
          child: Container(
            height: alone ? null : 28,
            padding: EdgeInsets.only(
              left: alone ? Space.sm - 2 : Space.md,
              right: alone ? Space.xs : Space.sm,
            ),
            decoration: BoxDecoration(
              color: widget.selected && !alone
                  ? OrblitColors.raised
                  : (_hovering && !alone ? OrblitColors.hover : null),
              borderRadius: BorderRadius.circular(Radii.control),
            ),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  panel.label,
                  style: alone
                      ? OrblitText.panelTitle
                      : OrblitText.label.copyWith(
                          color: colour,
                          fontSize: 12.5,
                          fontWeight: FontWeight.w500,
                        ),
                ),
                // Only under the cursor, so a row of tabs is a row of names
                // rather than a row of names and crosses. The room is kept
                // either way, or the tabs to the right would shuffle along as
                // the pointer crossed them.
                const SizedBox(width: Space.xs),
                SizedBox(
                  width: 12,
                  child: _hovering && !widget.locked
                      ? GestureDetector(
                          onTap: widget.onClose,
                          child: const Icon(Icons.close, size: 12,
                              color: OrblitColors.inkDim),
                        )
                      : null,
                ),
              ],
            ),
          ),
        ),
      ),
    );

    if (widget.locked) return tab;

    return Draggable<PanelDrag>(
      data: PanelDrag(panel),
      dragAnchorStrategy: pointerDragAnchorStrategy,
      feedback: _DragLabel(panel: panel),
      childWhenDragging: Opacity(opacity: 0.4, child: tab),
      child: tab,
    );
  }
}

/// What follows the pointer while a tab is dragged.
class _DragLabel extends StatelessWidget {
  const _DragLabel({required this.panel});

  final DockPanel panel;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: Colors.transparent,
      child: Container(
        padding: const EdgeInsets.symmetric(
          horizontal: Space.sm,
          vertical: Space.xs,
        ),
        decoration: BoxDecoration(
          color: OrblitColors.raised,
          borderRadius: BorderRadius.circular(Radii.control),
          border: Border.all(color: OrblitColors.ember),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(panel.kind.icon, size: 13, color: OrblitColors.ember),
            const SizedBox(width: Space.sm),
            Text(
              panel.label,
              style: OrblitText.label.copyWith(color: OrblitColors.ink),
            ),
          ],
        ),
      ),
    );
  }
}

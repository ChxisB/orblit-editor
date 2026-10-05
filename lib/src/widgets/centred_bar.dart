import 'package:flutter/widgets.dart';

import '../theme/orblit_theme.dart';

enum _Slot { start, middle, end }

/// A bar with something at each end and something in the middle of it.
///
/// The middle sits in the middle of the bar, not of the room the ends leave.
/// The ends are different widths, and a middle that shifts along whenever one
/// of them gains a dot is a middle somebody has to look for. It is pushed
/// aside only when an end would otherwise run into it, and it is given what
/// room is left, so a middle that can shrink does.
final class CentredBar extends StatelessWidget {
  const CentredBar({super.key, required this.start, this.middle, this.end});

  final Widget start;
  final Widget? middle;
  final Widget? end;

  @override
  Widget build(BuildContext context) => CustomMultiChildLayout(
    delegate: _CentredBarLayout(),
    children: [
      LayoutId(id: _Slot.start, child: start),
      if (middle != null) LayoutId(id: _Slot.middle, child: middle!),
      if (end != null) LayoutId(id: _Slot.end, child: end!),
    ],
  );
}

final class _CentredBarLayout extends MultiChildLayoutDelegate {
  @override
  void performLayout(Size size) {
    final loose = BoxConstraints.loose(size);
    // The end first: it is the small one, and the start is the one that can
    // be given less and still be readable.
    final end = hasChild(_Slot.end) ? layoutChild(_Slot.end, loose) : Size.zero;
    final start = layoutChild(
      _Slot.start,
      loose.copyWith(maxWidth: (size.width - end.width).clamp(0, size.width)),
    );
    positionChild(_Slot.start, Offset(0, (size.height - start.height) / 2));
    if (hasChild(_Slot.end)) {
      positionChild(
        _Slot.end,
        Offset(size.width - end.width, (size.height - end.height) / 2),
      );
    }

    if (!hasChild(_Slot.middle)) return;
    const gap = Space.lg;
    final room = (size.width - start.width - end.width - gap * 2).clamp(
      0.0,
      size.width,
    );
    final middle = layoutChild(_Slot.middle, loose.copyWith(maxWidth: room));
    final lowest = start.width + gap;
    final highest = size.width - end.width - gap - middle.width;
    final centred = (size.width - middle.width) / 2;
    positionChild(
      _Slot.middle,
      Offset(
        highest < lowest ? lowest : centred.clamp(lowest, highest),
        (size.height - middle.height) / 2,
      ),
    );
  }

  @override
  bool shouldRelayout(_CentredBarLayout oldDelegate) => false;
}

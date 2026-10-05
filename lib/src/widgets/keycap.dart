import 'package:flutter/widgets.dart';

import '../theme/orblit_theme.dart';

/// A key as it is printed on one.
final class Keycap extends StatelessWidget {
  const Keycap(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) => Container(
    constraints: const BoxConstraints(minWidth: 18),
    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
    alignment: Alignment.center,
    decoration: BoxDecoration(
      color: OrblitColors.raised,
      borderRadius: BorderRadius.circular(5),
    ),
    child: Text(
      text,
      style: OrblitText.mono.copyWith(fontSize: 11, color: OrblitColors.inkMid),
    ),
  );
}

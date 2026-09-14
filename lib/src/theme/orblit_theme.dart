import 'package:flutter/material.dart';

/// The editor's palette.
///
/// Dark, because a tool you look at for eight hours should not be the
/// brightest thing on the desk, and because the thing being authored is a lit
/// scene — a pale interface next to it lies about exposure.
///
/// One accent, spent deliberately. Everything that is merely present is grey;
/// the ember is reserved for what is selected, what is primary, and what is
/// being edited right now. An interface where everything is highlighted has
/// highlighted nothing.
abstract final class OrblitColors {
  /// Behind everything. Darker than any panel, so panels read as lifted.
  static const ground = Color(0xFF0E1116);

  /// The usual panel.
  static const surface = Color(0xFF161A21);

  /// A panel on a panel — inputs, cards, the thing under the cursor.
  static const raised = Color(0xFF1D222B);

  /// Hover, one step up from raised.
  static const hover = Color(0xFF242B36);

  static const line = Color(0xFF2A313C);
  static const lineSoft = Color(0xFF212733);

  static const ink = Color(0xFFE4E9F0);
  static const inkMid = Color(0xFFA6B0BF);
  static const inkDim = Color(0xFF78828F);

  /// The accent. Warm, because everything else is cold, and because it is the
  /// colour of the first frame the engine ever rendered.
  static const ember = Color(0xFFE5893F);
  static const emberDeep = Color(0xFFC25E22);
  static const emberWash = Color(0x1FE5893F);

  static const good = Color(0xFF5FB483);
  static const warn = Color(0xFFE0B252);
  static const bad = Color(0xFFD9634F);
}

/// Spacing, on a scale rather than by eye.
///
/// Everything is a multiple of four. Editors go wrong when each panel picks
/// its own padding, and the fix is a scale small enough that nobody needs a
/// value between two of its steps.
abstract final class Space {
  static const xs = 4.0;
  static const sm = 8.0;
  static const md = 12.0;
  static const lg = 16.0;
  static const xl = 24.0;
  static const xxl = 36.0;
}

abstract final class Radii {
  /// Controls: inputs, buttons, chips.
  static const control = 5.0;

  /// Panels and cards.
  static const panel = 8.0;

  /// The window itself.
  static const window = 12.0;
}

/// Type, with the roles named rather than the sizes.
///
/// The system face is used deliberately: on macOS it is San Francisco, which is
/// better at eleven point in a dense inspector than anything worth bundling.
/// Monospace is reserved for things that are read as data — paths, numbers,
/// identifiers — where proportional spacing actively costs comprehension.
abstract final class OrblitText {
  static const _mono = ['Menlo', 'SF Mono', 'Consolas', 'monospace'];

  static const display = TextStyle(
    fontSize: 26,
    height: 1.15,
    fontWeight: FontWeight.w600,
    letterSpacing: -0.5,
    color: OrblitColors.ink,
  );

  static const title = TextStyle(
    fontSize: 15,
    height: 1.3,
    fontWeight: FontWeight.w600,
    letterSpacing: -0.1,
    color: OrblitColors.ink,
  );

  static const body = TextStyle(
    fontSize: 13,
    height: 1.45,
    color: OrblitColors.inkMid,
  );

  /// Panel headers and field names.
  static const label = TextStyle(
    fontSize: 12,
    height: 1.3,
    fontWeight: FontWeight.w500,
    color: OrblitColors.inkMid,
  );

  /// Section headings inside panels.
  static const section = TextStyle(
    fontSize: 10.5,
    height: 1.2,
    fontWeight: FontWeight.w600,
    letterSpacing: 0.9,
    color: OrblitColors.inkDim,
  );

  static const caption = TextStyle(
    fontSize: 11.5,
    height: 1.4,
    color: OrblitColors.inkDim,
  );

  /// Paths, identifiers, coordinates.
  static const mono = TextStyle(
    fontFamily: 'Menlo',
    fontFamilyFallback: _mono,
    fontSize: 11.5,
    height: 1.4,
    color: OrblitColors.inkDim,
  );

  static const monoValue = TextStyle(
    fontFamily: 'Menlo',
    fontFamilyFallback: _mono,
    fontSize: 12,
    height: 1.3,
    color: OrblitColors.ink,
  );
}

/// The Material theme, so ordinary widgets land in the same world as the
/// hand-built ones rather than arriving in Material's defaults.
ThemeData orblitTheme() {
  const scheme = ColorScheme.dark(
    primary: OrblitColors.ember,
    onPrimary: Color(0xFF1A1206),
    secondary: OrblitColors.ember,
    surface: OrblitColors.surface,
    onSurface: OrblitColors.ink,
    error: OrblitColors.bad,
  );

  return ThemeData(
    useMaterial3: true,
    colorScheme: scheme,
    scaffoldBackgroundColor: OrblitColors.ground,
    canvasColor: OrblitColors.surface,
    dividerColor: OrblitColors.line,
    splashFactory: NoSplash.splashFactory,
    highlightColor: Colors.transparent,
    textTheme: const TextTheme(
      bodyMedium: OrblitText.body,
      labelMedium: OrblitText.label,
    ),
    sliderTheme: const SliderThemeData(
      trackHeight: 3,
      activeTrackColor: OrblitColors.ember,
      inactiveTrackColor: OrblitColors.line,
      thumbColor: OrblitColors.ember,
      overlayColor: OrblitColors.emberWash,
      thumbShape: RoundSliderThumbShape(enabledThumbRadius: 6.5),
      overlayShape: RoundSliderOverlayShape(overlayRadius: 14),
    ),
    scrollbarTheme: ScrollbarThemeData(
      thumbColor: WidgetStatePropertyAll(OrblitColors.line),
      thickness: const WidgetStatePropertyAll(7),
      radius: const Radius.circular(4),
    ),
    tooltipTheme: TooltipThemeData(
      decoration: BoxDecoration(
        color: OrblitColors.raised,
        border: Border.all(color: OrblitColors.line),
        borderRadius: BorderRadius.circular(Radii.control),
      ),
      textStyle: OrblitText.caption.copyWith(color: OrblitColors.ink),
      waitDuration: const Duration(milliseconds: 500),
    ),
  );
}

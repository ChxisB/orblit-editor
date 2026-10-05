import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';

/// How much weight a button carries.
enum ButtonTone {
  /// The one thing this screen is for. At most one per view.
  primary,

  /// A real action, but not the point of the screen.
  normal,

  /// Available without asking to be noticed.
  quiet,

  /// A menu on a bar: its name and nothing else until the pointer is on it,
  /// so a row of them reads as a menu bar rather than a row of buttons.
  flat,
}

/// A button, sized and coloured for a tool rather than for a phone.
///
/// Material's defaults are built for touch: tall, rounded, with a ripple. An
/// editor is used with a mouse for hours, so these are shorter, squarer, and
/// respond by changing colour rather than by animating.
class OrblitButton extends StatefulWidget {
  const OrblitButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.tone = ButtonTone.normal,
    this.expand = false,
    this.tooltip,
  });

  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final ButtonTone tone;

  /// Fills its parent's width, for a stacked column of actions.
  final bool expand;

  /// What pointing at it says, for a button whose label cannot say it all.
  final String? tooltip;

  double get _padding => tone == ButtonTone.flat ? Space.sm : Space.md;

  /// How much of its width is not room for its content: the padding, and
  /// the border on each side.
  double get _inset => _padding * 2 + 2;

  @override
  State<OrblitButton> createState() => _OrblitButtonState();
}

class _OrblitButtonState extends State<OrblitButton> {
  bool _hovering = false;
  bool _pressed = false;

  bool get _enabled => widget.onPressed != null;

  Color get _background {
    if (!_enabled) return OrblitColors.raised.withValues(alpha: 0.5);
    return switch (widget.tone) {
      ButtonTone.primary =>
        _pressed ? OrblitColors.emberDeep : OrblitColors.ember,
      ButtonTone.normal => _hovering ? OrblitColors.hover : OrblitColors.raised,
      ButtonTone.quiet ||
      ButtonTone.flat => _hovering ? OrblitColors.hover : Colors.transparent,
    };
  }

  Color get _foreground {
    if (!_enabled) return OrblitColors.inkDim;
    return switch (widget.tone) {
      // Near-black on ember rather than white: the accent is bright enough
      // that white text on it is the lower-contrast choice, not the higher.
      ButtonTone.primary => OrblitColors.emberInk,
      ButtonTone.normal => OrblitColors.ink,
      ButtonTone.quiet ||
      ButtonTone.flat => _hovering ? OrblitColors.ink : OrblitColors.inkMid,
    };
  }

  @override
  Widget build(BuildContext context) {
    final content = _ButtonContent(
      label: widget.label,
      icon: widget.icon,
      colour: _foreground,
      expand: widget.expand,
    );

    final button = MouseRegion(
      cursor: _enabled ? SystemMouseCursors.click : SystemMouseCursors.basic,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTapDown: _enabled ? (_) => setState(() => _pressed = true) : null,
        onTapUp: _enabled ? (_) => setState(() => _pressed = false) : null,
        onTapCancel: _enabled ? () => setState(() => _pressed = false) : null,
        onTap: widget.onPressed,
        child: AnimatedContainer(
          duration: const Duration(milliseconds: 90),
          height: widget.tone == ButtonTone.flat ? 28 : 30,
          padding: EdgeInsets.symmetric(horizontal: widget._padding),
          decoration: BoxDecoration(
            color: _background,
            borderRadius: BorderRadius.circular(Radii.control),
            border: Border.all(
              color: switch (widget.tone) {
                ButtonTone.primary || ButtonTone.flat => Colors.transparent,
                _ when _hovering && _enabled => OrblitColors.line,
                _ => OrblitColors.lineSoft,
              },
            ),
          ),
          child: content,
        ),
      ),
    );
    if (widget.tooltip case final message?) {
      return Tooltip(message: message, child: button);
    }
    return button;
  }
}

/// Buttons side by side, or one above another when a narrow panel would
/// cut a word off one of them.
///
/// Stacked rather than shrunk to icons, because the words are what tell
/// somebody new what each one does. Side by side, they show their icons
/// only if all of them can, so a row never mixes the two.
final class OrblitButtonRow extends StatelessWidget {
  const OrblitButtonRow({super.key, required this.buttons});

  /// Each fills its share, so each is built with `expand`.
  final List<OrblitButton> buttons;

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final gaps = Space.xs * (buttons.length - 1);
      final share = (constraints.maxWidth - gaps) / buttons.length;
      final fits = [
        for (final one in buttons)
          _ButtonContent.fitOf(
            context,
            one.label,
            one.icon,
            share - one._inset,
          ),
      ];
      if (fits.contains(_ButtonFit.icon)) {
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          spacing: Space.xs,
          children: buttons,
        );
      }
      return _SharedFit(
        fit: fits.contains(_ButtonFit.word)
            ? _ButtonFit.word
            : _ButtonFit.iconAndWord,
        child: Row(
          spacing: Space.xs,
          children: [for (final one in buttons) Expanded(child: one)],
        ),
      );
    },
  );
}

enum _ButtonFit { iconAndWord, word, icon }

/// What a row of buttons settled on for all of them.
final class _SharedFit extends InheritedWidget {
  const _SharedFit({required this.fit, required super.child});

  final _ButtonFit fit;

  static _ButtonFit? of(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<_SharedFit>()?.fit;

  @override
  bool updateShouldNotify(_SharedFit oldWidget) => fit != oldWidget.fit;
}

/// A button's icon and word, or as much of them as its room holds.
///
/// One that fills its parent gives up its icon first and its word last,
/// because the word is what tells somebody new what it does. Only a button
/// too narrow for its word gets the icon alone, and that says the word when
/// pointed at.
final class _ButtonContent extends StatelessWidget {
  const _ButtonContent({
    required this.label,
    required this.icon,
    required this.colour,
    required this.expand,
  });

  static const _iconSize = 15.0;
  static const _iconGap = Space.sm;

  final String label;
  final IconData? icon;
  final Color colour;
  final bool expand;

  static final _measured = OrblitText.label.copyWith(
    fontWeight: FontWeight.w500,
  );

  TextStyle get _wording => _measured.copyWith(color: colour);

  /// What of a button fits in [room]. [_ButtonFit.icon] means its word does
  /// not, even for a button with no icon to fall back on.
  static _ButtonFit fitOf(
    BuildContext context,
    String label,
    IconData? icon,
    double room,
  ) {
    // Merged as Text merges it, or an inherited letter spacing makes the
    // word wider on screen than it measured.
    final painter = TextPainter(
      text: TextSpan(
        text: label,
        style: DefaultTextStyle.of(context).style.merge(_measured),
      ),
      textDirection: TextDirection.ltr,
      textScaler: MediaQuery.textScalerOf(context),
    )..layout();
    final word = painter.width;
    painter.dispose();
    if (word > room) return _ButtonFit.icon;
    if (icon != null && _iconSize + _iconGap + word <= room) {
      return _ButtonFit.iconAndWord;
    }
    return _ButtonFit.word;
  }

  @override
  Widget build(BuildContext context) {
    if (!expand) {
      return Row(
        mainAxisSize: MainAxisSize.min,
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          if (icon != null) ...[
            Icon(icon, size: _iconSize, color: colour),
            const SizedBox(width: _iconGap),
          ],
          Text(label, style: _wording),
        ],
      );
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final fit = icon == null
            ? _ButtonFit.word
            : _SharedFit.of(context) ??
                  fitOf(context, label, icon, constraints.maxWidth);
        final row = Row(
          mainAxisAlignment: fit == _ButtonFit.icon
              ? MainAxisAlignment.center
              : MainAxisAlignment.start,
          children: [
            if (fit != _ButtonFit.word)
              Icon(icon, size: _iconSize, color: colour),
            if (fit == _ButtonFit.iconAndWord) const SizedBox(width: _iconGap),
            // Flexible, since a label long enough to overflow is a
            // translation away rather than a hypothetical.
            if (fit != _ButtonFit.icon)
              Flexible(
                child: Text(
                  label,
                  overflow: TextOverflow.ellipsis,
                  style: _wording,
                ),
              ),
          ],
        );
        return fit == _ButtonFit.icon
            ? Tooltip(message: label, child: row)
            : row;
      },
    );
  }
}

/// A titled region.
class OrblitPanel extends StatelessWidget {
  const OrblitPanel({
    super.key,
    required this.child,
    this.title,
    this.trailing,
    this.padding = const EdgeInsets.all(Space.lg),
  });

  final Widget child;
  final String? title;
  final Widget? trailing;
  final EdgeInsets padding;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: OrblitColors.surface,
        borderRadius: BorderRadius.circular(Radii.card),
        border: Border.all(color: OrblitColors.lineSoft),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          if (title != null)
            Container(
              height: 36,
              padding: const EdgeInsets.symmetric(horizontal: Space.lg),
              decoration: const BoxDecoration(
                border: Border(
                  bottom: BorderSide(color: OrblitColors.lineSoft),
                ),
              ),
              child: Row(
                children: [
                  Text(title!.toUpperCase(), style: OrblitText.section),
                  const Spacer(),
                  ?trailing,
                ],
              ),
            ),
          Flexible(
            child: Padding(padding: padding, child: child),
          ),
        ],
      ),
    );
  }
}

/// One group of fields inside a side panel, which folds away to its title.
///
/// The inspector, the mesh panel, the modelling panel and the UI editor each
/// stack several of these down a column, and they have to agree: a panel whose
/// groups sit on different margins or whose headings are different heights
/// reads as broken rather than as varied.
///
/// Whether it is open is remembered by title in the page storage, so a group
/// someone folded stays folded when the inspector is showing a different
/// object.
class OrblitSection extends StatefulWidget {
  const OrblitSection({super.key, required this.title, required this.child});

  final String title;
  final Widget child;

  @override
  State<OrblitSection> createState() => _OrblitSectionState();
}

class _OrblitSectionState extends State<OrblitSection> {
  bool _open = true;
  bool _hovering = false;

  @override
  void initState() {
    super.initState();
    final stored = PageStorage.maybeOf(
      context,
    )?.readState(context, identifier: widget.title);
    if (stored is bool) _open = stored;
  }

  void _toggle() {
    setState(() => _open = !_open);
    PageStorage.maybeOf(
      context,
    )?.writeState(context, _open, identifier: widget.title);
  }

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: const BoxDecoration(
        border: Border(top: BorderSide(color: OrblitColors.lineSoft)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          MouseRegion(
            cursor: SystemMouseCursors.click,
            onEnter: (_) => setState(() => _hovering = true),
            onExit: (_) => setState(() => _hovering = false),
            child: Semantics(
              button: true,
              expanded: _open,
              child: GestureDetector(
                behavior: HitTestBehavior.opaque,
                onTap: _toggle,
                child: Container(
                  height: 36,
                  color: _hovering ? OrblitColors.hover : null,
                  padding: const EdgeInsets.symmetric(horizontal: Space.lg),
                  child: Row(
                    spacing: 6,
                    children: [
                      Icon(
                        _open ? Icons.expand_more : Icons.chevron_right,
                        size: 14,
                        color: OrblitColors.inkDim,
                      ),
                      // A side panel at the smallest window is narrower than
                      // the longest heading.
                      Flexible(
                        child: Text(
                          widget.title,
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: OrblitText.panelTitle,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
          if (_open)
            Padding(
              padding: const EdgeInsets.fromLTRB(
                Space.lg,
                Space.xs,
                Space.lg,
                Space.lg,
              ),
              child: widget.child,
            ),
        ],
      ),
    );
  }
}

/// A single-line text input.
class OrblitField extends StatelessWidget {
  const OrblitField({
    super.key,
    required this.controller,
    this.hint,
    this.mono = false,
    this.autofocus = false,
    this.onSubmitted,
    this.onChanged,
    this.focusNode,
    this.suffix,
  });

  final TextEditingController controller;
  final String? hint;

  /// For paths and identifiers, where proportional spacing costs more than it
  /// gives.
  final bool mono;

  final bool autofocus;
  final ValueChanged<String>? onSubmitted;

  /// Called on every keystroke, for a field whose value is live.
  final ValueChanged<String>? onChanged;

  final FocusNode? focusNode;
  final Widget? suffix;

  @override
  Widget build(BuildContext context) {
    final style = mono
        ? OrblitText.monoValue
        : OrblitText.body.copyWith(color: OrblitColors.ink, fontSize: 13);

    return Container(
      height: 32,
      decoration: BoxDecoration(
        color: OrblitColors.ground,
        borderRadius: BorderRadius.circular(Radii.control),
        border: Border.all(color: OrblitColors.line),
      ),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              controller: controller,
              focusNode: focusNode,
              autofocus: autofocus,
              onSubmitted: onSubmitted,
              onChanged: onChanged,
              style: style,
              cursorColor: OrblitColors.ember,
              cursorWidth: 1.5,
              decoration: InputDecoration(
                isDense: true,
                border: InputBorder.none,
                contentPadding: const EdgeInsets.symmetric(
                  horizontal: Space.md,
                  vertical: Space.sm,
                ),
                hintText: hint,
                hintStyle: style.copyWith(color: OrblitColors.inkDim),
              ),
            ),
          ),
          if (suffix != null)
            Padding(
              padding: const EdgeInsets.only(right: Space.xs),
              child: suffix,
            ),
        ],
      ),
    );
  }
}

/// A heading inside a panel.
class SectionLabel extends StatelessWidget {
  const SectionLabel(this.text, {super.key, this.padding});

  final String text;
  final EdgeInsets? padding;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: padding ?? const EdgeInsets.only(bottom: Space.sm),
      child: Text(text.toUpperCase(), style: OrblitText.section),
    );
  }
}

/// Asks for a single name, and returns it trimmed, or null if cancelled.
///
/// A widget rather than a controller made at the call site, because a
/// controller disposed as soon as `showDialog` returns is still being read by
/// the dialog's own exit animation — which throws, once, in a place that has
/// nothing to do with where it was created.
Future<String?> promptForName(
  BuildContext context, {
  required String title,
  required String initial,
  String? hint,
  String action = 'OK',
}) {
  return showDialog<String>(
    context: context,
    builder: (context) =>
        _NamePrompt(title: title, initial: initial, hint: hint, action: action),
  );
}

class _NamePrompt extends StatefulWidget {
  const _NamePrompt({
    required this.title,
    required this.initial,
    required this.hint,
    required this.action,
  });

  final String title;
  final String initial;
  final String? hint;
  final String action;

  @override
  State<_NamePrompt> createState() => _NamePromptState();
}

class _NamePromptState extends State<_NamePrompt> {
  late final TextEditingController _controller =
      TextEditingController(text: widget.initial)
        ..selection = TextSelection(
          baseOffset: 0,
          extentOffset: widget.initial.length,
        );

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  void _accept() => Navigator.of(context).pop(_controller.text.trim());

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      backgroundColor: OrblitColors.surface,
      title: Text(widget.title, style: OrblitText.title),
      content: SizedBox(
        // Bounded on both axes: an AlertDialog gives its content whatever room
        // it asks for, and a Column that asks for infinity gets it.
        width: 320,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            TextField(
              controller: _controller,
              autofocus: true,
              style: OrblitText.body,
              cursorColor: OrblitColors.ember,
              onSubmitted: (_) => _accept(),
            ),
            if (widget.hint != null) ...[
              const SizedBox(height: Space.sm),
              Text(widget.hint!, style: OrblitText.caption),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        TextButton(onPressed: _accept, child: Text(widget.action)),
      ],
    );
  }
}

/// A text box that takes a value rather than a controller.
///
/// The controller version is right when the caller wants to drive the box —
/// select its contents, put focus in it, read it back on submit. Most places
/// only want "here is a string, tell me when it changes", and each of those
/// writing its own StatefulWidget to own a controller is how a codebase ends
/// up with six subtly different text boxes.
///
/// Reports on every keystroke, and adopts a value changed from outside only
/// while the box does not have focus — otherwise a rebuild mid-word would put
/// the cursor back at the start.
class ValueField extends StatefulWidget {
  const ValueField({
    super.key,
    required this.value,
    required this.onChanged,
    this.hint,
    this.mono = false,
    this.onDone,
  });

  final String value;
  final ValueChanged<String> onChanged;
  final String? hint;
  final bool mono;

  /// Called when the box loses focus, for callers that seal an undo step.
  final VoidCallback? onDone;

  @override
  State<ValueField> createState() => _ValueFieldState();
}

class _ValueFieldState extends State<ValueField> {
  late final TextEditingController _controller = TextEditingController(
    text: widget.value,
  );
  final FocusNode _focus = FocusNode();

  @override
  void initState() {
    super.initState();
    _focus.addListener(() {
      if (!_focus.hasFocus) widget.onDone?.call();
    });
  }

  @override
  void didUpdateWidget(ValueField old) {
    super.didUpdateWidget(old);
    if (widget.value != _controller.text && !_focus.hasFocus) {
      _controller.text = widget.value;
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    _focus.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return OrblitField(
      controller: _controller,
      focusNode: _focus,
      hint: widget.hint,
      mono: widget.mono,
      onChanged: widget.onChanged,
      onSubmitted: widget.onChanged,
    );
  }
}

/// A sentence in the middle of a panel with nothing to show, saying what
/// would be there.
final class PanelMessage extends StatelessWidget {
  const PanelMessage(this.text, {super.key});

  final String text;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(Space.lg),
        child: Text(
          text,
          style: OrblitText.caption,
          textAlign: TextAlign.center,
        ),
      ),
    );
  }
}

/// How a menu that drops from a control looks: raised, square, and lined
/// like the panel it opens over.
const orblitMenuStyle = MenuStyle(
  backgroundColor: WidgetStatePropertyAll(OrblitColors.raised),
  surfaceTintColor: WidgetStatePropertyAll(Colors.transparent),
  shape: WidgetStatePropertyAll(
    RoundedRectangleBorder(
      borderRadius: BorderRadius.all(Radius.circular(Radii.card)),
      side: BorderSide(color: OrblitColors.line),
    ),
  ),
);

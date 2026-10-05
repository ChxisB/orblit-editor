import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import '../theme/orblit_theme.dart';
import '../widgets/keycap.dart';

/// The heading an entry is listed under, in the order the headings run.
enum PaletteGroup {
  commands('Commands'),
  objects('Objects');

  const PaletteGroup(this.title);

  final String title;
}

/// One thing the palette can find and run.
final class PaletteEntry {
  const PaletteEntry({
    required this.group,
    required this.label,
    required this.icon,
    required this.onRun,
    this.meta = '',
    this.keys,
  });

  final PaletteGroup group;
  final String label;
  final IconData icon;

  /// Where it lives or what it works on, in small type after the label.
  final String meta;

  /// The shortcut that does the same thing, if there is one.
  final String? keys;

  final VoidCallback onRun;
}

/// An entry that matched, and which part of its label did.
final class PaletteMatch {
  const PaletteMatch(this.entry, this.hitStart, this.hitEnd);

  final PaletteEntry entry;

  /// The span of the label to light up. Empty when nothing was typed.
  final int hitStart;
  final int hitEnd;
}

/// The entries whose label has every word of [query] in it, a group at a time
/// and the earliest hit first.
///
/// Few are kept for each group while nothing is typed, because a scene with a
/// thousand objects is a list nobody reads, and more once there is something
/// to narrow it by.
List<PaletteMatch> searchPalette(List<PaletteEntry> entries, String query) {
  final words = query
      .toLowerCase()
      .split(RegExp(r'\s+'))
      .where((word) => word.isNotEmpty)
      .toList();
  final limit = words.isEmpty ? 6 : 12;

  final found = <(int, PaletteMatch)>[];
  for (var i = 0; i < entries.length; i++) {
    final match = _match(entries[i], words);
    if (match != null) found.add((i, match));
  }
  found.sort((a, b) {
    final byHit = a.$2.hitStart.compareTo(b.$2.hitStart);
    return byHit != 0 ? byHit : a.$1.compareTo(b.$1);
  });

  return [
    for (final group in PaletteGroup.values)
      ...found
          .where((each) => each.$2.entry.group == group)
          .take(limit)
          .map((each) => each.$2),
  ];
}

PaletteMatch? _match(PaletteEntry entry, List<String> words) {
  if (words.isEmpty) return PaletteMatch(entry, 0, 0);
  final label = entry.label.toLowerCase();
  final first = label.indexOf(words.first);
  if (first < 0 || words.any((word) => !label.contains(word))) return null;
  return PaletteMatch(entry, first, first + words.first.length);
}

/// Opens the palette over the editor. Gives back what was chosen, so the
/// caller runs it once the palette is gone and not from under it.
Future<PaletteEntry?> showCommandPalette(
  BuildContext context,
  List<PaletteEntry> entries,
) => showGeneralDialog<PaletteEntry>(
  context: context,
  barrierDismissible: true,
  barrierLabel: 'Close the command palette',
  barrierColor: const Color(0xA8080A0E),
  transitionDuration: const Duration(milliseconds: 90),
  pageBuilder: (context, _, _) => _CommandPalette(entries: entries),
  transitionBuilder: (context, animation, _, child) =>
      FadeTransition(opacity: animation, child: child),
);

class _CommandPalette extends StatefulWidget {
  const _CommandPalette({required this.entries});

  final List<PaletteEntry> entries;

  @override
  State<_CommandPalette> createState() => _CommandPaletteState();
}

class _CommandPaletteState extends State<_CommandPalette> {
  final _query = TextEditingController();
  final _selectedRow = GlobalKey();
  late List<PaletteMatch> _results = searchPalette(widget.entries, '');
  int _index = 0;

  @override
  void dispose() {
    _query.dispose();
    super.dispose();
  }

  void _search(String text) => setState(() {
    _results = searchPalette(widget.entries, text);
    _index = 0;
  });

  void _move(int by) {
    if (_results.isEmpty) return;
    setState(() => _index = (_index + by) % _results.length);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      final row = _selectedRow.currentContext;
      if (row != null) Scrollable.ensureVisible(row);
    });
  }

  void _run() {
    if (_results.isEmpty) return;
    Navigator.of(context).pop(_results[_index].entry);
  }

  KeyEventResult _key(FocusNode node, KeyEvent event) {
    if (event is KeyUpEvent) return KeyEventResult.ignored;
    final key = event.logicalKey;
    if (key == LogicalKeyboardKey.arrowDown) {
      _move(1);
    } else if (key == LogicalKeyboardKey.arrowUp) {
      _move(_results.length - 1);
    } else if (key == LogicalKeyboardKey.enter ||
        key == LogicalKeyboardKey.numpadEnter) {
      _run();
    } else if (key == LogicalKeyboardKey.escape) {
      Navigator.of(context).pop();
    } else {
      return KeyEventResult.ignored;
    }
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    return Align(
      alignment: Alignment.topCenter,
      child: Padding(
        padding: const EdgeInsets.only(
          top: 104,
          left: Space.lg,
          right: Space.lg,
        ),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 640),
          child: Material(
            color: OrblitColors.surface,
            borderRadius: BorderRadius.circular(14),
            clipBehavior: Clip.antiAlias,
            elevation: 24,
            shadowColor: const Color(0x99000000),
            child: DecoratedBox(
              decoration: BoxDecoration(
                borderRadius: BorderRadius.circular(14),
                border: Border.all(color: const Color(0x17FFFFFF)),
              ),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Focus(
                    onKeyEvent: _key,
                    child: _SearchField(controller: _query, onChanged: _search),
                  ),
                  const Divider(height: 1, color: OrblitColors.lineSoft),
                  Flexible(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxHeight: 420),
                      child: _results.isEmpty ? const _NothingFound() : _rows(),
                    ),
                  ),
                  const Divider(height: 1, color: OrblitColors.lineSoft),
                  _Footer(count: _results.length),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _rows() {
    final children = <Widget>[];
    PaletteGroup? heading;
    for (var i = 0; i < _results.length; i++) {
      final match = _results[i];
      if (match.entry.group != heading) {
        heading = match.entry.group;
        children.add(_Heading(heading.title));
      }
      children.add(
        _ResultRow(
          key: i == _index ? _selectedRow : null,
          match: match,
          selected: i == _index,
          onTap: () => Navigator.of(context).pop(match.entry),
        ),
      );
    }
    return ListView(
      shrinkWrap: true,
      padding: const EdgeInsets.fromLTRB(Space.sm, 6, Space.sm, 10),
      children: children,
    );
  }
}

class _SearchField extends StatelessWidget {
  const _SearchField({required this.controller, required this.onChanged});

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 54,
    child: Padding(
      padding: const EdgeInsets.only(left: 18, right: 14),
      child: Row(
        spacing: Space.md,
        children: [
          const Icon(Icons.search, size: 18, color: OrblitColors.inkDim),
          Expanded(
            child: TextField(
              controller: controller,
              onChanged: onChanged,
              autofocus: true,
              cursorColor: OrblitColors.ember,
              style: const TextStyle(fontSize: 15, color: OrblitColors.ink),
              decoration: const InputDecoration.collapsed(
                hintText: 'Search commands and objects',
                hintStyle: TextStyle(fontSize: 15, color: OrblitColors.inkDim),
              ),
            ),
          ),
          const Keycap('esc'),
        ],
      ),
    ),
  );
}

class _Heading extends StatelessWidget {
  const _Heading(this.title);

  final String title;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.fromLTRB(10, 12, 10, 4),
    child: Text(
      title,
      style: OrblitText.caption.copyWith(
        fontWeight: FontWeight.w600,
        color: OrblitColors.inkDim,
      ),
    ),
  );
}

class _ResultRow extends StatefulWidget {
  const _ResultRow({
    super.key,
    required this.match,
    required this.selected,
    required this.onTap,
  });

  final PaletteMatch match;
  final bool selected;
  final VoidCallback onTap;

  @override
  State<_ResultRow> createState() => _ResultRowState();
}

class _ResultRowState extends State<_ResultRow> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) {
    final entry = widget.match.entry;
    return MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: widget.onTap,
        child: Container(
          height: 36,
          margin: const EdgeInsets.only(bottom: 2),
          padding: const EdgeInsets.symmetric(horizontal: 10),
          decoration: BoxDecoration(
            color: widget.selected
                ? OrblitColors.emberWash
                : (_hovering ? OrblitColors.hover : null),
            borderRadius: BorderRadius.circular(Radii.card),
          ),
          child: Row(
            spacing: Space.md,
            children: [
              Icon(
                entry.icon,
                size: 15,
                color: widget.selected
                    ? OrblitColors.ember
                    : OrblitColors.inkDim,
              ),
              Expanded(child: _Label(widget.match)),
              if (entry.meta.isNotEmpty)
                Text(entry.meta, style: OrblitText.caption),
              if (entry.keys case final keys?) Keycap(keys),
            ],
          ),
        ),
      ),
    );
  }
}

/// The label with the part that matched lit in the accent.
class _Label extends StatelessWidget {
  const _Label(this.match);

  final PaletteMatch match;

  @override
  Widget build(BuildContext context) {
    final label = match.entry.label;
    const base = TextStyle(fontSize: 13, color: OrblitColors.ink);
    return Text.rich(
      TextSpan(
        style: base,
        children: [
          TextSpan(text: label.substring(0, match.hitStart)),
          TextSpan(
            text: label.substring(match.hitStart, match.hitEnd),
            style: const TextStyle(
              color: OrblitColors.ember,
              fontWeight: FontWeight.w600,
            ),
          ),
          TextSpan(text: label.substring(match.hitEnd)),
        ],
      ),
      maxLines: 1,
      overflow: TextOverflow.ellipsis,
    );
  }
}

class _NothingFound extends StatelessWidget {
  const _NothingFound();

  @override
  Widget build(BuildContext context) => const Padding(
    padding: EdgeInsets.symmetric(vertical: Space.xl),
    child: Center(child: Text('Nothing matches.', style: OrblitText.caption)),
  );
}

class _Footer extends StatelessWidget {
  const _Footer({required this.count});

  final int count;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: 40,
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: Space.lg),
      child: Row(
        spacing: Space.lg,
        children: [
          const _Hint(keys: '↑ ↓', what: 'Move'),
          const _Hint(keys: '↵', what: 'Run'),
          const Spacer(),
          Text(
            count == 1 ? '1 result' : '$count results',
            style: OrblitText.caption,
          ),
        ],
      ),
    ),
  );
}

class _Hint extends StatelessWidget {
  const _Hint({required this.keys, required this.what});

  final String keys;
  final String what;

  @override
  Widget build(BuildContext context) => Row(
    spacing: 6,
    children: [
      Keycap(keys),
      Text(what, style: OrblitText.caption),
    ],
  );
}

import 'dart:async';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../editor/assets.dart' show sizeLabel;
import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'install_session.dart';

/// A downloaded kit: its scripts, if any, the folder it goes into, and a tick
/// for each file.
///
/// A file the project already has with other bytes is unticked and marked,
/// and is replaced only when the person ticks it.
class KitChoose extends StatefulWidget {
  const KitChoose({super.key, required this.session});

  final InstallSession session;

  @override
  State<KitChoose> createState() => _KitChooseState();
}

class _KitChooseState extends State<KitChoose> {
  late final TextEditingController _folder = TextEditingController(
    text: 'assets/kits/${widget.session.row.manifest.slug}',
  );

  /// Files the person unticked.
  final Set<AssetId> _left = {};

  /// Files the project has that the person ticked to replace.
  final Set<AssetId> _replace = {};

  /// Files the project has with other bytes, or null while they are read or
  /// when the folder is not one a kit may go into.
  Set<AssetId>? _conflicts;
  String? _readProblem;

  // Each read of these builds a map, so they are read once.
  late final Map<AssetId, int> _files = widget.session.kit!.files;
  late final List<AssetId> _scripts = widget.session.kit!.scripts;

  String get _into => _folder.text.trim();

  @override
  void initState() {
    super.initState();
    unawaited(_readConflicts());
  }

  @override
  void dispose() {
    _folder.dispose();
    super.dispose();
  }

  Future<void> _readConflicts() async {
    final into = _into;
    if (kitPlaceProblem(into, _files.keys) != null) {
      _conflicts = null;
      return;
    }
    List<AssetId>? found;
    String? problem;
    try {
      found = await widget.session.conflicts(into: into);
    } on Exception catch (error) {
      problem = '$error';
    }
    if (!mounted || into != _into) return;
    setState(() {
      _conflicts = found?.toSet();
      _readProblem = problem;
      _replace.removeWhere((id) => found == null || !found.contains(id));
    });
  }

  bool _isConflict(AssetId id) => _conflicts?.contains(id) ?? false;

  bool _picked(AssetId id) =>
      _isConflict(id) ? _replace.contains(id) : !_left.contains(id);

  void _toggle(AssetId id) => setState(() {
    final set = _isConflict(id) ? _replace : _left;
    if (!set.add(id)) set.remove(id);
  });

  void _folderChanged() {
    setState(() {
      _conflicts = null;
      _readProblem = null;
    });
    unawaited(_readConflicts());
  }

  void _install(Set<AssetId> picks) => unawaited(
    widget.session.install(into: _into, only: picks, replacing: _replace),
  );

  @override
  Widget build(BuildContext context) {
    final ids = _files.keys.toList();
    final picks = {
      for (final id in ids)
        if (_picked(id)) id,
    };
    final folderProblem = kitPlaceProblem(_into, picks);
    final problem = folderProblem ?? _readProblem ?? widget.session.problem;
    final ready =
        folderProblem == null && picks.isNotEmpty && _conflicts != null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_scripts.isNotEmpty) _ScriptWarning(scripts: _scripts),
        const SectionLabel('Folder in the project'),
        OrblitField(
          controller: _folder,
          mono: true,
          onChanged: (_) => _folderChanged(),
        ),
        if (problem != null)
          Padding(
            padding: const EdgeInsets.only(top: Space.xs),
            child: Text(
              problem,
              style: OrblitText.caption.copyWith(color: OrblitColors.bad),
            ),
          ),
        const SizedBox(height: Space.md),
        SectionLabel('Files (${picks.length} of ${ids.length})'),
        Expanded(
          child: ListView.builder(
            itemCount: ids.length,
            itemBuilder: (context, index) => _FileTick(
              id: ids[index],
              size: _files[ids[index]]!,
              picked: _picked(ids[index]),
              replaces: _isConflict(ids[index]),
              onToggle: () => _toggle(ids[index]),
            ),
          ),
        ),
        const SizedBox(height: Space.sm),
        Align(
          alignment: Alignment.centerRight,
          child: OrblitButton(
            label: 'Install',
            tone: ButtonTone.primary,
            onPressed: ready ? () => _install(picks) : null,
          ),
        ),
      ],
    );
  }
}

/// What a kit with scripts says before anything is written.
class _ScriptWarning extends StatelessWidget {
  const _ScriptWarning({required this.scripts});

  final List<AssetId> scripts;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: Space.md),
      padding: const EdgeInsets.all(Space.sm),
      decoration: BoxDecoration(
        color: OrblitColors.raised,
        border: Border.all(color: OrblitColors.warn),
        borderRadius: BorderRadius.circular(Radii.control),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'This kit holds ${scripts.length} '
            '${scripts.length == 1 ? 'script' : 'scripts'}. Orblit never runs '
            'them when it installs a kit. Read them before you use them in '
            'your project.',
            style: OrblitText.caption.copyWith(color: OrblitColors.warn),
          ),
          const SizedBox(height: Space.xs),
          ConstrainedBox(
            constraints: const BoxConstraints(maxHeight: 64),
            child: ListView(
              shrinkWrap: true,
              children: [
                for (final id in scripts) Text('$id', style: OrblitText.mono),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _FileTick extends StatelessWidget {
  const _FileTick({
    required this.id,
    required this.size,
    required this.picked,
    required this.replaces,
    required this.onToggle,
  });

  final AssetId id;
  final int size;
  final bool picked;

  /// The project has this file with other bytes.
  final bool replaces;
  final VoidCallback onToggle;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onToggle,
      child: Row(
        children: [
          Checkbox(
            value: picked,
            activeColor: OrblitColors.ember,
            visualDensity: VisualDensity.compact,
            onChanged: (_) => onToggle(),
          ),
          Expanded(
            child: Text(
              '$id',
              style: OrblitText.mono,
              overflow: TextOverflow.ellipsis,
            ),
          ),
          if (replaces)
            Padding(
              padding: const EdgeInsets.only(right: Space.sm),
              child: Text(
                'replaces your file',
                style: OrblitText.caption.copyWith(color: OrblitColors.warn),
              ),
            ),
          Text(sizeLabel(size), style: OrblitText.caption),
        ],
      ),
    );
  }
}

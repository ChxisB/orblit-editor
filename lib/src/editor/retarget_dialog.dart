import 'package:flutter/material.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';
import '../widgets/orblit_switch.dart';
import 'retarget_model.dart';

/// What the retarget dialog asks of the project, declared beside the one
/// dialog that asks it so the dialog reads no file itself.
abstract interface class RetargetFiles {
  /// Project paths of the models that may have a skeleton.
  List<String> get models;

  /// Project paths of the bone maps.
  List<String> get maps;

  /// The skeleton of [model], or null for a file that has none.
  RestSkeleton? skeletonOf(String model);

  /// The bone map at [path]. Throws [BoneMapFormatException] for a file that
  /// cannot be read or is not one.
  BoneMapLoad readMap(String path);

  /// Writes [map] as a new file called [name] and answers its project path,
  /// or what went wrong.
  ({String? problem, String? path}) writeMap(String name, BoneMap map);
}

/// A clip made to move another model.
final class RetargetMade {
  const RetargetMade({
    required this.clip,
    required this.model,
    required this.problems,
  });

  final ClipDocument clip;

  /// The project path of the model the clip was made to move.
  final String model;

  /// What of the clip could not be carried.
  final List<String> problems;
}

/// Asks which model [clip] was made for, which one it should move, and how
/// their bones pair, and answers the clip made from that, or null.
Future<RetargetMade?> showRetargetDialog(
  BuildContext context, {
  required ClipDocument clip,
  required RetargetFiles files,
  String? from,
}) => showDialog<RetargetMade>(
  context: context,
  builder: (context) => RetargetDialog(clip: clip, files: files, from: from),
);

class RetargetDialog extends StatefulWidget {
  const RetargetDialog({
    super.key,
    required this.clip,
    required this.files,
    this.from,
  });

  final ClipDocument clip;
  final RetargetFiles files;

  /// The model the clip is most likely for, or null.
  final String? from;

  @override
  State<RetargetDialog> createState() => _RetargetDialogState();
}

class _RetargetDialogState extends State<RetargetDialog> {
  late final List<String> _models = widget.files.models;
  late final List<String> _maps = widget.files.maps;
  final Map<String, RestSkeleton?> _skeletons = {};

  late RetargetDraft _draft = RetargetDraft(from: widget.from, map: BoneMap());
  PairTable? _table;
  String? _note;

  @override
  void initState() {
    super.initState();
    _table = _tableFor(_draft);
  }

  RestSkeleton? _skeleton(String? model) => model == null
      ? null
      : _skeletons.putIfAbsent(model, () => widget.files.skeletonOf(model));

  PairTable? _tableFor(RetargetDraft draft) {
    final from = _skeleton(draft.from);
    final to = _skeleton(draft.to);
    if (from == null || to == null) return null;
    return pairTable(from: from, to: to, map: draft.map);
  }

  void _update(RetargetDraft draft, {String? note}) => setState(() {
    _draft = draft;
    _table = _tableFor(draft);
    _note = note;
  });

  void _change(BoneMap map) => _update(_draft.copyWith(map: map));

  String? _missing(String? model) => model != null && _skeleton(model) == null
      ? '${p.basename(model)} has no skeleton.'
      : null;

  void _load(String path) {
    final BoneMapLoad loaded;
    try {
      loaded = widget.files.readMap(path);
    } on BoneMapFormatException catch (error) {
      setState(() => _note = '${p.basename(path)}: ${error.message}');
      return;
    }
    final map = loaded.map;
    _update(
      RetargetDraft(
        from: _models.contains(map.fromAsset) ? map.fromAsset : _draft.from,
        to: _models.contains(map.toAsset) ? map.toAsset : _draft.to,
        map: map,
      ),
      note: loaded.problems.isEmpty
          ? 'Loaded ${p.basename(path)}.'
          : '${loaded.problems.length} things in ${p.basename(path)} could '
                'not be read. First: ${loaded.problems.first}',
    );
  }

  void _save() {
    final from = _draft.from;
    final to = _draft.to;
    if (from == null || to == null) return;
    final name =
        '${p.basenameWithoutExtension(from)}-to-'
        '${p.basenameWithoutExtension(to)}';
    final made = widget.files.writeMap(
      name,
      _draft.map.copyWith(fromAsset: from, toAsset: to),
    );
    setState(
      () => _note = made.path == null
          ? 'Could not save the map: ${made.problem}'
          : 'Saved ${made.path}.',
    );
  }

  void _make() {
    final table = _table;
    final from = _skeleton(_draft.from);
    final to = _skeleton(_draft.to);
    final model = _draft.to;
    if (table == null || from == null || to == null || model == null) return;
    final made = table.map.apply(widget.clip, from: from, to: to);
    Navigator.of(context).pop(
      RetargetMade(
        clip: made.clip.copyWith(name: retargetedName(widget.clip.name, model)),
        model: model,
        problems: made.problems,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final table = _table;
    return AlertDialog(
      backgroundColor: OrblitColors.surface,
      title: Text('Retarget ${widget.clip.name}', style: OrblitText.title),
      content: SizedBox(
        width: 640,
        height: 440,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _Models(
              models: _models,
              from: _draft.from,
              to: _draft.to,
              onFrom: (model) => _update(_draft.copyWith(from: model)),
              onTo: (model) => _update(_draft.copyWith(to: model)),
            ),
            const SizedBox(height: Space.sm),
            _Switches(map: _draft.map, onChanged: _change),
            const SizedBox(height: Space.sm),
            _MapBar(
              maps: _maps,
              canSave: table != null,
              onLoad: _load,
              onSave: _save,
            ),
            const SizedBox(height: Space.sm),
            Expanded(
              child: table == null
                  ? _Hint(
                      text:
                          _missing(_draft.from) ??
                          _missing(_draft.to) ??
                          'Choose the model the clip was made for and the '
                              'one it should move.',
                    )
                  : _Pairs(
                      table: table,
                      // One item to a name, since a drop-down cannot hold
                      // two with the same value.
                      sources: _skeleton(_draft.from)!.names.toSet().toList(),
                      onChanged: _change,
                    ),
            ),
            if (table != null) _Problems(table: table),
            if (_note != null) Text(_note!, style: OrblitText.caption),
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.of(context).pop(),
          child: const Text('Cancel'),
        ),
        TextButton(
          onPressed: table == null ? null : _make,
          child: const Text('Make clip'),
        ),
      ],
    );
  }
}

final class _Models extends StatelessWidget {
  const _Models({
    required this.models,
    required this.from,
    required this.to,
    required this.onFrom,
    required this.onTo,
  });

  final List<String> models;
  final String? from;
  final String? to;
  final ValueChanged<String?> onFrom;
  final ValueChanged<String?> onTo;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      Expanded(
        child: _Pick<String>(
          label: 'Made for',
          value: from,
          items: {for (final model in models) model: model},
          onChanged: onFrom,
        ),
      ),
      const Padding(
        padding: EdgeInsets.symmetric(horizontal: Space.sm),
        child: Icon(Icons.arrow_forward, size: 14, color: OrblitColors.inkDim),
      ),
      Expanded(
        child: _Pick<String>(
          label: 'Move',
          value: to,
          items: {for (final model in models) model: model},
          onChanged: onTo,
        ),
      ),
    ],
  );
}

final class _Switches extends StatelessWidget {
  const _Switches({required this.map, required this.onChanged});

  final BoneMap map;
  final ValueChanged<BoneMap> onChanged;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      OrblitSwitch(
        label: 'Pair by the parts of a person',
        value: map.humanoid,
        onChanged: (on) => onChanged(map.copyWith(humanoid: on)),
      ),
      const SizedBox(width: Space.sm),
      const Text('Pair by body', style: OrblitText.label),
      const SizedBox(width: Space.lg),
      OrblitSwitch(
        label: 'Turn each bone to point the way its driver does at rest',
        value: map.fixRest,
        onChanged: (on) => onChanged(map.copyWith(fixRest: on)),
      ),
      const SizedBox(width: Space.sm),
      const Text('Fix rest pose', style: OrblitText.label),
    ],
  );
}

final class _MapBar extends StatelessWidget {
  const _MapBar({
    required this.maps,
    required this.canSave,
    required this.onLoad,
    required this.onSave,
  });

  final List<String> maps;
  final bool canSave;
  final ValueChanged<String> onLoad;
  final VoidCallback onSave;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      Expanded(
        child: _Pick<String>(
          label: 'Bone map',
          value: null,
          hint: maps.isEmpty ? 'None saved yet' : 'Load a saved map',
          items: {for (final map in maps) map: map},
          onChanged: (map) {
            if (map != null) onLoad(map);
          },
        ),
      ),
      const SizedBox(width: Space.sm),
      TextButton(
        onPressed: canSave ? onSave : null,
        child: const Text('Save map'),
      ),
    ],
  );
}

/// A labelled drop-down, with its value taken from the caller.
final class _Pick<T extends Object> extends StatelessWidget {
  const _Pick({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
    this.hint,
  });

  final String label;
  final T? value;
  final Map<T, String> items;
  final ValueChanged<T?> onChanged;
  final String? hint;

  @override
  Widget build(BuildContext context) => Row(
    children: [
      SizedBox(width: 70, child: Text(label, style: OrblitText.caption)),
      Expanded(
        child: DropdownButtonHideUnderline(
          child: DropdownButton<T>(
            isExpanded: true,
            isDense: true,
            value: value,
            hint: Text(hint ?? 'Choose a model', style: OrblitText.caption),
            dropdownColor: OrblitColors.surface,
            style: OrblitText.label,
            items: [
              for (final MapEntry(:key, value: text) in items.entries)
                DropdownMenuItem<T>(
                  value: key,
                  child: Text(text, overflow: TextOverflow.ellipsis),
                ),
            ],
            onChanged: onChanged,
          ),
        ),
      ),
    ],
  );
}

final class _Hint extends StatelessWidget {
  const _Hint({required this.text});

  final String text;

  @override
  Widget build(BuildContext context) => Center(
    child: Text(text, style: OrblitText.caption, textAlign: TextAlign.center),
  );
}

/// A bone of the target and what moves it: names and the body decide, a
/// bone of the source, or nothing.
typedef _Choice = (PairHow, String?);

final class _Pairs extends StatelessWidget {
  const _Pairs({
    required this.table,
    required this.sources,
    required this.onChanged,
  });

  final PairTable table;
  final List<String> sources;
  final ValueChanged<BoneMap> onChanged;

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        '${table.driven} of ${table.rows.length} bones are moved by something.',
        style: OrblitText.caption,
      ),
      const SizedBox(height: Space.xs),
      Expanded(
        child: ListView.builder(
          itemCount: table.rows.length,
          itemExtent: 30,
          itemBuilder: (context, index) => _PairRowView(
            row: table.rows[index],
            sources: sources,
            onChoose: (choice) => onChanged(switch (choice) {
              (PairHow.hand, final String source) => table.map.withPair(
                table.rows[index].bone,
                source,
              ),
              (PairHow.none, _) => table.map.withUnpaired(
                table.rows[index].bone,
              ),
              _ => table.map.withAuto(table.rows[index].bone),
            }),
          ),
        ),
      ),
    ],
  );
}

final class _PairRowView extends StatelessWidget {
  const _PairRowView({
    required this.row,
    required this.sources,
    required this.onChoose,
  });

  final PairRow row;
  final List<String> sources;
  final ValueChanged<_Choice> onChoose;

  @override
  Widget build(BuildContext context) {
    final auto = row.driver == null ? 'no match' : row.driver!;
    final _Choice current = (
      row.how,
      row.how == PairHow.hand ? row.driver : null,
    );
    return Row(
      children: [
        Expanded(
          flex: 2,
          child: Text(
            row.bone,
            overflow: TextOverflow.ellipsis,
            style: OrblitText.label,
          ),
        ),
        Expanded(
          flex: 3,
          child: DropdownButtonHideUnderline(
            child: DropdownButton<_Choice>(
              key: ValueKey('pair/${row.bone}'),
              isExpanded: true,
              isDense: true,
              value: current,
              dropdownColor: OrblitColors.surface,
              style: OrblitText.label,
              items: [
                DropdownMenuItem<_Choice>(
                  value: (PairHow.auto, null),
                  child: Text('Automatic: $auto'),
                ),
                const DropdownMenuItem<_Choice>(
                  value: (PairHow.none, null),
                  child: Text('Nothing'),
                ),
                for (final source in sources)
                  DropdownMenuItem<_Choice>(
                    value: (PairHow.hand, source),
                    child: Text(source, overflow: TextOverflow.ellipsis),
                  ),
              ],
              onChanged: (choice) {
                if (choice != null) onChoose(choice);
              },
            ),
          ),
        ),
      ],
    );
  }
}

final class _Problems extends StatelessWidget {
  const _Problems({required this.table});

  final PairTable table;

  @override
  Widget build(BuildContext context) {
    final lines = [
      if (table.stale.isNotEmpty)
        'The map names ${table.stale.length} bones these models lack, which '
            'are left out: ${table.stale.take(4).join(', ')}'
            '${table.stale.length > 4 ? ' and more' : ''}.',
      ...table.problems.take(3),
    ];
    if (lines.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: Space.xs),
      child: Text(
        lines.join('\n'),
        style: OrblitText.caption.copyWith(color: OrblitColors.warn),
      ),
    );
  }
}

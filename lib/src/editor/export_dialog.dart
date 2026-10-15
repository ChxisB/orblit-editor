import 'dart:async';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'assets.dart';
import 'export_folder.dart';
import 'export_run.dart';
import 'export_session.dart';
import 'pack_contents.dart';

/// Opens the export dialog over the editor.
///
/// Not dismissed by a click outside, because an export that is running is a
/// cook that cannot be taken back, and a click in the wrong place should not
/// hide where it has got to.
Future<void> showExportDialog(
  BuildContext context, {
  required String projectDirectory,
  required void Function(String path) onReveal,
}) => showDialog<void>(
  context: context,
  barrierDismissible: false,
  builder: (context) =>
      ExportDialog(projectDirectory: projectDirectory, onReveal: onReveal),
);

ExportSession _realSession(String projectDirectory) =>
    ExportSession(projectDirectory: projectDirectory);

enum _View { export, contents }

/// File › Export: pick the targets, say whole pack or patch, run it, and
/// read what each target came to. A second view lists what an export serves.
class ExportDialog extends StatefulWidget {
  const ExportDialog({
    super.key,
    required this.projectDirectory,
    required this.onReveal,
    this.sessionFor = _realSession,
  });

  final String projectDirectory;

  /// Makes the session the dialog shows and then disposes. A test passes one
  /// built on a fake runner, so no cook starts.
  final ExportSession Function(String projectDirectory) sessionFor;

  /// Shows a path in the desktop's file browser.
  final void Function(String path) onReveal;

  @override
  State<ExportDialog> createState() => _ExportDialogState();
}

class _ExportDialogState extends State<ExportDialog> {
  late final ExportSession _session = widget.sessionFor(
    widget.projectDirectory,
  );

  _View _view = _View.export;

  @override
  void initState() {
    super.initState();
    unawaited(_session.load());
  }

  @override
  void dispose() {
    _session.dispose();
    super.dispose();
  }

  void _showContents(ExportedPack pack) {
    setState(() => _view = _View.contents);
    unawaited(_session.show(pack));
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: _session,
      builder: (context, _) => PopScope(
        canPop: !_session.running,
        child: AlertDialog(
          backgroundColor: OrblitColors.surface,
          title: Row(
            children: [
              const Text('Export', style: OrblitText.title),
              const Spacer(),
              _ViewSwitch(
                view: _view,
                onView: (view) => setState(() => _view = view),
              ),
            ],
          ),
          // The dialog clamps this to the window, so at the minimum of
          // 960x620 the body is shorter and its lists scroll.
          content: SizedBox(
            width: 680,
            height: 460,
            child: switch (_view) {
              _View.export => _ExportView(
                session: _session,
                onContents: _showContents,
              ),
              _View.contents => _ContentsView(session: _session),
            },
          ),
          actions: [
            TextButton(
              onPressed: () =>
                  widget.onReveal(exportsPath(widget.projectDirectory)),
              child: const Text('Show exports folder'),
            ),
            TextButton(
              onPressed: _session.running
                  ? null
                  : () => Navigator.of(context).pop(),
              child: const Text('Close'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ViewSwitch extends StatelessWidget {
  const _ViewSwitch({required this.view, required this.onView});

  final _View view;
  final ValueChanged<_View> onView;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      spacing: Space.xs,
      children: [
        OrblitButton(
          label: 'Export',
          tone: view == _View.export ? ButtonTone.normal : ButtonTone.quiet,
          onPressed: () => onView(_View.export),
        ),
        OrblitButton(
          label: 'Contents',
          tone: view == _View.contents ? ButtonTone.normal : ButtonTone.quiet,
          onPressed: () => onView(_View.contents),
        ),
      ],
    );
  }
}

class _ExportView extends StatelessWidget {
  const _ExportView({required this.session, required this.onContents});

  final ExportSession session;
  final ValueChanged<ExportedPack> onContents;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const SectionLabel('Targets'),
        _TargetPicker(session: session),
        const SizedBox(height: Space.lg),
        const SectionLabel('Make'),
        _BasePicker(session: session),
        const SizedBox(height: Space.lg),
        Row(
          children: [
            OrblitButton(
              label: session.patchOf == null ? 'Export' : 'Export patch',
              tone: ButtonTone.primary,
              icon: Icons.inventory_2_outlined,
              onPressed: session.canExport ? session.start : null,
            ),
            const SizedBox(width: Space.md),
            Expanded(child: _Progress(session: session)),
          ],
        ),
        const SizedBox(height: Space.lg),
        Expanded(
          child: _Results(session: session, onContents: onContents),
        ),
      ],
    );
  }
}

class _TargetPicker extends StatelessWidget {
  const _TargetPicker({required this.session});

  final ExportSession session;

  @override
  Widget build(BuildContext context) {
    final locked = session.patchOf != null;
    final chosen = session.targets;
    return Wrap(
      spacing: Space.sm,
      runSpacing: Space.sm,
      children: [
        for (final name in CookTargets.names)
          FilterChip(
            label: Text(name, style: OrblitText.label),
            selected: chosen.contains(name),
            showCheckmark: false,
            backgroundColor: OrblitColors.raised,
            selectedColor: OrblitColors.emberWash,
            side: BorderSide(
              color: chosen.contains(name)
                  ? OrblitColors.ember
                  : OrblitColors.line,
            ),
            onSelected: locked || session.running
                ? null
                : (_) => session.toggleTarget(name),
          ),
      ],
    );
  }
}

class _BasePicker extends StatelessWidget {
  const _BasePicker({required this.session});

  final ExportSession session;

  @override
  Widget build(BuildContext context) {
    final packs = session.listing.packs;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        DropdownButton<ExportedPack?>(
          value: session.patchOf,
          isExpanded: true,
          dropdownColor: OrblitColors.raised,
          underline: const SizedBox.shrink(),
          style: OrblitText.label,
          items: [
            const DropdownMenuItem<ExportedPack?>(child: Text('A whole pack')),
            for (final pack in packs)
              DropdownMenuItem<ExportedPack?>(
                value: pack,
                child: Text(
                  'A patch of ${pack.fileName}',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: session.running ? null : session.choosePatchOf,
        ),
        Text(_hint(session.patchOf), style: OrblitText.caption),
      ],
    );
  }

  static String _hint(ExportedPack? base) => base == null
      ? 'Everything in the project, as the one file an app ships.'
      : 'Only what differs from ${base.fileName}, for ${base.target}. '
            'Pick the newest patch when players already have the ones before.';
}

class _Progress extends StatelessWidget {
  const _Progress({required this.session});

  final ExportSession session;

  @override
  Widget build(BuildContext context) {
    final cooking = session.cooking;
    if (cooking == null) return const SizedBox.shrink();
    return Row(
      spacing: Space.sm,
      children: [
        const SizedBox(
          width: 14,
          height: 14,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
        Text('Cooking and packing $cooking…', style: OrblitText.caption),
      ],
    );
  }
}

class _Results extends StatelessWidget {
  const _Results({required this.session, required this.onContents});

  final ExportSession session;
  final ValueChanged<ExportedPack> onContents;

  @override
  Widget build(BuildContext context) {
    final results = session.results;
    if (results.isEmpty) {
      return _Existing(session: session, onContents: onContents);
    }
    return ListView(
      children: [
        for (final result in results)
          _ResultTile(
            result: result,
            onContents: () {
              final pack = session.listing.packs
                  .where((pack) => pack.path == result.path)
                  .firstOrNull;
              if (pack != null) onContents(pack);
            },
          ),
      ],
    );
  }
}

class _ResultTile extends StatelessWidget {
  const _ResultTile({required this.result, required this.onContents});

  final TargetExport result;
  final VoidCallback onContents;

  @override
  Widget build(BuildContext context) {
    final good = result.outcome == ExportOutcome.packed;
    return Padding(
      padding: const EdgeInsets.only(bottom: Space.md),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            spacing: Space.sm,
            children: [
              Icon(
                switch (result.outcome) {
                  ExportOutcome.packed => Icons.check_circle_outline,
                  ExportOutcome.unchanged => Icons.remove_circle_outline,
                  ExportOutcome.cookFailed ||
                  ExportOutcome.failed => Icons.error_outline,
                },
                size: 15,
                color: switch (result.outcome) {
                  ExportOutcome.packed => OrblitColors.good,
                  ExportOutcome.unchanged => OrblitColors.inkDim,
                  ExportOutcome.cookFailed ||
                  ExportOutcome.failed => OrblitColors.bad,
                },
              ),
              Text(result.target, style: OrblitText.panelTitle),
              Expanded(
                child: Text(
                  _headline(result),
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label,
                ),
              ),
              if (good)
                TextButton(
                  onPressed: onContents,
                  child: const Text('Contents'),
                ),
            ],
          ),
          Padding(
            padding: const EdgeInsets.only(left: 23),
            child: Text(_detail(result), style: OrblitText.caption),
          ),
          for (final failure in result.failures)
            Padding(
              padding: const EdgeInsets.only(left: 23, top: 2),
              child: SelectableText(
                '${failure.id}: ${failure.reason}',
                style: OrblitText.mono.copyWith(color: OrblitColors.bad),
              ),
            ),
        ],
      ),
    );
  }

  static String _headline(TargetExport result) {
    final summary = result.summary;
    if (summary == null) return result.message;
    final name = result.path == null ? '' : '${_name(result.path!)} · ';
    return '$name${_plural(summary.assets, 'asset')} · '
        '${sizeLabel(summary.bytes)}';
  }

  static String _detail(TargetExport result) {
    final summary = result.summary;
    if (summary == null) {
      return result.cooked.isEmpty ? '' : 'Cook: ${result.cooked}.';
    }
    final parts = [
      if (result.patchOf != null)
        'Patch of ${result.patchOf}: ${summary.added} new, '
            '${summary.replaced} replaced, ${summary.removed} removed.',
      '${_plural(summary.blobs, 'blob')}, '
          '${sizeLabel(summary.sharedBytes)} saved by sharing.',
      'Cook: ${result.cooked}.',
    ];
    return parts.join(' ');
  }

  static String _name(String path) => path.split(RegExp(r'[\\/]')).last;
}

String _plural(int count, String noun) =>
    '$count $noun${count == 1 ? '' : 's'}';

/// What is in the exports folder already, before anything is run.
class _Existing extends StatelessWidget {
  const _Existing({required this.session, required this.onContents});

  final ExportSession session;
  final ValueChanged<ExportedPack> onContents;

  @override
  Widget build(BuildContext context) {
    final packs = session.listing.packs;
    if (packs.isEmpty && session.listing.unreadable.isEmpty) {
      return const PanelMessage(
        'Nothing exported yet. A whole pack goes first. Patches are built '
        'against an export that is already here.',
      );
    }
    return ListView(
      children: [
        const SectionLabel('In the exports folder'),
        for (final pack in packs)
          InkWell(
            onTap: () => onContents(pack),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 3),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      pack.fileName,
                      overflow: TextOverflow.ellipsis,
                      style: OrblitText.mono,
                    ),
                  ),
                  Text(
                    '${pack.isPatch ? 'patch' : 'whole'} · '
                    '${_plural(pack.assets, 'asset')} · '
                    '${sizeLabel(pack.bytes)}',
                    style: OrblitText.caption,
                  ),
                ],
              ),
            ),
          ),
        for (final line in session.listing.unreadable)
          Padding(
            padding: const EdgeInsets.only(top: 3),
            child: Text(
              line,
              style: OrblitText.caption.copyWith(color: OrblitColors.warn),
            ),
          ),
      ],
    );
  }
}

class _ContentsView extends StatelessWidget {
  const _ContentsView({required this.session});

  final ExportSession session;

  @override
  Widget build(BuildContext context) {
    final packs = session.listing.packs;
    if (packs.isEmpty) {
      return const PanelMessage('There is no export to look inside yet.');
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        DropdownButton<ExportedPack>(
          value: session.shown,
          hint: const Text('Pick an export'),
          isExpanded: true,
          dropdownColor: OrblitColors.raised,
          underline: const SizedBox.shrink(),
          style: OrblitText.label,
          items: [
            for (final pack in packs)
              DropdownMenuItem(
                value: pack,
                child: Text(
                  '${pack.fileName} · ${pack.target}',
                  overflow: TextOverflow.ellipsis,
                ),
              ),
          ],
          onChanged: (pack) {
            if (pack != null) unawaited(session.show(pack));
          },
        ),
        const SizedBox(height: Space.sm),
        Expanded(child: _ContentsTable(session: session)),
      ],
    );
  }
}

class _ContentsTable extends StatelessWidget {
  const _ContentsTable({required this.session});

  final ExportSession session;

  @override
  Widget build(BuildContext context) {
    final problem = session.contentsProblem;
    if (problem != null) return PanelMessage(problem);
    final contents = session.contents;
    if (contents == null) {
      return PanelMessage(
        session.shown == null ? 'Pick an export above.' : 'Reading…',
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${_plural(contents.rows.length, 'asset')} · '
          '${sizeLabel(contents.bytes)}'
          '${contents.layered ? ' · ${_plural(contents.packs.length, 'pack')}' : ''}'
          '${contents.removed.isEmpty ? '' : ' · ${contents.removed.length} taken away'}',
          style: OrblitText.caption,
        ),
        const SizedBox(height: Space.xs),
        Expanded(
          child: ListView.builder(
            itemExtent: 22,
            itemCount: contents.rows.length,
            itemBuilder: (context, index) => _ContentsRow(
              row: contents.rows[index],
              layered: contents.layered,
            ),
          ),
        ),
      ],
    );
  }
}

class _ContentsRow extends StatelessWidget {
  const _ContentsRow({required this.row, required this.layered});

  final ContentsRow row;
  final bool layered;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        SizedBox(
          width: 72,
          child: Text(sizeLabel(row.bytes), style: OrblitText.monoValue),
        ),
        Expanded(
          child: Text(
            row.id,
            overflow: TextOverflow.ellipsis,
            style: OrblitText.mono,
          ),
        ),
        if (layered)
          Flexible(
            child: Text(
              row.pack,
              overflow: TextOverflow.ellipsis,
              style: OrblitText.caption,
            ),
          ),
      ],
    );
  }
}

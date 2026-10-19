import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:path/path.dart' as p;

import '../editor/assets.dart' show sizeLabel;
import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'publish_session.dart';

/// Opens the publish dialog. [session] is disposed with the dialog.
///
/// Not dismissed by a click outside, so a pack under way is not hidden by a
/// click in the wrong place.
Future<void> showPublishDialog(BuildContext context, PublishSession session) =>
    showDialog<void>(
      context: context,
      barrierDismissible: false,
      builder: (context) => PublishDialog(session: session),
    );

/// Scene › Publish kit: pack a folder of the project as a kit and show the
/// entry that lists it in a store.
class PublishDialog extends StatefulWidget {
  const PublishDialog({super.key, required this.session});

  final PublishSession session;

  @override
  State<PublishDialog> createState() => _PublishDialogState();
}

class _PublishDialogState extends State<PublishDialog> {
  final TextEditingController _folder = TextEditingController();
  final TextEditingController _url = TextEditingController();
  final TextEditingController _notes = TextEditingController();

  @override
  void dispose() {
    _folder.dispose();
    _url.dispose();
    _notes.dispose();
    widget.session.dispose();
    super.dispose();
  }

  void _publish() => unawaited(
    widget.session.start(
      folder: _folder.text,
      url: _url.text,
      notes: _notes.text,
    ),
  );

  @override
  Widget build(BuildContext context) {
    final session = widget.session;
    return ListenableBuilder(
      listenable: session,
      builder: (context, _) => PopScope(
        canPop: session.stage != PublishStage.working,
        child: AlertDialog(
          backgroundColor: OrblitColors.surface,
          title: const Text('Publish kit', style: OrblitText.title),
          content: SizedBox(
            width: 620,
            height: 440,
            child: session.stage == PublishStage.done
                ? _Done(session: session)
                : _Form(
                    session: session,
                    folder: _folder,
                    url: _url,
                    notes: _notes,
                    onPublish: _publish,
                  ),
          ),
          actions: [
            TextButton(
              onPressed: session.stage == PublishStage.working
                  ? null
                  : () => Navigator.of(context).pop(),
              child: Text(
                session.stage == PublishStage.done ? 'Close' : 'Cancel',
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Form extends StatelessWidget {
  const _Form({
    required this.session,
    required this.folder,
    required this.url,
    required this.notes,
    required this.onPublish,
  });

  final PublishSession session;
  final TextEditingController folder;
  final TextEditingController url;
  final TextEditingController notes;
  final VoidCallback onPublish;

  @override
  Widget build(BuildContext context) {
    final working = session.stage == PublishStage.working;
    final problem = session.problem;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'Packs a folder of this project as a kit and shows the entry that '
          'lists it in a store. The folder holds a kit.json and the files to '
          'give a project. Nothing is uploaded.',
          style: OrblitText.caption,
        ),
        const SizedBox(height: Space.md),
        IgnorePointer(
          ignoring: working,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SectionLabel('Folder in the project'),
              OrblitField(
                controller: folder,
                hint: 'assets/kits/crates',
                mono: true,
              ),
              const SizedBox(height: Space.md),
              const SectionLabel('Address the file will be hosted at'),
              OrblitField(
                controller: url,
                hint: 'https://example.com/crates-1.0.0.opack',
                mono: true,
              ),
              const SizedBox(height: Space.md),
              const SectionLabel('Notes for this release'),
              OrblitField(controller: notes, hint: 'What changed.'),
            ],
          ),
        ),
        if (problem != null)
          Padding(
            padding: const EdgeInsets.only(top: Space.sm),
            child: Text(
              problem,
              style: OrblitText.caption.copyWith(color: OrblitColors.bad),
            ),
          ),
        const Spacer(),
        Align(
          alignment: Alignment.centerRight,
          child: OrblitButton(
            label: working ? 'Packing…' : 'Publish',
            tone: ButtonTone.primary,
            onPressed: working ? null : onPublish,
          ),
        ),
      ],
    );
  }
}

class _Done extends StatelessWidget {
  const _Done({required this.session});

  final PublishSession session;

  @override
  Widget build(BuildContext context) {
    final published = session.published!; // The dialog shows this when done.
    final summary = published.summary;
    final entry = published.entry;
    return SingleChildScrollView(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Packed ${p.basename(session.kitFile!)}: '
            '${sizeLabel(summary.bytes)}.',
            style: OrblitText.body,
          ),
          SelectableText(session.kitFile!, style: OrblitText.mono),
          SelectableText(summary.hash.hex, style: OrblitText.mono),
          const SizedBox(height: Space.md),
          SectionLabel('Entry for ${entry.publisher}/${entry.slug}'),
          _EntryBox(text: session.entryText),
          const SizedBox(height: Space.md),
          Text(
            'Upload the file so that it is served at ${published.release.url}. '
            'Then save the entry as kits/${entry.publisher}/${entry.slug}.json '
            'in a clone of the orblit-kits repository, check it with '
            'kit index, and open a pull request.',
            style: OrblitText.caption,
          ),
        ],
      ),
    );
  }
}

/// The entry in a box that can be selected, with a button that copies it.
class _EntryBox extends StatefulWidget {
  const _EntryBox({required this.text});

  final String text;

  @override
  State<_EntryBox> createState() => _EntryBoxState();
}

class _EntryBoxState extends State<_EntryBox> {
  bool _copied = false;

  void _copy() {
    unawaited(Clipboard.setData(ClipboardData(text: widget.text)));
    setState(() => _copied = true);
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(Space.sm),
      decoration: BoxDecoration(
        color: OrblitColors.raised,
        border: Border.all(color: OrblitColors.line),
        borderRadius: BorderRadius.circular(Radii.control),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SelectableText(widget.text, style: OrblitText.mono),
          const SizedBox(height: Space.xs),
          Align(
            alignment: Alignment.centerRight,
            child: OrblitButton(
              label: _copied ? 'Copied' : 'Copy',
              icon: Icons.copy,
              onPressed: _copy,
            ),
          ),
        ],
      ),
    );
  }
}

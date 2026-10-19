import 'dart:async';

import 'package:flutter/material.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'install_session.dart';
import 'kit_choose.dart';

/// Opens the install dialog and starts the download behind it.
///
/// Not dismissed by a click outside, so a download or a write that is under
/// way is not hidden by a click in the wrong place. [session] is disposed with
/// the dialog.
Future<void> showInstallDialog(BuildContext context, InstallSession session) {
  unawaited(session.start());
  return showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder: (context) => InstallDialog(session: session),
  );
}

/// Download a kit, choose its files and folder, and write them.
///
/// It does not start the session, so a test can start one itself.
class InstallDialog extends StatefulWidget {
  const InstallDialog({super.key, required this.session});

  final InstallSession session;

  @override
  State<InstallDialog> createState() => _InstallDialogState();
}

class _InstallDialogState extends State<InstallDialog> {
  @override
  void dispose() {
    widget.session.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final session = widget.session;
    final manifest = session.row.manifest;
    return ListenableBuilder(
      listenable: session,
      builder: (context, _) => PopScope(
        canPop: session.stage != InstallStage.installing,
        child: AlertDialog(
          backgroundColor: OrblitColors.surface,
          title: Text(
            'Install ${manifest.name} ${session.release.version}',
            style: OrblitText.title,
          ),
          content: SizedBox(
            width: 620,
            height: 440,
            child: session.failed
                ? _Failed(session: session)
                : switch (session.stage) {
                    InstallStage.downloading => _Downloading(session: session),
                    InstallStage.choosing => KitChoose(session: session),
                    InstallStage.installing => const _Writing(),
                    InstallStage.done => _Done(session: session),
                  },
          ),
          actions: [
            TextButton(
              onPressed: session.stage == InstallStage.installing
                  ? null
                  : () => Navigator.of(context).pop(),
              child: Text(
                session.stage == InstallStage.done ? 'Close' : 'Cancel',
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _Downloading extends StatelessWidget {
  const _Downloading({required this.session});

  final InstallSession session;

  @override
  Widget build(BuildContext context) {
    final progress = session.progress;
    final fraction = progress?.fraction;
    return Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            fraction == null
                ? 'Downloading…'
                : 'Downloading… ${(fraction * 100).round()}%',
            style: OrblitText.body,
          ),
          const SizedBox(height: Space.md),
          SizedBox(
            width: 320,
            child: LinearProgressIndicator(
              value: fraction,
              color: OrblitColors.ember,
              backgroundColor: OrblitColors.line,
            ),
          ),
        ],
      ),
    );
  }
}

class _Failed extends StatelessWidget {
  const _Failed({required this.session});

  final InstallSession session;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Text(
          session.problem ?? '',
          style: OrblitText.caption.copyWith(color: OrblitColors.bad),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: Space.md),
        OrblitButton(
          label: 'Try again',
          onPressed: () => unawaited(session.start()),
        ),
      ],
    );
  }
}

class _Writing extends StatelessWidget {
  const _Writing();

  @override
  Widget build(BuildContext context) {
    return const Center(child: Text('Writing files…', style: OrblitText.body));
  }
}

class _Done extends StatelessWidget {
  const _Done({required this.session});

  final InstallSession session;

  @override
  Widget build(BuildContext context) {
    final locked = session.locked;
    final count = locked?.files.length ?? 0;
    final where = locked == null || locked.into.isEmpty
        ? 'in the project folder'
        : 'under ${locked.into}';
    return Center(
      child: Text(
        'Installed $count ${count == 1 ? 'file' : 'files'} $where. '
        'kits.lock lists them.',
        style: OrblitText.body,
        textAlign: TextAlign.center,
      ),
    );
  }
}

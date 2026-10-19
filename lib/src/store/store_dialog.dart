import 'dart:async';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'install_session.dart';
import 'kit_install_dialog.dart';
import 'store_browse.dart';
import 'store_detail.dart';
import 'store_repository.dart';
import 'store_session.dart';

/// Opens the kit store over the editor. [session] is disposed with the dialog.
///
/// [installFor] makes the session that downloads and writes one release.
Future<void> showStoreDialog(
  BuildContext context, {
  required StoreSession session,
  required InstallSession Function(KitRow row, KitRelease release) installFor,
}) => showDialog<void>(
  context: context,
  builder: (context) => StoreDialog(session: session, installFor: installFor),
);

/// Scene › Kit store: find a kit in a store, read about it and install it.
class StoreDialog extends StatefulWidget {
  const StoreDialog({
    super.key,
    required this.session,
    required this.installFor,
  });

  final StoreSession session;
  final InstallSession Function(KitRow row, KitRelease release) installFor;

  @override
  State<StoreDialog> createState() => _StoreDialogState();
}

class _StoreDialogState extends State<StoreDialog> {
  @override
  void dispose() {
    widget.session.dispose();
    super.dispose();
  }

  void _install(KitRow row, KitRelease release) =>
      unawaited(showInstallDialog(context, widget.installFor(row, release)));

  @override
  Widget build(BuildContext context) {
    final session = widget.session;
    return ListenableBuilder(
      listenable: session,
      builder: (context, _) => AlertDialog(
        backgroundColor: OrblitColors.surface,
        title: Row(
          children: [
            const Text('Kit store', style: OrblitText.title),
            const Spacer(),
            RepositoryPicker(session: session),
          ],
        ),
        // The dialog clamps this to the window, so at the minimum of 960x620
        // the body is shorter and its lists scroll.
        content: SizedBox(
          width: 680,
          height: 460,
          child: !session.online
              ? _Offline(
                  address: session.repository,
                  onGoOnline: () => unawaited(session.goOnline()),
                )
              : session.selected == null
              ? StoreBrowse(session: session)
              : StoreDetail(session: session, onInstall: _install),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(context).pop(),
            child: const Text('Close'),
          ),
        ],
      ),
    );
  }
}

/// What the dialog shows until a person goes online. Nothing has been sent.
class _Offline extends StatelessWidget {
  const _Offline({required this.address, required this.onGoOnline});

  final Uri address;
  final VoidCallback onGoOnline;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              'The store is at $address. Nothing has been sent to it. Going '
              'online reads its list of kits and their pictures. The files of '
              'a kit are listed only when you ask, and a kit is downloaded '
              'only when you install it.',
              style: OrblitText.body,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: Space.lg),
            OrblitButton(
              label: 'Go online',
              icon: Icons.public,
              tone: ButtonTone.primary,
              onPressed: onGoOnline,
            ),
          ],
        ),
      ),
    );
  }
}

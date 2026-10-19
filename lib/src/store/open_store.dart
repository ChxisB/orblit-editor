import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'install_session.dart';
import 'store_dialog.dart';
import 'store_session.dart';
import 'store_settings.dart';

/// Opens the kit store for the project in [projectDirectory].
///
/// The store's settings and the kits downloaded so far live in the editor's
/// support folder, not in a project, so a kit is fetched once for all of them.
Future<void> openKitStore(
  BuildContext context, {
  required String projectDirectory,
  required String engineVersion,
}) async {
  final support = (await getApplicationSupportDirectory()).path;
  if (!context.mounted) return;
  await showStoreDialog(
    context,
    session: StoreSession(
      settings: StoreSettings(File(p.join(support, 'store.json'))),
      engine: engineVersion,
      transport: HttpTransport.new,
    ),
    installFor: (row, release) => InstallSession(
      row: row,
      release: release,
      projectDirectory: projectDirectory,
      downloads: p.join(support, 'kit-downloads'),
      transport: HttpTransport(),
    ),
  );
}

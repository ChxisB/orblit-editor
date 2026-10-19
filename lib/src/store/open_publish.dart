import 'package:flutter/widgets.dart';

import 'publish_dialog.dart';
import 'publish_session.dart';

/// Opens the publish dialog for the project in [projectDirectory].
Future<void> openKitPublish(
  BuildContext context, {
  required String projectDirectory,
}) => showPublishDialog(
  context,
  PublishSession(projectDirectory: projectDirectory, clock: DateTime.now),
);

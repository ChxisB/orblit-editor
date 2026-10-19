import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

import '../editor/export_folder.dart';

enum PublishStage { editing, working, done }

/// A file name a url can end in and a shell can take as it stands.
final RegExp _plainName = RegExp(r'^[A-Za-z0-9][A-Za-z0-9._-]*$');

/// One run of Scene › Publish kit: pack a folder of the project as a kit
/// file and work out the entry that lists it.
///
/// The kit file goes in `exports/kits/` of the project, named after the end
/// of the address it will be hosted at. Nothing is uploaded and no entry is
/// written. The person copies the entry into a clone of the store's entries.
///
/// [clock] is read once per run, for the day the release is dated.
final class PublishSession extends ChangeNotifier {
  PublishSession({required this.projectDirectory, required this.clock});

  final String projectDirectory;
  final DateTime Function() clock;

  PublishStage _stage = PublishStage.editing;
  KitPublished? _published;
  String? _kitFile;
  String? _problem;
  bool _disposed = false;

  PublishStage get stage => _stage;

  /// What the run made, once [stage] is done.
  KitPublished? get published => _published;

  /// The path of the kit file the run wrote, once [stage] is done.
  String? get kitFile => _kitFile;

  /// What went wrong, or null.
  String? get problem => _problem;

  /// The entry file as it would be saved, once [stage] is done.
  String get entryText => utf8.decode(
    (_published ?? (throw StateError('Nothing is published yet.'))).entry
        .encode(),
  );

  /// Packs [folder], a path inside the project, hosted at [url]. A failure
  /// leaves the person at the form, with [problem] saying why.
  Future<void> start({
    required String folder,
    required String url,
    String notes = '',
  }) async {
    _problem = null;
    _stage = PublishStage.working;
    notifyListeners();
    try {
      final address = url.trim();
      final file = _fileFor(address);
      final source = _folderIn(folder.trim());
      Directory(p.dirname(file)).createSync(recursive: true);
      _published = await publishKit(
        folder: source,
        url: address,
        outPath: file,
        released: clock().toUtc(),
        notes: notes.trim(),
      );
      _kitFile = file;
      _stage = PublishStage.done;
    } on Object catch (error) {
      // Any failure, an Error too, must give the dialog back: it cannot be
      // closed while the stage is working.
      _problem = _describe(error);
      _stage = PublishStage.editing;
    }
    notifyListeners();
  }

  String _fileFor(String url) {
    final segments = Uri.tryParse(url)?.pathSegments ?? const <String>[];
    final name = segments.isEmpty ? '' : segments.last;
    if (!_plainName.hasMatch(name)) {
      throw const FormatException(
        'The address must end in the name of the kit file, such as '
        'https://example.com/crates-1.0.0.opack.',
      );
    }
    return p.join(exportsPath(projectDirectory), 'kits', name);
  }

  String _folderIn(String folder) {
    final clean = p.normalize(folder);
    if (folder.isEmpty ||
        p.isAbsolute(folder) ||
        clean == '.' ||
        p.split(clean).contains('..')) {
      throw const FormatException(
        'Name a folder inside the project, such as assets/kits/crates.',
      );
    }
    final path = p.join(projectDirectory, clean);
    if (!Directory(path).existsSync()) {
      throw FormatException('The project has no folder $folder.');
    }
    return path;
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}

String _describe(Object error) => switch (error) {
  FormatException(:final message) => message,
  FileSystemException(:final message, :final path) =>
    '$message${path == null || path.isEmpty ? '' : ' ($path)'}',
  _ => '$error',
};

import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:orblit_asset/orblit_asset.dart';

import 'store_install.dart';

enum InstallStage { downloading, choosing, installing, done }

/// One install of one release: the download, the person's choice of files
/// and folder, and the write.
///
/// The download is kept in [downloads] by its hash, so a second try after a
/// cut connection takes up where the first stopped, and a kit already held is
/// not fetched again. Installing never runs a file of the kit. The session
/// closes [transport] when it is disposed.
///
/// shortcut: the downloads folder is never cleaned, and a download cannot be
/// cancelled. A session disposed mid-download lets it fail unseen. Prune and
/// cancel when it matters.
final class InstallSession extends ChangeNotifier {
  InstallSession({
    required this.row,
    required this.release,
    required this.projectDirectory,
    required this.downloads,
    required this.transport,
    this.backoff = const Duration(seconds: 1),
  });

  final KitRow row;
  final KitRelease release;
  final String projectDirectory;

  /// The folder downloaded kits are kept in.
  final String downloads;
  final AssetTransport transport;

  /// The wait before the first new try of a download, doubled after each.
  final Duration backoff;

  InstallStage _stage = InstallStage.downloading;
  FetchProgress? _progress;
  Kit? _kit;
  LockedKit? _locked;
  String? _problem;
  bool _disposed = false;

  InstallStage get stage => _stage;

  /// How much of the download has arrived, or null before the first bytes.
  FetchProgress? get progress => _progress;

  /// The kit once it is downloaded and checked, or null.
  Kit? get kit => _kit;

  /// What the install wrote, once [stage] is done.
  LockedKit? get locked => _locked;

  /// What went wrong, or null.
  String? get problem => _problem;

  /// Whether the download or the check failed, so there is nothing to choose
  /// from.
  bool get failed => _problem != null && _kit == null;

  Kit get _opened =>
      _kit ?? (throw StateError('The kit is not downloaded and checked yet.'));

  /// Downloads the kit and checks it, leaving the person to choose.
  Future<void> start() async {
    _problem = null;
    _progress = null;
    notifyListeners();
    try {
      final pack = await PackFolder(downloads).download(
        release.offer,
        transport: transport,
        origin: release.origin,
        backoff: backoff,
        onProgress: (progress) {
          _progress = progress;
          notifyListeners();
        },
      );
      final kit = await Kit.open(pack);
      _checkIsTheRelease(kit.manifest);
      final problems = await kit.check();
      if (problems.isNotEmpty) throw FormatException(problems.join('\n'));
      _kit = kit;
      _stage = InstallStage.choosing;
    } on Exception catch (error) {
      _problem = _describe(error);
    }
    notifyListeners();
  }

  /// The files [only] would replace under [into] in the project.
  Future<List<AssetId>> conflicts({String into = '', Set<AssetId>? only}) =>
      _opened.conflictsIn(
        DirectoryAssetSource(projectDirectory),
        into: into,
        only: only,
      );

  /// Writes the kit. A failure leaves the person where they were, with
  /// [problem] saying why.
  Future<void> install({
    String into = '',
    Set<AssetId>? only,
    Set<AssetId> replacing = const {},
  }) async {
    _problem = null;
    _stage = InstallStage.installing;
    notifyListeners();
    try {
      final kit = _opened;
      final misplaced = kitPlaceProblem(into, kit.chosen(only));
      if (misplaced != null) throw FormatException(misplaced);
      _locked = await installKit(
        kit,
        projectDirectory: projectDirectory,
        into: into,
        only: only,
        replacing: replacing,
      );
      _stage = InstallStage.done;
    } on Object catch (error) {
      // Any failure, an Error too, must give the dialog back: it cannot be
      // closed while the stage is installing.
      _problem = _describe(error);
      _stage = InstallStage.choosing;
    }
    notifyListeners();
  }

  // A pack whose manifest names another kit than the one listed was not the
  // one the store promised, whatever its hash.
  void _checkIsTheRelease(KitManifest manifest) {
    if (manifest.publisher != row.manifest.publisher ||
        manifest.slug != row.manifest.slug ||
        manifest.version != release.version) {
      throw FormatException(
        'The file is ${manifest.publisher}/${manifest.slug} '
        '${manifest.version}, not ${row.manifest.publisher}/'
        '${row.manifest.slug} ${release.version}.',
      );
    }
  }

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    transport.close();
    super.dispose();
  }
}

String _describe(Object error) => switch (error) {
  FormatException(:final message) => message,
  FileSystemException(:final message, :final path) =>
    '$message${path == null || path.isEmpty ? '' : ' ($path)'}',
  _ => '$error',
};

import 'package:flutter/foundation.dart';
import 'package:orblit_asset/orblit_asset.dart';

import 'export_folder.dart';
import 'export_run.dart';
import 'pack_contents.dart';

/// Runs an export. The window passes [runExport] and a test passes a fake.
typedef ExportRunner = Future<List<TargetExport>> Function(
  ExportRequest request, {
  void Function(TargetExport done)? onTarget,
});

/// What one export dialog is showing and what it has been asked to do.
///
/// Held by the dialog and not by the editor: an export is a thing somebody
/// sets up, runs and reads, and nothing outside the dialog needs to know it
/// is half set up. The cook and the folder are reached through the functions
/// given here, so a test never starts an isolate.
class ExportSession extends ChangeNotifier {
  ExportSession({
    required this.projectDirectory,
    this.runner = runExport,
    this.lister = listExports,
    this.reader = readContents,
    this.cachePath,
  }) : _targets = {currentCookTarget.name};

  final String projectDirectory;
  final ExportRunner runner;
  final Future<ExportListing> Function(String projectDirectory) lister;
  final Future<PackContents> Function(String projectDirectory, String path)
  reader;

  /// Where cooked bytes are kept, or null for the machine's own cache.
  final String? cachePath;

  final Set<String> _targets;
  ExportListing _listing = const ExportListing();
  ExportedPack? _patchOf;
  bool _running = false;
  String? _cooking;
  List<TargetExport> _results = const [];

  ExportedPack? _shown;
  PackContents? _contents;
  String? _contentsProblem;

  bool _disposed = false;

  ExportListing get listing => _listing;

  /// The pack a patch is built on, or null for a whole pack.
  ExportedPack? get patchOf => _patchOf;

  bool get running => _running;

  /// The target being cooked now, or null between targets and at rest.
  String? get cooking => _cooking;

  /// How each target went, in the order they were asked for.
  List<TargetExport> get results => _results;

  /// The pack whose contents are on show, or null.
  ExportedPack? get shown => _shown;

  PackContents? get contents => _contents;

  /// Why the contents could not be read, or null.
  String? get contentsProblem => _contentsProblem;

  /// The targets an export would run for. A patch is for its base's target
  /// and no other, so the choice of target is the choice of base.
  List<String> get targets {
    final base = _patchOf;
    if (base != null) return [base.target];
    return [
      for (final name in CookTargets.names)
        if (_targets.contains(name)) name,
    ];
  }

  bool get canExport => !_running && targets.isNotEmpty;

  /// Reads the exports folder.
  Future<void> load() async {
    _listing = await lister(projectDirectory);
    final base = _patchOf;
    if (base != null && !_listing.packs.any((p) => p.path == base.path)) {
      _patchOf = null;
    }
    notifyListeners();
  }

  void toggleTarget(String name) {
    if (!_targets.add(name)) _targets.remove(name);
    notifyListeners();
  }

  void choosePatchOf(ExportedPack? base) {
    _patchOf = base;
    notifyListeners();
  }

  /// Cooks and packs each target in turn.
  Future<void> start() async {
    if (!canExport) return;
    _running = true;
    _results = const [];
    _cooking = targets.first;
    notifyListeners();
    final request = ExportRequest(
      projectDirectory: projectDirectory,
      targets: targets,
      patchOf: _patchOf?.path,
      cachePath: cachePath,
    );
    try {
      await runner(request, onTarget: _landed(request.targets));
    } finally {
      _running = false;
      _cooking = null;
      await load();
    }
  }

  void Function(TargetExport) _landed(List<String> asked) =>
      (TargetExport done) {
        _results = [..._results, done];
        final next = _results.length;
        _cooking = next < asked.length ? asked[next] : null;
        notifyListeners();
      };

  /// Reads what [pack] serves, with the packs under it.
  Future<void> show(ExportedPack pack) async {
    _shown = pack;
    _contents = null;
    _contentsProblem = null;
    notifyListeners();
    try {
      final read = await reader(projectDirectory, pack.path);
      if (_shown == pack) _contents = read;
    } on ExportChainBroken catch (error) {
      if (_shown == pack) _contentsProblem = error.message;
    }
    notifyListeners();
  }

  @override
  void notifyListeners() {
    // A read that lands after the dialog has closed has nobody to tell.
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }
}

import 'dart:io';
import 'dart:isolate';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

import 'export_folder.dart';

/// What to export: which targets, and whether each is a whole pack or a patch.
final class ExportRequest {
  const ExportRequest({
    required this.projectDirectory,
    required this.targets,
    this.patchOf,
    this.name = 'game',
    this.cachePath,
  });

  final String projectDirectory;

  /// Target names, each cooked and packed in turn.
  final List<String> targets;

  /// The pack in the exports folder a patch is built on, or null for a whole
  /// pack. A patch is for the one target that pack is for.
  final String? patchOf;

  /// The start of a file name. The app reads `game.<target>.opack`, so a
  /// project that changes this changes what its app opens.
  final String name;

  /// Where cooked bytes are kept, or null for the machine's own cache.
  final String? cachePath;
}

/// How one target's export ended.
enum ExportOutcome {
  /// A pack was written.
  packed,

  /// The cook worked and the result is what the export folder already
  /// holds, so nothing was written.
  unchanged,

  /// An asset did not cook. Nothing was packed for the target.
  cookFailed,

  /// Something else stopped it, such as a missing folder or a bad base.
  failed,
}

/// An asset that did not cook, and what the importer said.
final class ExportFailure {
  const ExportFailure(this.id, this.reason);

  final String id;
  final String reason;
}

/// What exporting one target came to, as plain values.
final class TargetExport {
  const TargetExport({
    required this.target,
    required this.outcome,
    this.message = '',
    this.cooked = '',
    this.failures = const [],
    this.path,
    this.summary,
    this.patchOf,
  });

  final String target;
  final ExportOutcome outcome;

  /// One sentence for an outcome that is not [ExportOutcome.packed].
  final String message;

  /// The cook's own count, such as `3 cooked, 1 cached`.
  final String cooked;

  final List<ExportFailure> failures;

  /// The file written, or for [ExportOutcome.unchanged] the file that
  /// already says the same.
  final String? path;

  final PackSummary? summary;

  /// The file name of the pack this is a patch of, or null for a whole pack.
  final String? patchOf;
}

/// Cooks the project for each of [request]'s targets and packs the result
/// into its exports folder.
///
/// Each target is its own isolate, so that the result of one is on screen
/// while the next cooks, and so that a cook that runs out of memory takes
/// one target down and not the editor. [onTarget] is told as each lands.
/// [cook] is how one target is run, and a test passes one that fails.
Future<List<TargetExport>> runExport(
  ExportRequest request, {
  void Function(TargetExport done)? onTarget,
  Future<TargetExport> Function(ExportRequest request, String target) cook =
      _inIsolate,
}) async {
  final done = <TargetExport>[];
  for (final target in request.targets) {
    final result = await _attempt(cook, request, target);
    done.add(result);
    onTarget?.call(result);
  }
  return done;
}

Future<TargetExport> _inIsolate(ExportRequest request, String target) =>
    Isolate.run(() => exportTarget(request, target));

Future<TargetExport> _attempt(
  Future<TargetExport> Function(ExportRequest request, String target) cook,
  ExportRequest request,
  String target,
) async {
  try {
    return await cook(request, target);
  } catch (error) {
    // An isolate that dies, from running out of memory or from a native
    // tool that crashes, throws here and not from inside the cook, where
    // nothing is left to catch it.
    return _stopped(target, error);
  }
}

TargetExport _stopped(String target, Object error) => TargetExport(
  target: target,
  outcome: ExportOutcome.failed,
  message: 'The cook for $target stopped before it finished: $error',
);

/// Cooks and packs one target. Never throws: whatever goes wrong comes back
/// as a [TargetExport] that says so, because it is read by a person who is
/// waiting on a dialog.
Future<TargetExport> exportTarget(ExportRequest request, String name) async {
  final target = CookTargets.find(name);
  if (target == null) {
    return TargetExport(
      target: name,
      outcome: ExportOutcome.failed,
      message:
          'Orblit cooks for ${CookTargets.names.join(', ')}. '
          '"$name" is not one.',
    );
  }
  try {
    return await _export(request, target);
  } catch (error) {
    // The edge of the isolate. Anything thrown inside must become a value,
    // or the dialog waits on a future that failed.
    return TargetExport(
      target: name,
      outcome: ExportOutcome.failed,
      message: '$error',
    );
  }
}

Future<TargetExport> _export(ExportRequest request, CookTarget target) async {
  final assets = p.join(request.projectDirectory, 'assets');
  if (!Directory(assets).existsSync()) {
    return TargetExport(
      target: target.name,
      outcome: ExportOutcome.failed,
      message: 'The project has no assets folder, so there is nothing to pack.',
    );
  }
  final project = CookProject(
    assetsPath: assets,
    outPath: p.join(request.projectDirectory, 'build', 'assets'),
    target: target,
    cachePath: request.cachePath,
  );
  final report = await project.run();
  if (!report.ok) return _cookFailed(target, report);

  final folder = exportsPath(request.projectDirectory);
  Directory(folder).createSync(recursive: true);
  final pending = p.join(
    folder,
    '${packFileName(target, name: request.name)}.pending',
  );
  final summary = await _pack(request, project, pending);
  final placing = _Placing(
    request: request,
    target: target,
    pending: pending,
    summary: summary,
    cooked: report.summary,
  );
  final patchOf = request.patchOf;
  return patchOf == null ? placing.whole() : placing.patch(patchOf);
}

TargetExport _cookFailed(CookTarget target, CookReport report) => TargetExport(
  target: target.name,
  outcome: ExportOutcome.cookFailed,
  cooked: report.summary,
  message: 'Nothing was packed for ${target.name}.',
  failures: [
    for (final result in report.withStatus(CookStatus.failed))
      ExportFailure(result.id.toString(), '${result.error}'),
  ],
);

/// Packs the bundle [project] just wrote to [pending], over the chain the
/// request's patch stands on when it asks for one.
///
/// The exports folder is open only for the length of the pack. Packing reads
/// each base pack's directory and never its blobs.
Future<PackSummary> _pack(
  ExportRequest request,
  CookProject project,
  String pending,
) async {
  final path = request.patchOf;
  if (path == null) {
    return packBundle(
      bundlePath: project.bundlePath,
      outPath: pending,
      target: project.target,
    );
  }
  final folder = await OpenedExports.of(request.projectDirectory);
  try {
    final top = folder.at(path);
    if (top == null) {
      throw ExportChainBroken(
        '${p.basename(path)} is not a readable pack in the exports folder.',
      );
    }
    return await packBundle(
      bundlePath: project.bundlePath,
      outPath: pending,
      target: project.target,
      base: folder.chainTo(top),
    );
  } finally {
    await folder.close();
  }
}

/// A pack written under a temporary name, and where it goes.
///
/// A whole pack is `<name>.<target>.opack`, the one an app ships. A patch,
/// and a whole pack that a newer one replaces, are kept as
/// `<name>.<target>.<hash>.opack`. Nothing is overwritten that a patch may
/// still be built on, since a patch finds its base by hash and not by name.
final class _Placing {
  const _Placing({
    required this.request,
    required this.target,
    required this.pending,
    required this.summary,
    required this.cooked,
  });

  final ExportRequest request;
  final CookTarget target;
  final String pending;
  final PackSummary summary;
  final String cooked;

  String get _folder => exportsPath(request.projectDirectory);

  String _named(ContentHash hash) => p.join(
    _folder,
    '${request.name}.${target.name}.${shortHash(hash)}.opack',
  );

  /// Puts a whole pack where the app reads it, setting the one it replaces
  /// aside under its hash.
  Future<TargetExport> whole() async {
    final current = p.join(_folder, packFileName(target, name: request.name));
    final old = await _hashOf(current);
    if (old == summary.hash) {
      File(pending).deleteSync();
      return _result(
        ExportOutcome.unchanged,
        message: 'The pack is the same as the last export.',
        path: current,
      );
    }
    if (old != null) File(current).renameSync(_named(old));
    File(pending).renameSync(current);
    return _result(ExportOutcome.packed, path: current, summary: summary);
  }

  /// Keeps a patch under its own hash, or nothing when it holds nothing.
  Future<TargetExport> patch(String basedOn) async {
    final name = p.basename(basedOn);
    if (summary.assets == 0 && summary.removed == 0) {
      File(pending).deleteSync();
      return _result(
        ExportOutcome.unchanged,
        message: 'Nothing has changed since $name.',
        patchOf: name,
      );
    }
    final destination = _named(summary.hash);
    File(pending).renameSync(destination);
    return _result(
      ExportOutcome.packed,
      path: destination,
      summary: summary,
      patchOf: name,
    );
  }

  TargetExport _result(
    ExportOutcome outcome, {
    String message = '',
    String? path,
    PackSummary? summary,
    String? patchOf,
  }) => TargetExport(
    target: target.name,
    outcome: outcome,
    message: message,
    cooked: cooked,
    path: path,
    summary: summary,
    patchOf: patchOf,
  );
}

/// The hash of the pack at [path], or null when there is no file there.
Future<ContentHash?> _hashOf(String path) async {
  if (!File(path).existsSync()) return null;
  final source = FilePackBytes(path);
  try {
    return (await AssetPack.open(source)).hash;
  } finally {
    await source.close();
  }
}

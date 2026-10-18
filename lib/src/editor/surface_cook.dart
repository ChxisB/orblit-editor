import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:isolate';

import 'package:flutter/foundation.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_filament/orblit_filament.dart' show OrblitSurface;
import 'package:orblit_scene/orblit_scene.dart' show shaderExtension;
import 'package:orblit_stage/orblit_stage.dart' show CookedSurface;
import 'package:path/path.dart' as p;

import 'scene.dart';

const _includeExtension = '.oshaderinc';

/// What the cook made of one shader. Plain strings, so it crosses from the
/// isolate that cooked it.
final class ShaderCooked {
  const ShaderCooked(this.path, {this.package, this.params, this.problem});

  /// The shader as a scene names it: relative to the project, with `/`.
  final String path;

  /// The compiled package, as an absolute path. Null when the cook failed.
  final String? package;

  /// The text of the `params.json` written beside the package.
  final String? params;

  /// The author's line and what is wrong with it. Null when it cooked.
  final String? problem;
}

/// Cooks every shader in the project and answers with one result for each.
typedef ShaderCook = Future<List<ShaderCooked>> Function(String project);

/// Keeps the last cook of each shader that worked.
///
/// A shader is saved half written all the time, and the object wearing it
/// should go on drawing the last version that compiled until the next one
/// does. So a failure adds a problem and leaves the surface alone.
class SurfaceCooks extends ChangeNotifier {
  SurfaceCooks({
    required this.projectDirectory,
    this.onProblem,
    this.cook = cookShaders,
  });

  final String projectDirectory;

  /// Told once for each shader whose cook fails with a new complaint.
  final void Function(String problem)? onProblem;

  final ShaderCook cook;

  final Map<String, CookedSurface> _good = {};
  final Map<String, String> _problems = {};
  String _stamp = '';
  String? _lastFailure;
  bool _running = false;
  bool _again = false;
  bool _disposed = false;

  /// What the shader at [asset] declares, from its last good cook.
  CookedSurface? cookedOf(String asset) => _good[_keyOf(asset)];

  /// Why the last cook of [asset] failed, or null when it did not.
  String? problemWith(String asset) => _problems[_keyOf(asset)];

  /// The surface [object] draws, or null when its shader has not cooked yet.
  OrblitSurface? surfaceFor(SceneObject object) {
    final asset = object.materialAsset;
    if (asset == null) return null;
    return _good[_keyOf(asset)]?.surfaceFor(
      object.materialValues,
      locate: (path) =>
          p.isAbsolute(path) ? path : p.join(projectDirectory, path),
    );
  }

  /// Cooks again when a shader file has changed since the last cook.
  ///
  /// Called on every change to the project folder, so the check has to be
  /// cheap: a stamp of the shader files, not a cook. A call during a cook
  /// asks for one more look when it lands.
  ///
  /// shortcut: the stamp walks the assets folder on the interface thread;
  /// move it into the isolate if a very large project stutters.
  Future<void> refresh() async {
    if (_running) {
      _again = true;
      return;
    }
    _running = true;
    try {
      do {
        _again = false;
        final stamp = _stampOfShaders();
        if (stamp == _stamp) break;
        _stamp = stamp;
        try {
          _take(await cook(projectDirectory));
        } on Object catch (error) {
          // The editor outlives a cook that could not even start. The stamp
          // is forgotten so the next change to the folder tries again, and
          // the same complaint is told once.
          _stamp = '';
          final problem = 'Could not cook the shaders: $error';
          if (problem != _lastFailure) onProblem?.call(problem);
          _lastFailure = problem;
        }
      } while (_again);
    } finally {
      _running = false;
    }
  }

  @override
  void dispose() {
    _disposed = true;
    super.dispose();
  }

  void _take(List<ShaderCooked> results) {
    // A cook can land after the editor that asked for it has closed.
    if (_disposed) return;
    _lastFailure = null;
    for (final result in results) {
      final key = result.path;
      final package = result.package;
      if (package != null) {
        _good[key] = CookedSurface.fromParams(
          package,
          jsonDecode(result.params ?? '[]'),
        );
        _problems.remove(key);
        continue;
      }
      final problem = result.problem ?? '$key did not cook.';
      if (_problems[key] != problem) onProblem?.call(problem);
      _problems[key] = problem;
    }
    final present = {for (final result in results) result.path};
    _good.removeWhere((key, _) => !present.contains(key));
    _problems.removeWhere((key, _) => !present.contains(key));
    notifyListeners();
  }

  String _keyOf(String asset) => p.posix.joinAll(p.split(asset));

  String _stampOfShaders() {
    final assets = Directory(p.join(projectDirectory, 'assets'));
    if (!assets.existsSync()) return '';
    final parts = <String>[];
    for (final file in assets.listSync(recursive: true).whereType<File>()) {
      if (!_isShaderFile(file.path)) continue;
      final stat = file.statSync();
      parts.add(
        '${file.path}:${stat.modified.microsecondsSinceEpoch}:${stat.size}',
      );
    }
    return (parts..sort()).join('\n');
  }
}

bool _isShaderFile(String path) {
  final extension = p.extension(path);
  return extension == shaderExtension || extension == _includeExtension;
}

/// The real cook, run in an isolate of its own so a slow `matc` or one that
/// runs out of memory costs the editor a note and not a frozen frame.
Future<List<ShaderCooked>> cookShaders(String project) =>
    Isolate.run(() => _cookShaders(project));

Future<List<ShaderCooked>> _cookShaders(String project) async {
  final assets = p.join(project, 'assets');
  final work = p.join(project, '.orblit', 'shaders');
  final stage = Directory(p.join(work, 'assets'));
  if (stage.existsSync()) stage.deleteSync(recursive: true);

  // Only the shaders and what they include are cooked. Cooking the whole
  // assets folder on every save would convert every texture the first time.
  final ids = <String>[];
  final source = Directory(assets);
  final files = source.existsSync()
      ? source.listSync(recursive: true).whereType<File>()
      : const <File>[];
  for (final file in files) {
    if (!_isShaderFile(file.path)) continue;
    final parts = p.split(p.relative(file.path, from: assets));
    final copy = File(p.joinAll([stage.path, ...parts]))
      ..createSync(recursive: true);
    file.copySync(copy.path);
    if (p.extension(file.path) == shaderExtension) {
      ids.add(p.posix.joinAll(parts));
    }
  }
  if (ids.isEmpty) return const [];

  final cooking = CookProject(
    assetsPath: stage.path,
    outPath: p.join(work, 'cooked'),
    target: currentCookTarget,
  );
  final report = await cooking.run();
  final failed = {
    for (final result in report.withStatus(CookStatus.failed))
      result.id.toString(): switch (result.error) {
        final ImportFailure failure => failure.reason,
        final error => '$error',
      },
  };

  final packages = p.join(work, 'packages');
  return [
    for (final id in ids)
      _resultOf(id, failed[id], cooking.bundlePath, packages),
  ];
}

/// What the cook made of the shader at [id].
///
/// The cook empties its bundle on every run, and the renderer reads a package
/// again whenever its file changes. So a good package is copied out to a
/// folder of its own, and only when the bytes differ. A save of one shader
/// then leaves the others as they were, and a save that fails leaves the last
/// good package on disk for objects that are added after it.
///
/// shortcut: the package of a deleted shader stays in `packages` until the
/// `.orblit` folder is cleared; it is a few hundred kilobytes.
ShaderCooked _resultOf(
  String id,
  String? problem,
  String bundle,
  String packages,
) {
  final path = p.posix.join('assets', id);
  if (problem != null) return ShaderCooked(path, problem: problem);
  try {
    final built = File(p.join(bundle, '$id.material.filamat'));
    final kept = File(p.joinAll([packages, ...p.posix.split('$id.filamat')]));
    final bytes = built.readAsBytesSync();
    if (!kept.existsSync() || !listEquals(kept.readAsBytesSync(), bytes)) {
      kept.parent.createSync(recursive: true);
      kept.writeAsBytesSync(bytes);
    }
    return ShaderCooked(
      path,
      package: kept.path,
      params: File(p.join(bundle, '$id.params.json')).readAsStringSync(),
    );
  } on FileSystemException catch (error) {
    return ShaderCooked(path, problem: '$path did not cook: ${error.message}');
  }
}

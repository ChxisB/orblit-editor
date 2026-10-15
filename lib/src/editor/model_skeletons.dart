import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:path/path.dart' as p;

import 'scene.dart';

/// The skeleton an imported model says it has.
///
/// Read off the file for the same reason [ModelBounds] reads its bounds: the
/// renderer owns the loaded model and the editor knows a path. The bones are
/// named as the clips imported from the same file name them, so a clip's
/// bone channels land on the bones drawn here.
class ModelSkeletons {
  ModelSkeletons(this.projectRoot);

  final String projectRoot;

  /// Answers by path, misses included, so a scene naming a missing file
  /// does not read the disk every frame.
  final Map<String, RestSkeleton?> _known = {};

  /// The skeleton of [object]'s model, or null when it has no file, the file
  /// has no skin or the file cannot be read.
  ///
  /// A file with several skins answers the one with the most bones, which is
  /// the character and not a prop riding on it.
  RestSkeleton? of(SceneObject object) => ofAsset(object.meshAsset);

  /// The skeleton of the model at project path [asset], with the same
  /// answers as [of] for a missing or skinless file.
  RestSkeleton? ofAsset(String? asset) {
    if (asset == null || asset.isEmpty) return null;
    if (_known.containsKey(asset)) return _known[asset];
    final found = _read(
      p.isAbsolute(asset) ? asset : p.join(projectRoot, asset),
    );
    _known[asset] = found;
    return found;
  }

  /// Forgets one file, for when it has been written again.
  void forget(String asset) => _known.remove(asset);

  void clear() => _known.clear();

  static RestSkeleton? _read(String path) {
    final extension = p.extension(path).toLowerCase();
    if (extension != '.glb' && extension != '.gltf') return null;
    try {
      final file = File(path);
      if (!file.existsSync()) return null;
      final bytes = file.readAsBytesSync();
      final files = extension == '.gltf'
          ? _buffersBeside(path, bytes)
          : const <String, Uint8List>{};
      final skins = restSkeletonsFromGltf(bytes, files: files);
      if (skins.isEmpty) return null;
      return skins.reduce((a, b) => b.length > a.length ? b : a);
    } on FileSystemException {
      return null;
    } on ClipFormatException {
      return null;
    }
  }

  /// The `.bin` files a `.gltf` names, by the uri it names them with.
  ///
  /// Only files inside the folder of the model are read. A uri that climbs
  /// out of it is not something a model needs.
  static Map<String, Uint8List> _buffersBeside(String path, Uint8List bytes) {
    final Object? json;
    try {
      json = jsonDecode(utf8.decode(bytes));
    } on FormatException {
      return const {};
    }
    final buffers = json is Map<String, Object?> ? json['buffers'] : null;
    if (buffers is! List<Object?>) return const {};

    final folder = p.dirname(path);
    final out = <String, Uint8List>{};
    for (final buffer in buffers) {
      final uri = buffer is Map<String, Object?> ? buffer['uri'] : null;
      if (uri is! String || uri.startsWith('data:')) continue;
      final file = p.join(folder, Uri.decodeFull(uri));
      if (!p.isWithin(folder, file)) continue;
      final source = File(file);
      if (source.existsSync()) out[uri] = source.readAsBytesSync();
    }
    return out;
  }
}

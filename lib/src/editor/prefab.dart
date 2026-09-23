import 'dart:io';

import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;

/// The extension a prefab file carries.
const String prefabExtension = doc.prefabExtension;

/// A project's prefabs, as the scenes open in this editor were opened
/// against them.
///
/// The file is `orblit_scene`'s [doc.PrefabDocument], and so is everything
/// done with one — opening an instance, folding it, applying it. What is here
/// is the part only an editor has: a folder on disk, and a memory of what was
/// in it.
///
/// Remembered rather than read afresh each time, because an instance is saved
/// as what is different about it from its prefab, and that difference has to
/// be worked out against the prefab it was opened from. Read the file again
/// after somebody has changed it in another program and the gap between the
/// old lamp and the new one would be saved as a change to every lamp in the
/// scene, pinning the old one in place for good. Kept, the scene saves what
/// was changed in it and the new lamp arrives the next time it is opened.
///
/// A prefab that is not there, or cannot be read, is not remembered: its
/// instances stay folded, exactly as they were saved, and the next look may
/// find the file.
class PrefabFiles {
  PrefabFiles(this.root);

  /// The project directory, which prefab paths are relative to.
  final String root;

  final Map<String, doc.PrefabDocument> _known = {};

  /// The prefab at [asset], or null when there is not a readable one.
  ///
  /// A [doc.PrefabSource], so this is what is handed to everything in
  /// `orblit_scene` that opens or folds an instance.
  doc.PrefabDocument? find(String asset) => read(asset).prefab;

  /// The prefab at [asset], or why there is not one.
  ({doc.PrefabDocument? prefab, String? problem}) read(String asset) {
    final known = _known[asset];
    if (known != null) return (prefab: known, problem: null);

    final file = File(p.join(root, asset));
    if (!file.existsSync()) {
      return (
        prefab: null,
        problem: '${p.basename(asset)} is not in the project any more.',
      );
    }
    try {
      final prefab = doc.PrefabDocument.decode(file.readAsStringSync()).prefab;
      _known[asset] = prefab;
      return (prefab: prefab, problem: null);
    } on doc.SceneFormatException catch (error) {
      return (
        prefab: null,
        problem: '${p.basename(asset)} is not a readable prefab: '
            '${error.message}',
      );
    } on FileSystemException catch (error) {
      return (
        prefab: null,
        problem: 'Could not read ${p.basename(asset)}: '
            '${error.osError?.message ?? error.message}',
      );
    }
  }

  /// Writes [prefab] to [asset] and remembers it as what that file now is.
  ///
  /// Returns why it could not, or null.
  String? write(String asset, doc.PrefabDocument prefab) {
    final path = p.join(root, asset);
    try {
      File(path)
        ..parent.createSync(recursive: true)
        ..writeAsStringSync(prefab.encode());
    } on FileSystemException catch (error) {
      return 'Could not write ${p.basename(path)}: '
          '${error.osError?.message ?? error.message}';
    }
    _known[asset] = prefab;
    return null;
  }

  /// Forgets every prefab but [assets], so each is read again the next time
  /// it is wanted.
  ///
  /// Only safe for a prefab no open scene has an instance of: those were
  /// opened against what is remembered, and have to be saved against it.
  void keepOnly(Set<String> assets) =>
      _known.removeWhere((asset, _) => !assets.contains(asset));

  /// A source that hands back [prefab] for [asset] and this project's own
  /// for everything else.
  ///
  /// What a change to one prefab is worked out with: the instances of it
  /// that are open now were opened against the prefab as it was, and are
  /// folded against that before being opened against the new one.
  doc.PrefabSource withOne(String asset, doc.PrefabDocument? prefab) =>
      (wanted) => wanted == asset ? prefab : find(wanted);
}

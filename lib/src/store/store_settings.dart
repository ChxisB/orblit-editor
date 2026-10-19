import 'dart:convert';
import 'dart:io';

import 'package:orblit_asset/orblit_asset.dart';

/// The store the editor reads until a person adds another.
final Uri defaultStoreAddress = Uri.parse('https://orblitengine.com/store/');

/// The stores the editor reads and whether it lists kits made for another
/// engine version, kept in one file beside the editor's other settings.
final class StoreSettings {
  StoreSettings(this.file) {
    try {
      if (!file.existsSync()) return;
      final raw = jsonDecode(file.readAsStringSync());
      if (raw is! Map<String, Object?>) return;
      _showIncompatible = raw['showIncompatible'] == true;
      final added = raw['repositories'];
      for (final item in added is List<Object?> ? added : const []) {
        if (item is String && repositoryProblem(item) == null) {
          _add(_folder(Uri.parse(item.trim())));
        }
      }
    } on FormatException {
      // A damaged file must not stop the store opening.
    } on FileSystemException {
      // A file that cannot be read leaves the default store.
    }
  }

  final File file;
  final List<Uri> _added = [];
  bool _showIncompatible = false;

  /// The default store first, then the ones a person added, in that order.
  List<Uri> get repositories => [defaultStoreAddress, ..._added];

  bool get showIncompatible => _showIncompatible;

  /// Why [text] cannot be added as a store, or null.
  String? repositoryProblem(String text) {
    final address = Uri.tryParse(text.trim());
    if (address == null || !address.hasScheme) {
      return 'That is not an address.';
    }
    final problem = kitStoreAddressProblem(address);
    if (problem != null) return problem;
    return repositories.contains(_folder(address))
        ? 'That store is already in the list.'
        : null;
  }

  /// Throws an [ArgumentError] when [repositoryProblem] has something to say.
  void addRepository(String text) {
    final problem = repositoryProblem(text);
    if (problem != null) throw ArgumentError.value(text, 'text', problem);
    _add(_folder(Uri.parse(text.trim())));
    _write();
  }

  void removeRepository(Uri address) {
    if (_added.remove(address)) _write();
  }

  void setShowIncompatible(bool value) {
    _showIncompatible = value;
    _write();
  }

  void _add(Uri address) {
    if (!repositories.contains(address)) _added.add(address);
  }

  void _write() {
    try {
      file.parent.createSync(recursive: true);
      final pending = File('${file.path}.tmp');
      pending.writeAsStringSync(
        jsonEncode({
          'repositories': [for (final address in _added) '$address'],
          'showIncompatible': _showIncompatible,
        }),
      );
      pending.renameSync(file.path);
    } on FileSystemException {
      // shortcut: a settings file that cannot be written is kept for this run
      // only. Tell the person when it matters.
    }
  }
}

Uri _folder(Uri address) => address.path.endsWith('/')
    ? address
    : address.replace(path: '${address.path}/');

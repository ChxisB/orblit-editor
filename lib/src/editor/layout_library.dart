import 'dart:convert';
import 'dart:io';

import 'dock.dart';

/// Named arrangements for each workspace in one project.
final class LayoutLibrary {
  LayoutLibrary(this.file, {required this.kinds}) {
    try {
      if (!file.existsSync()) return;
      final raw = jsonDecode(file.readAsStringSync());
      if (raw is Map<String, Object?>) _saved.addAll(raw);
    } on FormatException {
      // A damaged library must not stop the project opening.
    } on FileSystemException {
      // A read-only project can still use its built-in arrangements.
    }
  }

  final File file;
  final Iterable<PanelKind> kinds;
  final Map<String, Object?> _saved = {};

  Map<String, Object?> _workspace(String mode) => switch (_saved[mode]) {
    final Map<String, Object?> layouts => layouts,
    _ => {},
  };

  List<String> names(String mode) => _workspace(mode).keys.toList()..sort();

  DockLayout? find(String mode, String name) {
    final raw = _workspace(mode)[name];
    return raw == null ? null : DockLayout.read(jsonEncode(raw), kinds: kinds);
  }

  void save(String mode, String name, DockLayout layout) {
    final layouts = {..._workspace(mode), name: jsonDecode(layout.toText())};
    _write(mode, layouts);
  }

  void remove(String mode, String name) {
    final layouts = {..._workspace(mode)}..remove(name);
    _write(mode, layouts);
  }

  void _write(String mode, Map<String, Object?> layouts) {
    final next = {..._saved, mode: layouts};
    file.parent.createSync(recursive: true);
    final pending = File('${file.path}.tmp');
    pending.writeAsStringSync(jsonEncode(next));
    pending.renameSync(file.path);
    _saved[mode] = layouts;
  }
}

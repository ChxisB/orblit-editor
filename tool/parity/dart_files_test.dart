import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/dock.dart';
import 'package:orblit_editor/src/editor/layout_library.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/launcher/project.dart';
import 'package:path/path.dart' as p;

// Writes the files the Dart editor saves, for the C# editor's tests to read
// back and write again byte for byte. Run from the editor package with
// ORBLIT_PARITY_OUT set to the folder the fixtures go in:
//
//   flutter test tool/parity/dart_files_test.dart
//
// It is a test, not a script, because the files come from code that imports
// Flutter, which a plain `dart run` cannot load.

void main() {
  test('writes the files the Dart editor saves', () {
    final out = Platform.environment['ORBLIT_PARITY_OUT'];
    if (out == null) fail('Set ORBLIT_PARITY_OUT to the fixtures folder.');

    void write(String name, String text) {
      final file = File(p.join(out, name));
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(text);
    }

    final project = Project(
      name: 'My Game',
      directory: '/ignored',
      engineVersion: '0.3.0',
      lastOpened: DateTime(2026, 10, 24, 10, 20, 30, 123),
    );
    write(
      'project/$projectFileName',
      const JsonEncoder.withIndent('  ').convert(project.toJson()),
    );

    for (final template in ProjectTemplate.values) {
      write('scene.${template.name}.oscene', SceneDocument.encode(sceneFor(template)));
    }

    final layouts = {
      'scene': DockLayout.standard(),
      'modelling': DockLayout.modelling(),
      'animation': DockLayout.animation(),
      'fourViews': DockLayout.fourViews(),
    };
    for (final MapEntry(:key, :value) in layouts.entries) {
      write('layout.$key.json', value.toText());
    }

    final library = LayoutLibrary(
      File(p.join(out, 'layouts.json')),
      kinds: [for (final layout in layouts.values) ...layout.panels.map((panel) => panel.kind)],
    );
    library.save('scene', 'mine', layouts['scene']!);
    library.save('modelling', 'wide', layouts['modelling']!);
  });
}

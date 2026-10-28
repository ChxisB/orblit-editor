import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/store/store_install.dart';
import 'package:path/path.dart' as p;

// Writes the kits.lock the Dart editor leaves after installing a kit in two
// steps, for the C# install of the same kit to write byte for byte. The kit
// is the one in test/support/store_fixture.dart. Run from the editor package
// with ORBLIT_PARITY_OUT set to the folder the fixtures go in:
//
//   flutter test tool/parity/kits_lock_test.dart

void main() {
  test('writes the lock of a kit installed in two steps', () async {
    final out = Platform.environment['ORBLIT_PARITY_OUT'];
    if (out == null) fail('Set ORBLIT_PARITY_OUT to the fixtures folder.');
    final scratch = Directory.systemTemp.createTempSync('orblit_kits_lock_');
    addTearDown(() => scratch.deleteSync(recursive: true));

    final source = p.join(scratch.path, 'source');
    void write(String name, List<int> bytes) => File(p.join(source, name))
      ..parent.createSync(recursive: true)
      ..writeAsBytesSync(bytes);
    write('models/crate.obj', utf8.encode('a crate'));
    write('scripts/spin.js', utf8.encode('spin();'));
    write('shots/a.png', [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1]);
    write(
      'kit.json',
      utf8.encode(
        jsonEncode({
          'slug': 'crates',
          'publisher': 'brass-lamp',
          'name': 'Crates',
          'summary': 'Some crates.',
          'version': '1.0.0',
          'licence': {'spdx': 'MIT', 'url': 'https://example.com/licence'},
          'engine': {'from': '0.30.0', 'to': '0.40.0'},
          'tags': ['props'],
          'previews': ['shots/a.png'],
        }),
      ),
    );
    final packed = p.join(scratch.path, 'crates.opack');
    await packKit(folder: source, outPath: packed);
    final kit = await Kit.open(await AssetPack.open(FilePackBytes(packed)));

    final project = Directory(p.join(scratch.path, 'project'))..createSync();
    const into = 'assets/kits/crates';
    await installKit(
      kit,
      projectDirectory: project.path,
      into: into,
      only: {AssetId.parse('scripts/spin.js')},
    );
    await installKit(
      kit,
      projectDirectory: project.path,
      into: into,
      only: {AssetId.parse('models/crate.obj'), AssetId.parse('shots/a.png')},
    );
    File(p.join(project.path, kitLockName)).copySync(p.join(out, 'kits.lock'));
  });
}

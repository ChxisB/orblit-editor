import 'dart:convert';
import 'dart:io';

import 'package:path/path.dart' as p;

/// The address a kit made by [writeKitFolder] is hosted at.
const String hostedAt = 'https://example.com/kits/crates-1.0.0.opack';

/// A folder with a manifest for `brass-lamp/crates` and one model.
void writeKitFolder(String folder, {String version = '1.0.0'}) {
  File(p.join(folder, 'kit.json'))
    ..parent.createSync(recursive: true)
    ..writeAsStringSync(
      jsonEncode({
        'slug': 'crates',
        'publisher': 'brass-lamp',
        'name': 'Crates',
        'summary': 'Some crates.',
        'version': version,
        'licence': {'spdx': 'MIT', 'url': 'https://example.com/licence'},
        'engine': {'from': '0.30.0', 'to': '0.40.0'},
        'tags': <String>[],
        'previews': <String>[],
      }),
    );
  File(p.join(folder, 'models', 'crate.obj'))
    ..parent.createSync(recursive: true)
    ..writeAsStringSync('v 0 0 0');
}

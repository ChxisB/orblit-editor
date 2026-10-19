import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

// A store with one kit in it, on a fake network, built the way a publisher
// builds one: pack the kit, list it in an entry, run the store builder.

// The editor's default store, so a session that is not told where to read
// finds this one.
final Uri storeBase = Uri.parse('https://orblitengine.com/store/');
final Uri crateUrl = Uri.parse('https://example.com/crates.opack');

const List<int> pngBytes = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 1];

/// What the kit holds, by the name it gives each file.
const Map<String, String> crateFiles = {
  'models/crate.obj': 'a crate',
  'scripts/spin.js': 'spin();',
};

final class StoreFixture {
  StoreFixture._(this.pages, this.pack, this.hash);

  /// Everything the fake network serves: the store's files and the kit file.
  final Map<Uri, TransportPage> pages;

  /// The bytes of `brass-lamp/crates` 1.0.0.
  final Uint8List pack;

  /// The hash the entry gives the kit: the hash of the pack's directory.
  final ContentHash hash;

  /// A network that has not been used. A transport is closed by whoever
  /// ends up holding it, so every reader gets its own.
  MapTransport transport() => MapTransport(pages);

  /// Packs the kit and builds the store under [scratch].
  static Future<StoreFixture> build(Directory scratch) async {
    final source = Directory(p.join(scratch.path, 'source'));
    for (final MapEntry(:key, :value) in crateFiles.entries) {
      File(p.join(source.path, key))
        ..parent.createSync(recursive: true)
        ..writeAsStringSync(value);
    }
    File(p.join(source.path, 'shots', 'a.png'))
      ..parent.createSync(recursive: true)
      ..writeAsBytesSync(pngBytes);
    File(p.join(source.path, 'kit.json')).writeAsStringSync(
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
    );
    final out = p.join(scratch.path, 'crates.opack');
    final summary = await packKit(folder: source.path, outPath: out);
    final pack = File(out).readAsBytesSync();
    File(p.join(scratch.path, 'entries', 'brass-lamp', 'crates.json'))
      ..parent.createSync(recursive: true)
      ..writeAsStringSync(
        jsonEncode({
          'releases': [
            {
              'version': '1.0.0',
              'url': '$crateUrl',
              'hash': summary.hash.hex,
              'bytes': pack.length,
              'released': '2026-10-19',
              'notes': 'The first release.',
            },
          ],
        }),
      );
    final pages = {crateUrl: TransportPage(pack)};
    await buildKitStore(
      entriesPath: p.join(scratch.path, 'entries'),
      outPath: p.join(scratch.path, 'store'),
      transport: MapTransport(pages),
    );
    final store = Directory(p.join(scratch.path, 'store'));
    for (final file in store.listSync(recursive: true).whereType<File>()) {
      final name = p
          .relative(file.path, from: store.path)
          .replaceAll(p.separator, '/');
      pages[storeBase.resolve(name)] = TransportPage(file.readAsBytesSync());
    }
    return StoreFixture._(pages, pack, summary.hash);
  }

  /// The store's one row and its release, read back the way the editor reads
  /// them.
  Future<(KitRow, KitRelease)> listing() async {
    final client = KitStoreClient(storeBase, transport());
    final row = (await client.index()).rows.single;
    return (row, (await client.releases(row)).releases.single.release);
  }
}

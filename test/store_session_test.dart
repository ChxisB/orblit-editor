import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/store/store_query.dart';
import 'package:orblit_editor/src/store/store_session.dart';
import 'package:orblit_editor/src/store/store_settings.dart';
import 'package:path/path.dart' as p;

import 'support/store_fixture.dart';

late Directory scratch;
late StoreFixture fixture;
late List<MapTransport> made;

/// A server that does not understand a range: it sends the whole file.
final class _NoRanges implements AssetTransport {
  _NoRanges(this._inner);

  final AssetTransport _inner;

  @override
  Future<FetchReply> send(FetchRequest request) =>
      _inner.send(FetchRequest(request.url));

  @override
  void close() => _inner.close();
}

StoreSession session({
  String engine = '0.35.0',
  AssetTransport Function(AssetTransport)? wrap,
}) => StoreSession(
  settings: StoreSettings(File(p.join(scratch.path, 'store.json'))),
  engine: engine,
  transport: () {
    final transport = fixture.transport();
    made.add(transport);
    return wrap == null ? transport : wrap(transport);
  },
);

void main() {
  setUp(() async {
    scratch = Directory.systemTemp.createTempSync('orblit_store_session_');
    fixture = await StoreFixture.build(scratch);
    made = [];
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  test('sends nothing until it is told to go online', () async {
    final store = session();
    addTearDown(store.dispose);
    expect(store.online, isFalse);
    expect(made, isEmpty);
    await store.goOnline();
    expect(store.online, isTrue);
    expect(store.shown.single.manifest.slug, 'crates');
  });

  test('hides a kit for other engines until asked to show it', () async {
    final store = session(engine: '0.50.0');
    addTearDown(store.dispose);
    await store.goOnline();
    expect(store.rows, hasLength(1));
    expect(store.shown, isEmpty);
    expect(store.fits(store.rows.single), isFalse);
    store.setShowIncompatible(true);
    expect(store.shown, hasLength(1));
    store.setQuery(const StoreQuery(text: 'nothing like it'));
    expect(store.shown, isEmpty);
  });

  test('reads the releases and the file list of a chosen kit', () async {
    final store = session();
    addTearDown(store.dispose);
    await store.goOnline();
    await store.select(store.rows.single);
    expect(store.releases!.releases.single.release.version, '1.0.0');
    expect(store.chosen, same(store.releases!.releases.single));
    expect(store.files!.map((file) => '${file.key}'), [
      'models/crate.obj',
      'scripts/spin.js',
      'shots/a.png',
    ]);
    expect(store.chosen!.containsScripts, isTrue);
  });

  test('lists no files when the server cannot answer a range', () async {
    final store = session(wrap: _NoRanges.new);
    addTearDown(store.dispose);
    await store.goOnline();
    await store.select(store.rows.single);
    expect(store.chosen, isNotNull);
    expect(store.files, isNull);
    expect(store.problem, isNull);
  });

  test('drops a reply for a kit the person has left', () async {
    final store = session();
    addTearDown(store.dispose);
    await store.goOnline();
    final pending = store.select(store.rows.single);
    await store.select(null);
    await pending;
    expect(store.selected, isNull);
    expect(store.releases, isNull);
  });

  test('reads an image once and gives null for one it cannot read', () async {
    final store = session();
    addTearDown(store.dispose);
    await store.goOnline();
    final row = store.rows.single;
    expect(identical(store.thumbnail(row), store.thumbnail(row)), isTrue);
    expect(await store.thumbnail(row), pngBytes);
    await store.select(row);
    expect(await store.preview('preview-0.png'), pngBytes);
    expect(await store.preview('preview-1.png'), isNull);
  });

  test('says what went wrong with a store that cannot be read', () async {
    fixture.pages.remove(storeBase.resolve('index.json'));
    final store = session();
    addTearDown(store.dispose);
    await store.goOnline();
    expect(store.rows, isEmpty);
    expect(store.loading, isFalse);
    expect(store.problem, contains('index.json'));
  });

  test(
    'reads another store when it is added, and goes back when removed',
    () async {
      final other = Uri.parse('https://other.example/store/');
      for (final entry in fixture.pages.entries.toList()) {
        if (entry.key.host == storeBase.host) {
          fixture.pages[other.resolve(entry.key.path.substring(7))] =
              entry.value;
        }
      }
      final store = session();
      addTearDown(store.dispose);
      await store.goOnline();
      await store.addRepository('https://other.example/store');
      expect(store.repository, other);
      expect(store.rows, hasLength(1));
      expect(store.settings.repositories, contains(other));
      await store.removeRepository(other);
      expect(store.repository, defaultStoreAddress);
      expect(store.settings.repositories, [defaultStoreAddress]);
    },
  );
}

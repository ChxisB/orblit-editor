import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/store/store_query.dart';

KitRow row(
  String slug, {
  String name = '',
  String summary = 'A kit.',
  String publisher = 'brass-lamp',
  String licence = 'MIT',
  String updated = '2026-10-01',
  List<String> tags = const [],
  bool scripts = false,
  String from = '0.30.0',
  String to = '0.40.0',
}) => KitRow(
  manifest: KitManifest(
    slug: slug,
    publisher: publisher,
    name: name.isEmpty ? slug : name,
    summary: summary,
    version: '1.0.0',
    licence: (spdx: licence, url: 'https://example.com/l'),
    engine: (from: from, to: to),
    containsScripts: scripts,
    tags: tags,
  ),
  updated: updated,
);

List<String> slugs(List<KitRow> rows) => [
  for (final row in rows) row.manifest.slug,
];

List<String> ask(
  List<KitRow> rows,
  StoreQuery query, {
  String engine = '0.35.0',
  bool showIncompatible = false,
}) => slugs(
  query.apply(rows, engine: engine, showIncompatible: showIncompatible),
);

void main() {
  final rows = [
    row('crates', name: 'Crates', tags: ['props'], updated: '2026-10-03'),
    row('rain', summary: 'Rain and fog.', licence: 'CC0-1.0', scripts: true),
    row('zebra', name: 'A zebra', publisher: 'stripes', updated: '2026-10-03'),
    row('old', from: '0.10.0', to: '0.20.0', updated: '2026-10-09'),
  ];

  test('lists the newest first, and by name when asked', () {
    expect(ask(rows, const StoreQuery()), ['zebra', 'crates', 'rain']);
    expect(ask(rows, const StoreQuery(sort: StoreSort.name)), [
      'zebra',
      'crates',
      'rain',
    ]);
  });

  test('leaves out a kit made for other engines unless asked', () {
    expect(ask(rows, const StoreQuery()), isNot(contains('old')));
    expect(
      ask(rows, const StoreQuery(), showIncompatible: true),
      contains('old'),
    );
  });

  test('keeps a kit when the engine version is not known', () {
    expect(ask(rows, const StoreQuery(), engine: 'unknown'), contains('old'));
  });

  test('needs every word, in the name, summary, publisher, slug or tags', () {
    expect(ask(rows, const StoreQuery(text: 'crat PROPS')), ['crates']);
    expect(ask(rows, const StoreQuery(text: 'fog')), ['rain']);
    expect(ask(rows, const StoreQuery(text: 'stripes')), ['zebra']);
    expect(ask(rows, const StoreQuery(text: 'crates fog')), isEmpty);
  });

  test('narrows by licence, by tag and by scripts', () {
    expect(ask(rows, const StoreQuery(licence: 'CC0-1.0')), ['rain']);
    expect(ask(rows, const StoreQuery(tag: 'props')), ['crates']);
    expect(ask(rows, const StoreQuery(hideScripts: true)), ['zebra', 'crates']);
  });

  test('copies with one field changed', () {
    final query = const StoreQuery(text: 'a').copyWith(tag: 'props');
    expect((query.text, query.tag), ('a', 'props'));
  });

  test('lists the licences and tags in use, sorted', () {
    expect(storeLicences(rows), ['CC0-1.0', 'MIT']);
    expect(storeTags(rows), ['props']);
  });
}

import 'package:orblit_asset/orblit_asset.dart';

enum StoreSort { updated, name }

/// What a person has typed and picked to narrow the list of kits.
final class StoreQuery {
  const StoreQuery({
    this.text = '',
    this.sort = StoreSort.updated,
    this.licence = '',
    this.tag = '',
    this.hideScripts = false,
  });

  /// Words that must each appear in a kit's name, summary, publisher, slug or
  /// tags.
  final String text;
  final StoreSort sort;

  /// An SPDX name, or empty for any licence.
  final String licence;

  /// A tag, or empty for any.
  final String tag;
  final bool hideScripts;

  StoreQuery copyWith({
    String? text,
    StoreSort? sort,
    String? licence,
    String? tag,
    bool? hideScripts,
  }) => StoreQuery(
    text: text ?? this.text,
    sort: sort ?? this.sort,
    licence: licence ?? this.licence,
    tag: tag ?? this.tag,
    hideScripts: hideScripts ?? this.hideScripts,
  );

  /// The rows that pass, in the order [sort] gives.
  ///
  /// A kit made for other engine versions than [engine] is left out unless
  /// [showIncompatible].
  List<KitRow> apply(
    List<KitRow> rows, {
    required String engine,
    required bool showIncompatible,
  }) {
    final words = text.toLowerCase().split(RegExp(r'\s+'))
      ..removeWhere((word) => word.isEmpty);
    final kept = [
      for (final row in rows)
        if (_passes(row, words) &&
            (showIncompatible || kitFitsEngine(row.manifest.engine, engine)))
          row,
    ];
    return kept..sort(switch (sort) {
      StoreSort.updated => _newest,
      StoreSort.name => _byName,
    });
  }

  bool _passes(KitRow row, List<String> words) {
    final manifest = row.manifest;
    if (licence.isNotEmpty && manifest.licence.spdx != licence) return false;
    if (tag.isNotEmpty && !manifest.tags.contains(tag)) return false;
    if (hideScripts && manifest.containsScripts) return false;
    final haystack = [
      manifest.name,
      manifest.summary,
      manifest.publisher,
      manifest.slug,
      ...manifest.tags,
    ].join('\n').toLowerCase();
    return words.every(haystack.contains);
  }
}

int _byName(KitRow a, KitRow b) =>
    a.manifest.name.toLowerCase().compareTo(b.manifest.name.toLowerCase());

// The date is year, month and day in that order, so text order is date
// order.
int _newest(KitRow a, KitRow b) {
  final later = b.updated.compareTo(a.updated);
  return later != 0 ? later : _byName(a, b);
}

/// The licences [rows] use, sorted.
List<String> storeLicences(List<KitRow> rows) =>
    {for (final row in rows) row.manifest.licence.spdx}.toList()..sort();

/// The tags [rows] use, sorted.
List<String> storeTags(List<KitRow> rows) =>
    {for (final row in rows) ...row.manifest.tags}.toList()..sort();

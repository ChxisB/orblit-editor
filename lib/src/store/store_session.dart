import 'package:flutter/foundation.dart';
import 'package:orblit_asset/orblit_asset.dart';

import 'store_query.dart';
import 'store_settings.dart';

/// What the store dialog shows and asks for: the kits of one store, the one
/// a person is looking at and the words and picks that narrow the list.
///
/// Nothing is fetched until [goOnline], so opening the dialog sends nothing.
/// A reply that arrives after the person moved on is dropped.
final class StoreSession extends ChangeNotifier {
  StoreSession({
    required this.settings,
    required this.engine,
    required AssetTransport Function() transport,
  }) : _newTransport = transport,
       _repository = defaultStoreAddress;

  final StoreSettings settings;

  /// The version of the engine the project uses, or text that is not a
  /// version when it is not known.
  final String engine;
  final AssetTransport Function() _newTransport;

  Uri _repository;
  AssetTransport? _transport;
  KitStoreClient? _client;
  bool _disposed = false;
  bool _loading = false;
  List<KitRow> _rows = const [];
  StoreQuery _query = const StoreQuery();
  KitRow? _selected;
  KitReleases? _releases;
  PublishedRelease? _chosen;
  List<MapEntry<AssetId, int>>? _files;
  String? _problem;
  final Map<String, Future<Uint8List?>> _images = {};

  Uri get repository => _repository;
  bool get online => _client != null;
  bool get loading => _loading;
  StoreQuery get query => _query;
  KitRow? get selected => _selected;
  KitReleases? get releases => _releases;
  PublishedRelease? get chosen => _chosen;

  /// The files of the chosen release, or null while they are being read or
  /// when the server cannot say.
  List<MapEntry<AssetId, int>>? get files => _files;

  /// What went wrong with the last thing asked of the store, or null.
  String? get problem => _problem;

  List<KitRow> get rows => _rows;

  /// The rows that pass the query.
  List<KitRow> get shown => _query.apply(
    _rows,
    engine: engine,
    showIncompatible: settings.showIncompatible,
  );

  bool fits(KitRow row) => kitFitsEngine(row.manifest.engine, engine);

  void setQuery(StoreQuery query) {
    _query = query;
    notifyListeners();
  }

  void setShowIncompatible(bool value) {
    settings.setShowIncompatible(value);
    notifyListeners();
  }

  /// Starts reading the store. Later calls read it again.
  Future<void> goOnline() async {
    _transport?.close();
    final transport = _transport = _newTransport();
    final client = _client = KitStoreClient(_repository, transport);
    _rows = const [];
    _forgetKit();
    _images.clear();
    _problem = null;
    _loading = true;
    notifyListeners();
    try {
      final index = await client.index();
      if (_stale(client)) return;
      _rows = index.rows;
    } on Exception catch (error) {
      if (_stale(client)) return;
      _problem = _describe(error);
    }
    _loading = false;
    notifyListeners();
  }

  /// Reads [address] instead, when the session is online.
  Future<void> useRepository(Uri address) async {
    _repository = address;
    notifyListeners();
    if (online) await goOnline();
  }

  /// Lists the store at [text] in the settings and reads it. Throws an
  /// [ArgumentError] when [StoreSettings.repositoryProblem] has something to
  /// say.
  Future<void> addRepository(String text) {
    settings.addRepository(text);
    return useRepository(settings.repositories.last);
  }

  /// Forgets a store a person added, and goes back to the default one when
  /// it was the one being read.
  Future<void> removeRepository(Uri address) async {
    settings.removeRepository(address);
    if (_repository == address) await useRepository(defaultStoreAddress);
  }

  Future<void> select(KitRow? row) async {
    _forgetKit();
    _selected = row;
    _problem = null;
    notifyListeners();
    final client = _client;
    if (row == null || client == null) return;
    try {
      final releases = await client.releases(row);
      if (_stale(client) || _selected != row) return;
      _releases = releases;
      notifyListeners();
      await chooseRelease(releases.releases.first);
    } on Exception catch (error) {
      if (_stale(client) || _selected != row) return;
      _problem = _describe(error);
      notifyListeners();
    }
  }

  /// Makes [release] the one shown and reads its file list.
  ///
  /// A server that does not answer a range leaves the list empty and the
  /// install still works, because the whole file is then downloaded.
  Future<void> chooseRelease(PublishedRelease release) async {
    final client = _client;
    if (client == null) return;
    _chosen = release;
    _files = null;
    notifyListeners();
    List<MapEntry<AssetId, int>>? files;
    try {
      files = (await client.open(release.release)).files.entries.toList();
    } on Exception {
      // The list is a convenience. The install reads the whole file anyway.
    }
    if (_stale(client) || _chosen != release) return;
    _files = files;
    notifyListeners();
  }

  /// The card image of [row], or null when it has none or cannot be read.
  Future<Uint8List?> thumbnail(KitRow row) {
    final path = row.thumbnail;
    final client = _client;
    if (path == null || client == null) return Future.value();
    return _images.putIfAbsent(path, () => _image(client.thumbnail(row)));
  }

  /// The preview image [name] of [selected], or null when it cannot be read.
  Future<Uint8List?> preview(String name) {
    final row = _selected;
    final client = _client;
    if (row == null || client == null) return Future.value();
    // The same key as a thumbnail has, so an image is read once.
    final key = 'kits/${row.manifest.publisher}/${row.manifest.slug}/$name';
    return _images.putIfAbsent(key, () => _image(client.preview(row, name)));
  }

  Future<Uint8List?> _image(Future<Uint8List?> read) async {
    try {
      return await read;
    } on Exception {
      return null;
    }
  }

  void _forgetKit() {
    _selected = null;
    _releases = null;
    _chosen = null;
    _files = null;
  }

  bool _stale(KitStoreClient client) => _disposed || _client != client;

  @override
  void notifyListeners() {
    if (!_disposed) super.notifyListeners();
  }

  @override
  void dispose() {
    _disposed = true;
    _transport?.close();
    super.dispose();
  }
}

String _describe(Exception error) =>
    error is FormatException ? error.message : '$error';

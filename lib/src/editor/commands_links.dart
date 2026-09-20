part of 'commands.dart';

// Links out of the scene — to a prefab, a data file, or an interface — and
// the breaking of them.

/// Points an object at a data object, or stops pointing at one.
///
/// Attaching rather than copying: what changes here is which file the object
/// reads its settings from, and the settings themselves stay in the file where
/// everything else using them can see the same values.
class SetDataLinks extends EditorCommand {
  SetDataLinks({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.paths,
    required this.what,
  });

  @override
  final String sceneId;

  final String id;
  final String name;

  /// The whole list afterwards, rather than one path added or removed. An
  /// undo then puts back exactly what was there, including the order.
  final List<String> paths;

  /// What the step is called: "Add weight" or "Remove weight".
  final String what;

  List<String> _was = const [];

  @override
  String get label => '$what on $name';

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was = List<String>.from(object.data);
    object.data
      ..clear()
      ..addAll(paths);
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.data
      ..clear()
      ..addAll(_was);
    host.sceneFor(sceneId)?.invalidate();
  }
}

/// Points an object and everything under it at a prefab.
///
/// What happens to the original when somebody drags it into the project: they
/// have made a prefab *of* this thing, and the thing they made it from should
/// be the first instance of it. Otherwise the object on screen quietly stops
/// being the one that gets the changes.
class LinkPrefab extends EditorCommand {
  LinkPrefab({
    required this.sceneId,
    required this.ids,
    required this.source,
    required this.what,
  });

  @override
  final String sceneId;

  final List<String> ids;

  /// The prefab's path, relative to the project.
  final String source;
  final String what;

  final Map<String, String?> _was = {};

  @override
  String get label => 'Make prefab $what';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    _was.clear();
    for (final id in ids) {
      final object = scene[id];
      if (object == null) continue;
      _was[id] = object.prefab;
      object.prefab = source;
    }
    scene.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    for (final entry in _was.entries) {
      scene[entry.key]?.prefab = entry.value;
    }
    scene.invalidate();
  }
}

/// Breaks an instance's link to the prefab it came from.
///
/// After this the objects are ordinary objects that happen to be shaped like
/// the prefab: nothing propagates to them and nothing propagates from them.
/// The way out for the one lamp post that has to be different.
class UnpackPrefab extends EditorCommand {
  UnpackPrefab({required this.sceneId, required this.ids, required this.what});

  @override
  final String sceneId;

  /// The root and everything under it — the link is on every one of them.
  final List<String> ids;
  final String what;

  final Map<String, String> _was = {};

  @override
  String get label => 'Unpack $what';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    _was.clear();
    for (final id in ids) {
      final object = scene[id];
      final source = object?.prefab;
      if (object == null || source == null) continue;
      _was[id] = source;
      object.prefab = null;
    }
    scene.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    for (final entry in _was.entries) {
      scene[entry.key]?.prefab = entry.value;
    }
    scene.invalidate();
  }
}

/// Changes which interface a canvas object shows.
///
/// A reference rather than a copy, like a mesh: the `.oui` is the document and
/// the scene says which one is on screen, so one interface can be on two
/// scenes and editing it changes both.
class SetInterface extends EditorCommand {
  SetInterface({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final String? to;

  String? _was;

  @override
  String get label => to == null ? 'Clear $name' : 'Set $name';

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was = object.interfaceAsset;
    object.interfaceAsset = to;
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.interfaceAsset = _was;
    host.sceneFor(sceneId)?.invalidate();
  }
}

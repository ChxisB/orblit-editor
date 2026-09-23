part of 'commands.dart';

// Links out of the scene — to a data file, or an interface — and the
// breaking of them. A prefab's link is a change to the whole document, and
// goes through ApplySceneDiff.

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

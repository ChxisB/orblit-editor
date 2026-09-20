part of 'commands.dart';

// Edits to the tree itself: what exists, what it is called, and where it sits.

/// Renames an object.
class Rename extends EditorCommand {
  Rename({
    required this.sceneId,
    required this.id,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String from;

  /// Not final: typing merges into one step rather than one per keystroke.
  String to;

  @override
  String get label => 'Rename $from';

  @override
  Object? get mergeKey => (id, 'name');

  @override
  void absorb(EditorCommand later) {
    if (later is Rename) to = later.to;
  }

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.name = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.name = from;
}

/// Moves an object: to a new parent, to a new place among its siblings, or
/// both.
///
/// One command rather than two, because dragging in a tree is one gesture and
/// the answer to "where did that go" should be one step of undo.
///
/// The local transform is rewritten so the object does not jump when its
/// parent changes. Dropping something into a folder should not move it — an
/// editor that teleported things on reparent would be unusable for layout.
class MoveObject extends EditorCommand {
  MoveObject({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.from,
    required this.to,
    required this.fromIndex,
    required this.toIndex,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final String? from;
  final String? to;
  final int fromIndex;
  final int toIndex;

  Vector3? _oldPosition;
  Vector3? _oldRotation;
  Vector3? _oldScale;

  @override
  String get label => from == to ? 'Reorder $name' : 'Move $name';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    final object = scene?[id];
    if (scene == null || object == null) return;

    _oldPosition = object.position.clone();
    _oldRotation = object.rotation.clone();
    _oldScale = object.scale.clone();

    final world = scene.worldOf(id).clone();
    scene.moveTo(id, parentId: to, index: toIndex);
    // Only when the parent actually changed: a reorder among siblings leaves
    // the transform alone, and recomposing it would introduce rounding for no
    // reason.
    if (from != to) placeInWorld(scene, object, world);
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    final object = scene?[id];
    if (scene == null || object == null) return;

    scene.moveTo(id, parentId: from, index: fromIndex);
    object.position.setFrom(_oldPosition ?? object.position);
    object.rotation.setFrom(_oldRotation ?? object.rotation);
    object.scale.setFrom(_oldScale ?? object.scale);
    scene.invalidate();
  }
}

/// Moves an object and everything under it from one scene into another.
///
/// A separate command from [MoveObject] because it is a different operation:
/// one scene loses a subtree and another gains it, and an undo has to put it
/// back in the scene it came from at the index it came from. Trying to do both
/// through one command means every field is nullable and neither case is
/// clear.
///
/// What makes it worth having at all is the shared set — the objects every
/// scene has. Without this, the only way to put something in it was to build
/// it there, and the obvious gesture of dragging a prop onto the Shared row
/// did nothing at all.
class MoveBetweenScenes extends EditorCommand {
  MoveBetweenScenes({
    required this.fromSceneId,
    required this.sceneId,
    required this.id,
    required this.name,
    required this.parentId,
    required this.index,
  });

  /// Where it came from.
  final String fromSceneId;

  /// Where it is going, which is the scene the history records this against:
  /// an undo should show the object where it will reappear.
  @override
  final String sceneId;

  final String id;
  final String name;

  /// The parent it lands under in the destination, or null for a root.
  final String? parentId;

  /// Where among its new siblings.
  final int index;

  @override
  Set<String> get touches => {sceneId, fromSceneId};

  /// What was taken out, with the indices to put it back at.
  List<({SceneObject object, int index})> _removed = const [];

  /// What actually went into the destination.
  ///
  /// The very same objects when nothing had to change, so the renderer keeps
  /// the key it knows them by and an undo puts back exactly what was there.
  /// Copies only when an id collided.
  List<SceneObject> _moved = const [];

  @override
  String get label => 'Move $name';

  @override
  void apply(SceneHost host) {
    final from = host.sceneFor(fromSceneId);
    final to = host.sceneFor(sceneId);
    final object = from?[id];
    if (from == null || to == null || object == null) return;

    // Read before the move, so the thing lands where it looked rather than
    // wherever its old local transform points under a new parent.
    final world = from.worldOf(id).clone();

    _removed = from.remove(id);

    // Two scenes can hold the same id — the starter scene names its objects
    // outright — and adding a second one would throw. Renaming on the way
    // across rather than refusing keeps the drag working.
    final clash = <String, String>{
      for (final entry in _removed)
        if (to.contains(entry.object.id))
          entry.object.id: '${entry.object.id}~${to.length}',
    };

    _moved = [
      for (final entry in _removed)
        if (clash.isEmpty)
          entry.object
        else
          entry.object.copyAs(
            id: clash[entry.object.id] ?? entry.object.id,
            parentId: entry.object.parentId == null
                ? null
                : (clash[entry.object.parentId] ?? entry.object.parentId),
          ),
    ];

    final root = _moved.first;
    root.parentId = parentId;
    for (final moving in _moved) {
      to.add(moving);
    }

    to.moveTo(root.id, parentId: parentId, index: index);
    placeInWorld(to, root, world);
  }

  @override
  void revert(SceneHost host) {
    final from = host.sceneFor(fromSceneId);
    final to = host.sceneFor(sceneId);
    if (from == null || to == null || _removed.isEmpty) return;

    to.remove(_moved.first.id);
    from.restore(_removed);
  }
}

/// Adds an object to the scene.
class AddObject extends EditorCommand {
  AddObject(this.object, {required this.sceneId, this.parentId});

  @override
  final String sceneId;

  final SceneObject object;
  final String? parentId;

  @override
  String get label => 'Add ${object.name}';

  @override
  void apply(SceneHost host) {
    object.parentId = parentId;
    host.sceneFor(sceneId)?.add(object);
  }

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?.remove(object.id);
}

/// Puts objects into a scene, keeping the shape they had.
///
/// One command for the whole paste rather than one per object: a paste is one
/// thing somebody did, and undoing it halfway would leave a subtree with its
/// parent missing.
class PasteObjects extends EditorCommand {
  PasteObjects({
    required this.sceneId,
    required this.objects,
    required this.roots,
    required this.what,
    this.worlds = const {},
  });

  @override
  final String sceneId;

  /// In insertion order: a parent is always added before its children, so no
  /// object is ever briefly pointing at something that is not there.
  final List<SceneObject> objects;

  /// The tops of what was pasted, which is what a delete has to take.
  final List<String> roots;

  /// Where each top sat in the world when it was copied.
  ///
  /// Pasting into a scene whose parent chain is different would otherwise put
  /// the object somewhere else entirely, because a local transform only means
  /// anything relative to the parent it was measured against.
  final Map<String, Matrix4> worlds;

  final String what;

  @override
  String get label => 'Paste $what';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    for (final object in objects) {
      if (!scene.contains(object.id)) scene.add(object);
    }

    // After every object is in, so a root's new parent can be resolved.
    for (final root in roots) {
      final world = worlds[root];
      final object = scene[root];
      if (world != null && object != null) placeInWorld(scene, object, world);
    }
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    // Roots only: removing one takes everything under it, and asking for a
    // child that has already gone is not an error worth having.
    for (final root in roots) {
      scene.remove(root);
    }
  }
}

/// Deletes objects and everything under them.
///
/// One command however many are selected: deleting three things is one thing
/// somebody did, and undoing it a third at a time would be tedious and would
/// let a subtree come back without its parent.
class DeleteObjects extends EditorCommand {
  DeleteObjects({required this.sceneId, required this.ids, required this.what});

  @override
  final String sceneId;

  final List<String> ids;
  final String what;

  final List<List<({SceneObject object, int index})>> _removed = [];

  @override
  String get label => 'Delete $what';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    _removed.clear();
    for (final id in ids) {
      // Already gone if it was inside something removed a moment ago, which is
      // not a mistake — the selection simply held a parent and its child.
      if (!scene.contains(id)) continue;
      _removed.add(scene.remove(id));
    }
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    // Backwards, so each restore puts things back at indices the ones after it
    // have not yet shifted.
    for (final batch in _removed.reversed) {
      scene.restore(batch);
    }
  }
}

/// Puts a prefab's contents back over an instance already in the scene.
///
/// What a revert does, and what applying changes to a prefab does to every
/// other instance of it. The whole subtree goes at once rather than field by
/// field: a prefab can gain and lose children, and a change that only ever
/// touched properties would leave the extra ones behind.
///
/// One command per instance, so undo puts an instance back exactly as it was
/// rather than approximately.
class ReplaceSubtree extends EditorCommand {
  ReplaceSubtree({
    required this.sceneId,
    required this.rootId,
    required this.objects,
    required this.what,
    this.label_ = 'Revert',
  });

  @override
  final String sceneId;

  /// The object being replaced, which keeps its id so the selection survives.
  final String rootId;

  /// The replacement, parents before children.
  final List<SceneObject> objects;

  final String what;
  final String label_;

  List<({SceneObject object, int index})> _removed = const [];

  @override
  String get label => '$label_ $what';

  @override
  void apply(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null || !scene.contains(rootId)) return;

    // Where it sat among its siblings, so a revert does not send it to the
    // bottom of the tree under somebody's cursor.
    final at = _removed.isEmpty ? null : _removed.first.index;
    _removed = scene.remove(rootId);

    // Kept contiguous rather than appended, so the subtree stays where it was
    // in the list and everything after it keeps its order.
    final index = at ?? _removed.first.index;
    for (var i = 0; i < objects.length; i++) {
      final into = index + i;
      scene.add(objects[i], at: into > scene.objects.length ? null : into);
    }
  }

  @override
  void revert(SceneHost host) {
    final scene = host.sceneFor(sceneId);
    if (scene == null || _removed.isEmpty) return;
    scene.remove(rootId);
    scene.restore(_removed);
  }
}

part of 'editor_shell.dart';

// Prefabs: made from a selection, placed, and kept in step with the file
// every instance of one came from.
//
// An instance is a link and what is different about it, and all the working
// out of that lives in orblit_scene. What is here is the editor's side: which
// file, which scene, what to say, and every change made as one step that can
// be undone.

extension _Prefabs on _EditorShellState {
  /// Saves an object and everything under it as a prefab asset.
  ///
  /// The object it was made from becomes the first instance, the way it does
  /// in every editor that has prefabs. Anything else and the thing on screen
  /// would look like the prefab while quietly not being one.
  void _makePrefab(ObjectDrag dragged, String directory) {
    final open = _workspace.sceneHolding(dragged.id);
    final scene = open?.scene;
    if (open == null || scene == null) return;

    final object = scene[dragged.id];
    if (object == null) return;
    if (object.kind == ObjectKind.scene) {
      _say('A scene is already a file. Save it instead.');
      return;
    }

    final path = _assets.available(
      directory,
      '${object.name}$prefabExtension',
    );
    final asset = _assets.relative(path);

    final doc.PrefabEdit made;
    try {
      made = doc.makePrefab(
        SceneDocument.documentOf(scene),
        dragged.id,
        asset: asset,
        source: _prefabs.find,
        name: object.name,
      );
    } on doc.PrefabException catch (error) {
      _say(error.message);
      return;
    }

    final problem = _prefabs.write(asset, made.prefab);
    if (problem != null) {
      _say('Could not save the prefab: $problem', level: LogLevel.error);
      return;
    }

    _changeScene(open, made.document, 'Make prefab ${object.name}');
    _follow(made.renamed);
    _say(
      'Saved ${p.basename(path)}. '
      '${object.name} is now an instance of it.',
    );
    _report(made.problems);
  }

  /// Puts an instance of a prefab into the open scene.
  ///
  /// Where the prefab's root stood when it was made, which is where the thing
  /// it was made from stood — and under the selected group, or beside the
  /// selected object, like anything else added to a scene.
  void _placePrefab(String path) {
    final open = _current;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.');
      return;
    }

    final asset = _assets.relative(path);
    final read = _prefabs.read(asset);
    final prefab = read.prefab;
    if (prefab == null) {
      _say(read.problem ?? '${p.basename(path)} is not a readable prefab.');
      return;
    }

    final selected = _primary == null ? null : scene[_primary!];
    final parent = selected == null
        ? null
        : (selected.kind == ObjectKind.group ? selected.id : selected.parentId);

    // Placed folded and opened at once, so a new instance is made exactly the
    // way one read from a file is and cannot come out any different.
    final id = _nextObjectId();
    final before = SceneDocument.documentOf(scene);
    final placed = doc.expandInstances(
      before.copyWith(
        entities: [
          ...before.entities,
          doc.SceneEntity(
            id: id,
            name: _uniqueName(scene, prefab.name),
            parent: parent,
            components: {
              doc.SceneComponents.prefab: doc.PrefabComponent(asset: asset),
            },
          ),
        ],
      ),
      _prefabs.find,
      only: [id],
    );

    _changeScene(open, placed.document, 'Place ${prefab.name}');
    _report(placed.problems);
    _select(id);
  }

  /// Writes what an instance looks like now into its prefab, and brings every
  /// other instance of it up to date.
  ///
  /// The other instances keep what was changed about each of them and take
  /// the rest from the prefab: a lamp somebody made red stays red when the
  /// lamp post grows a second arm. That includes instances inside other
  /// prefabs, and instances in scenes other than this one.
  void _applyPrefab(String id) {
    final instance = _instanceAt(id);
    if (instance == null) return;
    final (:entry, :scene, :head, :asset) = instance;

    // What every other instance was opened against, needed to work out what
    // each of them had changed before the prefab changes under them.
    final was = _prefabs.find(asset);

    final doc.PrefabEdit made;
    try {
      made = doc.applyInstance(
        SceneDocument.documentOf(scene),
        head,
        source: _prefabs.find,
      );
    } on doc.PrefabException catch (error) {
      _say(error.message);
      return;
    }

    final problem = _prefabs.write(asset, made.prefab);
    if (problem != null) {
      _say(problem, level: LogLevel.error);
      return;
    }

    var touched = _instancesUsing(scene, asset, except: head);
    _changeScene(entry, made.document, 'Apply ${scene[head]?.name ?? asset}');
    _follow(made.renamed);
    final problems = [...made.problems];

    for (final other in _workspace.entries) {
      final otherScene = other.scene;
      if (identical(other, entry) || otherScene == null) continue;
      final using = _instancesUsing(otherScene, asset);
      if (using == 0) continue;

      final refreshed = doc.refreshInstances(
        SceneDocument.documentOf(otherScene),
        asset,
        before: _prefabs.withOne(asset, was),
        after: _prefabs.find,
      );
      _changeScene(other, refreshed.document, 'Update ${made.prefab.name}');
      problems.addAll(refreshed.problems);
      touched += using;
    }

    final file = p.basename(asset);
    _say(
      touched == 0
          ? 'Saved $file.'
          : 'Saved $file and updated $touched other '
                'instance${touched == 1 ? '' : 's'}.',
    );
    _report(problems);
  }

  /// Throws away what an instance changed about its prefab. It keeps where it
  /// stands, and whatever was hung on it from outside stays hung on it.
  void _revertPrefab(String id) {
    final instance = _instanceAt(id);
    if (instance == null) return;
    final (:entry, :scene, :head, :asset) = instance;

    final reverted = doc.revertInstance(
      SceneDocument.documentOf(scene),
      head,
      _prefabs.find,
    );
    _changeScene(entry, reverted.document, 'Revert ${scene[head]?.name}');
    _report(reverted.problems);
  }

  /// Cuts an instance loose from its prefab: its parts become ordinary
  /// objects, with ids of their own, that the prefab no longer changes.
  void _unpackPrefab(String id) {
    final instance = _instanceAt(id);
    if (instance == null) return;
    final (:entry, :scene, :head, :asset) = instance;

    final unpacked = doc.unpackInstance(
      SceneDocument.documentOf(scene),
      head,
      source: _prefabs.find,
      fresh: _nextObjectId,
    );
    _changeScene(entry, unpacked.document, 'Unpack ${scene[head]?.name}');
    _follow(unpacked.renamed);
  }

  /// The instance [id] is, or is part of, and where.
  ///
  /// A part answers for the outermost instance it is in: that is the one this
  /// scene links to, and a prefab inside another is changed by opening the
  /// outer one. Says why, and returns null, for an instance whose prefab
  /// cannot be read — it is only a link until it can be.
  ({SceneEntry entry, EditorScene scene, String head, String asset})?
  _instanceAt(String id) {
    final entry = _workspace.sceneHolding(id);
    final scene = entry?.scene;
    if (entry == null || scene == null) return null;

    final head = doc.EntityPath.instanceOf(id) ?? id;
    final link = scene[head]?.prefab;
    final asset = link?.asset;
    if (link == null || asset == null) return null;

    if (link.state != doc.PrefabState.open) {
      final problem = _prefabs.read(asset).problem;
      _say(
        problem == null
            ? '${scene[head]?.name} has not been opened. Reload the scene.'
            : '$problem Until it is, ${scene[head]?.name} is only a link.',
      );
      return null;
    }
    return (entry: entry, scene: scene, head: head, asset: asset);
  }

  /// How many instances in [scene], outermost, use [asset] — directly, or by
  /// holding a prefab that does.
  int _instancesUsing(EditorScene scene, String asset, {String? except}) => {
    for (final object in scene.objects)
      if (object.prefab?.asset == asset &&
          (except == null || !doc.EntityPath.within(except, object.id)))
        doc.EntityPath.instanceOf(object.id) ?? object.id,
  }.length;

  /// Every prefab the scenes in memory hold instances of.
  ///
  /// What has to stay as it was read: those instances are saved as what they
  /// changed about exactly that version.
  Set<String> _prefabsInUse() => {
    for (final entry in _workspace.entries)
      if (entry.scene case final scene?)
        for (final object in scene.objects)
          ?object.prefab?.asset,
  };

  /// Makes the scene in [entry] into [after], as one step.
  void _changeScene(SceneEntry entry, doc.SceneDocument after, String label) {
    final scene = entry.scene;
    if (scene == null) return;
    final diff = doc.SceneDiff.between(SceneDocument.documentOf(scene), after);
    if (diff.isEmpty) return;
    _run(ApplySceneDiff(sceneId: entry.id, label: label, diff: diff));
  }

  /// Carries the selection across ids that changed.
  void _follow(Map<String, String> renamed) {
    if (renamed.isEmpty) return;
    setState(() {
      final now = [for (final id in _selected) renamed[id] ?? id];
      _selected
        ..clear()
        ..addAll(now);
      if (_primary case final id?) _primary = renamed[id] ?? id;
    });
  }
}

part of 'editor_shell.dart';

// Terrains: made from the Add menu, found for the brush, saved with
// everything else, and drawn again when a picture they paint with arrives.

extension _Terrains on _EditorShellState {
  /// The terrain a brush lands on: the selected object's, or else the first
  /// one the loaded scene or the shared set puts in the world.
  ///
  /// Not only the selected one, so the brush works on the ground straight
  /// after switching to the terrain mode, without finding it in the outliner
  /// first.
  OpenTerrain? _terrainTarget() {
    final selected = _selectedObject;
    final chosen = selected == null ? null : terrainComponentOf(selected);
    if (chosen != null) return _terrains.terrainFor(chosen.file);
    for (final entry in [_current, _workspace.sharedEntry]) {
      for (final object in entry?.scene?.objects ?? const <SceneObject>[]) {
        final component = terrainComponentOf(object);
        if (component == null) continue;
        final open = _terrains.terrainFor(component.file);
        if (open != null) return open;
      }
    }
    return null;
  }

  /// A change on the bench that is not a step on the undo stack: a picture
  /// arriving, a terrain written, the brush set differently.
  ///
  /// The brush's panels listen for themselves. What is left is the ground,
  /// which the views draw again when a picture arrives, and the top bar's
  /// mark, which changes when a terrain is saved.
  void _onTerrainsChanged() {
    final unsaved = _terrains.anyUnsaved;
    final pictures = _terrains.picturesRevision;
    if (unsaved != _terrainsUnsaved) {
      _terrainsUnsaved = unsaved;
      _terrainPictures = pictures;
      setState(() {});
    } else if (pictures != _terrainPictures) {
      _terrainPictures = pictures;
      _rebuildForMove();
    }
  }

  /// Writes every terrain with changes, and says which could not be written.
  void _saveTerrains() {
    for (final problem in _terrains.saveAll()) {
      _say(problem, level: LogLevel.error);
    }
  }

  /// Puts new ground in the scene: a terrain file of its own, written now,
  /// and a group object that shows it.
  ///
  /// Written straight away rather than when the scene is saved, so the
  /// object never names a file that is not there. Undoing the add takes the
  /// object away and leaves the file, as it leaves any other asset.
  void _newTerrain() {
    final open = _working;
    final scene = open?.scene;
    if (open == null || scene == null) {
      _say('There is no scene loaded to add to.', level: LogLevel.warning);
      return;
    }

    final file = _freeTerrainFile();
    final problem = _terrains.create(file, _startingGround());
    if (problem != null) {
      _say(problem, level: LogLevel.error);
      return;
    }

    final object = SceneObject(
      id: _nextObjectId(),
      name: _uniqueName(scene, 'Terrain'),
      kind: ObjectKind.group,
      components: {
        doc.SceneComponents.terrain: doc.TerrainComponent(file: file),
      },
    );
    _run(AddObject(object, sceneId: open.id));
    _select(object.id);
    _say('Made $file');
  }

  /// A folder of its own under `terrain/`, since a terrain's regions are
  /// written beside it and two in one folder would write over each other's.
  String _freeTerrainFile() {
    for (var i = 1; ; i++) {
      final name = i == 1 ? 'terrain' : 'terrain_$i';
      final file = p.posix.join('terrain', name, '$name$terrainExtension');
      final folder = p.join(widget.project.directory, 'terrain', name);
      if (_terrains[file] == null && !Directory(folder).existsSync()) {
        return file;
      }
    }
  }

  /// Flat ground half a kilometre across round the origin, with rock for
  /// cliffs and grass for the rest: the two sets the automatic cover starts
  /// out choosing between.
  static Terrain _startingGround() {
    final terrain = Terrain(
      sets: [
        TerrainSet(name: 'Rock', tileSize: 8, triplanar: true),
        TerrainSet(name: 'Grass'),
      ],
    );
    for (final z in const [-1, 0]) {
      for (final x in const [-1, 0]) {
        terrain.addRegion(RegionKey(x, z));
      }
    }
    return terrain;
  }
}

part of 'editor_shell.dart';

// Cutscenes in the Cinematics workspace: made, opened, cut between cameras
// and played from a mark. And the scene view steering a camera, so a shot
// is framed by moving rather than by typing numbers.

/// Where a project keeps its cutscenes, and where playing looks for the
/// ones a mark can start.
const String _cutsceneFolder = 'cutscenes';

extension _Cinematics on _EditorShellState {
  EditorMode _cinematicsMode() => cinematicsMode(
    bench: _cuts,
    onNew: _newCutscene,
    onAddShot: _addShot,
    onUseView: _useThisView,
    onLeave: () => _lookThrough(null),
  );

  /// Opens the cutscene at [path] under the view, in Cinematics.
  void _openCutscene(String path) {
    _enterModeNamed('cinematics');
    if (!_readCutscene(path)) return;
    _open(cutsceneTimelinePanel);
  }

  bool _readCutscene(String path) {
    final List<String> problems;
    try {
      problems = _cuts.openFile(path);
    } on CutsceneFormatException catch (error) {
      _say(
        '${p.basename(path)} is not a cutscene: ${error.message}',
        level: LogLevel.error,
      );
      return false;
    } on FileSystemException catch (error) {
      _say(
        '${p.basename(path)} could not be read: ${error.message}',
        level: LogLevel.error,
      );
      return false;
    }
    if (problems.isEmpty) return true;
    _say(
      problems.length == 1
          ? problems.single
          : '${problems.length} things in that cutscene could not be read. '
                'First: ${problems.first}',
      level: LogLevel.warning,
    );
    return true;
  }

  /// Makes a cutscene in the project's cutscenes folder and opens it.
  void _newCutscene() {
    final path = _makeIn(_cutsceneFolder, NewAsset.cutscene);
    if (path != null) _openCutscene(path);
  }

  /// Cuts to the selected camera at the playhead, or to the scene's first
  /// camera when no camera is selected.
  void _addShot() {
    final scene = _current?.scene;
    if (scene == null) return;
    final selected = _primary == null ? null : scene[_primary!];
    final camera = selected?.kind == ObjectKind.camera
        ? selected
        : scene.objects
              .where((object) => object.kind == ObjectKind.camera)
              .firstOrNull;
    if (camera == null) {
      _say(
        'Add a camera to the scene first, or choose Use this view.',
        level: LogLevel.warning,
      );
      return;
    }
    _cutTo(camera.id);
  }

  /// Puts a camera where the scene view is, looking where it looks, and
  /// cuts to it at the playhead.
  ///
  /// In the loaded scene rather than wherever the selection is, since a
  /// cutscene only looks through cameras there.
  void _useThisView() {
    final entry = _current;
    final scene = entry?.scene;
    if (entry == null || scene == null || _cuts.clip == null) return;
    final place = cameraPlaceFor(_camera, scene);
    final camera = SceneObject(
      id: _nextObjectId(),
      name: _uniqueName(scene, 'Camera'),
      kind: ObjectKind.camera,
      position: place.position,
      rotation: place.rotation,
    );
    _run(AddObject(camera, sceneId: entry.id));
    _select(camera.id);
    _cutTo(camera.id);
  }

  void _cutTo(String camera) {
    final clip = _cuts.clip;
    if (clip == null) return;
    _cuts.editShots(
      'Add shot',
      (shots) =>
          cutTo(shots, camera: camera, at: _cuts.frame, length: clip.duration),
    );
  }

  /// Flies the view being worked in to [camera], and has it steer the camera
  /// from then on. Null stops, and leaves the view where it is.
  void _lookThrough(String? camera) {
    // So the steering that follows is a step of its own.
    _history.seal();
    final scene = _current?.scene;
    final object = camera == null ? null : scene?[camera];
    if (scene == null || object == null) {
      if (_piloting != null) setState(() => _piloting = null);
      return;
    }
    // The view last worked in, when this workspace shows it.
    final view = _layout.groupOf(_using) == null ? 'scene' : _using;
    setState(() {
      _piloting = (camera: object.id, view: view);
      _cameras[view] = viewThrough(scene, object);
    });
  }

  /// Moves the camera being looked through to where [view] now is, when
  /// [panel] is the view that steers it.
  void _steer(String panel, OrbitCamera view) {
    final piloting = _piloting;
    if (piloting == null || piloting.view != panel) return;
    final entry = _current;
    final scene = entry?.scene;
    final object = scene?[piloting.camera];
    if (entry == null || scene == null || object == null) {
      setState(() => _piloting = null);
      return;
    }
    _run(
      PlaceObject(
        sceneId: entry.id,
        id: object.id,
        name: object.name,
        from: (position: object.position, rotation: object.rotation),
        to: cameraPlaceFor(view, scene, parent: object.parentId),
      ),
    );
  }

  /// The finished shot: the open cutscene at its playhead, looking through
  /// its shots as the game will. Between shots, and with none open, it is
  /// the scene's own camera, as it is in the game.
  Widget _shotPreview() => ListenableBuilder(
    listenable: _cuts,
    builder: (context, _) {
      final scene = _current?.scene;
      final cutscene = _cuts.shown?.cutscene;
      return GameView(
        workspace: _workspace,
        camera: scene == null || cutscene == null
            ? null
            : cutsceneCamera(scene, cutscene, _cuts.at),
        projectRoot: widget.project.directory,
        geometryOf: _geometry.pathFor,
        terrainOf: _terrains.renderFor,
        scatterOf: _terrains.scatterFor,
      );
    },
  );

  /// The project's cutscenes by name, for a mark to start as it would in
  /// the game. The open ones are as they are now, not as last saved.
  Map<String, CutsceneDocument> _cutscenesForPlayback() {
    final folder = Directory(p.join(widget.project.directory, _cutsceneFolder));
    if (!folder.existsSync()) return const {};
    final files =
        folder
            .listSync()
            .whereType<File>()
            .where((file) => p.extension(file.path) == cutsceneExtension)
            .toList()
          ..sort((a, b) => a.path.compareTo(b.path));
    final byName = <String, CutsceneDocument>{};
    for (final file in files) {
      final cutscene = _cuts[file.path]?.cutscene ?? _cutsceneIn(file);
      if (cutscene == null) continue;
      if (byName.containsKey(cutscene.name)) {
        _say(
          'Two cutscenes are called ${cutscene.name}. The game will not load '
          'both, so rename one.',
          level: LogLevel.warning,
        );
        continue;
      }
      byName[cutscene.name] = cutscene;
    }
    return byName;
  }

  CutsceneDocument? _cutsceneIn(File file) {
    final name = p.basename(file.path);
    try {
      return CutsceneDocument.decode(file.readAsStringSync()).cutscene;
    } on CutsceneFormatException catch (error) {
      _say('$name is not a cutscene: ${error.message}', level: LogLevel.error);
    } on FileSystemException catch (error) {
      _say('$name could not be read: ${error.message}', level: LogLevel.error);
    }
    return null;
  }
}

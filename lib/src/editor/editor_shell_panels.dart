part of 'editor_shell.dart';

// The dock: where the panels are, which camera each viewport looks
// through, and what the editor registers for itself.

extension _Panels on _EditorShellState {
  /// The editor's own panels, sections and mode, registered the way anything
  /// else would register one, and then whatever [EditorShell.extend] adds.
  EditorRegistry _register() {
    final registry = EditorRegistry();
    _registerPanels(registry.panels);
    _registerSections(registry.sections);
    registry.gizmos
      ..register(bodyGizmo())
      ..register(jointGizmo());
    registry.modes.register(
      const EditorMode(
        name: 'scene',
        label: 'Scene',
        icon: Icons.open_with,
        layout: DockLayout.standard,
      ),
    );
    registry.modes.register(
      terrainMode(bench: _terrains, history: _history, target: _terrainTarget),
    );
    widget.extend?.call(registry);
    return registry;
  }

  /// The panels the editor has always had, in the order the View menu lists
  /// them.
  void _registerPanels(PanelRegistry panels) => panels
    ..register(
      PanelType(kind: PanelKind.outliner, build: (_, _) => _outliner()),
    )
    // Not one that shows movement, even though it shows the numbers: the
    // three rows that do listen for themselves, so the rest of it — a dozen
    // text fields with their own focus, actions and overlays — is left alone.
    ..register(
      PanelType(
        kind: PanelKind.inspector,
        build: (_, _) => _inspector(_selectedObject),
      ),
    )
    ..register(
      PanelType(
        kind: PanelKind.viewport,
        opensAs: 'scene',
        showsMovement: true,
        build: (_, panel) => _viewport(panel),
      ),
    )
    ..register(
      PanelType(
        kind: PanelKind.game,
        showsMovement: true,
        build: (_, _) => GameView(
          workspace: _workspace,
          projectRoot: widget.project.directory,
          geometryOf: _geometry.pathFor,
          interface: _sceneInterface,
          terrainOf: _terrains.renderFor,
          scatterOf: _terrains.scatterFor,
        ),
      ),
    )
    ..register(PanelType(kind: PanelKind.project, build: (_, _) => _project()))
    ..register(
      PanelType(
        kind: PanelKind.console,
        build: (_, _) => ConsolePanel(log: _log),
      ),
    )
    // Not one that shows movement either: it listens to the bench itself,
    // which is where the playhead and the keys are.
    ..register(
      PanelType(
        kind: PanelKind.timeline,
        build: (_, _) => TimelinePanel(
          bench: _bench,
          selected: _selectedObject,
          onProblem: (message) => _say(message, level: LogLevel.error),
        ),
      ),
    )
    ..register(
      PanelType(
        kind: PanelKind.modelling,
        build: (_, _) => SingleChildScrollView(
          padding: const EdgeInsets.all(Space.sm),
          child: _modellingTools(),
        ),
      ),
    )
    ..register(
      PanelType(
        kind: PanelKind.uvs,
        build: (_, _) => SingleChildScrollView(
          padding: const EdgeInsets.all(Space.sm),
          child: _uvEditor(),
        ),
      ),
    )
    ..register(
      PanelType(
        kind: terrainBrushPanel,
        build: (_, _) => SingleChildScrollView(
          padding: const EdgeInsets.all(Space.sm),
          child: TerrainBrushPanel(bench: _terrains, target: _terrainTarget()),
        ),
      ),
    );

  /// The inspector's sections: the ones it has always had, the shape and
  /// geometry controls, the physics body and the terrain. Those are the shell's, because
  /// the shell owns what is being edited; the inspector does not know what an
  /// extrude is.
  void _registerSections(Registry<InspectorSection> sections) {
    InspectorSection.builtIn.forEach(sections.register);
    sections.register(
      InspectorSection(
        name: 'shape',
        appliesTo: (target) => target.object.kind == ObjectKind.shape,
        build: (target) => _meshPanel(target.object),
      ),
      // Where they have always been: after what every object has, ahead of
      // anything it puts on screen.
      before: 'interface',
    );
    sections.register(
      bodySection(
        boundsOf: (object) => object.localBounds(reported: _models.of(object)),
      ),
      before: 'interface',
    );
    sections
      ..register(jointSection(), before: 'interface')
      ..register(terrainSection(bench: _terrains), before: 'interface');
  }

  /// The object the inspector is showing, if it is one rather than a scene.
  SceneObject? get _selectedObject =>
      _primary == null ? null : _inspected?.scene?[_primary!];

  /// Opens the panel registered for [kind], or shows it if it is open.
  void _open(PanelKind kind) {
    final type = _registry.panels.typeOf(kind);
    if (type == null) return;
    setState(() => _layout = _layout.add(type.panel));
  }

  OrbitCamera _cameraFor(String id) => _cameras[id] ??= OrbitCamera();

  /// The camera of the view being worked in.
  OrbitCamera get _camera => _cameraFor(_using);

  set _camera(OrbitCamera camera) => _cameras[_using] = camera;

  /// Switches to [mode], with the panels as they were last left in it.
  void _enterMode(EditorMode mode) {
    if (mode.name == _mode.name) return;
    setState(() {
      _mode = mode;
      _layout = _readLayout() ?? mode.layout();
    });
  }

  /// Where the layout is kept: with the project, since it is about this
  /// project's panels rather than about the editor.
  ///
  /// One for each mode. The scene's keeps the name it had before there were
  /// modes, so a layout saved then still opens.
  File get _layoutFile => File(
    p.join(
      widget.project.directory,
      '.orblit',
      _mode.name == 'scene' ? 'layout.json' : 'layout.${_mode.name}.json',
    ),
  );

  DockLayout? _readLayout() {
    try {
      final file = _layoutFile;
      if (!file.existsSync()) return null;
      return DockLayout.read(
        file.readAsStringSync(),
        kinds: _registry.panels.kinds,
      );
    } on FileSystemException {
      return null;
    }
  }

  void _relayout(DockLayout layout) {
    setState(() => _layout = layout);
    try {
      final file = _layoutFile;
      file.parent.createSync(recursive: true);
      file.writeAsStringSync(layout.toText());
    } on FileSystemException {
      // Not worth a message. A layout that cannot be saved comes back as the
      // standard one, which is a small loss and not one worth interrupting
      // somebody over.
    }
  }

  /// One panel, whatever it happens to be.
  ///
  /// The layout says what goes where and this says what each one is. Keeping
  /// the two apart is what lets the arrangement be a file and a drag rather
  /// than a widget tree somebody has to edit.
  Widget _buildPanel(BuildContext context, DockPanel panel) {
    final type = _registry.panels.typeOf(panel.kind);

    // A move can only change where things are, so a panel that does not show
    // that is handed back exactly as it was. Flutter compares the widget by
    // identity and skips the subtree — which is the whole saving, because a
    // subtree that is not rebuilt is not laid out or painted either.
    if (!_deeply && !(type?.showsMovement ?? false)) {
      final was = _panels[panel.id];
      if (was != null) return was;
    }

    final built = type == null
        ? UnregisteredPanel(panel: panel)
        : type.build(context, panel);
    _panels[panel.id] = built;
    return built;
  }

  Widget _outliner() => Outliner(
    workspace: _workspace,
    selected: _selected,
    primary: _primary,
    onSelect: _select,
    onSelectScene: (entry) => setState(() {
      _selected.clear();
      _primary = null;
      _selectedScene = entry.id;
    }),
    onLoadScene: _loadScene,
    onMove: _move,
    onDelete: _delete,
    onCloseScene: _closeScene,
  );

  Widget _inspector(SceneObject? selected) => Inspector(
    entry: _inspected,
    object: selected,
    history: _history,
    onLoad: _loadScene,
    selectionCount: _selected.length,
    dataAsset: _dataAsset,
    dataPanel: _dataAsset == null
        ? null
        : DataPanel(
            key: ValueKey(_dataAsset),
            path: _dataAsset!,
            store: _data,
            onProblem: _say,
            onExportTypes: () => _exportBindings(_dataAsset!),
          ),
    onOpenData: _showData,
    sections: _registry.sections.all,
    onOpenInterface: (path) =>
        _openInterface(p.join(widget.project.directory, path)),
    onDetachData: _detachData,
    onApplyPrefab: _applyPrefab,
    onRevertPrefab: _revertPrefab,
    onUnpackPrefab: _unpackPrefab,
    keying: _bench,
  );

  /// The shape and geometry controls, for an object that has geometry.
  Widget _meshPanel(SceneObject selected) => MeshPanel(
    shape: selected.shape,
    geometry: selected.geometry,
    onShape: (shape) => _reshape(selected, shape),
    outline: selected.outline,
    onOutline: (next, {required live}) =>
        _setOutline(selected, next, live: live),
    boundary: selected.boundary,
    onBoundary: (next, {required live}) =>
        _setBoundary(selected, next, live: live),
    naturalSize: selected.localBounds(reported: _models.of(selected)),
    onOpenTools: () => _open(PanelKind.modelling),
  );

  Widget _viewport(DockPanel panel) => SceneViewport(
    workspace: _workspace,
    camera: _cameraFor(panel.id),
    onCameraChanged: (camera) => setState(() => _cameras[panel.id] = camera),
    selected: _selected,
    primary: _primary,
    history: _history,
    onPick: (id, {required bool add}) {
      // Whichever view was last used is the one F frames in.
      _using = panel.id;
      // Clicking empty space clears the
      // selection, which is how somebody puts the
      // handles away without reaching for a menu.
      if (id == null) {
        if (add) return;
        setState(() {
          _selected.clear();
          _primary = null;
          _selectedScene = null;
        });
        return;
      }
      _select(id, additive: add);
    },
    onDropAsset: _dropAsset,
    projectRoot: widget.project.directory,
    editing: _editing,
    elementMode: _elementMode,
    elementSelection: _elements,
    onPickElement: _pickElement,
    onDragElements: _dragElements,
    onSelectElements: _selectElements,
    seeThroughElements: _seeThrough,
    snapping: _snapping,
    grid: _grid,
    models: _models,
    drawing: _drawing,
    onDrawPoint: _drawPoint,
    onDrawFinish: _finishDrawing,
    onSnapping: (next) => setState(() {
      _snapping
        ..on = next.on
        ..step = next.step
        ..angle = next.angle;
    }),
    geometryOf: _geometry.pathFor,
    interface: _sceneInterface,
    showInterface: _showInterface,
    onToggleInterface: () => setState(() => _showInterface = !_showInterface),
    outlineSelection: _outlineSelection,
    onToggleOutline: () =>
        setState(() => _outlineSelection = !_outlineSelection),
    previewOf: (camera) => GameView(
      workspace: _workspace,
      projectRoot: widget.project.directory,
      geometryOf: _geometry.pathFor,
      through: camera,
      plain: true,
      terrainOf: _terrains.renderFor,
      scatterOf: _terrains.scatterFor,
    ),
    onSceneNotes: _reportSceneNotes,
    modeInput: _mode.input,
    modeOverlay: _mode.overlay,
    terrainOf: _terrains.renderFor,
    scatterOf: _terrains.scatterFor,
    gizmos: _registry.gizmos.all,
    // The viewport owns the clock; this is how
    // the tree and the inspector hear about it.
    onClock: () {
      if (mounted) setState(() {});
    },
  );

  Widget _project() => AssetBrowser(
    tree: _assets,
    cookStatus: _cookStatus,
    onOpenAsset: (asset) {
      // A scene opens here; anything somebody would
      // type into goes where they type.
      if (asset.kind == AssetKind.scene) {
        _openScene(asset.path);
        return;
      }
      if (asset.kind == AssetKind.canvas) {
        _openInterface(asset.path);
        return;
      }
      if (asset.kind == AssetKind.texture) {
        _applyTexture(asset.path);
        return;
      }
      if (asset.kind == AssetKind.clip) {
        _openClip(asset.path);
        return;
      }
      const editable = {
        AssetKind.script,
        AssetKind.style,
        AssetKind.native,
        AssetKind.data,
        AssetKind.material,
      };
      if (editable.contains(asset.kind)) {
        _openInCode(asset.path);
      }
    },
    onProblem: _say,
    onMakePrefab: _makePrefab,
    onBuild: (asset) => _buildScript(asset.path),
    onSelectAsset: (asset) => setState(() {
      // Only a data object claims the inspector.
      // Selecting a mesh should not take the panel
      // away from the object being edited.
      _dataAsset = asset?.kind == AssetKind.dataObject
          ? _assets.relative(asset!.path)
          : null;
    }),
  );
}

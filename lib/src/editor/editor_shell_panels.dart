part of 'editor_shell.dart';

// The dock: where the panels are, which camera each viewport looks
// through, and what goes inside a panel of each kind.

/// Which panels show where things are.
///
/// The inspector is not one of them, even though it shows the numbers: the
/// three rows that do listen for themselves, so the rest of it — a dozen
/// text fields with their own focus, actions and overlays — is left alone.
bool _showsMovement(PanelKind kind) =>
    kind == PanelKind.viewport || kind == PanelKind.game;

extension _Panels on _EditorShellState {
  OrbitCamera _cameraFor(String id) => _cameras[id] ??= OrbitCamera();

  /// The camera of the view being worked in.
  OrbitCamera get _camera => _cameraFor(_using);

  set _camera(OrbitCamera camera) => _cameras[_using] = camera;

  /// Where the layout is kept: with the project, since it is about this
  /// project's panels rather than about the editor.
  File get _layoutFile =>
      File(p.join(widget.project.directory, '.orblit', 'layout.json'));

  DockLayout? _readLayout() {
    try {
      final file = _layoutFile;
      if (!file.existsSync()) return null;
      return DockLayout.read(file.readAsStringSync());
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
    // A move can only change where things are, so a panel that does not show
    // that is handed back exactly as it was. Flutter compares the widget by
    // identity and skips the subtree — which is the whole saving, because a
    // subtree that is not rebuilt is not laid out or painted either.
    if (!_deeply && !_showsMovement(panel.kind)) {
      final was = _panels[panel.id];
      if (was != null) return was;
    }

    final built = _panelFor(panel);
    _panels[panel.id] = built;
    return built;
  }

  Widget _panelFor(DockPanel panel) {
    final selected = _primary == null ? null : _inspected?.scene?[_primary!];

    return switch (panel.kind) {
      PanelKind.outliner => _outliner(selected),
      PanelKind.inspector => _inspector(selected),
      PanelKind.viewport => _viewport(panel, selected),
      PanelKind.game => GameView(
        workspace: _workspace,
        projectRoot: widget.project.directory,
        geometryOf: _geometry.pathFor,
        interface: _sceneInterface,
      ),
      PanelKind.project => _project(),
      PanelKind.console => ConsolePanel(log: _log),
      PanelKind.modelling => SingleChildScrollView(
        padding: const EdgeInsets.all(Space.sm),
        child: _modellingTools(),
      ),
      PanelKind.uvs => SingleChildScrollView(
        padding: const EdgeInsets.all(Space.sm),
        child: _uvEditor(),
      ),
    };
  }

  Widget _outliner(SceneObject? selected) => Outliner(
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
    // The shape and geometry controls. The inspector shows what it is
    // given and does not know what an extrude is.
    meshPanel: selected?.kind != ObjectKind.shape
        ? null
        : MeshPanel(
            shape: selected!.shape,
            geometry: selected.geometry,
            onShape: (shape) => _reshape(selected, shape),
            outline: selected.outline,
            onOutline: (next, {required live}) =>
                _setOutline(selected, next, live: live),
            boundary: selected.boundary,
            onBoundary: (next, {required live}) =>
                _setBoundary(selected, next, live: live),
            naturalSize: selected.localBounds(
              reported: _models.of(selected),
            ),
            onOpenTools: () => setState(
              () => _layout = _layout.add(
                const DockPanel(id: 'modelling', kind: PanelKind.modelling),
              ),
            ),
          ),
    onOpenInterface: (path) =>
        _openInterface(p.join(widget.project.directory, path)),
    onDetachData: _detachData,
    onApplyPrefab: _applyPrefab,
    onRevertPrefab: _revertPrefab,
    onUnpackPrefab: _unpackPrefab,
  );

  Widget _viewport(DockPanel panel, SceneObject? selected) => SceneViewport(
    workspace: _workspace,
    camera: _cameraFor(panel.id),
    onCameraChanged: (camera) =>
        setState(() => _cameras[panel.id] = camera),
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
    onToggleInterface: () =>
        setState(() => _showInterface = !_showInterface),
    outlineSelection: _outlineSelection,
    onToggleOutline: () =>
        setState(() => _outlineSelection = !_outlineSelection),
    previewOf: (camera) => GameView(
      workspace: _workspace,
      projectRoot: widget.project.directory,
      geometryOf: _geometry.pathFor,
      through: camera,
      plain: true,
    ),
    onSceneNotes: _reportSceneNotes,
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

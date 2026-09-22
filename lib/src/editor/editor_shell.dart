import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart' hide Clipboard;
import 'package:flutter/services.dart' as services;
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' show Matrix4, Vector3;

import '../launcher/project.dart';
import '../platform/command_shortcuts.dart';
import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'asset_browser.dart';
import 'assets.dart';
import 'body_gizmo.dart';
import 'body_section.dart';
import 'clipboard.dart';
import 'code_editor.dart';
import 'boundary.dart';
import 'commands.dart';
import 'console.dart';
import 'console_panel.dart';
import 'cook_status.dart';
import 'data_panel.dart';
import 'data_store.dart';
import 'dock.dart';
import 'grid.dart';
import 'drawing.dart';
import 'frame_rate.dart';
import 'dock_view.dart';
import 'editor_mode.dart';
import 'game_view.dart';
import 'geometry_store.dart';
import 'gizmo_registry.dart';
import 'script_build.dart';
import 'ui_editor.dart';
import 'history.dart';
import 'inspector.dart';
import 'mesh_edit.dart';
import 'mesh_panel.dart';
import 'model_bounds.dart';
import 'modelling_panel.dart';
import 'mesh_tools.dart';
import 'outliner.dart';
import 'panel_registry.dart';
import 'prefab.dart';
import 'registry.dart';
import 'scene.dart';
import 'snapping.dart';
import 'surface.dart';
import 'uv_panel.dart';
import 'scene_document.dart';

import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_ui/orblit_ui.dart';

import 'viewport.dart';
import 'workspace.dart';

part 'editor_shell_modelling.dart';
part 'editor_shell_drawing.dart';
part 'editor_shell_geometry.dart';
part 'editor_shell_scenes.dart';
part 'editor_shell_objects.dart';
part 'editor_shell_panels.dart';
part 'editor_shell_documents.dart';
part 'editor_shell_prefabs.dart';
part 'editor_shell_intents.dart';
part 'editor_shell_chrome.dart';
part 'editor_shell_menus.dart';

/// The editor, once a project is open.
///
/// Regions rather than free-floating windows: a fixed rail, an outliner, the
/// viewport, an inspector and a status bar. Docking comes later, and it comes
/// more easily to a layout that already knows what its regions are.
class EditorShell extends StatefulWidget {
  const EditorShell({
    super.key,
    required this.project,
    required this.onClose,
    this.extend,
  });

  final Project project;

  /// Back to the launcher.
  final VoidCallback onClose;

  /// Adds to what the editor shows: panels, modes, inspector sections and
  /// gizmos. Called once, after the editor has registered its own.
  final void Function(EditorRegistry registry)? extend;

  @override
  State<EditorShell> createState() => _EditorShellState();
}

/// What the editor shows that something outside it can add to, one registry
/// for each sort.
///
/// Handed to [EditorShell.extend] once the editor has registered its own, so
/// what is added lands after the built-in ones, or ahead of a named one with
/// `before:`. The editor keeps no other list: every panel, mode and section
/// on screen reached it through here.
class EditorRegistry {
  /// Panels, in the order the View menu lists them.
  final PanelRegistry panels = PanelRegistry();

  /// Ways of working. The editor opens in the first, and the switcher shows
  /// once there is a second.
  final Registry<EditorMode> modes = Registry();

  /// The inspector's sections, in the order they stack.
  final Registry<InspectorSection> sections = Registry();

  /// What a scene view draws and asks after its own handles. The view's own
  /// are registered in the view, which is where their state is.
  final Registry<GizmoType> gizmos = Registry();
}

class _EditorShellState extends State<EditorShell> {
  late final Workspace _workspace = Workspace(widget.project.directory);

  late final History _history = History(_workspace);

  late final AssetTree _assets = AssetTree(widget.project.directory);

  /// Where each asset stands with the cook, for the marks in the browser.
  ///
  /// One per window rather than one per browser panel: two panels open on the
  /// same project are looking at the same cache, and hashing the project
  /// twice to tell them the same thing would be work done to say nothing new.
  late final CookStatusIndex _cookStatus = CookStatusIndex(
    projectDirectory: widget.project.directory,
  );

  late final DataStore _data = DataStore(widget.project.directory);

  /// Geometry built here, written out for the renderer to load.
  late final GeometryStore _geometry = GeometryStore(widget.project.directory);

  // ---- editing geometry ----

  /// Whether the whole object is selected, or its parts.
  EditContext _context = EditContext.object;

  ElementMode _elementMode = ElementMode.face;

  ElementSelection _elements = ElementSelection();

  /// How much the next action does, per action.
  ///
  /// Kept between presses: somebody extruding a corridor extrudes it in equal
  /// steps, and a distance that reset to a half every time would be a number
  /// they retyped every time.
  final Map<String, double> _amounts = {};

  /// What a drag lands on.
  ///
  /// One for the whole editor rather than one a viewport, so four views of a
  /// scene agree about the grid — and a view setting, not a document one: it
  /// is not saved and it is not undone.
  final Snapping _snapping = Snapping();

  /// What a drag in the coordinate view does.
  UvGesture _uvGesture = UvGesture.move;

  /// How fast the editor is actually drawing.
  ///
  /// Listened to rather than read on every build, and it only speaks a couple
  /// of times a second — a status bar rebuilt sixty times a second to say how
  /// fast things are would be its own answer to the question.
  final FrameRate _frames = FrameRate();

  /// The grid, made once and then only placed.
  late final GridStore _grid = GridStore(widget.project.directory);

  /// How big each imported model says it is, read once a file.
  late final ModelBounds _models = ModelBounds(widget.project.directory);

  /// The outline or cut being drawn, if one is.
  ///
  /// One for the editor rather than one a viewport, so the same drawing shows
  /// in all four views and can be finished in a different one from the one it
  /// was started in.
  final Drawing _drawing = Drawing();

  /// Which format the export button writes. A view setting: not saved, not
  /// undone, and remembered only for as long as the editor is open.
  MeshFormat _format = MeshFormat.obj;

  /// Whether picking reaches what is behind the surface.
  ///
  /// A view setting, not a document one: it is not saved and it is not
  /// undone, and two people editing the same shape can disagree about it.
  bool _seeThrough = false;

  /// What ties one gesture's worth of commands together.
  Object? _gesture;

  late final ScriptBuilder _builder = ScriptBuilder(widget.project.directory);

  /// Interfaces read off disk, by path.
  ///
  /// Cached because the scene is rebuilt every frame and a canvas object asks
  /// for its document each time. Cleared when the project folder changes, so
  /// editing an interface shows up in the scene without reopening it.
  final Map<String, UiDocument?> _interfaces = {};

  /// Whether the interface is drawn over the viewport.
  bool _showInterface = true;

  /// Whether the renderer outlines the selection, or the boundaries are drawn
  /// over the picture instead. Held here so every viewport agrees.
  bool _outlineSelection = true;

  /// Everything the editor has said. Kept, rather than shown for four seconds
  /// in a corner and lost.
  final EditorLog _log = EditorLog();

  /// Puts Flutter's own errors in the console. Undone on dispose.
  late final VoidCallback _stopCatching = _log.catchFlutterErrors();

  /// The data object being looked at, or null when the inspector is showing
  /// the scene's selection.
  String? _dataAsset;

  /// The selected objects. Empty when the scene itself is selected.
  final Set<String> _selected = {};

  /// The one the inspector shows, and what a shift-click ranges from.
  String? _primary;

  bool _playing = false;

  /// What the renderer has already been heard on, so a note is said once
  /// rather than on every frame of a drag. A subject that stops being
  /// reported leaves this set, so fixing a scene and breaking it again is
  /// heard both times.
  final Set<String> _reportedNotes = {};

  int _nextSceneId = 0;

  /// Survives a scene being unloaded, which is what makes moving something
  /// from one scene to another possible at all.
  final SceneClipboard _clipboard = SceneClipboard();

  @override
  void initState() {
    super.initState();
    // Read once so the handlers are installed, since a late final is not
    // initialised until something asks for it.
    _stopCatching;
    _history.addListener(_onHistoryChanged);
    _workspace.addListener(_onChanged);
    _frames
      ..start()
      ..addListener(_onChanged);

    // The grid's quad and lines, written once. Nothing waits for it: until it
    // is there `planFor` says there is no grid, and a frame or two without
    // one at startup is not worth blocking on.
    _grid.prepare().then((_) {
      if (mounted) setState(() {});
    });

    final opened = _read(_defaultScenePath(), quiet: true);
    _workspace.add(
      SceneEntry(
        id: 'scene${_nextSceneId++}',
        name: opened.scene.name,
        scene: opened.scene,
        path: opened.path,
        neverWritten: opened.isNew,
      ),
    );
    // Every other scene in the project is listed but not loaded, so they can
    // be reached without going hunting for them.
    _listSiblingScenes();
    _readShared();

    if (opened.problems.isNotEmpty) {
      WidgetsBinding.instance.addPostFrameCallback(
        (_) => _report(opened.problems),
      );
    }
  }

  /// Reads what every scene in this project has in it.
  ///
  /// A project without one is not a problem — it means nobody has put
  /// anything there yet, and the empty set behaves exactly like an empty
  /// scene.
  void _readShared() {
    final file = File(p.join(widget.project.directory, sharedFileName));
    if (!file.existsSync()) return;

    try {
      final loaded = SceneDocument.decode(file.readAsStringSync());
      _workspace.sharedEntry
        ..scene = loaded.scene
        ..neverWritten = false;
      if (loaded.problems.isNotEmpty) {
        WidgetsBinding.instance.addPostFrameCallback(
          (_) => _report(loaded.problems),
        );
      }
    } on SceneFormatException catch (error) {
      WidgetsBinding.instance.addPostFrameCallback(
        (_) => _say('The shared objects could not be read: ${error.message}'),
      );
    } on FileSystemException catch (error) {
      WidgetsBinding.instance.addPostFrameCallback(
        (_) => _say('The shared objects could not be read: ${error.message}'),
      );
    }
  }

  @override
  void dispose() {
    _history
      ..removeListener(_onHistoryChanged)
      ..dispose();
    _workspace
      ..removeListener(_onChanged)
      ..dispose();
    _assets.dispose();
    _cookStatus.dispose();
    _frames
      ..removeListener(_onChanged)
      ..dispose();
    // Flutter's error handlers are global: leaving ours installed would send
    // the next editor window's errors, and a test's, into a log that is gone.
    _stopCatching();
    _log.dispose();
    super.dispose();
  }

  void _onChanged() {
    // An undo can bring a shape back or change what it is, and the file the
    // renderer loads has to follow it.
    _refreshGeometry();
    setState(() {});
  }

  /// A change from the undo stack.
  ///
  /// Split from the one above so that a drag — which runs a command a frame
  /// and only ever moves things — can rebuild the parts that show where
  /// things are and leave the rest of the editor alone.
  void _onHistoryChanged() {
    _refreshGeometry();
    if (_history.lastOnlyMoved) {
      _rebuildForMove();
      return;
    }
    setState(() {});
  }

  /// Whether everything has to be built again, or only what shows movement.
  ///
  /// Set by `setState` itself rather than by each caller, so the safe answer
  /// is the automatic one: a path that forgets to say anything gets a full
  /// rebuild, which costs a frame. The other way round costs a panel showing
  /// something that is no longer true.
  bool _deep = true;

  bool _shallow = false;

  /// What the frame being built decided. [_deep] is cleared as the build
  /// starts, and the panels are built after that.
  bool _deeply = true;

  @override
  void setState(VoidCallback fn) {
    if (!_shallow) _deep = true;
    super.setState(fn);
  }

  void _rebuildForMove() {
    _shallow = true;
    setState(() {});
    _shallow = false;
  }

  /// The panels as they were last built, so a panel a move cannot affect is
  /// handed back unchanged — and Flutter, seeing the same widget, leaves its
  /// whole subtree alone: no rebuild, no layout, no paint.
  final Map<String, Widget> _panels = {};

  /// Replaces, adds to, or extends the selection.
  void _select(String id, {bool additive = false, bool range = false}) {
    final scene = _current?.scene;
    if (scene == null) return;

    setState(() {
      _selectedScene = null;
      // Back to the scene: the inspector shows one thing, and it is whatever
      // was touched last.
      _dataAsset = null;

      if (range && _primary != null) {
        // Everything between the anchor and here, in the order the tree is
        // drawn — which is what somebody shift-clicking means, rather than the
        // order objects happen to sit in the document.
        final order = _visibleOrder(scene);
        final from = order.indexOf(_primary!);
        final to = order.indexOf(id);
        if (from >= 0 && to >= 0) {
          final low = from < to ? from : to;
          final high = from < to ? to : from;
          _selected.addAll(order.sublist(low, high + 1));
          _primary = id;
          return;
        }
      }

      if (additive) {
        if (!_selected.remove(id)) {
          _selected.add(id);
          _primary = id;
        } else if (_primary == id) {
          _primary = _selected.isEmpty ? null : _selected.last;
        }
        return;
      }

      _selected
        ..clear()
        ..add(id);
      _primary = id;
    });
  }

  /// Object ids in the order the tree draws them.
  List<String> _visibleOrder(EditorScene scene) {
    final order = <String>[];
    void walk(List<SceneObject> objects) {
      for (final object in objects) {
        order.add(object.id);
        walk(scene.childrenOf(object.id));
      }
    }

    walk(scene.roots);
    return order;
  }

  void _clearSelection() => setState(() {
    _selected.clear();
    _primary = null;
  });

  /// The scene an edit goes into. Only one is loaded, so there is only one.
  SceneEntry? get _current => _workspace.loaded;

  /// The scene whose settings the inspector shows: the loaded one, or one
  /// somebody has clicked to look at without opening.
  SceneEntry? get _inspected =>
      _selectedScene == null ? _workspace.loaded : _workspace[_selectedScene!];

  /// What is on the clipboard, kept so a menu can name it without reading the
  /// system clipboard, which cannot be done without waiting.
  String get _clipboardLabel => _clipboard.description;

  String? _selectedScene;

  void _run(EditorCommand command) {
    try {
      _history.run(command);
    } on SceneError catch (error) {
      _say(error.message);
    }
  }

  int _nextObject = 0;

  /// Says something, in both places it belongs.
  ///
  /// The status bar for somebody who is looking, the console for somebody who
  /// was not — which, while they were reading the last message, they were not.
  void _say(
    String message, {
    LogLevel level = LogLevel.info,
    String detail = '',
  }) {
    _log.say(message, level: level, detail: detail);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(message),
        backgroundColor: OrblitColors.raised,
        behavior: SnackBarBehavior.floating,
        width: 460,
        duration: const Duration(seconds: 4),
      ),
    );
  }

  /// Everything the editor can show: its own, and whatever
  /// [EditorShell.extend] added.
  late final EditorRegistry _registry = _register();

  /// The way of working the editor is in.
  late EditorMode _mode = _registry.modes.all.first;

  /// How the panels are arranged. Data, so it survives being closed.
  late DockLayout _layout = _readLayout() ?? _mode.layout();

  /// A camera per scene view.
  ///
  /// Four views onto one world is four places to be standing. Without one
  /// each, the second view would jump to wherever the first was looking the
  /// moment anybody moved it.
  final Map<String, OrbitCamera> _cameras = {};

  /// The view last used, which is the one F frames in.
  String _using = 'scene';

  /// Undo, then show what it changed, so a step in another scene is not
  /// invisible.
  void _undo() {
    final sceneId = _history.undoSceneId;
    _history.undo();
    _reveal(sceneId);
  }

  void _redo() {
    final sceneId = _history.redoSceneId;
    _history.redo();
    _reveal(sceneId);
  }

  void _reveal(String? sceneId) {
    if (sceneId == null) return;
    final scene = _workspace[sceneId]?.scene;
    if (scene == null) return;
    setState(() {
      _selected.removeWhere((id) => !scene.contains(id));
      if (_primary != null && !scene.contains(_primary!)) {
        _primary = _selected.lastOrNull;
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    // Read and cleared here, so the next change decides afresh how much has
    // to be built.
    _deeply = _deep;
    _deep = false;

    return Shortcuts(
      shortcuts: _shortcuts,
      child: Actions(actions: _actions, child: _shell()),
    );
  }

  // The window itself: a bar, the panels the layout asks for, a bar.
  Widget _shell() {
    final open = _current;

    return Focus(
      autofocus: true,
      child: Scaffold(
        backgroundColor: OrblitColors.ground,
        body: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _TopBar(
              project: widget.project,
              playing: _playing,
              history: _history,
              dirty: _anyUnsaved,
              onPlay: () => setState(() => _playing = !_playing),
              onClose: widget.onClose,
              onAdd: _add,
              onAddShape: _addShape,
              onSave: _save,
              onSaveAs: _saveAs,
              onNewScene: () => _newScene(),
              layout: _layout,
              onLayout: _relayout,
              panels: _registry.panels.all,
              onOpenInCode: _openInCode,
              onReveal: () {
                final problem = CodeEditor.reveal(widget.project.directory);
                if (problem != null) _say(problem);
              },
              onUndo: _undo,
              onRedo: _redo,
              selectionCount: _selected.length,
              clipboard: _clipboardLabel,
              onCopy: _copy,
              onCut: _cut,
              onPaste: _paste,
              onDuplicate: _duplicate,
            ),
            if (_registry.modes.all.length > 1 || _mode.tools != null)
              _ModeBar(
                modes: _registry.modes.all,
                mode: _mode,
                onMode: _enterMode,
              ),
            // The panels, arranged as the layout says. What is where is
            // data — saved with the project, put back exactly, and
            // changed by dragging a tab rather than by editing this.
            Expanded(
              child: DockView(
                layout: _layout,
                panel: _buildPanel,
                onChanged: _relayout,
              ),
            ),
            _StatusBar(
              objects: open?.scene?.length ?? 0,
              message: _history.undoLabel == null
                  ? 'Ready'
                  : 'Last change: ${_history.undoLabel}',
              file: open == null
                  ? 'No scene loaded'
                  : (open.path == null
                        ? '${open.title} (unsaved)'
                        : _assets.relative(open.path!)),
              dirty: open != null && _isUnsaved(open),
              rate: _frames.fps,
              frameMs: _frames.fps == null
                  ? null
                  : (_frames.gpuBound ? _frames.rasterMs : _frames.buildMs),
              gpuBound: _frames.gpuBound,
            ),
          ],
        ),
      ),
    );
  }
}

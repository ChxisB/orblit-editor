import 'dart:math' as math;

import 'package:flutter/gestures.dart';
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_mesh/orblit_mesh.dart';
import 'package:orblit_ui/orblit_ui.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../platform/command_shortcuts.dart';
import '../platform/renderer_support.dart';
import '../theme/orblit_theme.dart';
import 'commands.dart';
import 'drawing.dart';
import 'gizmo.dart';
import 'grid.dart';
import 'model_bounds.dart';
import 'snapping.dart';
import 'history.dart';
import 'mesh_edit.dart';
import 'scene.dart';
import 'selection_outline.dart';
import 'ui_canvas.dart';
import 'workspace.dart';

part 'viewport_camera.dart';
part 'viewport_painters.dart';
part 'viewport_chrome.dart';
part 'viewport_selection.dart';
part 'viewport_dragging.dart';
part 'viewport_flying.dart';
part 'viewport_picking.dart';
part 'viewport_surface.dart';

/// What is being edited: an object, its geometry, and where it stands.
typedef EditingMesh = ({SceneObject object, Mesh mesh, Matrix4 transform});

/// The 3D view.
///
/// On macOS this is Filament compositing into a Flutter texture. Everywhere
/// else it is an honest placeholder — a viewport that pretends to work on a
/// platform where the renderer does not exist is worse than one that says so.
// Named SceneViewport rather than Viewport: Flutter already exports a
// Viewport, and a name that collides with the framework's is one somebody has
// to disambiguate at every call site.
class SceneViewport extends StatefulWidget {
  const SceneViewport({
    super.key,
    required this.workspace,
    required this.camera,
    required this.onCameraChanged,
    this.editing,
    this.elementMode = ElementMode.face,
    this.elementSelection = nothingSelected,
    this.onPickElement,
    this.onDragElements,
    this.onSelectElements,
    this.seeThroughElements = false,
    required this.snapping,
    this.onSnapping,
    this.grid,
    this.models,
    this.drawing,
    this.onDrawPoint,
    this.onDrawFinish,
    this.geometryOf,
    this.interface,
    this.showInterface = true,
    this.onToggleInterface,
    this.outlineSelection = true,
    this.onToggleOutline,
    this.previewOf,
    this.selected = const {},
    this.onDropAsset,
    this.projectRoot,
    this.onSceneNotes,
    this.onClock,
    this.primary,
    this.history,
    this.onPick,
  });

  /// Only the loaded scene is drawn. The others are names and paths until
  /// somebody opens them.
  final Workspace workspace;

  /// Owned by the shell rather than here, so pressing F anywhere can frame the
  /// selection and so the view could later be saved with the scene.
  final OrbitCamera camera;

  final ValueChanged<OrbitCamera> onCameraChanged;

  /// The objects to outline. All of them, so a multiple selection is visible
  /// in the viewport rather than only in the tree.
  final Set<String> selected;

  /// Called when a file is dragged in from the project browser.
  final ValueChanged<String>? onDropAsset;

  /// Where mesh references are resolved from.
  final String? projectRoot;

  /// Called with anything the scene asked for that the renderer could not
  /// give: a mesh that would not load, a light it has no room to shade.
  final ValueChanged<Map<String, String>>? onSceneNotes;

  /// The object whose parts are being edited, with its geometry and where it
  /// stands, or null when the whole object is what is selected.
  ///
  /// Passed in already resolved: the viewport draws what it is given and does
  /// not decide what is being edited, which is what lets four of them show the
  /// same edit from four angles.
  final EditingMesh? editing;

  final ElementMode elementMode;
  final ElementSelection elementSelection;

  /// Called when a click lands on a vertex, an edge or a face — or on none of
  /// them, which is how somebody clears a selection.
  final void Function(Object? what, {required bool add})? onPickElement;

  /// Called with everything a marquee drew round.
  ///
  /// A list rather than one at a time, so a box over forty vertices is one
  /// step and not forty — and so an empty box clears the selection, which is
  /// what somebody drawing on nothing means by it.
  final void Function(List<Object> what, {required bool add})? onSelectElements;

  /// Whether what is behind the surface can be picked as well.
  final bool seeThroughElements;

  /// What a drag lands on. Passed in rather than owned here, so four views of
  /// one scene agree about the grid.
  final Snapping snapping;

  /// The grid, when there is one to draw.
  final GridStore? grid;

  /// How big an imported model says it is. Passed in rather than read here,
  /// so four views share one answer per file instead of reading it four
  /// times.
  final ModelBounds? models;

  /// The outline or cut being drawn, if one is.
  ///
  /// Owned by the shell, so the same drawing appears in all four views and
  /// can be finished in a different one from the one it was started in.
  final Drawing? drawing;

  /// Called with a point a click landed on, and the plane it landed on.
  ///
  /// The plane travels with the point because the first click is what decides
  /// it: whichever surface was under the pointer then is the one the rest of
  /// the outline is drawn on, whatever is under the pointer later.
  final void Function(Vector3 at, Vector3 origin, Vector3 normal, int? face)?
  onDrawPoint;

  /// Called when a click lands back on the first point, which is how somebody
  /// says they have finished.
  final VoidCallback? onDrawFinish;

  /// Called when the chip or a key changes it.
  final ValueChanged<Snapping>? onSnapping;

  /// Called with a mesh a drag has changed, and what should be selected after.
  ///
  /// The whole mesh rather than the change, for the same reason every other
  /// geometry edit is: describing a drag as a diff is more code than the drag.
  /// [merge] is set on every frame after the first, so a gesture that produces
  /// a hundred of these is one step to undo.
  final void Function(
    Mesh mesh,
    ElementSelection selection,
    String what, {
    required bool merge,
  })?
  onDragElements;

  /// Where an object's built geometry was written, if anywhere.
  ///
  /// Passed in rather than worked out here: the file is written by whatever
  /// owns the project folder, and a viewport that wrote files would be four
  /// viewports writing the same one four times over.
  final String? Function(SceneObject)? geometryOf;

  /// The interface a canvas object in the scene shows, already read.
  ///
  /// Read by the shell rather than here: the viewport draws what it is given
  /// and does not open files, which is what keeps it testable without a
  /// project on disk.
  final UiDocument? interface;

  /// Whether to draw the interface at all. Off while somebody is arranging
  /// the scene behind it and does not want a full-screen heads-up display
  /// over everything they are trying to look at.
  final bool showInterface;

  /// Turns that on and off. This is a view setting and not a scene edit —
  /// hiding the canvas object is what hides the interface in the game.
  final VoidCallback? onToggleInterface;

  /// Whether the selection is outlined by the renderer.
  ///
  /// On, the renderer draws a line round each selected object's silhouette —
  /// hidden parts fainter and dashed — and nothing is painted over the
  /// picture. Off, the object's boundary is drawn over it instead, as it was
  /// before the renderer could outline: the shape a click or a collision
  /// meets, which is sometimes the thing being checked rather than where the
  /// object is.
  final bool outlineSelection;

  /// Turns that on and off. A view setting, held by the shell so four views
  /// agree.
  final VoidCallback? onToggleOutline;

  /// What a selected camera sees, shown in the corner.
  ///
  /// Built by the shell rather than here, so the viewport does not have to
  /// know how a game view is put together — and so the preview and the game
  /// panel are the same widget rather than two things that agree for now.
  final Widget Function(SceneObject camera)? previewOf;

  /// The one of the selection the handles sit on, and whose transform a drag
  /// writes first. The others follow it.
  final String? primary;

  /// Where a drag's changes go. Without it the viewport can still show and
  /// select, but nothing in it can be moved.
  final History? history;

  /// Called when something in the scene is clicked, or nothing is.
  ///
  /// `add` is set when a modifier was held, which the shell reads as adding to
  /// or taking away from what is already selected rather than replacing it.
  final void Function(String? id, {required bool add})? onPick;

  /// Called a few times a second while a scene is animating, so the panels
  /// that are not the viewport can keep up.
  ///
  /// A few, not sixty: the tree and the inspector show the hour and the name
  /// of whatever is in the sky, and those change slowly enough to read. The
  /// viewport draws every frame; the rest of the editor does not have to.
  final VoidCallback? onClock;

  @override
  State<SceneViewport> createState() => _SceneViewportState();
}

class _SceneViewportState extends State<SceneViewport>
    with SingleTickerProviderStateMixin {
  Offset? _dragAnchor;

  /// What a drag on a handle does. Kept here rather than in the shell because
  /// it is a property of how somebody is working in this view, not of the
  /// document — it is not saved and it is not undone.
  GizmoMode _mode = GizmoMode.move;

  /// The size of the surface, as laid out. Needed to turn a pointer position
  /// into a ray, and only known once the viewport has been given a box.
  Size? _surface;

  GizmoAxis? _hovered;

  GizmoAxis? _dragging;

  /// Where on the handle the drag began, in world space, and what every
  /// object being dragged looked like before it started.
  Vector3? _grabbed;

  /// How far the part being put on the line is from the handle, along the
  /// axis being dragged. Worked out once, at the start, for the same reason
  /// the pivot is.
  double _grabbedAnchor = 0;

  /// Where the handle stood when the drag began.
  ///
  /// Not read from the gizmo each frame, which is the whole point. The gizmo
  /// sits on the thing being dragged, so a grid worked out from it is a grid
  /// that moves with what it is snapping — and a thing that did not start on
  /// a line then flickers between two of them for as long as the drag lasts,
  /// with the pointer perfectly still. The line to land on is decided by
  /// where the drag started, once.
  Vector3? _grabbedPivot;

  final Map<String, Vector3> _before = {};

  final Map<String, Matrix3> _beforeWorld = {};

  /// The mesh as it stood when an element drag began, and which of its corners
  /// are moving.
  ///
  /// Kept whole and moved from, rather than moved a little each frame: a drag
  /// applied incrementally accumulates every rounding error it makes, and a
  /// slow drag out and back does not come home.
  Mesh? _beforeMesh;

  List<int> _movingPoints = const [];

  /// What the selection should be while an element drag is running. Set when a
  /// drag extrudes, because the faces that come out of an extrude are not the
  /// ones that went in.
  ElementSelection? _draggingSelection;

  /// Whether anything has actually been written yet this drag, so the first
  /// change starts an undo step and the rest fold into it.
  bool _dragStarted = false;

  /// A number with its sign always shown, because a drag has a direction and
  /// up two squares is not the same answer as down two.
  static String _signed(double value, int decimals) {
    final text = value.toStringAsFixed(decimals);
    return text.startsWith('-') ? text : '+$text';
  }

  /// A grid step as somebody would say it: millimetres below a centimetre,
  /// centimetres below a metre.
  static String _gridLabel(double step) {
    if (step < 0.01) return '${(step * 1000).round()}mm';
    if (step < 1) return '${(step * 100).round()}cm';
    return '${step.toStringAsFixed(step % 1 == 0 ? 0 : 1)}m';
  }

  /// Drives anything in the scene that moves on its own — a day running, mist
  /// drifting.
  ///
  /// It lives here rather than in the shell so that a running clock repaints
  /// the viewport and nothing else. A ticker at the top would rebuild the
  /// outliner, the inspector and the browser sixty times a second to animate
  /// a sky none of them draw.
  late final Ticker _clock = createTicker(_tick);

  /// Where the clock had got to when it was last stopped, so pausing and
  /// starting again does not jump the sky back to the beginning.
  Duration _elapsed = Duration.zero;

  @override
  void initState() {
    super.initState();
    _syncClock();
  }

  @override
  void didUpdateWidget(SceneViewport oldWidget) {
    super.didUpdateWidget(oldWidget);
    // A scene loaded, unloaded, or had its cycle switched on or off.
    _syncClock();
  }

  @override
  void dispose() {
    _clock.dispose();
    _flyFocus.dispose();
    super.dispose();
  }

  /// Runs the clock only while something is actually moving.
  ///
  /// A ticker that never stops is a viewport that republishes sixty times a
  /// second to draw a scene that has not changed, and on a laptop that is a
  /// fan spinning up to animate nothing.
  void _syncClock() {
    final scene = widget.workspace.loaded?.scene;
    // Flying needs it as much as a day cycle does: the camera moves a little
    // every frame while a key is held, and without a frame there is no every.
    final wanted = _flying || (scene != null && scene.isAnimated);
    if (wanted == _clock.isActive) return;
    if (wanted) {
      _clock.start();
    } else {
      _clock.stop();
    }
  }

  // ---- flying ----

  /// The keys held down while the right button is, in view terms.
  final Set<LogicalKeyboardKey> _held = {};

  /// Where the pointer was when it last moved, while a button is held down to
  /// look around.
  Offset? _looking;

  /// Whether flying was switched on rather than held.
  ///
  /// Holding a button is fine with a mouse and awkward on a trackpad: a
  /// two-finger click held down while the other hand types WASD is a hand
  /// position nobody keeps for long. Switched on, the keys just work and the
  /// view is steered with an ordinary two-finger drag.
  bool _flyLocked = false;

  /// Metres a second. Adjusted by the wheel while flying, the way it is in
  /// every editor that has this — somebody flying across a level and somebody
  /// nudging along a wall want very different numbers, and reaching for a
  /// slider to change it means stopping.
  double _flySpeed = 8;

  final FocusNode _flyFocus = FocusNode(debugLabel: 'viewport fly');

  /// The pinch scale at the last trackpad event, so a zoom is the change
  /// rather than the total — the total restarts at one on every gesture.
  double _panZoomFrom = 1;

  /// Whether fingers are on the trackpad.
  ///
  /// Flutter hands a trackpad gesture to the pan-zoom listeners *and* turns it
  /// into an ordinary drag for the gesture recognizers, so without this both
  /// run and the second undoes the first — the view would move once and then
  /// jump back every frame of the gesture.
  bool _onTrackpad = false;

  Duration _lastFlew = Duration.zero;

  void _tick(Duration elapsed) {
    _fly(elapsed);
    _lastFlew = elapsed;

    final scene = widget.workspace.loaded?.scene;
    if (scene == null) return;
    _elapsed = elapsed;
    setState(() => scene.clock = _elapsed.inMicroseconds / 1e6);

    if (elapsed - _lastTold >= _tellInterval) {
      _lastTold = elapsed;
      widget.onClock?.call();
    }
  }

  Duration _lastTold = Duration.zero;

  static const Duration _tellInterval = Duration(milliseconds: 250);

  /// The part of the mesh the pointer is over, while editing one.
  Object? _hoveredElement;

  /// Where a marquee started and where it has reached, in local pixels.
  Offset? _boxFrom;

  Offset? _boxTo;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.all(Space.sm),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: const Color(0xFF14181F),
        borderRadius: BorderRadius.circular(Radii.panel),
        border: Border.all(color: OrblitColors.lineSoft),
      ),
      child: DragTarget<String>(
        onWillAcceptWithDetails: (_) => widget.onDropAsset != null,
        onAcceptWithDetails: (details) =>
            widget.onDropAsset?.call(details.data),
        builder: (context, candidate, _) => LayoutBuilder(
          builder: (context, constraints) {
            // The size the handles are projected into. Recorded rather than
            // asked for at gesture time, because a pointer arrives before the
            // next layout does.
            _surface = constraints.biggest;

            return Stack(
              children: [
                // The input wraps the placeholder as well as the renderer. A
                // camera is Dart and works whether or not Filament does, and a
                // viewport that cannot be navigated on the platforms the renderer
                // has not reached yet is worse than one that says so and still
                // moves.
                Positioned.fill(child: _buildSurface()),
                if (candidate.isNotEmpty) _dropHint(),
                // The selected objects' boundaries, projected over the texture —
                // only while the renderer's outline is switched off. The outline
                // follows the silhouette and knows what hides what; this shows the
                // shape a click or a collision meets, which is worth having back
                // when that is the thing being checked.
                if (!widget.outlineSelection) _bounds(),
                // Over the scene and under the handles: an interface is drawn on
                // top of the world in the game, and a gizmo you cannot reach
                // because a heads-up display is over it is a gizmo that does not
                // work. Ignoring the pointer for the same reason — this is a
                // preview of the interface, not the interface.
                if (widget.interface != null && widget.showInterface)
                  _interface(),
                // The parts of whatever is being edited, over the scene and under
                // the handles. A wireframe over everything all the time is a scene
                // nobody can read; this is only up while somebody is in it.
                if (widget.editing case final editing?) _elements(editing),
                // Over the outline, because a handle you cannot see is a handle
                // you cannot grab — and under nothing, because it has to be the
                // thing the pointer finds first.
                _handles(),
                if (widget.drawing?.tool.isDrawing ?? false) _outline(),
                if (_box != null) _marquee(),
                _chips(),
                // What the selected camera sees, in the corner. Unity puts this
                // bottom-right; it is bottom-left here because that is the corner
                // this editor leaves empty, and a preview under the transform
                // tools would cover the thing somebody is about to press.
                if (_preview != null) _cameraPreview(),
                if (_rendererAvailable) _tools(),
                _help(),
              ],
            );
          },
        ),
      ),
    );
  }

  Widget _dropHint() => Positioned.fill(
    child: IgnorePointer(
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: OrblitColors.emberWash,
          border: Border.all(
            color: OrblitColors.ember,
            width: 2,
          ),
          borderRadius: BorderRadius.circular(Radii.panel),
        ),
      ),
    ),
  );

  Widget _bounds() => Positioned.fill(
    child: IgnorePointer(
      child: CustomPaint(
        painter: _SelectionPainter(
          models: widget.models,
          workspace: widget.workspace,
          selected: widget.selected,
          camera: widget.camera,
        ),
      ),
    ),
  );

  Widget _interface() => Positioned.fill(
    child: IgnorePointer(
      child: UiCanvasView(
        document: widget.interface!,
        designing: false,
      ),
    ),
  );

  Widget _elements(EditingMesh editing) => Positioned.fill(
    child: IgnorePointer(
      child: CustomPaint(
        painter: ElementPainter(
          mesh: editing.mesh,
          transform: editing.transform,
          camera: widget.camera,
          mode: widget.elementMode,
          selection: _selectedElements,
          hovered: _hoveredElement,
        ),
      ),
    ),
  );

  Widget _handles() => Positioned.fill(
    child: IgnorePointer(
      child: CustomPaint(
        painter: GizmoPainter(
          gizmo: _gizmo,
          hovered: _hovered,
          dragging: _dragging,
        ),
      ),
    ),
  );

  Widget _outline() => Positioned.fill(
    child: IgnorePointer(
      child: CustomPaint(
        painter: DrawingPainter(
          drawing: widget.drawing!,
          camera: widget.camera,
        ),
      ),
    ),
  );

  Widget _marquee() => Positioned.fromRect(
    rect: _box!,
    child: IgnorePointer(
      child: DecoratedBox(
        decoration: BoxDecoration(
          color: const Color(0x22E5893F),
          border: Border.all(color: const Color(0xCCE5893F)),
        ),
      ),
    ),
  );

  Widget _cameraPreview() => Positioned(
    left: Space.md,
    bottom: 52,
    child: _CameraPreview(child: _preview!),
  );

  Widget _tools() => Positioned(
    left: Space.md,
    bottom: Space.md,
    child: Row(
      children: [
        for (final mode in GizmoMode.values) ...[
          _ToolButton(
            mode: mode,
            selected: _mode == mode,
            onPressed: () => setState(() => _mode = mode),
          ),
          const SizedBox(width: Space.xs),
        ],
      ],
    ),
  );

  Widget _help() => Positioned(
    right: Space.md,
    bottom: Space.md,
    child: const _ViewportChip(
      'Drag to orbit · two fingers to orbit, shift to pan, pinch '
      'to zoom · ` to fly, then WASD',
    ),
  );

  // The tools themselves live in the modelling panel. What is here is
  // only what a tool is *doing*, and only while it is doing it — a
  // viewport is a place to look at a scene, not a row of buttons that
  // are somewhere else as well.
  Widget _chips() {
    return Positioned(
      left: Space.md,
      top: Space.md,
      child: Row(
        children: [
          const _ViewportChip('Perspective'),
          const SizedBox(width: Space.xs),
          const _ViewportChip('Shaded'),
          const SizedBox(width: Space.xs),
          _ViewportChip(_summary),
          const SizedBox(width: Space.xs),
          // What the drag has done so far, while it is doing it.
          if (_dragged case final moved?)
            Padding(
              padding: const EdgeInsets.only(right: Space.xs),
              child: _ViewportChip(
                widget.snapping.on
                    ? '${_signed(moved.squares, 0)} '
                          '${moved.squares.abs() == 1 ? "square" : "squares"}'
                          ' · ${_signed(moved.metres, 2)} m'
                    : '${_signed(moved.metres, 2)} m',
                on: true,
              ),
            ),
          if (widget.drawing?.tool.isDrawing ?? false)
            _ViewportChip(
              '${widget.drawing!.tool.label} · '
              '${widget.drawing!.points.length} '
              '${widget.drawing!.points.length == 1 ? "point" : "points"}',
              on: true,
              tooltip:
                  'Enter finishes, backspace takes one back, '
                  'escape gives up.',
            ),
          _ViewportChip(
            widget.snapping.on
                ? 'Grid ${_gridLabel(widget.snapping.step)} · '
                      '${widget.snapping.to.label.toLowerCase()}'
                : 'Grid off',
            on: widget.snapping.on,
            tooltip:
                'Where a drag lands, and which part of the thing is '
                'put on the line. Hold control or option to suspend it '
                'for one drag; the brackets make it coarser and finer, '
                'and the arrow keys move by whole squares.',
            onTap: widget.onSnapping == null
                ? null
                : () => widget.onSnapping!(
                    Snapping(
                      on: !widget.snapping.on,
                      step: widget.snapping.step,
                      angle: widget.snapping.angle,
                    ),
                  ),
          ),
          // Only when there is one to hide. A switch for something that
          // is not there is a switch that teaches somebody nothing.
          if (_flying) ...[
            const SizedBox(width: Space.xs),
            _ViewportChip(
              'Flying · ${_flySpeed.toStringAsFixed(1)} m/s',
              on: true,
              tooltip:
                  'WASD to move, Q and E for down and up, shift to '
                  'go faster. Two fingers or the right button to steer, '
                  'the wheel to change how fast. Escape or ` to stop.',
              onTap: _toggleFlying,
            ),
          ],
          // Only while something is selected: it is a switch for how the
          // selection is shown, and with nothing selected it would change
          // nothing anybody could see.
          if (widget.selected.isNotEmpty) ...[
            const SizedBox(width: Space.xs),
            _ViewportChip(
              widget.outlineSelection ? 'Outline' : 'Boundary',
              on: widget.outlineSelection,
              tooltip:
                  'How the selection is shown. Outline follows each '
                  'object\'s silhouette, and draws what something hides '
                  'fainter and dashed; the active object is the brighter '
                  'one. Boundary draws the shape a click or a collision '
                  'meets instead.',
              onTap: widget.onToggleOutline,
            ),
          ],
          if (widget.interface != null) ...[
            const SizedBox(width: Space.xs),
            _ViewportChip(
              'Interface',
              on: widget.showInterface,
              tooltip:
                  'Draws the scene\'s interface over the viewport. '
                  'Turning it off here is for getting at what is behind '
                  'it — hiding the canvas object is what hides it in the '
                  'game.',
              onTap: widget.onToggleInterface,
            ),
          ],
        ],
      ),
    );
  }
}

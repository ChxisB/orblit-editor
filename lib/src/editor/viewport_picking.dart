// setState on `this` from an extension is exactly what @protected asks
// for; the analyzer does not count an extension body as inside the class.
// ignore_for_file: invalid_use_of_protected_member

part of 'viewport.dart';

// Reading the scene under the pointer -- an object, a face, a mesh element,
// or a rectangle's worth of them -- and the drawing that starts from it.

extension _Picking on _SceneViewportState {
  /// The preview to show, or null when nothing that has a view is selected.
  Widget? get _preview {
    final make = widget.previewOf;
    if (make == null || widget.selected.length != 1) return null;

    final scene = widget.workspace.loaded?.scene;
    final object = scene?[widget.selected.first];
    if (object == null || object.kind != ObjectKind.camera) return null;
    if (!object.visible || !scene!.isShown(object.id)) return null;

    return make(object);
  }

  MeshPicker _pickerFor(
    EditingMesh editing,
    Size surface,
  ) => MeshPicker(
    mesh: editing.mesh,
    transform: editing.transform,
    projection: ViewportProjection(camera: widget.camera, size: surface),
    seeThrough: widget.seeThroughElements,
  );

  /// The marquee as a rectangle, or null when there is not one.
  Rect? get _box {
    final from = _boxFrom;
    final to = _boxTo;
    if (from == null || to == null) return null;
    final box = Rect.fromPoints(from, to);
    // A rectangle a couple of pixels across is a click that wobbled, and
    // taking it as a marquee would clear a selection somebody meant to keep.
    return box.width < 3 && box.height < 3 ? null : box;
  }

  /// Takes everything the marquee drew round.
  void _takeBox(bool add) {
    final editing = widget.editing;
    final surface = _surface;
    final box = _box;
    if (editing == null || surface == null || box == null) return;

    final picker = _pickerFor(editing, surface);
    final found = switch (widget.elementMode) {
      ElementMode.vertex => picker.verticesIn(box).cast<Object>(),
      ElementMode.edge => picker.edgesIn(box).cast<Object>(),
      ElementMode.face => picker.facesIn(box).cast<Object>(),
    };
    widget.onSelectElements?.call(found.toList(), add: add);
  }

  /// Puts down a point for whichever tool is drawing.
  ///
  /// The first one decides the plane. For a cut that is the face under the
  /// pointer and nothing else will do; for a shape it is a face if there is
  /// one and the ground otherwise, because a plan is usually drawn on the
  /// floor and sometimes on top of a wall.
  void _drawAt(Offset local) {
    final drawing = widget.drawing;
    final surface = _surface;
    final report = widget.onDrawPoint;
    if (drawing == null || surface == null || report == null) return;
    if (!drawing.tool.isDrawing) return;

    final ray = ViewportProjection(
      camera: widget.camera,
      size: surface,
    ).rayThrough(local);

    // Once the plane is down, every later point is on it — a plane worked out
    // afresh each click would follow whatever happened to be behind.
    final already = drawing.placeOn(ray.origin, ray.direction);
    if (already != null) {
      if (drawing.wouldClose(already)) {
        widget.onDrawFinish?.call();
        return;
      }
      report(already, drawing.origin!, drawing.normal!, drawing.face);
      return;
    }

    final onFace = _faceUnder(local);
    if (onFace != null) {
      report(onFace.at, onFace.origin, onFace.normal, onFace.face);
      return;
    }
    if (drawing.tool == ViewportTool.cut) return;

    // The ground, at the height the grid is drawn at. A plan is drawn on the
    // floor unless somebody aimed at something.
    final at = PolyShape.onPlane(
      ray.origin,
      ray.direction,
      Vector3.zero(),
      Vector3(0, 1, 0),
    );
    if (at == null) return;
    report(at, Vector3.zero(), Vector3(0, 1, 0), null);
  }

  /// The face of the shape being edited that a pixel lands on, with its
  /// plane. Null when nothing of it is under the pointer.
  ({Vector3 at, Vector3 origin, Vector3 normal, int face})? _faceUnder(
    Offset local,
  ) {
    final editing = widget.editing;
    final surface = _surface;
    if (editing == null || surface == null) return null;

    final found = _pickerFor(editing, surface).faceAt(local);
    if (found == null) return null;

    final face = editing.mesh.faces[found];
    // Into the world, because the drawing is in the world and the object may
    // be somewhere else entirely.
    final centre = editing.transform.transformed3(editing.mesh.centreOf(face));
    final normal = (editing.transform.rotated3(
      editing.mesh.normalOf(face).clone(),
    ))..normalize();

    final ray = ViewportProjection(
      camera: widget.camera,
      size: surface,
    ).rayThrough(local);
    final at = PolyShape.onPlane(ray.origin, ray.direction, centre, normal);
    if (at == null) return null;

    return (at: at, origin: centre, normal: normal, face: found);
  }

  /// Follows the pointer while drawing, so the line reaches it.
  void _hoverDraw(Offset local) {
    final drawing = widget.drawing;
    final surface = _surface;
    if (drawing == null || surface == null || !drawing.tool.isDrawing) return;

    final ray = ViewportProjection(
      camera: widget.camera,
      size: surface,
    ).rayThrough(local);
    final at =
        drawing.placeOn(ray.origin, ray.direction) ??
        _faceUnder(local)?.at ??
        PolyShape.onPlane(
          ray.origin,
          ray.direction,
          Vector3.zero(),
          Vector3(0, 1, 0),
        );
    if (at == drawing.hovering) return;
    setState(() => drawing.hovering = at);
  }

  /// What a pixel lands on, in whatever mode is on.
  Object? _elementAt(Offset pixel) {
    final editing = widget.editing;
    final surface = _surface;
    if (editing == null || surface == null || surface.isEmpty) return null;

    final picker = _pickerFor(editing, surface);

    return switch (widget.elementMode) {
      ElementMode.vertex => picker.vertexAt(pixel),
      ElementMode.edge => picker.edgeAt(pixel),
      ElementMode.face => picker.faceAt(pixel),
    };
  }

  bool get _rendererAvailable => rendererAvailable;

  /// What is on screen.
  String get _summary {
    final scene = widget.workspace.loaded?.scene;
    if (scene == null) return 'No scene';
    return '${scene.objects.where((o) => o.isDrawable).length} drawn';
  }
}

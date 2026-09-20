part of 'commands.dart';

// Edits that replace an object's geometry.

/// Changes the numbers a shape is made from.
///
/// One command for the whole shape rather than one per field: the fields are
/// dragged, they interact — a cylinder's sides and its radius are the same
/// decision — and an undo stack with "width" and "sides" as separate steps is
/// one somebody has to walk back through twice.
class SetShape extends EditorCommand {
  SetShape({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final Shape to;

  Shape? _was;

  @override
  String get label => 'Change $name';

  /// Dragging a slider produces one of these a frame; they collapse into one
  /// step, the way a dragged transform does.
  @override
  Object? get mergeKey => 'shape/$id';

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was ??= object.shape;
    object.shape = to;
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.shape = _was;
    host.sceneFor(sceneId)?.invalidate();
  }
}

/// Replaces an object's geometry.
///
/// What every mesh edit runs through. The whole mesh rather than the change:
/// an extrude adds vertices and faces and moves others, and describing that as
/// a diff is more code than copying a few thousand doubles — which is what a
/// mesh is, and is nothing next to a frame.
class SetGeometry extends EditorCommand {
  SetGeometry({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
    required this.what,
    this.gesture,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  Mesh to;

  /// What the step is called: "Extrude", "Inset".
  final String what;

  /// Set while a drag is running, so the hundred commands a gesture produces
  /// are one step to undo. Null for an edit that stands alone — a menu item,
  /// a tool button.
  final Object? gesture;

  @override
  Object? get mergeKey => gesture;

  @override
  void absorb(EditorCommand later) {
    if (later is! SetGeometry) return;
    // The mesh the drag has reached now, over the one it started from. The
    // `_was` this command is holding is from before the gesture began, which
    // is where undo has to land.
    to = later.to;
  }

  Mesh? _was;
  Shape? _wasShape;

  @override
  String get label => '$what $name';

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;

    _was ??= object.geometry;
    _wasShape ??= object.shape;
    object.geometry = to;
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;

    object
      ..geometry = _was
      ..shape = _wasShape;
    host.sceneFor(sceneId)?.invalidate();
  }
}

/// Changes where an object begins and ends.
class SetBoundary extends EditorCommand {
  SetBoundary({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
    this.gesture,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  Boundary to;

  /// Set while a slider is moving, so the run is one step.
  final Object? gesture;

  @override
  Object? get mergeKey => gesture;

  Boundary? _was;

  @override
  String get label => 'Boundary of $name';

  @override
  void absorb(EditorCommand later) {
    if (later is SetBoundary) to = later.to;
  }

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was ??= object.boundary;
    object.boundary = to;
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    final was = _was;
    if (object == null || was == null) return;
    object.boundary = was;
    host.sceneFor(sceneId)?.invalidate();
  }
}

part of 'commands.dart';

// How an object looks without changing its shape.

class SetColour extends EditorCommand {
  SetColour({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final Color from;
  final Color to;

  @override
  String get label => 'Recolour $name';

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.colour = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.colour = from;
}

/// Gives an object a texture, or takes one away.
///
/// The texture is the material, for now: a colour map is what an asset pack
/// ships separately from its model, and binding the two is the whole job.
class SetMaterialAsset extends EditorCommand {
  SetMaterialAsset({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final String? from;
  final String? to;

  @override
  String get label => to == null ? 'Clear texture on $name' : 'Texture $name';

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.materialAsset = to;

  @override
  void revert(SceneHost host) =>
      host.sceneFor(sceneId)?[id]?.materialAsset = from;
}

/// Changes how much an object answers the wind.
class SetSway extends EditorCommand {
  SetSway({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final double from;

  /// Not final: a merged run of drags rewrites where it ends up.
  double to;

  @override
  String get label => to <= 0 ? 'Stop $name swaying' : 'Set $name sway';

  @override
  Object? get mergeKey => (id, 'sway');

  @override
  void absorb(EditorCommand later) {
    if (later is SetSway) to = later.to;
  }

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.sway = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.sway = from;
}

/// Shows or hides an object.
class SetVisible extends EditorCommand {
  SetVisible({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final bool to;

  @override
  String get label => to ? 'Show $name' : 'Hide $name';

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.visible = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.visible = !to;
}

/// Turns shadow casting on or off.
class SetCastShadows extends EditorCommand {
  SetCastShadows({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final bool to;

  @override
  String get label => to ? 'Cast shadows from $name' : 'Stop $name casting';

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.castShadows = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.castShadows = !to;
}

/// Turns shadow receiving on or off.
class SetReceiveShadows extends EditorCommand {
  SetReceiveShadows({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final bool to;

  @override
  String get label =>
      to ? 'Let shadows fall on $name' : 'Keep shadows off $name';

  @override
  void apply(SceneHost host) =>
      host.sceneFor(sceneId)?[id]?.receiveShadows = to;

  @override
  void revert(SceneHost host) =>
      host.sceneFor(sceneId)?[id]?.receiveShadows = !to;
}

/// Replaces a shape's material slots.
///
/// The whole list rather than one slot, for the same reason geometry is the
/// whole mesh: a slot's position in the list is what a face points at, so an
/// edit to one is a fact about all of them.
class SetSurfaces extends EditorCommand {
  SetSurfaces({
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
  List<Surface> to;
  final String what;

  /// Set while a slider is being dragged, so a gesture is one step.
  final Object? gesture;

  @override
  Object? get mergeKey => gesture;

  List<Surface>? _was;

  @override
  String get label => '$what on $name';

  @override
  void absorb(EditorCommand later) {
    if (later is SetSurfaces) to = later.to;
  }

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was ??= [...object.surfaces];
    object.surfaces
      ..clear()
      ..addAll(to);
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    final was = _was;
    if (object == null || was == null) return;
    object.surfaces
      ..clear()
      ..addAll(was);
    host.sceneFor(sceneId)?.invalidate();
  }
}

/// Changes the outline a shape was drawn from.
class SetOutline extends EditorCommand {
  SetOutline({
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
  PolyShape to;

  /// Set while a slider is moving, so the run is one step.
  final Object? gesture;

  @override
  Object? get mergeKey => gesture;

  PolyShape? _was;

  @override
  String get label => 'Reshape $name';

  @override
  void absorb(EditorCommand later) {
    if (later is SetOutline) to = later.to;
  }

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    _was ??= object.outline;
    object.outline = to;
    host.sceneFor(sceneId)?.invalidate();
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.outline = _was;
    host.sceneFor(sceneId)?.invalidate();
  }
}

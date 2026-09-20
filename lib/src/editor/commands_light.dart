part of 'commands.dart';

// A light's own settings: how much, what kind, what shape.

/// Changes a light's power, in watts.
class SetPower extends EditorCommand {
  SetPower({
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
  String get label => 'Set $name power';

  @override
  Object? get mergeKey => (id, 'power');

  @override
  void absorb(EditorCommand later) {
    if (later is SetPower) to = later.to;
  }

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.power = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.power = from;
}

/// Changes what kind of light an object is.
///
/// The power comes with it, because the units change with the type: a sun is
/// stated in watts per square metre and everything else in watts, and keeping
/// the number while changing what it means is how a scene ends up blinding.
class SetLightType extends EditorCommand {
  SetLightType({
    required this.sceneId,
    required this.id,
    required this.name,
    required this.from,
    required this.to,
    required this.fromPower,
    required this.toPower,
  });

  @override
  final String sceneId;

  final String id;
  final String name;
  final LightType from;
  final LightType to;
  final double fromPower;
  final double toPower;

  @override
  String get label => 'Make $name a ${to.name}';

  @override
  void apply(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.lightType = to;
    object.power = toPower;
  }

  @override
  void revert(SceneHost host) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.lightType = from;
    object.power = fromPower;
  }
}

/// Changes a light's cone or the size of its source.
///
/// One command for the three, because they are three sliders on one shape and
/// an undo stack that distinguishes them buys nothing.
class SetLightShape extends EditorCommand {
  SetLightShape({
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
  final ({double size, double blend, double radius, double sun}) from;

  /// Not final: a merged run of drags rewrites where it ends up.
  ({double size, double blend, double radius, double sun}) to;

  @override
  String get label => 'Shape $name';

  @override
  Object? get mergeKey => (id, 'light shape');

  @override
  void absorb(EditorCommand later) {
    if (later is SetLightShape) to = later.to;
  }

  @override
  void apply(SceneHost host) => _write(host, to);

  @override
  void revert(SceneHost host) => _write(host, from);

  void _write(
    SceneHost host,
    ({double size, double blend, double radius, double sun}) values,
  ) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.spotSize = values.size;
    object.spotBlend = values.blend;
    object.sourceRadius = values.radius;
    object.sunAngle = values.sun;
  }
}

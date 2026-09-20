part of 'commands.dart';

// Edits to the scene rather than to anything in it: its name, its sky, its
// clock and its weather.

/// Changes something about the scene itself rather than a thing in it.
class SetSceneSky extends EditorCommand {
  SetSceneSky({
    required this.sceneId,
    required this.fromColour,
    required this.toColour,
    required this.fromAmbient,
    required this.toAmbient,
  });

  @override
  final String sceneId;

  final Color fromColour;
  final Color toColour;
  final double fromAmbient;
  double toAmbient;

  @override
  String get label => 'Change the sky';

  @override
  Object? get mergeKey => (sceneId, 'sky');

  @override
  void absorb(EditorCommand later) {
    if (later is SetSceneSky) toAmbient = later.toAmbient;
  }

  @override
  void apply(SceneHost host) => _set(host, toColour, toAmbient);

  @override
  void revert(SceneHost host) => _set(host, fromColour, fromAmbient);

  void _set(SceneHost host, Color colour, double ambient) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    scene
      ..skyColour = colour
      ..ambient = ambient;
  }
}

/// Renames the scene itself.
/// Changes the hour a scene is set at, and whether that hour advances.
class SetSceneTime extends EditorCommand {
  SetSceneTime({required this.sceneId, required this.from, required this.to});

  @override
  final String sceneId;

  final ({double hour, bool cycle, double speed}) from;

  /// Not final: a merged run of drags rewrites where it ends up.
  ({double hour, bool cycle, double speed}) to;

  @override
  String get label => from.cycle != to.cycle
      ? (to.cycle ? 'Start the day' : 'Stop the day')
      : 'Set the time';

  @override
  Object? get mergeKey => (sceneId, 'time');

  @override
  void absorb(EditorCommand later) {
    if (later is SetSceneTime) to = later.to;
  }

  @override
  void apply(SceneHost host) => _write(host, to);

  @override
  void revert(SceneHost host) => _write(host, from);

  void _write(SceneHost host, ({double hour, bool cycle, double speed}) v) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    scene.timeOfDay = v.hour;
    scene.dayCycle = v.cycle;
    scene.hoursPerSecond = v.speed;
    // A cycle that is starting begins at the hour it was set to rather than
    // wherever the clock had got to before it was last stopped.
    scene.clock = 0;
  }
}

/// Switches a light between being the sun and being the moon.
class SetCelestialBody extends EditorCommand {
  SetCelestialBody({
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
  final CelestialBody from;
  final CelestialBody to;

  @override
  String get label => 'Make $name the ${to.label.toLowerCase()}';

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?[id]?.body = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?[id]?.body = from;
}

/// Puts the scene into a different sort of weather.
///
/// The values come with the name, because a condition is a set of them rather
/// than a mode: once it has been applied, every one of them is free to be
/// moved, and the name is only a record of where they started.
/// Chooses which shape of cloud a sky has.
///
/// Separate from the condition because it is a separate decision: the same
/// weather makes very different skies, and somebody who has asked for cirrus
/// over a fair afternoon should keep it when they nudge the cover.
class SetCloudKind extends EditorCommand {
  SetCloudKind({
    required this.sceneId,
    required this.id,
    required this.from,
    required this.to,
    required this.fromHeight,
    required this.toHeight,
  });

  @override
  final String sceneId;

  final String id;
  final CloudKind? from;
  final CloudKind? to;

  /// Where the base sits, which moves with the shape.
  ///
  /// Cirrus is ice seven kilometres up and cumulus condense below one, so a
  /// shape that arrived without its height would arrive in the wrong place.
  /// The slider still moves it afterwards; this is only where it starts.
  final double fromHeight;
  final double toHeight;

  @override
  String get label => to == null
      ? 'Follow the condition'
      : 'Set the cloud to ${to!.label.toLowerCase()}';

  @override
  void apply(SceneHost host) => _set(host, to, toHeight);

  @override
  void revert(SceneHost host) => _set(host, from, fromHeight);

  void _set(SceneHost host, CloudKind? kind, double height) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.cloudKind = kind;
    object.weather = object.weather.copyWith(cloudHeight: height);
  }
}

class SetWeatherCondition extends EditorCommand {
  SetWeatherCondition({
    required this.sceneId,
    required this.id,
    required this.from,
    required this.to,
    required this.fromState,
    required this.toState,
  });

  @override
  final String sceneId;

  final String id;
  final WeatherCondition from;
  final WeatherCondition to;
  final WeatherState fromState;
  final WeatherState toState;

  @override
  String get label => 'Set the weather to ${to.label.toLowerCase()}';

  @override
  void apply(SceneHost host) => _change(host, condition: to, state: toState);

  @override
  void revert(SceneHost host) =>
      _change(host, condition: from, state: fromState);

  /// Sets the weather going rather than switching it.
  ///
  /// The change starts from what is on screen this instant, which may itself
  /// be halfway through an earlier one — otherwise changing your mind during a
  /// transition snaps back to where it set off from before starting again.
  void _change(
    SceneHost host, {
    required WeatherCondition condition,
    required WeatherState state,
  }) {
    final scene = host.sceneFor(sceneId);
    final object = scene?[id];
    if (scene == null || object == null) return;

    object.blendFrom = scene.weatherNow ?? object.weather;
    object.blendSince = scene.clock;
    object.condition = condition;
    object.weather = state;
  }
}

/// Adjusts one of the numbers behind the weather.
class SetWeatherValues extends EditorCommand {
  SetWeatherValues({
    required this.sceneId,
    required this.id,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final WeatherState from;

  /// Not final: a merged run of drags rewrites where it ends up.
  WeatherState to;

  @override
  String get label => 'Set the weather';

  @override
  Object? get mergeKey => (id, 'weather');

  @override
  void absorb(EditorCommand later) {
    if (later is SetWeatherValues) to = later.to;
  }

  @override
  void apply(SceneHost host) => _write(host, to);

  @override
  void revert(SceneHost host) => _write(host, from);

  void _write(SceneHost host, WeatherState state) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.weather = state;
    // A slider is somebody's hand on the weather. It arrives as they move it
    // rather than easing in behind them.
    object.blendFrom = null;
  }
}

/// Sets which way the wind blows, and how long a change of weather takes.
class SetWeatherWind extends EditorCommand {
  SetWeatherWind({
    required this.sceneId,
    required this.id,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;
  final ({double direction, double transition}) from;

  /// Not final: a merged run of drags rewrites where it ends up.
  ({double direction, double transition}) to;

  @override
  String get label => 'Set the wind';

  @override
  Object? get mergeKey => (id, 'wind');

  @override
  void absorb(EditorCommand later) {
    if (later is SetWeatherWind) to = later.to;
  }

  @override
  void apply(SceneHost host) => _write(host, to);

  @override
  void revert(SceneHost host) => _write(host, from);

  void _write(SceneHost host, ({double direction, double transition}) values) {
    final object = host.sceneFor(sceneId)?[id];
    if (object == null) return;
    object.windDirection = values.direction;
    object.transitionSeconds = values.transition;
  }
}

class RenameScene extends EditorCommand {
  RenameScene({required this.sceneId, required this.from, required this.to});

  @override
  final String sceneId;

  final String from;
  String to;

  @override
  String get label => 'Rename $from';

  @override
  Object? get mergeKey => (sceneId, 'sceneName');

  @override
  void absorb(EditorCommand later) {
    if (later is RenameScene) to = later.to;
  }

  @override
  void apply(SceneHost host) => host.sceneFor(sceneId)?.name = to;

  @override
  void revert(SceneHost host) => host.sceneFor(sceneId)?.name = from;
}

/// A change stated as the difference between two documents.
///
/// Every other command in this file names the field it touches: a colour, a
/// transform, a parent. That is the right shape for an edit somebody makes
/// with a mouse, and the wrong shape for one that arrives whole — a scene
/// pasted in, a file reloaded from disk underneath somebody, a generated
/// layout, an import. For those the change is not a field, it is "this
/// document became that one", and a diff says so exactly.
///
/// It is also the shape an edit has to be in to leave this machine. A field
/// command is a Dart object; a diff is a list of operations that serialise,
/// which is what a second editor on the same scene would have to be sent.
///
/// The blunt instrument, deliberately. It rebuilds the objects rather than
/// mutating the ones that are there — what the renderer knows each by is
/// carried across, so it is not blunt where it would cost a frame, but the
/// outliner is rebuilt and a drag should not go through here.
class ApplySceneDiff extends EditorCommand {
  ApplySceneDiff({
    required this.sceneId,
    required this.label,
    required this.diff,
  });

  /// The change from what is there now to what should be.
  ///
  /// Both directions are in it: the inverse is what undo applies, so there is
  /// no second description of the change to fall out of step with the first.
  final doc.SceneDiff diff;

  @override
  final String sceneId;

  @override
  final String label;

  @override
  void apply(SceneHost host) => _move(host, diff);

  @override
  void revert(SceneHost host) => _move(host, diff.inverse);

  void _move(SceneHost host, doc.SceneDiff by) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    SceneDocument.reconcile(scene, by.applyTo(SceneDocument.documentOf(scene)));
  }
}

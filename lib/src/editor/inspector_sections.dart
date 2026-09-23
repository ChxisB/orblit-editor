part of 'inspector.dart';

// What a section of the inspector is, and the ones the editor has always
// had, registered the way anything else would register one.

/// What an inspector section is showing: one object, and what its fields
/// need in order to change it.
@immutable
class InspectorTarget {
  const InspectorTarget({
    required this.sceneId,
    required this.scene,
    required this.object,
    required this.history,
    this.onOpenData,
    this.onDetachData,
    this.onOpenInterface,
    this.keying,
  });

  /// The scene [object] is in, by id, which is how a command names it.
  final String sceneId;

  final EditorScene scene;

  final SceneObject object;

  /// Where every change goes, so that undo knows about all of them.
  final History history;

  /// Shows one of [object]'s data objects in the project browser.
  final ValueChanged<String>? onOpenData;

  /// Takes one of [object]'s data objects off it.
  final void Function(String id, String path)? onDetachData;

  /// Opens the interface a canvas puts on screen.
  final ValueChanged<String>? onOpenInterface;

  /// What keys one of [object]'s fields into the clip being edited, when
  /// there is one. A section that has a field a clip can move puts a
  /// [KeyButton] after it.
  final Keying? keying;
}

/// One block of the inspector, and the objects it is shown for.
///
/// Keyed by what an object is rather than listed in the inspector, so a body
/// or a terrain brings its own fields and the inspector does not have to know
/// either of them exists.
class InspectorSection implements Registered {
  const InspectorSection({
    required this.name,
    required this.appliesTo,
    required this.build,
  });

  @override
  final String name;

  /// Whether [InspectorTarget.object] has anything to show here.
  final bool Function(InspectorTarget target) appliesTo;

  /// The section itself: an [OrblitSection], so it reads like the rest.
  final Widget Function(InspectorTarget target) build;

  /// The sections the editor has always had, in the order they stack.
  static final List<InspectorSection> builtIn = List.unmodifiable([
    InspectorSection(
      name: 'visibility',
      appliesTo: (target) => target.object.kind != ObjectKind.scene,
      build: (target) => target._visibility(target.scene),
    ),
    // Weather is everywhere at once, so it has no position to show.
    InspectorSection(
      name: 'transform',
      appliesTo: (target) =>
          target.object.kind != ObjectKind.scene &&
          target.object.kind != ObjectKind.weather,
      build: (target) => target._transform(),
    ),
    InspectorSection(
      name: 'light',
      appliesTo: (target) => target.object.kind == ObjectKind.light,
      build: (target) => target._light(),
    ),
    InspectorSection(
      name: 'mesh',
      appliesTo: (target) => target.object.kind == ObjectKind.mesh,
      build: (target) => target._mesh(),
    ),
    InspectorSection(
      name: 'weather',
      appliesTo: (target) => target.object.kind == ObjectKind.weather,
      build: (target) => target._weather(),
    ),
    InspectorSection(
      name: 'air',
      appliesTo: (target) => target.object.kind == ObjectKind.weather,
      build: (target) => target._air(),
    ),
    InspectorSection(
      name: 'interface',
      appliesTo: (target) => target.object.kind == ObjectKind.canvas,
      build: (target) => target._interface(),
    ),
    InspectorSection(
      name: 'data',
      appliesTo: (target) => target.object.data.isNotEmpty,
      build: (target) => target._data(),
    ),
  ]);
}

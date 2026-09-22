part of 'commands.dart';

// The components a row carries without fields of its own for them.

/// Puts a component on an object, replaces the one it has, or takes it off.
///
/// One command for every component in [SceneObject.components] rather than
/// one per field, because those components are replaced whole: a body with a
/// heavier mass is a new body. That is also what makes undo cheap — [from] is
/// the component that was there, not a copy of it.
///
/// Null on either side means none: [from] null is adding one, [to] null is
/// taking it off.
class SetObjectComponent extends EditorCommand {
  SetObjectComponent({
    required this.sceneId,
    required this.id,
    required this.label,
    required this.type,
    required this.from,
    required this.to,
  });

  @override
  final String sceneId;

  final String id;

  @override
  final String label;

  /// Which component, as it is keyed on the entity.
  final String type;

  final doc.SceneComponent? from;

  /// Not final: a merged run of drags rewrites where it ends up.
  doc.SceneComponent? to;

  /// A drag through one component's fields is one step. Adding one or taking
  /// it off is a step of its own, so undoing a slider never takes the
  /// component with it.
  @override
  Object? get mergeKey =>
      from == null || to == null ? null : (id, 'component', type);

  @override
  void absorb(EditorCommand later) {
    if (later is SetObjectComponent) to = later.to;
  }

  @override
  void apply(SceneHost host) => _put(host, to);

  @override
  void revert(SceneHost host) => _put(host, from);

  void _put(SceneHost host, doc.SceneComponent? component) {
    final components = host.sceneFor(sceneId)?[id]?.components;
    if (components == null) return;
    if (component == null) {
      components.remove(type);
    } else {
      components[type] = component;
    }
  }
}

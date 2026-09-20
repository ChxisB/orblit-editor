part of 'commands.dart';

// Moving, rotating and scaling — one object or a selection.

/// Which of an object's three vectors an edit is touching.
enum TransformField {
  position('Move'),
  rotation('Rotate'),
  scale('Scale');

  const TransformField(this.verb);

  /// The word that appears next to Undo.
  final String verb;

  Vector3 of(SceneObject object) => switch (this) {
    TransformField.position => object.position,
    TransformField.rotation => object.rotation,
    TransformField.scale => object.scale,
  };
}

/// Moves, turns or resizes one object.
///
/// Holds the whole vector either side rather than one component, so a drag on
/// X and a later drag on Y merge into one "Move" step the way somebody
/// nudging a thing into place expects.
class SetTransform extends EditorCommand {
  SetTransform({
    required this.sceneId,
    required this.id,
    required this.field,
    required this.name,
    required Vector3 from,
    required Vector3 to,
  }) : _from = from.clone(),
       _to = to.clone();

  @override
  final String sceneId;

  final String id;
  final TransformField field;

  /// Kept for the label so it survives the object being deleted and restored.
  final String name;

  final Vector3 _from;
  Vector3 _to;

  @override
  String get label => '${field.verb} $name';

  @override
  Object? get mergeKey => (id, field);

  @override
  void absorb(EditorCommand later) {
    if (later is SetTransform) _to = later._to.clone();
  }

  @override
  void apply(SceneHost host) => _set(host, _to);

  @override
  void revert(SceneHost host) => _set(host, _from);

  void _set(SceneHost host, Vector3 value) {
    final scene = host.sceneFor(sceneId);
    final object = scene?[id];
    if (scene == null || object == null) return;
    field.of(object).setFrom(value);
    scene.invalidate();
  }
}

/// Changes an object's colour.
/// Moves or turns several objects at once, as one step.
///
/// A gizmo drag with three things selected is one gesture and has to be one
/// undo. Running a command per object would merge each into its own run and
/// leave somebody pressing undo once per thing they moved together.
class TransformMany extends EditorCommand {
  TransformMany({
    required this.sceneId,
    required this.field,
    required this.what,
    required Map<String, ({Vector3 from, Vector3 to})> changes,
  }) : _changes = {
         for (final entry in changes.entries)
           entry.key: (
             from: entry.value.from.clone(),
             to: entry.value.to.clone(),
           ),
       };

  @override
  final String sceneId;

  final TransformField field;

  /// What is being moved, for the label: a name, or how many there are.
  final String what;

  final Map<String, ({Vector3 from, Vector3 to})> _changes;

  @override
  String get label => '${field.verb} $what';

  /// One key for the whole gesture rather than one per object, which is what
  /// collapses a drag into a single step however many things it moved.
  @override
  Object? get mergeKey => (sceneId, 'gizmo', field);

  /// Nothing about what these objects *are* has changed — only where they
  /// stand. Which is what a drag does sixty times a second.
  @override
  bool get onlyMoves => true;

  @override
  void absorb(EditorCommand later) {
    if (later is! TransformMany) return;
    for (final entry in later._changes.entries) {
      final existing = _changes[entry.key];
      // Keeps where each object started and takes where it has got to, so
      // undoing the run puts everything back where the drag began.
      _changes[entry.key] = (
        from: existing?.from ?? entry.value.from.clone(),
        to: entry.value.to.clone(),
      );
    }
  }

  @override
  void apply(SceneHost host) => _write(host, (change) => change.to);

  @override
  void revert(SceneHost host) => _write(host, (change) => change.from);

  void _write(
    SceneHost host,
    Vector3 Function(({Vector3 from, Vector3 to}) change) pick,
  ) {
    final scene = host.sceneFor(sceneId);
    if (scene == null) return;
    for (final entry in _changes.entries) {
      final object = scene[entry.key];
      if (object == null) continue;
      field.of(object).setFrom(pick(entry.value));
    }
    scene.invalidate();
  }
}

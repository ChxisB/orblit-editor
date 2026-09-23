import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import 'scene.dart';
import 'scene_document.dart';

/// What posing the scene changed, which decides how much is built again.
enum PoseChange {
  /// Nothing.
  none,

  /// Only where things are, which the views that show movement follow.
  moved,

  /// Something else about an object, which any panel might be showing.
  rebuilt,
}

typedef _Field = ({String id, String type, String field});

/// The clip on the timeline, shown on the scene where the playhead is.
///
/// Written into the objects directly, not through the undo stack: scrubbing
/// is looking, not editing, and a scene should not read as changed because
/// somebody looked at it. What each field was before the clip took it is
/// kept, and [lift] puts it all back — which is done before anything that
/// reads the scene as a document, so a saved scene is the scene as it rests
/// and not wherever the playhead happened to be.
///
/// A field the clip moves belongs to the clip while it is shown. Changing
/// one by hand shows until the playhead moves, and keying it is how the
/// change is kept.
class ClipPreview {
  /// Each field the clip has taken, and what it was before.
  final Map<_Field, Object?> _rest = {};

  bool get posing => _rest.isNotEmpty;

  /// Shows [clip] at [at] on [scene], as played by [owner].
  ///
  /// Fields the clip moved last time and does not now go back to how they
  /// were, so choosing something else to play on, or deleting the last key
  /// of a channel, leaves nothing behind.
  PoseChange pose(
    EditorScene scene,
    ClipDocument? clip,
    String? owner,
    double at,
  ) {
    final playing = owner == null ? null : scene[owner];
    if (clip == null || owner == null || playing == null) return lift(scene);

    final scope = ClipScope.inScene(
      doc.SceneDocument(entities: [SceneDocument.entityOf(playing)]),
      owner,
    );
    final frame = clip.sampleAt(at);
    final ids = {for (final target in frame.values.keys) scope.resolve(target)};
    final document = doc.SceneDocument(
      entities: [
        for (final id in ids)
          if (scene[id] case final object?) SceneDocument.entityOf(object),
      ],
    );

    final driven = <_Field>{};
    for (final MapEntry(key: target, value: properties)
        in frame.values.entries) {
      final entity = document[scope.resolve(target)];
      if (entity == null) continue;
      for (final property in properties.keys) {
        final dot = property.indexOf('.');
        if (dot <= 0) continue;
        final type = property.substring(0, dot);
        if (entity[type] == null) continue;
        driven.add((
          id: entity.id,
          type: type,
          field: property.substring(dot + 1),
        ));
      }
    }

    final restoring = <doc.SetField>[];
    for (final field in _rest.keys.toList()) {
      if (driven.contains(field)) continue;
      restoring.add(_back(field, _rest.remove(field)));
    }
    for (final field in driven) {
      _rest.putIfAbsent(
        field,
        () => document[field.id]![field.type]!.toJson()[field.field],
      );
    }

    return _write(scene, [
      ...restoring,
      ...sceneOpsFor(frame, document, scope),
    ]);
  }

  /// Puts back every field the clip has taken.
  PoseChange lift(EditorScene scene) {
    if (_rest.isEmpty) return PoseChange.none;
    final ops = [
      for (final MapEntry(key: field, value: rest) in _rest.entries)
        _back(field, rest),
    ];
    _rest.clear();
    return _write(scene, ops);
  }

  /// Lets go without putting anything back, for a scene that is going away
  /// and taking the fields with it.
  void forget() => _rest.clear();

  static doc.SetField _back(_Field field, Object? rest) =>
      doc.SetField(field.id, field.type, field.field, from: null, to: rest);

  /// Writes [ops] into [scene].
  ///
  /// Where something is goes straight into the row, which is all a drag does
  /// too. Anything else goes through the document and back, the way an undo
  /// does, one object at a time.
  static PoseChange _write(EditorScene scene, List<doc.SetField> ops) {
    if (ops.isEmpty) return PoseChange.none;
    var change = PoseChange.moved;
    final others = <String, List<doc.SetField>>{};
    for (final op in ops) {
      final object = scene[op.id];
      if (object == null) continue;
      final vector = op.type != doc.SceneComponents.transform
          ? null
          : switch (op.field) {
              'position' => object.position,
              'rotation' => object.rotation,
              'scale' => object.scale,
              _ => null,
            };
      if (vector != null) {
        vector.setFrom(
          doc.Values.vector(op.to, fallback: op.field == 'scale' ? 1 : 0),
        );
        continue;
      }
      (others[op.id] ??= []).add(op);
    }

    for (final MapEntry(key: id, value: changes) in others.entries) {
      final object = scene[id]!;
      var document = doc.SceneDocument(
        entities: [SceneDocument.entityOf(object)],
      );
      for (final op in changes) {
        document = op.applyTo(document);
      }
      scene.replace(
        SceneDocument.objectOf(document[id]!, renderKey: object.renderKey),
      );
      change = PoseChange.rebuilt;
    }
    scene.invalidate();
    return change;
  }
}

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/body_section.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// A trigger and a zone on an object: carried through the editor, told apart
// from a solid body, and edited as one thing each.

/// A fixed box that is [trigger] or has [zone], the way a place is laid out.
SceneObject pool({doc.BodyComponent? body, doc.ZoneComponent? zone}) =>
    SceneObject(
      id: 'pool',
      name: 'Pool',
      kind: ObjectKind.group,
      components: {
        doc.SceneComponents.body:
            body ?? doc.BodyComponent(motion: doc.BodyMotion.fixed),
        doc.SceneComponents.zone: ?zone,
      },
    );

EditorScene reopened(EditorScene scene) =>
    SceneDocument.decode(SceneDocument.encode(scene)).scene;

SetObjectComponent putZone(
  EditorScene scene,
  doc.ZoneComponent? to, {
  String label = 'Set Pool zone',
}) => SetObjectComponent(
  sceneId: 'a',
  id: 'pool',
  label: label,
  type: doc.SceneComponents.zone,
  from: zoneOf(scene['pool']!),
  to: to,
);

void main() {
  group('a trigger and a zone in a scene file', () {
    test('a trigger, its stay and its belt come back as they were saved', () {
      final body = doc.BodyComponent(
        motion: doc.BodyMotion.fixed,
        trigger: true,
        stay: true,
        surface: Vector3(2, 0, -1),
      );

      final back = reopened(EditorScene([pool(body: body)]));

      expect(physicsBodyOf(back['pool']!)!.toJson(), body.toJson());
    });

    test('a zone comes back as it was saved', () {
      final zone = doc.ZoneComponent(
        gravity: Vector3(0, 20, 0),
        linearDamping: 4,
        angularDamping: 2,
        priority: 3,
      );

      final back = reopened(EditorScene([pool(zone: zone)]));

      expect(zoneOf(back['pool']!)!.toJson(), zone.toJson());
    });

    test('a field a zone leaves out stays out of the file', () {
      final back = reopened(
        EditorScene([pool(zone: doc.ZoneComponent(linearDamping: 3))]),
      );

      final saved = zoneOf(back['pool']!)!.toJson();
      expect(saved['linearDamping'], 3);
      expect(saved.containsKey('gravity'), isFalse);
      expect(saved.containsKey('angularDamping'), isFalse);
    });

    test('an object without a zone does not grow one', () {
      final back = reopened(EditorScene([pool()]));
      expect(zoneOf(back['pool']!), isNull);
      expect(SceneDocument.encode(back), isNot(contains('"zone"')));
    });

    test('a copy carries the zone, and does not share the map', () {
      final object = pool(zone: doc.ZoneComponent());
      final copy = object.copyAs(id: 'other');
      copy.components.remove(doc.SceneComponents.zone);

      expect(zoneOf(object), isNotNull);
      expect(zoneOf(copy), isNull);
    });
  });

  group('what is a place', () {
    test('a fixed trigger is one, and a fixed solid body is not', () {
      final trigger = doc.BodyComponent(
        motion: doc.BodyMotion.fixed,
        trigger: true,
      );
      expect(isPlace(pool(body: trigger)), isTrue);
      expect(isPlace(pool()), isFalse);
    });

    test('a driven trigger is one, for a moving lift or a patrol', () {
      final trigger = doc.BodyComponent(
        motion: doc.BodyMotion.driven,
        trigger: true,
      );
      expect(isPlace(pool(body: trigger)), isTrue);
    });

    test('a zone makes its body one without being asked', () {
      expect(isPlace(pool(zone: doc.ZoneComponent())), isTrue);
    });

    test('a free body is never one, whatever it says', () {
      final free = doc.BodyComponent(trigger: true);
      expect(isPlace(pool(body: free)), isFalse);
      expect(isPlace(pool(body: free, zone: doc.ZoneComponent())), isFalse);
    });

    test('a zone with no body is none', () {
      final lone = SceneObject(
        id: 'pool',
        name: 'Pool',
        kind: ObjectKind.group,
        components: {doc.SceneComponents.zone: doc.ZoneComponent()},
      );
      expect(isPlace(lone), isFalse);
    });

    test('ground is fixed even when its file says free', () {
      final ground = doc.BodyComponent(shape: doc.BodyShape.plane);
      expect(movesFreely(ground), isFalse);
      expect(movesFreely(doc.BodyComponent()), isTrue);
    });
  });

  group('editing a zone', () {
    late EditorScene scene;
    late History history;

    setUp(() {
      scene = EditorScene([pool()]);
      history = History(
        Workspace('/project')
          ..add(SceneEntry(id: 'a', name: 'A', scene: scene)),
      );
    });

    test('adding one is undone by taking it off again', () {
      history.run(putZone(scene, doc.ZoneComponent(), label: 'Add zone'));
      expect(zoneOf(scene['pool']!), isNotNull);

      history.undo();
      expect(zoneOf(scene['pool']!), isNull);

      history.redo();
      expect(zoneOf(scene['pool']!), isNotNull);
    });

    test('a drag through a field is one step', () {
      history
        ..run(putZone(scene, doc.ZoneComponent(), label: 'Add zone'))
        ..seal();
      for (var i = 1; i <= 30; i++) {
        final zone = zoneOf(scene['pool']!)!;
        history.run(putZone(scene, zone.copyWith(linearDamping: i * 0.1)));
      }

      expect(zoneOf(scene['pool']!)!.linearDamping, closeTo(3, 1e-9));
      expect(history.labels, ['Add zone', 'Set Pool zone']);

      // Back to before the drag, and the zone stays on.
      history.undo();
      expect(zoneOf(scene['pool']!)!.linearDamping, isNull);
      expect(zoneOf(scene['pool']!), isNotNull);
    });

    test('leaving a field to the body puts it out of the zone', () {
      history
        ..run(
          putZone(
            scene,
            doc.ZoneComponent(gravity: Vector3.zero(), linearDamping: 2),
            label: 'Add zone',
          ),
        )
        ..seal();

      final zone = zoneOf(scene['pool']!)!;
      history.run(
        putZone(scene, doc.ZoneComponent(linearDamping: zone.linearDamping)),
      );

      expect(zoneOf(scene['pool']!)!.gravity, isNull);
      expect(zoneOf(scene['pool']!)!.linearDamping, 2);
    });

    test('a trigger flag is one undoable step on the body', () {
      final body = physicsBodyOf(scene['pool']!)!;
      history
        ..run(
          SetObjectComponent(
            sceneId: 'a',
            id: 'pool',
            label: 'Set Pool body',
            type: doc.SceneComponents.body,
            from: body,
            to: body.copyWith(trigger: true),
          ),
        )
        ..seal();
      expect(isPlace(scene['pool']!), isTrue);

      history.undo();
      expect(isPlace(scene['pool']!), isFalse);
    });
  });
}

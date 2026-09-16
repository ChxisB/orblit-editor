import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

class _Host implements SceneHost {
  _Host(this.scene);

  final EditorScene scene;

  @override
  EditorScene? sceneFor(String id) => scene;
}

EditorScene sceneOf() => EditorScene([
  SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light, power: 110),
  SceneObject(
    id: 'crate',
    name: 'Crate',
    kind: ObjectKind.mesh,
    position: Vector3(1, 0, 0),
    colour: const Color(0xFFD9634F),
  ),
  SceneObject(id: 'props', name: 'Props', kind: ObjectKind.group),
]);

/// A change worked out on documents, landed on the editor's scene.
ApplySceneDiff diffTo(
  EditorScene scene,
  doc.SceneDocument Function(doc.SceneDocument) change, {
  String label = 'Change',
}) {
  final before = SceneDocument.documentOf(scene);
  return ApplySceneDiff(
    sceneId: 'main',
    label: label,
    diff: doc.SceneDiff.between(before, change(before)),
  );
}

void main() {
  group('an edit stated as a difference', () {
    test('moves an object, and undoing puts it back', () {
      final scene = sceneOf();
      final host = _Host(scene);

      final command = diffTo(scene, (document) {
        final crate = document['crate']!;
        return document.withEntity(
          'crate',
          crate.withComponent(
            doc.SceneComponents.transform,
            doc.TransformComponent(position: Vector3(1, 5, 0)),
          ),
        );
      });

      command.apply(host);
      expect(scene['crate']!.position.y, 5);

      command.revert(host);
      expect(scene['crate']!.position.y, 0);
      expect(scene['crate']!.position.x, 1);
    });

    test('adds and removes objects, both ways round', () {
      final scene = sceneOf();
      final host = _Host(scene);

      final command = diffTo(scene, (document) {
        final without = document.withEntity('sun', null);
        return without.copyWith(
          entities: [
            ...without.entities,
            const doc.SceneEntity(
              id: 'lamp',
              name: 'Lamp',
              components: {
                doc.SceneComponents.light: doc.LightComponent(power: 40),
              },
            ),
          ],
        );
      });

      command.apply(host);
      expect(scene.contains('sun'), isFalse);
      expect(scene['lamp']!.kind, ObjectKind.light);
      expect(scene['lamp']!.power, 40);

      command.revert(host);
      expect(scene['lamp'], isNull);
      expect(scene['sun']!.power, 110);
      expect(scene.objects.map((o) => o.id), ['sun', 'crate', 'props']);
    });

    test('reparents, and undoing unparents', () {
      final scene = sceneOf();
      final host = _Host(scene);

      final command = diffTo(
        scene,
        (document) => document.withEntity(
          'crate',
          document['crate']!.copyWith(parent: 'props'),
        ),
      );

      command.apply(host);
      expect(scene['crate']!.parentId, 'props');
      expect(scene.childrenOf('props').single.id, 'crate');

      command.revert(host);
      expect(scene['crate']!.parentId, isNull);
    });

    test('the scene\'s own settings travel in it too', () {
      final scene = sceneOf();
      final host = _Host(scene);

      final command = diffTo(
        scene,
        (document) => document.copyWith(
          settings: document.settings.copyWith(ambient: 400, timeOfDay: 21),
        ),
      );

      command.apply(host);
      expect(scene.ambient, 400);
      expect(scene.timeOfDay, 21);

      command.revert(host);
      expect(scene.ambient, 28000);
      expect(scene.timeOfDay, 10);
    });

    test('what the renderer knows an object by survives the round trip', () {
      final scene = sceneOf();
      final host = _Host(scene);
      final was = {for (final o in scene.objects) o.id: o.renderKey};

      final command = diffTo(
        scene,
        (document) => document.withEntity(
          'crate',
          document['crate']!.copyWith(name: 'Barrel'),
        ),
      );

      command.apply(host);
      expect(scene['crate']!.name, 'Barrel');
      expect(scene['crate']!.renderKey, was['crate']);
      expect(scene['sun']!.renderKey, was['sun']);

      command.revert(host);
      expect(scene['crate']!.name, 'Crate');
      expect(scene['crate']!.renderKey, was['crate']);
    });

    test('an undone edit leaves the scene byte-identical to what it was', () {
      final scene = sceneOf();
      final host = _Host(scene);
      final before = SceneDocument.encode(scene);

      final command = diffTo(scene, (document) {
        final crate = document['crate']!;
        return document
            .withEntity(
              'crate',
              crate
                  .withComponent(
                    doc.SceneComponents.mesh,
                    const doc.MeshComponent(asset: 'models/barrel.glb'),
                  )
                  .copyWith(name: 'Barrel', visible: false),
            )
            .withEntity('props', null);
      });

      command.apply(host);
      expect(SceneDocument.encode(scene), isNot(before));

      command.revert(host);
      expect(SceneDocument.encode(scene), before);
    });

    test('a diff can be written down and read back', () {
      final scene = sceneOf();
      final host = _Host(scene);

      final made = diffTo(
        scene,
        (document) => document.withEntity(
          'crate',
          document['crate']!.copyWith(name: 'Barrel'),
        ),
      );

      // What a second editor on the same scene would be sent, and what an
      // undo stack could be kept on disk as.
      final stored = ApplySceneDiff(
        sceneId: 'main',
        label: made.label,
        diff: doc.SceneDiff.fromJson(made.diff.toJson()),
      );

      stored.apply(host);
      expect(scene['crate']!.name, 'Barrel');
      stored.revert(host);
      expect(scene['crate']!.name, 'Crate');
    });
  });
}

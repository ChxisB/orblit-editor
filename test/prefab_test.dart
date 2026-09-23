import 'dart:convert';
import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clipboard.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/prefab.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart' hide Colors;

const String lampAsset = 'prefabs/Lamp post$prefabExtension';
const String streetAsset = 'prefabs/Street$prefabExtension';

/// A lamp post: a group with a post and a bulb under it.
EditorScene lampScene() => EditorScene([
  SceneObject(
    id: 'lamp',
    name: 'Lamp post',
    kind: ObjectKind.group,
    position: Vector3(40, 0, 12),
  ),
  SceneObject(
    id: 'post',
    name: 'Post',
    kind: ObjectKind.mesh,
    parentId: 'lamp',
    scale: Vector3(0.2, 4, 0.2),
  ),
  SceneObject(
    id: 'bulb',
    name: 'Bulb',
    kind: ObjectKind.light,
    parentId: 'lamp',
    position: Vector3(0, 4, 0),
    colour: const Color(0xFFFFCC88),
  ),
]);

/// A project folder with the lamp post saved in it as a prefab.
PrefabFiles project() {
  final root = Directory.systemTemp.createTempSync('orblit_prefabs');
  addTearDown(() => root.deleteSync(recursive: true));
  final files = PrefabFiles(root.path);
  final made = doc.makePrefab(
    SceneDocument.documentOf(lampScene()),
    'lamp',
    asset: lampAsset,
    source: files.find,
  );
  expect(files.write(lampAsset, made.prefab), isNull);
  return files;
}

/// A scene file holding a folded instance of [asset] for each of [ids].
String sceneWith(List<String> ids, {String asset = lampAsset}) =>
    doc.SceneDocument(
      name: 'Street',
      entities: [
        for (final id in ids)
          doc.SceneEntity(
            id: id,
            name: id,
            components: {
              doc.SceneComponents.prefab: doc.PrefabComponent(asset: asset),
            },
          ),
      ],
    ).encode();

/// The objects a scene file holds, by id.
Map<String, Map<String, Object?>> savedObjects(String text) {
  final json = jsonDecode(text) as Map<String, Object?>;
  return {
    for (final entry in (json['entities']! as List).cast<Map<String, Object?>>())
      entry['id']! as String: entry,
  };
}

/// Ids that do not collide with the ones already in a scene.
String Function() counter() {
  var next = 0;
  return () => 'new${next++}';
}

/// A host for running commands against named scenes.
class _Host implements SceneHost {
  _Host(this.scenes);

  final Map<String, EditorScene> scenes;

  @override
  EditorScene? sceneFor(String id) => scenes[id];
}

void main() {
  group('the project\'s prefabs', () {
    test('are read once, and kept as the scenes were opened against them', () {
      final files = project();
      final first = files.find(lampAsset)!;

      // Changed in another program while a scene is open.
      File(p.join(files.root, lampAsset)).writeAsStringSync(
        doc.PrefabDocument(
          name: 'Changed',
          root: first.root,
          document: first.document,
        ).encode(),
      );
      expect(files.find(lampAsset)!.name, 'Lamp post');

      // Once nothing open uses it, the next look reads the file again.
      files.keepOnly({});
      expect(files.find(lampAsset)!.name, 'Changed');
    });

    test('one that is not there says so, and is looked for again', () {
      final files = project();
      const later = 'prefabs/Later.oprefab';

      expect(files.read(later).problem, contains('not in the project'));
      File(
        p.join(files.root, later),
      ).writeAsStringSync(files.find(lampAsset)!.encode());
      expect(files.find(later), isNotNull);
    });

    test('one that is not a prefab says why', () {
      final files = project();
      File(p.join(files.root, 'prefabs/Junk.oprefab')).writeAsStringSync('{');

      final read = files.read('prefabs/Junk.oprefab');
      expect(read.prefab, isNull);
      expect(read.problem, contains('not a readable prefab'));
    });

    test('withOne answers for one prefab and the files for the rest', () {
      final files = project();
      final other = doc.PrefabDocument(
        name: 'Other',
        root: 'lamp',
        document: files.find(lampAsset)!.document,
      );

      final source = files.withOne('prefabs/Other.oprefab', other);
      expect(source('prefabs/Other.oprefab'), same(other));
      expect(source(lampAsset), same(files.find(lampAsset)));
    });
  });

  group('a scene with instances in it', () {
    test('is saved as the link and what is different, not the parts', () {
      final files = project();
      final scene = SceneDocument.decode(
        sceneWith(['lamp1']),
        prefabs: files.find,
      ).scene;
      expect(scene['lamp1/bulb'], isNotNull);

      scene['lamp1/bulb']!.colour = const Color(0xFF3366FF);
      final text = SceneDocument.encode(scene, prefabs: files.find);
      final saved = savedObjects(text);

      expect(saved.keys, ['lamp1']);
      final link =
          (saved['lamp1']!['components']! as Map<String, Object?>)['prefab']!
              as Map<String, Object?>;
      expect(link['asset'], lampAsset);
      expect(link['overrides'], isNotNull);
      expect(text, isNot(contains('lamp1/')));

      final back = SceneDocument.decode(text, prefabs: files.find).scene;
      expect(back['lamp1/bulb']!.colour, const Color(0xFF3366FF));
      expect(back['lamp1/post'], isNotNull);
    });

    test('keeps an edit to one instance in that one, and takes a change to '
        'the prefab in both', () {
      final files = project();
      final scene = SceneDocument.decode(
        sceneWith(['lamp1', 'lamp2']),
        prefabs: files.find,
      ).scene;
      scene['lamp1/bulb']!.colour = const Color(0xFF3366FF);
      final text = SceneDocument.encode(scene, prefabs: files.find);

      // The prefab's post grows, in a later session.
      final was = files.find(lampAsset)!;
      final post = was.document['post']!;
      File(p.join(files.root, lampAsset)).writeAsStringSync(
        doc.PrefabDocument(
          name: was.name,
          root: was.root,
          document: was.document.withEntity(
            'post',
            post.withComponent(
              doc.SceneComponents.transform,
              doc.TransformComponent(scale: Vector3(0.2, 9, 0.2)),
            ),
          ),
        ).encode(),
      );

      final later = PrefabFiles(files.root);
      final back = SceneDocument.decode(text, prefabs: later.find).scene;
      expect(back['lamp1/bulb']!.colour, const Color(0xFF3366FF));
      expect(back['lamp2/bulb']!.colour, const Color(0xFFFFCC88));
      expect(back['lamp1/post']!.scale.y, 9);
      expect(back['lamp2/post']!.scale.y, 9);
    });

    test('opens a prefab inside a prefab, and saves an edit made inside it', () {
      final files = project();
      files.write(
        streetAsset,
        doc.PrefabDocument(
          name: 'Street',
          root: 'street',
          document: doc.SceneDocument(
            name: 'Street',
            entities: [
              const doc.SceneEntity(id: 'street', name: 'Street'),
              const doc.SceneEntity(
                id: 'lamp3',
                name: 'Corner lamp',
                parent: 'street',
                components: {
                  doc.SceneComponents.prefab: doc.PrefabComponent(
                    asset: lampAsset,
                  ),
                },
              ),
            ],
          ),
        ),
      );

      final scene = SceneDocument.decode(
        sceneWith(['main'], asset: streetAsset),
        prefabs: files.find,
      ).scene;
      expect(scene['main/lamp3/bulb']!.parentId, 'main/lamp3');

      scene['main/lamp3/bulb']!.colour = const Color(0xFF00FF00);
      final text = SceneDocument.encode(scene, prefabs: files.find);
      expect(savedObjects(text).keys, ['main']);

      final back = SceneDocument.decode(text, prefabs: files.find).scene;
      expect(back['main/lamp3/bulb']!.colour, const Color(0xFF00FF00));
    });

    test('whose prefab has gone stays a link, and is saved as one', () {
      final files = project();
      final text = sceneWith(['lamp1'], asset: 'prefabs/Gone.oprefab');

      final load = SceneDocument.decode(text, prefabs: files.find);
      expect(load.problems.single, contains('could not be read'));
      expect(load.scene.objects.map((o) => o.id), ['lamp1']);
      expect(
        savedObjects(SceneDocument.encode(load.scene, prefabs: files.find))
            .keys,
        ['lamp1'],
      );
    });
  });

  group('copying an instance', () {
    EditorScene opened(PrefabFiles files) =>
        SceneDocument.decode(sceneWith(['lamp1']), prefabs: files.find).scene;

    test('pastes another instance, its parts under its new id', () {
      final scene = opened(project());
      scene['lamp1/bulb']!.colour = const Color(0xFF3366FF);

      final clipboard = SceneClipboard()..take(scene, ['lamp1']);
      final pasted = clipboard.contents(nextId: counter());
      final byId = {for (final o in pasted.objects) o.id: o};

      expect(byId.keys, unorderedEquals(['new0', 'new0/post', 'new0/bulb']));
      expect(byId['new0']!.prefab?.asset, lampAsset);
      expect(byId['new0/bulb']!.parentId, 'new0');
      expect(byId['new0/bulb']!.colour, const Color(0xFF3366FF));
      expect(pasted.roots, ['new0']);
    });

    test('a part copied on its own pastes as an ordinary object', () {
      final scene = opened(project());

      final clipboard = SceneClipboard()..take(scene, ['lamp1/bulb']);
      final pasted = clipboard.contents(nextId: counter());

      expect(pasted.objects.single.id, 'new0');
      expect(pasted.objects.single.prefab, isNull);
    });

    test('text from an older editor is read at the version it was written',
        () {
      final text = jsonEncode({
        'kind': 'orblit.objects',
        'formatVersion': 4,
        'roots': ['a'],
        'objects': [
          {
            'id': 'a',
            'name': 'Old lamp',
            'components': {
              'prefab': {'asset': lampAsset},
            },
          },
        ],
      });

      final clipboard = SceneClipboard();
      expect(clipboard.takeText(text), isTrue);
      final pasted = clipboard.contents(nextId: counter()).objects.single;
      expect(pasted.prefab?.state, doc.PrefabState.stamped);
    });
  });

  group('moving an instance to another scene', () {
    test('an instance whose id is taken there brings its parts along', () {
      final files = project();
      final from = SceneDocument.decode(
        sceneWith(['lamp']),
        prefabs: files.find,
      ).scene;
      final to = EditorScene([
        SceneObject(id: 'lamp', name: 'Their lamp', kind: ObjectKind.group),
      ]);
      final history = History(_Host({'a': from, 'b': to}));

      history.run(
        MoveBetweenScenes(
          fromSceneId: 'a',
          sceneId: 'b',
          id: 'lamp',
          name: 'Lamp post',
          parentId: null,
          index: 1,
        ),
      );

      expect(to['lamp~1']!.prefab?.asset, lampAsset);
      expect(to['lamp~1/bulb']!.parentId, 'lamp~1');
      expect(to['lamp~1/post']!.parentId, 'lamp~1');
      expect(from.length, 0);

      history.undo();
      expect(from['lamp/bulb']!.parentId, 'lamp');
      expect(to.length, 1);
    });
  });
}

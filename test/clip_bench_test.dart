import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/clip_edits.dart';
import 'package:orblit_editor/src/editor/clip_preview.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

// The timeline's side of the editor: clips opened, keyed from the inspector,
// changed through the undo stack, and shown on the scene without the scene
// reading as changed.

({EditorScene scene, History history, ClipBench bench}) rig() {
  final scene = EditorScene([
    SceneObject(id: 'lamp', name: 'Lamp', kind: ObjectKind.light, power: 100),
    SceneObject(id: 'box', name: 'Box', kind: ObjectKind.group),
  ]);
  final workspace = Workspace('/project')
    ..add(SceneEntry(id: 'a', name: 'A', scene: scene));
  final history = History(workspace);
  final bench = ClipBench(history: history, scene: () => scene)
    ..openClip('/project/wave.oclip', ClipDocument(name: 'wave', duration: 2));
  return (scene: scene, history: history, bench: bench);
}

const ChannelAddress lampPower = (
  target: '',
  bone: null,
  property: 'light.power',
);

void main() {
  group('keying', () {
    test('nothing can be keyed with no clip open', () {
      final scene = EditorScene([
        SceneObject(id: 'lamp', name: 'Lamp', kind: ObjectKind.light),
      ]);
      final bench = ClipBench(
        history: History(Workspace('/project')),
        scene: () => scene,
      );
      expect(bench.markFor(scene['lamp']!, 'light.power'), KeyMark.none);
    });

    test('keys what the row shows, and plays on what was keyed first', () {
      final (:scene, :history, :bench) = rig();
      final lamp = scene['lamp']!;
      expect(bench.markFor(lamp, 'light.power'), KeyMark.unkeyed);
      // A colour is four numbers, which is not something a key is guessed
      // to be.
      expect(bench.markFor(lamp, 'light.colour'), KeyMark.none);

      bench.key(lamp, 'light.power');
      expect(bench.owner, 'lamp');
      expect(bench.markFor(lamp, 'light.power'), KeyMark.keyed);
      final channel = channelAt(bench.clip!, lampPower)!;
      expect(channel.keys.single.value, 100);
      expect(history.labels, ['Key Lamp power']);

      bench.at = 0.5;
      expect(bench.markFor(lamp, 'light.power'), KeyMark.animated);
    });

    test('names anything else by its id from whatever plays the clip', () {
      final (:scene, :history, :bench) = rig();
      bench
        ..owner = 'lamp'
        ..key(scene['box']!, 'transform.position');
      final channel = bench.clip!.channels.single;
      expect(channel.target, 'box');
      expect(channel.kind, ChannelKind.vector);
    });

    test('a key goes on the frame nearest the playhead', () {
      final (:scene, :history, :bench) = rig();
      bench
        ..at = 0.51
        ..key(scene['lamp']!, 'light.power');
      expect(
        channelAt(bench.clip!, lampPower)!.keys.single.at,
        closeTo(15 / 30, 1e-9),
      );
    });
  });

  group('undo', () {
    test('takes a key back off, and the clip is saved when it is back', () {
      final (:scene, :history, :bench) = rig();
      final clip = bench.shown!;
      expect(bench.isUnsaved(clip), isFalse);

      bench.key(scene['lamp']!, 'light.power');
      expect(bench.isUnsaved(clip), isTrue);
      history.undo();
      expect(bench.clip!.channels, isEmpty);
      expect(bench.isUnsaved(clip), isFalse);
      history.redo();
      expect(bench.clip!.channels, hasLength(1));
    });

    test('a drag of keys is one step, and says it only moved them', () {
      final (:scene, :history, :bench) = rig();
      bench.key(scene['lamp']!, 'light.power');
      final ref = (channel: lampPower, index: 0);
      bench.selection = {ref};
      final start = bench.clip!;
      final gesture = Object();
      for (final by in [0.1, 0.2, 0.3]) {
        final moved = movedKeys(start, {ref}, by);
        bench.edit(
          'Move keys',
          (_) => moved.clip,
          keys: moved.keys,
          gesture: gesture,
          onlyMoves: true,
        );
        expect(history.lastOnlyMoved, isTrue);
      }
      history.seal();

      expect(history.labels, ['Key Lamp power', 'Move keys']);
      expect(keyOf(bench.clip!, ref)!.at, closeTo(0.3, 1e-9));
      history.undo();
      expect(keyOf(bench.clip!, ref)!.at, 0);
      expect(bench.selection, {ref});
    });

    test('undoing a clip that is not shown shows it', () {
      final (:scene, :history, :bench) = rig();
      bench.key(scene['lamp']!, 'light.power');
      bench.openClip(
        '/project/other.oclip',
        ClipDocument(name: 'o', duration: 1),
      );
      expect(bench.shown!.clip.name, 'o');
      history.undo();
      expect(bench.shown!.clip.name, 'wave');
    });

    test('closing a clip forgets its steps', () {
      final (:scene, :history, :bench) = rig();
      bench
        ..key(scene['lamp']!, 'light.power')
        ..close('/project/wave.oclip');
      expect(history.canUndo, isFalse);
      expect(bench.shown, isNull);
    });
  });

  group('saving', () {
    test('writes the clips that changed, and they read as saved', () {
      final folder = Directory.systemTemp.createTempSync('clip_bench');
      addTearDown(() => folder.deleteSync(recursive: true));
      final path = '${folder.path}/wave.oclip';
      File(path).writeAsStringSync(
        ClipDocument(name: 'wave', duration: 2).encode(),
      );

      final scene = EditorScene([
        SceneObject(id: 'lamp', name: 'Lamp', kind: ObjectKind.light),
      ]);
      final history = History(Workspace(folder.path));
      final bench = ClipBench(history: history, scene: () => scene);
      expect(bench.openFile(path), isEmpty);
      bench.key(scene['lamp']!, 'light.power');
      expect(bench.anyUnsaved, isTrue);

      expect(bench.saveAll(), isEmpty);
      expect(bench.anyUnsaved, isFalse);
      final read = ClipDocument.decode(File(path).readAsStringSync()).clip;
      expect(read.channels.single.property, 'light.power');
    });
  });

  group('preview', () {
    ClipDocument rising() => ClipDocument(
      name: 'rise',
      duration: 1,
      channels: [
        ClipChannel<Vector3>(
          target: 'box',
          property: 'transform.position',
          kind: ChannelKind.vector,
          keys: [
            Key(0, Vector3.zero(), hold: Hold.linear),
            Key(1, Vector3(0, 2, 0)),
          ],
        ),
        ClipChannel<double>(
          target: '',
          property: 'light.power',
          kind: ChannelKind.number,
          keys: const [Key(0, 10, hold: Hold.linear), Key(1, 30)],
        ),
      ],
    );

    test('poses the scene where the playhead is, and puts it back', () {
      final (:scene, :history, :bench) = rig();
      final preview = ClipPreview();
      final clip = rising();

      expect(preview.pose(scene, clip, 'lamp', 0.5), PoseChange.rebuilt);
      expect(scene['box']!.position.y, closeTo(1, 1e-9));
      expect(scene['lamp']!.power, closeTo(20, 1e-9));
      // Looking is not editing.
      expect(history.canUndo, isFalse);

      expect(preview.pose(scene, clip, 'lamp', 0.75), PoseChange.rebuilt);
      expect(scene['box']!.position.y, closeTo(1.5, 1e-9));

      expect(preview.lift(scene), PoseChange.rebuilt);
      expect(scene['box']!.position.y, 0);
      expect(scene['lamp']!.power, 100);
      expect(preview.posing, isFalse);
    });

    test('only moving things is only a move', () {
      final (:scene, :history, :bench) = rig();
      final preview = ClipPreview();
      final clip = rising();
      final moving = clip.copyWith(channels: [clip.channels.first]);
      expect(preview.pose(scene, moving, 'lamp', 0.5), PoseChange.moved);
      expect(preview.pose(scene, moving, 'lamp', 0.75), PoseChange.moved);
      // Where it already is: nothing to build again.
      expect(preview.pose(scene, moving, 'lamp', 0.75), PoseChange.none);
    });

    test('what the clip stops moving goes back', () {
      final (:scene, :history, :bench) = rig();
      final preview = ClipPreview();
      final clip = rising();
      preview.pose(scene, clip, 'lamp', 1);
      expect(scene['lamp']!.power, 30);

      // The power channel deleted: the lamp is its own again.
      preview.pose(
        scene,
        clip.copyWith(channels: [clip.channels.first]),
        'lamp',
        1,
      );
      expect(scene['lamp']!.power, 100);
      expect(scene['box']!.position.y, 2);

      // Nothing to play on: everything is.
      preview.pose(scene, clip, null, 1);
      expect(scene['box']!.position.y, 0);
      expect(preview.posing, isFalse);
    });

    test('keeps what the renderer knows each object by', () {
      final (:scene, :history, :bench) = rig();
      final key = scene['lamp']!.renderKey;
      ClipPreview().pose(scene, rising(), 'lamp', 0.5);
      expect(scene['lamp']!.renderKey, key);
      expect(scene.objects.first.id, 'lamp');
    });
  });
}

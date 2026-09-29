import 'dart:io';
import 'dart:math' as math;

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/cinematics_mode.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/clip_preview.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/game_view.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:vector_math/vector_math_64.dart';

// Cutscenes in the editor: cutting between cameras, framing a camera from
// the scene view, and a cutscene's shots and keys edited through the undo
// stack.

CutsceneShot shot(String camera, double start, double duration) =>
    CutsceneShot(camera: camera, start: start, duration: duration);

/// Each shot as camera, start and end, which reads more plainly in a
/// failure than the shots themselves.
List<(String, double, double)> spans(List<CutsceneShot> shots) => [
  for (final shot in shots) (shot.camera, shot.start, shot.end),
];

/// A cube and two cameras, one looking down -Z and one turned to look
/// down -X.
({EditorScene scene, History history, ClipBench bench}) rig({String? folder}) {
  final scene = EditorScene([
    SceneObject(id: 'cube', name: 'Cube', kind: ObjectKind.group),
    SceneObject(
      id: 'front',
      name: 'Front',
      kind: ObjectKind.camera,
      position: Vector3(0, 1, 5),
    ),
    SceneObject(
      id: 'side',
      name: 'Side',
      kind: ObjectKind.camera,
      position: Vector3(5, 1, 0),
      rotation: Vector3(0, 90, 0),
    ),
  ]);
  final workspace = Workspace(folder ?? '/project')
    ..add(SceneEntry(id: 'a', name: 'A', scene: scene));
  final history = History(workspace);
  final bench =
      ClipBench(history: history, scene: () => scene, playedOn: PlayedOn.scene)
        ..openCutscene(
          '${folder ?? '/project'}/intro.ocutscene',
          CutsceneDocument(motion: ClipDocument(name: 'intro', duration: 4)),
        );
  return (scene: scene, history: history, bench: bench);
}

void main() {
  group('cutting to a camera', () {
    test('into an empty cutscene holds to the end', () {
      expect(spans(cutTo(const [], camera: 'front', at: 1, length: 4)), [
        ('front', 1, 4),
      ]);
    });

    test('cuts the shot running at the playhead short', () {
      final shots = cutTo(
        [shot('front', 0, 4)],
        camera: 'side',
        at: 1.5,
        length: 4,
      );
      expect(spans(shots), [('front', 0, 1.5), ('side', 1.5, 4)]);
    });

    test('holds until the next shot starts', () {
      final shots = cutTo(
        [shot('front', 0, 1), shot('front', 3, 1)],
        camera: 'side',
        at: 1,
        length: 4,
      );
      expect(spans(shots), [('front', 0, 1), ('front', 3, 4), ('side', 1, 3)]);
    });

    test('takes the place of a shot that starts at the playhead', () {
      final shots = cutTo(
        [shot('front', 0, 2), shot('front', 2, 2)],
        camera: 'side',
        at: 2,
        length: 4,
      );
      expect(spans(shots), [('front', 0, 2), ('side', 2, 4)]);
    });

    test('at the very end still makes a shot with a length', () {
      final shots = cutTo(
        [shot('front', 0, 4)],
        camera: 'side',
        at: 4,
        length: 4,
      );
      expect(spans(shots), [('front', 0, 4), ('side', 4, 6)]);
    });
  });

  group('framing from the scene view', () {
    final view = OrbitCamera(
      yaw: 0.7,
      pitch: 0.3,
      distance: 8,
      target: Vector3(1, 2, 3),
    );

    /// Where [camera] is seen from and which way it looks.
    (Vector3, Vector3) seen(EditorScene scene, SceneObject camera) {
      final at = cameraOf(scene, camera);
      return (at.position, (at.target - at.position).normalized());
    }

    void expectSame(Vector3 actual, Vector3 expected) {
      expect(actual.distanceTo(expected), lessThan(1e-9), reason: '$actual');
    }

    test('a camera put where the view is sees what the view sees', () {
      final scene = EditorScene([]);
      final place = cameraPlaceFor(view, scene);
      final camera = SceneObject(
        id: 'eye',
        name: 'Eye',
        kind: ObjectKind.camera,
        position: place.position,
        rotation: place.rotation,
      );
      scene.add(camera);

      final wanted = view.toRenderCamera();
      final (position, forward) = seen(scene, camera);
      expectSame(position, wanted.position);
      expectSame(forward, (wanted.target - wanted.position).normalized());
      // A view cannot roll, and the angles are the ones typed rather than
      // another set that turns the same way.
      expect(place.rotation.z, 0);
      expect(place.rotation.y, closeTo(degrees(0.7), 1e-9));
    });

    test('looking through it puts the view back where it was', () {
      final scene = EditorScene([]);
      final place = cameraPlaceFor(view, scene);
      final camera = SceneObject(
        id: 'eye',
        name: 'Eye',
        kind: ObjectKind.camera,
        position: place.position,
        rotation: place.rotation,
      );
      scene.add(camera);

      final back = viewThrough(scene, camera);
      expect(back.yaw, closeTo(view.yaw, 1e-9));
      expect(back.pitch, closeTo(view.pitch, 1e-9));
      expectSame(back.toRenderCamera().position, place.position);
    });

    test('a camera inside something turned is placed in its terms', () {
      final scene = EditorScene([
        SceneObject(
          id: 'rig',
          name: 'Rig',
          kind: ObjectKind.group,
          position: Vector3(2, 0, -1),
          rotation: Vector3(0, 40, 10),
        ),
      ]);
      final place = cameraPlaceFor(view, scene, parent: 'rig');
      final camera = SceneObject(
        id: 'eye',
        name: 'Eye',
        kind: ObjectKind.camera,
        parentId: 'rig',
        position: place.position,
        rotation: place.rotation,
      );
      scene.add(camera);

      final wanted = view.toRenderCamera();
      final (position, forward) = seen(scene, camera);
      expectSame(position, wanted.position);
      expectSame(forward, (wanted.target - wanted.position).normalized());
    });

    test('looking straight down stops short of the pole', () {
      final scene = EditorScene([
        SceneObject(
          id: 'eye',
          name: 'Eye',
          kind: ObjectKind.camera,
          rotation: Vector3(-90, 0, 0),
        ),
      ]);
      final back = viewThrough(scene, scene['eye']!);
      expect(back.pitch, lessThan(math.pi / 2));
      expect(back.pitch, greaterThan(math.pi / 2 - 0.1));
    });
  });

  group('the finished shot', () {
    test('looks through the camera of the shot running', () {
      final (:scene, history: _, bench: _) = rig();
      final cutscene = CutsceneDocument(
        motion: ClipDocument(name: 'intro', duration: 4),
        shots: [shot('front', 0, 2), shot('side', 2, 2)],
      );

      final early = cutsceneCamera(scene, cutscene, 1)!;
      expect(early.position, cameraOf(scene, scene['front']!).position);
      final late = cutsceneCamera(scene, cutscene, 3)!;
      expect(late.position, cameraOf(scene, scene['side']!).position);
    });

    test('is halfway between two cameras halfway through an overlap', () {
      final (:scene, history: _, bench: _) = rig();
      final cutscene = CutsceneDocument(
        motion: ClipDocument(name: 'intro', duration: 4),
        shots: [shot('front', 0, 3), shot('side', 2, 2)],
      );
      final front = cameraOf(scene, scene['front']!).position;
      final side = cameraOf(scene, scene['side']!).position;

      final middle = cutsceneCamera(scene, cutscene, 2.5)!;
      expect(middle.position.distanceTo((front + side) / 2), lessThan(1e-9));
    });

    test('is nothing between shots or for a camera the scene lacks', () {
      final (:scene, history: _, bench: _) = rig();
      final cutscene = CutsceneDocument(
        motion: ClipDocument(name: 'intro', duration: 4),
        shots: [shot('front', 0, 1), shot('gone', 2, 2)],
      );
      expect(cutsceneCamera(scene, cutscene, 1.5), isNull);
      expect(cutsceneCamera(scene, cutscene, 3), isNull);
    });
  });

  group('a cutscene on the bench', () {
    test('keys things by their ids in the scene', () {
      final (:scene, :history, :bench) = rig();
      bench.key(scene['cube']!, 'transform.position');

      final channel = bench.clip!.channels.single;
      expect(channel.target, 'cube');
      // A cutscene plays on the scene, so nothing is chosen to play it on.
      expect(bench.owner, isNull);
      expect(history.labels, ['Key Cube position']);
    });

    test('keeps its shots in order, and undo puts them back', () {
      final (scene: _, :history, :bench) = rig();
      bench
        ..editShots('Add shot', (_) => [shot('side', 2, 2)])
        ..editShots('Add shot', (shots) => [...shots, shot('front', 0, 2)]);
      expect(spans(bench.shown!.shots!), [('front', 0, 2), ('side', 2, 4)]);
      expect(history.labels, ['Add shot', 'Add shot']);
      expect(bench.anyUnsaved, isTrue);

      history
        ..undo()
        ..undo();
      expect(bench.shown!.shots, isEmpty);
      expect(bench.anyUnsaved, isFalse);
    });

    test('one number typed into a shot is one step', () {
      final (scene: _, :history, :bench) = rig();
      bench.editShots('Add shot', (_) => [shot('front', 0, 1)]);
      final typing = Object();
      for (final length in [2.0, 2.5]) {
        bench.editShots(
          'Change shot length',
          (shots) => [shots.single.copyWith(duration: length)],
          gesture: typing,
        );
      }
      expect(history.labels, ['Add shot', 'Change shot length']);
      expect(bench.shown!.shots!.single.duration, 2.5);

      history.undo();
      expect(bench.shown!.shots!.single.duration, 1);
    });

    test('is saved with its shots and keys', () {
      final folder = Directory.systemTemp.createTempSync('cutscene_bench');
      addTearDown(() => folder.deleteSync(recursive: true));
      final (:scene, history: _, :bench) = rig(folder: folder.path);
      bench
        ..key(scene['cube']!, 'transform.position')
        ..editShots('Add shot', (_) => [shot('front', 0, 4)]);

      expect(bench.saveAll(), isEmpty);
      expect(bench.anyUnsaved, isFalse);
      final read = CutsceneDocument.decode(
        File('${folder.path}/intro.ocutscene').readAsStringSync(),
      ).cutscene;
      expect(spans(read.shots), [('front', 0, 4)]);
      expect(read.motion.channels.single.target, 'cube');
    });
  });

  group('posing the whole scene', () {
    ClipDocument rising() => ClipDocument(
      name: 'rise',
      duration: 1,
      channels: [
        ClipChannel<Vector3>(
          target: 'cube',
          property: 'transform.position',
          kind: ChannelKind.vector,
          keys: [
            Key(0, Vector3.zero(), hold: Hold.linear),
            Key(1, Vector3(0, 2, 0)),
          ],
        ),
      ],
    );

    test('moves things by their ids, and no clip puts them back', () {
      final (:scene, :history, bench: _) = rig();
      final preview = ClipPreview();

      preview.poseAll(scene, rising(), 0.5);
      expect(scene['cube']!.position.y, closeTo(1, 1e-9));
      expect(history.canUndo, isFalse);

      preview.poseAll(scene, null, 0.5);
      expect(scene['cube']!.position.y, 0);
      expect(preview.posing, isFalse);
    });
  });

  group('steering a camera', () {
    PlaceObject steer(EditorScene scene, Vector3 to) => PlaceObject(
      sceneId: 'a',
      id: 'front',
      name: 'Front',
      from: (
        position: scene['front']!.position,
        rotation: scene['front']!.rotation,
      ),
      to: (position: to, rotation: Vector3(10, 20, 0)),
    );

    test('moves and turns it as one step, however long it goes on', () {
      final (:scene, :history, bench: _) = rig();
      history
        ..run(steer(scene, Vector3(0, 1, 4)))
        ..run(steer(scene, Vector3(0, 1, 3)));

      expect(history.labels, ['Steer Front']);
      expect(scene['front']!.position, Vector3(0, 1, 3));
      expect(scene['front']!.rotation, Vector3(10, 20, 0));

      history.undo();
      expect(scene['front']!.position, Vector3(0, 1, 5));
      expect(scene['front']!.rotation, Vector3.zero());
    });
  });
}

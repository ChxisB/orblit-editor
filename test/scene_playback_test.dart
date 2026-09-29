import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_playback.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart';

ClipDocument bounce({WhenDone whenDone = WhenDone.loop}) => ClipDocument(
  name: 'Bounce',
  duration: 1,
  whenDone: whenDone,
  channels: [
    ClipChannel(
      target: '',
      property: 'transform.position',
      kind: ChannelKind.vector,
      keys: [Key(0, Vector3.zero()), Key(1, Vector3(0, 2, 0))],
    ),
  ],
);

void main() {
  test('autoplay animates a copy and includes objects in every scene', () {
    SceneObject cube(String id) => SceneObject(
      id: id,
      name: id,
      kind: ObjectKind.mesh,
      components: {
        doc.SceneComponents.motion: const doc.MotionComponent(
          clips: ['bounce.oclip'],
          autoplay: 'bounce.oclip',
        ),
      },
    );
    final editing = Workspace('/project')
      ..add(
        SceneEntry(
          id: 'main',
          name: 'Main',
          scene: EditorScene([cube('cube')]),
        ),
      );
    editing.sharedEntry.scene = EditorScene([cube('shared')]);
    final playback = ScenePlayback(editing, read: (_) => bounce());
    addTearDown(editing.dispose);
    addTearDown(playback.dispose);

    playback.advance(0.5);
    expect(
      playback.workspace.loaded!.scene!['cube']!.position.y,
      closeTo(1, 1e-9),
    );
    expect(playback.workspace.shared['shared']!.position.y, closeTo(1, 1e-9));
    expect(editing.loaded!.scene!['cube']!.position.y, 0);
    expect(editing.shared['shared']!.position.y, 0);
    playback.advance(1);
    expect(
      playback.workspace.loaded!.scene!['cube']!.position.y,
      closeTo(1, 1e-9),
    );
  });

  test('an unassigned autoplay is ignored and release restores the pose', () {
    final editing = Workspace('/project')
      ..add(
        SceneEntry(
          id: 'main',
          name: 'Main',
          scene: EditorScene([
            SceneObject(
              id: 'ignored',
              name: 'Ignored',
              kind: ObjectKind.mesh,
              components: {
                doc.SceneComponents.motion: const doc.MotionComponent(
                  clips: [],
                  autoplay: 'missing.oclip',
                ),
              },
            ),
            SceneObject(
              id: 'release',
              name: 'Release',
              kind: ObjectKind.mesh,
              components: {
                doc.SceneComponents.motion: const doc.MotionComponent(
                  clips: ['bounce.oclip'],
                  autoplay: 'bounce.oclip',
                ),
              },
            ),
          ]),
        ),
      );
    final read = <String>[];
    final playback = ScenePlayback(
      editing,
      read: (path) {
        read.add(path);
        return bounce(whenDone: WhenDone.release);
      },
    );
    addTearDown(editing.dispose);
    addTearDown(playback.dispose);
    expect(read, ['bounce.oclip']);
    playback.advance(0.5);
    expect(
      playback.workspace.loaded!.scene!['release']!.position.y,
      closeTo(1, 1e-9),
    );
    playback.advance(0.5);
    expect(playback.workspace.loaded!.scene!['release']!.position.y, 0);
  });
}

import 'package:orblit_filament/orblit_filament.dart' show OrblitCamera;
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import 'clip_preview.dart';
import 'game_view.dart';
import 'scene.dart';
import 'scene_document.dart';
import 'workspace.dart';

/// Animation on a copy of the scene, so playing never edits the document.
///
/// A mark named after one of [cutscenes] starts it, as it does in the game.
/// While it runs it moves the loaded scene and [camera] looks through its
/// shots.
final class ScenePlayback {
  ScenePlayback(
    Workspace editing, {
    required ClipDocument? Function(String) read,
    this.cutscenes = const {},
  }) : source = editing.loaded?.scene,
       workspace = Workspace(editing.projectDirectory) {
    final original = source;
    if (original != null) {
      final scene = _copy(original);
      workspace.add(SceneEntry(id: 'preview', name: scene.name, scene: scene));
      _load(scene, read);
    }
    workspace.sharedEntry.scene = _copy(editing.shared);
    _load(workspace.shared, read);
    advance(0);
  }

  final EditorScene? source;
  final Workspace workspace;

  /// The cutscenes a mark can start, by name.
  final Map<String, CutsceneDocument> cutscenes;

  final List<_PlayingClip> _clips = [];
  _PlayingCutscene? _cutscene;

  static EditorScene _copy(EditorScene scene) =>
      SceneDocument.decode(SceneDocument.encode(scene)).scene;

  /// What the game looks through now: the running cutscene's shots, or null
  /// for the scene's own camera.
  OrblitCamera? get camera {
    final running = _cutscene;
    final scene = workspace.loaded?.scene;
    if (running == null || scene == null) return null;
    return cutsceneCamera(scene, running.cutscene, running.player.at);
  }

  void _load(EditorScene scene, ClipDocument? Function(String) read) {
    for (final object in scene.objects) {
      final motion = object.components[doc.SceneComponents.motion];
      if (motion is! doc.MotionComponent) continue;
      final path = motion.autoplay;
      if (path == null || !motion.clips.contains(path)) continue;
      final clip = read(path);
      if (clip == null) continue;
      _clips.add(_PlayingClip(scene, object.id, ClipPlayer(clip)..play()));
    }
  }

  void advance(double seconds) {
    final marks = <Mark>[];
    for (final clip in _clips) {
      marks.addAll(clip.player.advance(seconds).marks);
      clip.preview.pose(
        clip.scene,
        clip.player.released ? null : clip.player.clip,
        clip.owner,
        clip.player.at,
      );
    }
    _advanceCutscene(seconds, marks);
  }

  /// Plays the running cutscene on, then starts the one named by the first
  /// mark passed, if any. A cutscene's own marks can start the next.
  void _advanceCutscene(double seconds, List<Mark> marks) {
    final scene = workspace.loaded?.scene;
    if (scene == null) return;
    if (_cutscene case final running?) {
      marks.addAll(running.player.advance(seconds).marks);
      running.show(scene);
      if (running.player.finished) _end(running, scene);
    }
    for (final mark in marks) {
      if (cutscenes[mark.name] case final cutscene?) {
        _start(cutscene, scene);
        return;
      }
    }
  }

  void _start(CutsceneDocument cutscene, EditorScene scene) {
    if (_cutscene case final running?) _end(running, scene);
    _cutscene = _PlayingCutscene(cutscene)..show(scene);
  }

  /// One that holds leaves the scene where it put it, and one that releases
  /// puts back everything it moved.
  void _end(_PlayingCutscene running, EditorScene scene) {
    _cutscene = null;
    if (running.player.whenDone == WhenDone.release) {
      running.preview.lift(scene);
    }
  }

  void dispose() => workspace.dispose();
}

final class _PlayingClip {
  _PlayingClip(this.scene, this.owner, this.player);

  final EditorScene scene;
  final String owner;
  final ClipPlayer player;
  final ClipPreview preview = ClipPreview();
}

final class _PlayingCutscene {
  _PlayingCutscene(this.cutscene)
    : player = ClipPlayer(cutscene.motion)..play();

  final CutsceneDocument cutscene;

  /// Plays the keys and the marks. The shots are read from [cutscene] at
  /// its playhead.
  final ClipPlayer player;

  final ClipPreview preview = ClipPreview();

  void show(EditorScene scene) =>
      preview.poseAll(scene, player.clip, player.at);
}

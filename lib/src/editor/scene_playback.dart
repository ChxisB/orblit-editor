import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import 'clip_preview.dart';
import 'scene.dart';
import 'scene_document.dart';
import 'workspace.dart';

/// Animation on a copy of the scene, so playing never edits the document.
final class ScenePlayback {
  ScenePlayback(
    Workspace editing, {
    required ClipDocument? Function(String) read,
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
  final List<_PlayingClip> _clips = [];

  static EditorScene _copy(EditorScene scene) =>
      SceneDocument.decode(SceneDocument.encode(scene)).scene;

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
    for (final clip in _clips) {
      clip.player.advance(seconds);
      clip.preview.pose(
        clip.scene,
        clip.player.released ? null : clip.player.clip,
        clip.owner,
        clip.player.at,
      );
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

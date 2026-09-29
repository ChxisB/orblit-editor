import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import 'clip_edits.dart';
import 'history.dart';
import 'scene.dart';
import 'scene_document.dart';

/// What the undo stack files a clip's changes under.
///
/// A clip is not a scene, but it is a document that changes and is saved, and
/// the stack already answers "has this changed since it was written" for
/// anything with an id. Prefixed so no scene can ever be given the same one.
String clipSceneId(String path) => 'clip:$path';

/// What a bench's documents are played on, which decides how a key names
/// the thing it moves.
enum PlayedOn {
  /// A clip, played on whichever object is chosen. Targets are named from
  /// that object, so one clip plays on every copy of a prefab.
  owner,

  /// A cutscene, played on the whole scene. Targets are the scene's own ids.
  scene,
}

/// A clip open on the timeline.
class OpenClip {
  OpenClip({
    required this.path,
    required this.clip,
    this.shots,
    this.sounds = const [],
  });

  final String path;

  /// As it is now, edits and all. Only ever replaced whole, by a [ClipEdit].
  ClipDocument clip;

  /// A cutscene's shots in the order they start, or null for a clip. Only
  /// ever replaced whole, by a [ShotEdit].
  List<CutsceneShot>? shots;

  /// A cutscene's sounds. Nothing here edits them yet, but saving must not
  /// drop what another tool wrote.
  final List<CutsceneSound> sounds;

  /// The undo stack's stamp for this clip when it was last written.
  int savedStamp = 0;

  String get id => clipSceneId(path);

  /// The cutscene as it is now, or null for a clip.
  CutsceneDocument? get cutscene => switch (shots) {
    final shots? => CutsceneDocument(
      motion: clip,
      shots: shots,
      sounds: sounds,
    ),
    null => null,
  };
}

/// What the inspector asks of whatever is keying, so a row can grow a key
/// button without knowing anything about clips.
abstract interface class Keying implements Listenable {
  /// How [property] of [object] looks from the clip, where the playhead is.
  KeyMark markFor(SceneObject object, String property);

  /// Keys [property] of [object] where the playhead is, as it is now.
  void key(SceneObject object, String property);
}

/// The clips being edited, and where the timeline is in the one it shows.
///
/// A notifier rather than state inside the timeline panel: the inspector's
/// key buttons, the preview on the scene and the panel all read it, and the
/// panel can be closed and opened again without the playhead going back to
/// the start.
class ClipBench extends ChangeNotifier implements Keying {
  ClipBench({
    required this.history,
    required this.scene,
    this.playedOn = PlayedOn.owner,
  });

  final History history;

  /// The scene a clip is played on, which is whichever one is loaded.
  final EditorScene? Function() scene;

  /// Whether this bench edits clips or cutscenes.
  final PlayedOn playedOn;

  final List<OpenClip> _open = [];

  List<OpenClip> get open => List.unmodifiable(_open);

  OpenClip? operator [](String path) {
    for (final one in _open) {
      if (one.path == path) return one;
    }
    return null;
  }

  String? _shown;

  /// The clip the timeline shows, or null.
  OpenClip? get shown => _shown == null ? null : this[_shown!];

  ClipDocument? get clip => shown?.clip;

  double _at = 0;

  /// Seconds into the shown clip.
  double get at => _at;

  set at(double value) {
    final clip = this.clip;
    final to = clip == null ? 0.0 : value.clamp(0.0, clip.duration);
    if (to == _at) return;
    _at = to.toDouble();
    notifyListeners();
  }

  /// The playhead on the nearest frame, which is where a key goes.
  double get frame => snapped(_at, clip?.rate ?? 0);

  String? _owner;

  /// The object the clip is played on, by id in the loaded scene.
  ///
  /// Chosen rather than worked out: the same clip plays on every copy of a
  /// prefab, and which copy the timeline shows is somebody's choice.
  String? get owner => _owner;

  set owner(String? id) {
    if (id == _owner) return;
    _owner = id;
    notifyListeners();
  }

  bool _playing = false;

  bool get playing => _playing;

  set playing(bool value) {
    if (value == _playing) return;
    _playing = value && clip != null;
    notifyListeners();
  }

  Set<KeyRef> _selection = const {};

  /// The keys selected on the timeline.
  Set<KeyRef> get selection => _selection;

  set selection(Set<KeyRef> keys) {
    if (setEquals(keys, _selection)) return;
    _selection = Set.unmodifiable(keys);
    notifyListeners();
  }

  ChannelAddress? _curve;

  /// The channel the curve view draws.
  ChannelAddress? get curve => _curve;

  set curve(ChannelAddress? address) {
    if (address == _curve) return;
    _curve = address;
    notifyListeners();
  }

  // ---- opening and saving ----

  /// Shows the clip at [path], reading it first if it is not open, and says
  /// what could not be read.
  ///
  /// Throws a [ClipFormatException], a [CutsceneFormatException] or a
  /// [FileSystemException] for a file that cannot be opened at all.
  List<String> openFile(String path) {
    if (this[path] != null) {
      show(path);
      return const [];
    }
    final text = File(path).readAsStringSync();
    switch (playedOn) {
      case PlayedOn.owner:
        final load = ClipDocument.decode(text);
        openClip(path, load.clip);
        return load.problems;
      case PlayedOn.scene:
        final load = CutsceneDocument.decode(text);
        openCutscene(path, load.cutscene);
        return load.problems;
    }
  }

  /// Shows [clip] as the one at [path].
  void openClip(String path, ClipDocument clip) {
    if (this[path] == null) _open.add(OpenClip(path: path, clip: clip));
    show(path);
  }

  /// Shows [cutscene] as the one at [path].
  void openCutscene(String path, CutsceneDocument cutscene) {
    if (this[path] == null) {
      _open.add(
        OpenClip(
          path: path,
          clip: cutscene.motion,
          shots: cutscene.shots,
          sounds: cutscene.sounds,
        ),
      );
    }
    show(path);
  }

  void show(String path) {
    if (_shown == path || this[path] == null) return;
    _shown = path;
    _selection = const {};
    _curve = null;
    _playing = false;
    _at = _at.clamp(0, this[path]!.clip.duration).toDouble();
    notifyListeners();
  }

  /// Clears the preview without discarding any open clip or its edits.
  void hide() {
    _shown = null;
    _owner = null;
    _playing = false;
    _selection = const {};
    _curve = null;
    notifyListeners();
  }

  /// Closes the clip at [path], and forgets its steps on the undo stack.
  void close(String path) {
    final closing = this[path];
    if (closing == null) return;
    _open.remove(closing);
    history.forget(closing.id);
    if (_shown == path) {
      _shown = _open.lastOrNull?.path;
      _selection = const {};
      _curve = null;
      _playing = false;
    }
    notifyListeners();
  }

  bool isUnsaved(OpenClip clip) =>
      history.stampFor(clip.id) != clip.savedStamp;

  bool get anyUnsaved => _open.any(isUnsaved);

  /// Writes every clip with changes, and says which could not be written.
  List<String> saveAll() => [
    for (final one in _open)
      if (isUnsaved(one)) ?save(one),
  ];

  /// Writes [clip], or says why it could not be.
  String? save(OpenClip clip) {
    try {
      File(
        clip.path,
      ).writeAsStringSync(clip.cutscene?.encode() ?? clip.clip.encode());
    } on FileSystemException catch (error) {
      return '${clip.clip.name} could not be saved: ${error.message}';
    }
    clip.savedStamp = history.stampFor(clip.id);
    notifyListeners();
    return null;
  }

  // ---- editing ----

  /// Changes the shown clip through the undo stack.
  ///
  /// [keys] is the selection afterwards, and is the one before when not
  /// given. A [gesture] ties a drag's worth of these into one step; say
  /// [onlyMoves] when all it does is move keys or bend a curve, never when
  /// one comes or goes.
  void edit(
    String label,
    ClipDocument Function(ClipDocument clip) change, {
    Set<KeyRef>? keys,
    Object? gesture,
    bool onlyMoves = false,
  }) {
    final shown = this.shown;
    if (shown == null) return;
    final to = change(shown.clip);
    if (identical(to, shown.clip) && keys == null) return;
    history.run(
      ClipEdit(
        bench: this,
        path: shown.path,
        label: label,
        from: shown.clip,
        to: to,
        keysBefore: _selection,
        keysAfter: keys ?? _selection,
        gesture: gesture,
        onlyMoves: onlyMoves,
      ),
    );
  }

  /// Where a [ClipEdit] puts a clip, forwards or back.
  ///
  /// Shows the clip it puts, so undoing a step in a clip somebody has moved
  /// away from is not a change they cannot see.
  void _put(String path, ClipDocument clip, Set<KeyRef> keys) {
    final open = this[path];
    if (open == null) return;
    open.clip = clip;
    if (_shown != path) {
      _shown = path;
      _playing = false;
      _curve = null;
    }
    _selection = Set.unmodifiable(keys);
    _at = _at.clamp(0, clip.duration).toDouble();
    if (_curve case final curve? when channelAt(clip, curve) == null) {
      _curve = null;
    }
    notifyListeners();
  }

  /// Changes the shown cutscene's shots through the undo stack. A [gesture]
  /// ties a typed number's worth of these into one step.
  void editShots(
    String label,
    List<CutsceneShot> Function(List<CutsceneShot> shots) change, {
    Object? gesture,
  }) {
    final shown = this.shown;
    final shots = shown?.shots;
    if (shown == null || shots == null) return;
    history.run(
      ShotEdit(
        bench: this,
        path: shown.path,
        label: label,
        from: shots,
        to: List<CutsceneShot>.unmodifiable(
          <CutsceneShot>[...change(shots)]
            ..sort((a, b) => a.start.compareTo(b.start)),
        ),
        gesture: gesture,
      ),
    );
  }

  /// Where a [ShotEdit] puts a cutscene's shots, forwards or back.
  void _putShots(String path, List<CutsceneShot> shots) {
    final open = this[path];
    if (open == null) return;
    open.shots = shots;
    if (_shown != path) {
      _shown = path;
      _playing = false;
      _curve = null;
    }
    notifyListeners();
  }

  // ---- keying from the inspector ----

  @override
  KeyMark markFor(SceneObject object, String property) {
    final clip = this.clip;
    final address = _addressOf(object, property);
    if (clip == null || address == null) return KeyMark.none;
    if (channelAt(clip, address) == null &&
        kindOfField(_fieldOf(object, property)) == null) {
      return KeyMark.none;
    }
    return markOf(clip, address, frame);
  }

  @override
  void key(SceneObject object, String property) {
    final clip = this.clip;
    final address = _addressOf(object, property);
    if (clip == null || address == null) return;
    final raw = _fieldOf(object, property);
    final kind = channelAt(clip, address)?.kind ?? kindOfField(raw);
    if (kind == null) return;
    final value = keyValueOf(kind, property, raw);
    if (value == null) return;

    // Keying something with nothing chosen to play on makes it the one: the
    // first key is where most clips start, and asking first would be a
    // question with one sensible answer. A cutscene plays on the scene.
    if (playedOn == PlayedOn.owner && _ownerIn(scene()) == null) {
      _owner = object.id;
    }
    final at = frame;
    edit(
      'Key ${object.name} ${_labelOf(property)}',
      (clip) => keyed(clip, address, kind, at, value),
    );
  }

  /// The channel [property] of [object] would be keyed on, or null when the
  /// clip, played where it is, has no way to name it.
  ChannelAddress? _addressOf(SceneObject object, String property) {
    final scene = this.scene();
    if (scene == null) return null;
    final target = switch ((playedOn, _ownerIn(scene))) {
      (PlayedOn.scene, _) => object.id,
      (PlayedOn.owner, null) => '',
      (PlayedOn.owner, final owner?) => _scopeIn(
        scene,
        owner,
      ).targetOf(object.id),
    };
    if (target == null) return null;
    return (target: target, bone: null, property: property);
  }

  /// What [target] names where the clip is played, or null when the loaded
  /// scene has nothing by that name, or nothing is chosen to play it on.
  SceneObject? objectOf(String target) {
    final scene = this.scene();
    if (scene == null) return null;
    return switch ((playedOn, _ownerIn(scene))) {
      (PlayedOn.scene, _) => scene[target],
      (PlayedOn.owner, null) => null,
      (PlayedOn.owner, final owner?) =>
        scene[_scopeIn(scene, owner).resolve(target)],
    };
  }

  static ClipScope _scopeIn(EditorScene scene, String owner) =>
      ClipScope.inScene(
        doc.SceneDocument(entities: [SceneDocument.entityOf(scene[owner]!)]),
        owner,
      );

  /// The owner, when the loaded scene has it.
  String? _ownerIn(EditorScene? scene) {
    final owner = _owner;
    if (owner == null || scene == null || !scene.contains(owner)) return null;
    return owner;
  }

  /// [property] of [object] as the scene writes it.
  static Object? _fieldOf(SceneObject object, String property) {
    final dot = property.indexOf('.');
    if (dot <= 0) return null;
    final component = SceneDocument.entityOf(
      object,
    )[property.substring(0, dot)];
    return component?.toJson()[property.substring(dot + 1)];
  }

  static String _labelOf(String property) {
    final dot = property.indexOf('.');
    return dot < 0 ? property : property.substring(dot + 1);
  }
}

/// One change to a clip.
///
/// Holds the clip from before and the clip from after, which are both whole
/// and neither ever changed, so undoing is putting one back.
class ClipEdit extends EditorCommand {
  ClipEdit({
    required this.bench,
    required this.path,
    required this.label,
    required this.from,
    required this.to,
    required this.keysBefore,
    required this.keysAfter,
    this.gesture,
    this.onlyMoves = false,
  });

  final ClipBench bench;

  final String path;

  @override
  final String label;

  final ClipDocument from;

  /// Not final: a merged drag rewrites where it ends up.
  ClipDocument to;

  final Set<KeyRef> keysBefore;

  Set<KeyRef> keysAfter;

  /// What ties a drag's worth of these together, or null for one alone.
  final Object? gesture;

  @override
  final bool onlyMoves;

  @override
  String get sceneId => clipSceneId(path);

  @override
  Object? get mergeKey => gesture == null ? null : (path, gesture);

  @override
  void apply(SceneHost host) => bench._put(path, to, keysAfter);

  @override
  void revert(SceneHost host) => bench._put(path, from, keysBefore);

  @override
  void absorb(EditorCommand later) {
    if (later is! ClipEdit) return;
    to = later.to;
    keysAfter = later.keysAfter;
  }
}

/// One change to a cutscene's shots, holding them whole from before and
/// after the way a [ClipEdit] holds a clip.
final class ShotEdit extends EditorCommand {
  ShotEdit({
    required this.bench,
    required this.path,
    required this.label,
    required this.from,
    required this.to,
    this.gesture,
  });

  final ClipBench bench;

  final String path;

  @override
  final String label;

  final List<CutsceneShot> from;

  /// Not final: a merged run of typing rewrites where it ends up.
  List<CutsceneShot> to;

  /// What ties a run of these together, or null for one alone.
  final Object? gesture;

  @override
  String get sceneId => clipSceneId(path);

  @override
  Object? get mergeKey => gesture == null ? null : (path, 'shots', gesture);

  @override
  void apply(SceneHost host) => bench._putShots(path, to);

  @override
  void revert(SceneHost host) => bench._putShots(path, from);

  @override
  void absorb(EditorCommand later) {
    if (later is ShotEdit) to = later.to;
  }
}

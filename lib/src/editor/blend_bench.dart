import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:orblit_motion/orblit_motion.dart';

import 'history.dart';

/// What the undo stack files a blend's changes under.
///
/// Prefixed so no scene or clip can ever be given the same one.
String blendSceneId(String path) => 'blend:$path';

/// What is chosen in a blend's graph, which is what the inspector shows.
sealed class BlendPick {
  const BlendPick();
}

/// Nothing is chosen, and the inspector shows the blend itself.
final class NothingPicked extends BlendPick {
  const NothingPicked();

  @override
  bool operator ==(Object other) => other is NothingPicked;

  @override
  int get hashCode => 0;
}

/// A state, by the name a file writes it under.
final class StatePicked extends BlendPick {
  const StatePicked(this.name);

  final String name;

  @override
  bool operator ==(Object other) => other is StatePicked && other.name == name;

  @override
  int get hashCode => Object.hash(StatePicked, name);
}

/// A change, by where it stands in [BlendDocument.writtenChanges].
final class ChangePicked extends BlendPick {
  const ChangePicked(this.index);

  final int index;

  @override
  bool operator ==(Object other) =>
      other is ChangePicked && other.index == index;

  @override
  int get hashCode => Object.hash(ChangePicked, index);
}

/// A blend open in the graph.
class OpenBlend {
  OpenBlend({required this.path, required this.blend});

  final String path;

  /// As it is now, edits and all. Only ever replaced whole, by a
  /// [BlendEdit].
  BlendDocument blend;

  /// The undo stack's stamp for this blend when it was last written.
  int savedStamp = 0;

  String get id => blendSceneId(path);
}

/// The blends being edited, and what is chosen in the one the graph shows.
///
/// A notifier rather than state inside the panel, for the reason the clip
/// bench is: the panel can be closed and opened again, and the shell asks
/// whether anything is unsaved without knowing about widgets.
class BlendBench extends ChangeNotifier {
  BlendBench({required this.history});

  final History history;

  final List<OpenBlend> _open = [];

  List<OpenBlend> get open => List.unmodifiable(_open);

  OpenBlend? operator [](String path) {
    for (final one in _open) {
      if (one.path == path) return one;
    }
    return null;
  }

  String? _shown;

  /// The blend the graph shows, or null.
  OpenBlend? get shown => _shown == null ? null : this[_shown!];

  BlendDocument? get blend => shown?.blend;

  BlendPick _pick = const NothingPicked();

  /// What is chosen in the shown blend.
  BlendPick get pick => _pick;

  set pick(BlendPick value) {
    if (value == _pick) return;
    _pick = value;
    notifyListeners();
  }

  /// Shows the blend at [path], reading it first if it is not open, and says
  /// what could not be read.
  ///
  /// Throws a [BlendFormatException] or a [FileSystemException] for a file
  /// that cannot be opened at all.
  List<String> openFile(String path) {
    if (this[path] != null) {
      show(path);
      return const [];
    }
    final load = BlendDocument.decode(File(path).readAsStringSync());
    openBlend(path, load.blend);
    return load.problems;
  }

  /// Shows [blend] as the one at [path].
  void openBlend(String path, BlendDocument blend) {
    if (this[path] == null) _open.add(OpenBlend(path: path, blend: blend));
    show(path);
  }

  void show(String path) {
    if (_shown == path || this[path] == null) return;
    _shown = path;
    _pick = const NothingPicked();
    notifyListeners();
  }

  /// Closes the blend at [path], and forgets its steps on the undo stack.
  void close(String path) {
    final closing = this[path];
    if (closing == null) return;
    _open.remove(closing);
    history.forget(closing.id);
    if (_shown == path) {
      _shown = _open.lastOrNull?.path;
      _pick = const NothingPicked();
    }
    notifyListeners();
  }

  bool isUnsaved(OpenBlend blend) =>
      history.stampFor(blend.id) != blend.savedStamp;

  bool get anyUnsaved => _open.any(isUnsaved);

  /// Writes every blend with changes, and says which could not be written.
  List<String> saveAll() => [
    for (final one in _open)
      if (isUnsaved(one)) ?save(one),
  ];

  /// Writes [blend], or says why it could not be.
  String? save(OpenBlend blend) {
    try {
      File(blend.path).writeAsStringSync(blend.blend.encode());
    } on FileSystemException catch (error) {
      return '${blend.blend.name} could not be saved: ${error.message}';
    }
    blend.savedStamp = history.stampFor(blend.id);
    notifyListeners();
    return null;
  }

  /// Changes the shown blend through the undo stack.
  ///
  /// [pick] is what is chosen afterwards, and is what was chosen before when
  /// not given. A [gesture] ties a typed number's worth of these into one
  /// step. A change that answers the blend it was given changes nothing and
  /// is not a step.
  void edit(
    String label,
    BlendDocument Function(BlendDocument blend) change, {
    BlendPick? pick,
    Object? gesture,
  }) {
    final shown = this.shown;
    if (shown == null) return;
    final to = change(shown.blend);
    if (identical(to, shown.blend)) return;
    history.run(
      BlendEdit(
        bench: this,
        path: shown.path,
        label: label,
        from: shown.blend,
        to: to,
        pickBefore: _pick,
        pickAfter: pick ?? _pick,
        gesture: gesture,
      ),
    );
  }

  /// Where a [BlendEdit] puts a blend, forwards or back.
  ///
  /// Shows the blend it puts, so undoing a step in one somebody has moved
  /// away from is not a change they cannot see.
  void _put(String path, BlendDocument blend, BlendPick pick) {
    final open = this[path];
    if (open == null) return;
    open.blend = blend;
    _shown = path;
    _pick = _stillThere(blend, pick);
    notifyListeners();
  }

  static BlendPick _stillThere(BlendDocument blend, BlendPick pick) =>
      switch (pick) {
        NothingPicked() => pick,
        StatePicked(:final name) =>
          blend.writtenStates.any((state) => state.name == name)
              ? pick
              : const NothingPicked(),
        ChangePicked(:final index) =>
          index >= 0 && index < blend.writtenChanges.length
              ? pick
              : const NothingPicked(),
      };
}

/// One change to a blend.
///
/// Holds the blend from before and the blend from after, which are both
/// whole and neither ever changed, so undoing is putting one back.
final class BlendEdit extends EditorCommand {
  BlendEdit({
    required this.bench,
    required this.path,
    required this.label,
    required this.from,
    required this.to,
    required this.pickBefore,
    required this.pickAfter,
    this.gesture,
  });

  final BlendBench bench;

  final String path;

  @override
  final String label;

  final BlendDocument from;

  /// Not final: a merged run of typing rewrites where it ends up.
  BlendDocument to;

  final BlendPick pickBefore;

  BlendPick pickAfter;

  /// What ties a run of these together, or null for one alone.
  final Object? gesture;

  @override
  String get sceneId => blendSceneId(path);

  @override
  Object? get mergeKey => gesture == null ? null : (path, 'blend', gesture);

  @override
  void apply(SceneHost host) => bench._put(path, to, pickAfter);

  @override
  void revert(SceneHost host) => bench._put(path, from, pickBefore);

  @override
  void absorb(EditorCommand later) {
    if (later is! BlendEdit) return;
    to = later.to;
    pickAfter = later.pickAfter;
  }
}

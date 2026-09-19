import 'dart:async';
import 'dart:io';
import 'dart:isolate';

import 'package:flutter/material.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';

/// Where every asset in a project stands with the cook: already cooked for
/// this machine, waiting to be cooked, or known not to cook at all.
///
/// The browser draws a mark from this beside each file. The point of the mark
/// is to answer, without leaving the editor, the question somebody otherwise
/// answers by running a build and reading the log — and the point of *this*
/// class is that the answer comes from the cook rather than from the editor's
/// own idea of one. It builds a [CookProject] over the project's real assets
/// folder, with the real cache and the real importers, and asks it. An editor
/// that worked it out another way would drift from the build the first time
/// an importer changed how it resolves a setting, and a green mark on an
/// asset a build has everything still to do is worse than no mark at all.
class CookStatusIndex extends ChangeNotifier {
  CookStatusIndex({
    required this.projectDirectory,
    this.cachePath,
    CookTarget? target,
  }) : target = target ?? currentCookTarget;

  /// The project folder, the same one the asset browser is rooted at.
  final String projectDirectory;

  /// The machine being asked about, which is this one. A build for a phone
  /// would say something different about the same files, and saying *that*
  /// here would be a mark about a build nobody is running.
  final CookTarget target;

  /// Where cooked bytes are kept, or null for the machine's own cache —
  /// which is the ordinary case, and the point: the editor should be looking
  /// at the same cache the build fills, not one of its own that always
  /// disagrees with it.
  final String? cachePath;

  /// The folder the cook reads. Only what is under here is cooked at all, so
  /// a scene or a script gets no mark rather than a bad one.
  String get assetsPath => p.join(projectDirectory, 'assets');

  /// Where a build would write its bundle. Nothing is written while only
  /// asking, but naming the real place keeps this from being a lie the day
  /// the editor runs the cook itself.
  String get outPath => p.join(projectDirectory, 'build', 'assets');

  /// By absolute path, because that is what the browser has in its hand. The
  /// cook works in ids relative to the assets folder, and the two are
  /// converted here rather than at every tile.
  Map<String, CookState> _states = const {};

  bool _asking = false;
  bool _askAgain = false;
  bool _held = false;
  String? _problem;

  /// Whether an answer is being worked out. The browser says so, because
  /// hashing a large project takes long enough to look like nothing
  /// happening.
  bool get asking => _asking;

  /// What went wrong the last time this asked, or null.
  ///
  /// Kept rather than thrown: failing to *describe* the assets should never
  /// take the browser away from somebody who was using it to find a file.
  String? get problem => _problem;

  /// Whether anything is known at all yet.
  bool get ready => _states.isNotEmpty || !_asking;

  /// How [path] stands, or null when it is not something the cook looks at —
  /// a folder, a scene, anything outside the assets folder.
  CookState? operator [](String path) => _states[p.normalize(path)];

  /// How many assets are in each state, for a one-line summary.
  Map<CookState, int> get counts {
    final counted = <CookState, int>{};
    for (final state in _states.values) {
      counted[state] = (counted[state] ?? 0) + 1;
    }
    return counted;
  }

  /// Asks again, off the interface thread.
  ///
  /// Two calls at once do not make two isolates: the second sets a flag and
  /// the first goes round again when it lands. A file watcher firing while a
  /// large project is being hashed is the ordinary case, not the odd one, and
  /// the interesting answer is the one after the last change rather than the
  /// one after the first.
  Future<void> refresh() async {
    if (_held) return;
    if (_asking) {
      _askAgain = true;
      return;
    }

    _asking = true;
    notifyListeners();
    try {
      do {
        _askAgain = false;
        final answered = await _ask(
          assetsPath,
          outPath,
          target.name,
          cachePath,
        );
        _problem = answered.problem;
        _states = {
          for (final entry in answered.states.entries)
            _pathOf(entry.key): entry.value,
        };
      } while (_askAgain);
    } finally {
      _asking = false;
      notifyListeners();
    }
  }

  /// An id back to the file it names. Ids are always separated by `/`,
  /// whatever this machine spells a path with, so the parts are split apart
  /// and rejoined rather than pasted on.
  String _pathOf(AssetId id) =>
      p.normalize(p.join(assetsPath, p.joinAll(id.toString().split('/'))));

  /// Puts an answer in without asking for one.
  ///
  /// Only for tests. Everything a person sees should come from [refresh],
  /// because the whole value of these marks is that they were worked out by
  /// the cook rather than by something that looked like it.
  ///
  /// Holding an answer also stops this asking for one. A test that put an
  /// answer in wants that answer drawn, not one worked out a moment later
  /// from a folder the test never made.
  @visibleForTesting
  void hold(Map<String, CookState> states) {
    _held = true;
    _states = {
      for (final entry in states.entries) p.normalize(entry.key): entry.value,
    };
    notifyListeners();
  }

  /// Forgets everything, for a project being closed.
  void clear() {
    _states = const {};
    _held = false;
    _problem = null;
    notifyListeners();
  }
}

/// What one run of the question came back with.
class _Answer {
  const _Answer(this.states, this.problem);

  final Map<AssetId, CookState> states;
  final String? problem;
}

/// The question, asked in another isolate.
///
/// In another isolate because answering it reads and hashes every asset in
/// the project and everything each one depends on. That is cheap as work goes
/// and ruinous as an interruption: a folder of 4K textures is a second or two
/// of sha256, and a second or two on the interface thread is an editor that
/// has stopped.
///
/// Only strings cross, and only a map of names comes back. The cook is plain
/// Dart with no plugin in it, so there is nothing here that needs the main
/// isolate; keeping the message to strings means nothing has to stay sendable
/// as the cook grows.
Future<_Answer> _ask(
  String assetsPath,
  String outPath,
  String targetName,
  String? cachePath,
) => Isolate.run(() async {
  if (!Directory(assetsPath).existsSync()) {
    // A project made before there were assets, or one somebody has
    // emptied. Nothing to say about no files, and nothing wrong either.
    return const _Answer({}, null);
  }

  final target = CookTargets.find(targetName);
  if (target == null) {
    return _Answer(const {}, 'There is no cook target called $targetName.');
  }

  try {
    final states = await CookProject(
      assetsPath: assetsPath,
      outPath: outPath,
      target: target,
      cachePath: cachePath,
    ).statesOf();
    return _Answer(states, null);
  } catch (error) {
    return _Answer(const {}, '$error');
  }
});

/// How a cook state reads to somebody looking at a folder of files.
///
/// Here rather than in the browser so that the panel, the preview and the
/// tooltip all call the same state the same thing. Two places calling `stale`
/// two things is how a person ends up believing they are two states.
extension CookStateLook on CookState {
  /// What to call it. Plain words: somebody reading a tooltip wants to know
  /// what to do about the file, not what the pipeline calls the condition.
  String get label => switch (this) {
    CookState.cooked => 'Cooked',
    CookState.stale => 'Needs cooking',
    CookState.failed => 'Would not cook',
    CookState.ignored => 'Nothing to cook',
  };

  /// A longer line, for where there is room to say why.
  String get explanation => switch (this) {
    CookState.cooked => 'The cache already has this for this machine.',
    CookState.stale => 'It has changed, or has never been cooked.',
    CookState.failed => 'A cook tried this and the importer could not do it.',
    CookState.ignored => 'No importer claims this kind of file.',
  };

  /// The dot's colour, or null for the states not worth a dot.
  ///
  /// [CookState.ignored] gets none. A README is not a problem and never will
  /// be, and a mark on every file that is fine is a panel of marks that
  /// nobody reads the important one in.
  Color? get mark => switch (this) {
    CookState.cooked => OrblitColors.good,
    CookState.stale => OrblitColors.warn,
    CookState.failed => OrblitColors.bad,
    CookState.ignored => null,
  };
}

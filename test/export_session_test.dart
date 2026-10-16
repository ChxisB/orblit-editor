import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/editor/export_folder.dart';
import 'package:orblit_editor/src/editor/export_run.dart';
import 'package:orblit_editor/src/editor/export_session.dart';
import 'package:orblit_editor/src/editor/pack_contents.dart';

ExportedPack _pack(
  String name, {
  String target = 'macos',
  String? base,
  int assets = 3,
}) => ExportedPack(
  path: '/project/exports/$name',
  target: target,
  hash: 'hash-of-$name',
  base: base,
  assets: assets,
  removed: 0,
  bytes: 2048,
  modified: DateTime(2026, 10, 15),
);

TargetExport _packed(String target) =>
    TargetExport(target: target, outcome: ExportOutcome.packed);

/// A runner that waits for the test before landing each target, so the
/// session can be looked at between them.
final class _Gate {
  final _landing = <Completer<void>>[];
  final requests = <ExportRequest>[];

  Future<List<TargetExport>> run(
    ExportRequest request, {
    void Function(TargetExport done)? onTarget,
  }) async {
    requests.add(request);
    final done = <TargetExport>[];
    for (final target in request.targets) {
      final gate = Completer<void>();
      _landing.add(gate);
      await gate.future;
      final result = _packed(target);
      done.add(result);
      onTarget?.call(result);
    }
    return done;
  }

  void land(int index) => _landing[index].complete();
}

void main() {
  late List<ExportedPack> held;
  late int listed;

  Future<ExportListing> lister(String directory) async {
    listed++;
    return ExportListing(packs: held);
  }

  ExportSession session(
    ExportRunner runner, {
    Future<PackContents> Function(String, String)? reader,
  }) => ExportSession(
    projectDirectory: '/project',
    runner: runner,
    lister: lister,
    reader:
        reader ??
        (_, _) async => const PackContents(packs: [], rows: [], removed: []),
    cachePath: '/cache',
  );

  setUp(() {
    held = [];
    listed = 0;
  });

  /// Chooses exactly [names], whatever the machine started on.
  void choose(ExportSession s, List<String> names) {
    for (final name in s.targets.toList()) {
      s.toggleTarget(name);
    }
    names.forEach(s.toggleTarget);
  }

  group('choosing', () {
    test('starts on the machine\'s own target', () {
      final s = session(_Gate().run);
      addTearDown(s.dispose);

      expect(s.targets, [currentCookTarget.name]);
      expect(s.canExport, isTrue);
    });

    test('lists targets in the order Orblit names them', () {
      final s = session(_Gate().run);
      addTearDown(s.dispose);

      choose(s, ['web', 'android', 'macos']);

      expect(s.targets, ['macos', 'android', 'web']);
    });

    test('with no target chosen there is nothing to export', () {
      final s = session(_Gate().run);
      addTearDown(s.dispose);

      choose(s, []);

      expect(s.targets, isEmpty);
      expect(s.canExport, isFalse);
    });

    test('a patch is for its base\'s target and no other', () {
      final s = session(_Gate().run);
      addTearDown(s.dispose);
      choose(s, ['macos', 'web']);

      s.choosePatchOf(_pack('game.android.opack', target: 'android'));
      expect(s.targets, ['android']);

      s.choosePatchOf(null);
      expect(s.targets, ['macos', 'web']);
    });
  });

  group('running', () {
    test('lands each target as it finishes', () async {
      final gate = _Gate();
      final s = session(gate.run);
      addTearDown(s.dispose);
      choose(s, ['macos', 'android']);

      final done = s.start();
      await pumpEventQueue();

      expect(s.running, isTrue);
      expect(s.cooking, 'macos');
      expect(s.canExport, isFalse);

      gate.land(0);
      await pumpEventQueue();
      expect(s.results.map((r) => r.target), ['macos']);
      expect(s.cooking, 'android');

      gate.land(1);
      await done;

      expect(s.results.map((r) => r.target), ['macos', 'android']);
      expect(s.running, isFalse);
      expect(s.cooking, isNull);
    });

    test('hands the runner the base and the cache', () async {
      final gate = _Gate();
      final s = session(gate.run);
      addTearDown(s.dispose);
      final base = _pack('game.macos.opack');
      held = [base];
      await s.load();
      s.choosePatchOf(base);

      final done = s.start();
      await pumpEventQueue();
      gate.land(0);
      await done;

      expect(gate.requests.single.patchOf, base.path);
      expect(gate.requests.single.cachePath, '/cache');
      expect(gate.requests.single.projectDirectory, '/project');
      expect(gate.requests.single.targets, ['macos']);
    });

    test('a second start while one runs does nothing', () async {
      final gate = _Gate();
      final s = session(gate.run);
      addTearDown(s.dispose);

      final first = s.start();
      await pumpEventQueue();
      await s.start();
      gate.land(0);
      await first;

      expect(gate.requests, hasLength(1));
    });

    test('reads the folder again when it is done', () async {
      final gate = _Gate();
      final s = session(gate.run);
      addTearDown(s.dispose);
      await s.load();
      final before = listed;
      held = [_pack('game.macos.opack')];

      final done = s.start();
      await pumpEventQueue();
      gate.land(0);
      await done;

      expect(listed, before + 1);
      expect(s.listing.packs, hasLength(1));
    });

    test('lets go of the running state when the runner throws', () async {
      final s = session((request, {onTarget}) async => throw StateError('no'));
      addTearDown(s.dispose);

      await expectLater(s.start(), throwsStateError);

      expect(s.running, isFalse);
      expect(s.cooking, isNull);
      expect(s.canExport, isTrue);
    });
  });

  group('the folder', () {
    test('drops a base that is no longer there', () async {
      final base = _pack('game.macos.opack');
      held = [base];
      final s = session(_Gate().run);
      addTearDown(s.dispose);
      await s.load();
      s.choosePatchOf(base);

      held = [];
      await s.load();

      expect(s.patchOf, isNull);
    });

    test('keeps a base that is still there', () async {
      final base = _pack('game.macos.opack');
      held = [base];
      final s = session(_Gate().run);
      addTearDown(s.dispose);
      await s.load();
      s.choosePatchOf(base);

      await s.load();

      expect(s.patchOf, base);
    });
  });

  group('the contents', () {
    test('reads what a pack serves', () async {
      final pack = _pack('game.macos.opack');
      const contents = PackContents(
        packs: ['game.macos.opack'],
        rows: [],
        removed: [],
      );
      final s = session(
        _Gate().run,
        reader: (directory, path) async {
          expect(directory, '/project');
          expect(path, pack.path);
          return contents;
        },
      );
      addTearDown(s.dispose);

      await s.show(pack);

      expect(s.shown, pack);
      expect(s.contents, contents);
      expect(s.contentsProblem, isNull);
    });

    test('says why a broken chain cannot be read', () async {
      final pack = _pack('game.macos.abc.opack', base: 'missing');
      final s = session(
        _Gate().run,
        reader: (_, _) async =>
            throw const ExportChainBroken('no pack is that one'),
      );
      addTearDown(s.dispose);

      await s.show(pack);

      expect(s.contents, isNull);
      expect(s.contentsProblem, 'no pack is that one');
    });

    test(
      'a slow read for an earlier pack does not replace a later one',
      () async {
        final slow = Completer<PackContents>();
        final first = _pack('game.macos.opack');
        final second = _pack('game.macos.abc.opack');
        const answer = PackContents(packs: ['second'], rows: [], removed: []);
        final s = session(
          _Gate().run,
          reader: (_, path) =>
              path == first.path ? slow.future : Future.value(answer),
        );
        addTearDown(s.dispose);

        final reading = s.show(first);
        await s.show(second);
        slow.complete(
          const PackContents(packs: ['first'], rows: [], removed: []),
        );
        await reading;

        expect(s.shown, second);
        expect(s.contents, answer);
      },
    );
  });

  test('a read that lands after dispose tells nobody', () async {
    final slow = Completer<ExportListing>();
    final s = ExportSession(
      projectDirectory: '/project',
      runner: _Gate().run,
      lister: (_) => slow.future,
    );
    final loading = s.load();

    s.dispose();
    slow.complete(const ExportListing());

    await loading;
  });
}

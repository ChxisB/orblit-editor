@TestOn('vm')
library;

import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/editor/cook_status.dart';
import 'package:path/path.dart' as p;

/// The smallest thing `GltfImporter` will take. Pure Dart, with no encoder
/// behind it, so a test can cook for real on a machine with no tools built.
const _gltf = '{"asset": {"version": "2.0"}}';

void main() {
  late Directory project;

  String at(List<String> parts) => p.joinAll([project.path, ...parts]);

  void writeAsset(String name, String contents) {
    final file = File(at(['assets', ...name.split('/')]));
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(contents);
  }

  CookStatusIndex indexFor() => CookStatusIndex(
    projectDirectory: project.path,
    // Never the machine's own: a test that cooked into it would leave its
    // rubbish in somebody's cache and read somebody's rubbish back.
    cachePath: at(['cache']),
  );

  Future<void> cook() => CookProject(
    assetsPath: at(['assets']),
    outPath: at(['build', 'assets']),
    target: currentCookTarget,
    cachePath: at(['cache']),
  ).run();

  setUp(() {
    project = Directory.systemTemp.createTempSync('orblit_cook_status');
  });
  tearDown(() => project.deleteSync(recursive: true));

  group('where the assets stand', () {
    test('a project with no assets folder says nothing, and is fine', () async {
      final index = indexFor();
      await index.refresh();

      expect(index.counts, isEmpty);
      expect(index.problem, isNull);
    });

    test('an asset nobody has cooked needs cooking', () async {
      writeAsset('ship.gltf', _gltf);
      final index = indexFor();
      await index.refresh();

      expect(index[at(['assets', 'ship.gltf'])], CookState.stale);
    });

    test('after a cook it is cooked', () async {
      writeAsset('ship.gltf', _gltf);
      await cook();

      final index = indexFor();
      await index.refresh();
      expect(index[at(['assets', 'ship.gltf'])], CookState.cooked);
    });

    test('changing it needs cooking again', () async {
      writeAsset('ship.gltf', _gltf);
      await cook();
      writeAsset('ship.gltf', '{"asset": {"version": "2.0"}, "scenes": []}');

      final index = indexFor();
      await index.refresh();
      expect(index[at(['assets', 'ship.gltf'])], CookState.stale);
    });

    test('a file no importer claims has nothing to cook', () async {
      writeAsset('notes.md', 'read me');
      final index = indexFor();
      await index.refresh();

      expect(index[at(['assets', 'notes.md'])], CookState.ignored);
    });

    test('a file outside the assets folder is not asked about', () async {
      File(at(['scenes', 'main.oscene'])).createSync(recursive: true);
      writeAsset('ship.gltf', _gltf);

      final index = indexFor();
      await index.refresh();
      expect(
        index[at(['scenes', 'main.oscene'])],
        isNull,
        reason: 'the cook reads assets/, so nothing else gets a mark',
      );
    });

    test('a folder deep in the project is found and named', () async {
      writeAsset('ships/small/scout.gltf', _gltf);
      final index = indexFor();
      await index.refresh();

      expect(
        index[at(['assets', 'ships', 'small', 'scout.gltf'])],
        CookState.stale,
        reason: 'ids are separated by / whatever this machine spells with',
      );
    });

    test('it counts what is outstanding', () async {
      writeAsset('one.gltf', _gltf);
      writeAsset('two.gltf', '{"asset": {"version": "2.0"}, "scenes": []}');
      writeAsset('notes.md', 'read me');

      final index = indexFor();
      await index.refresh();
      expect(index.counts[CookState.stale], 2);
      expect(index.counts[CookState.ignored], 1);
    });

    test('a target it does not know is said, not thrown', () async {
      writeAsset('ship.gltf', _gltf);
      final index = CookStatusIndex(
        projectDirectory: project.path,
        cachePath: at(['cache']),
        target: const CookTarget(name: 'nintendo-64'),
      );
      await index.refresh();

      expect(index.problem, contains('nintendo-64'));
      expect(
        index.counts,
        isEmpty,
        reason: 'no answer is better than an answer about the wrong machine',
      );
    });

    test('asking twice at once asks again rather than twice', () async {
      writeAsset('ship.gltf', _gltf);
      final index = indexFor();

      final first = index.refresh();
      expect(index.asking, isTrue);
      final second = index.refresh();
      await Future.wait([first, second]);

      expect(index.asking, isFalse);
      expect(index[at(['assets', 'ship.gltf'])], CookState.stale);
    });

    test('it says when it is working, and when it has stopped', () async {
      writeAsset('ship.gltf', _gltf);
      final index = indexFor();
      final seen = <bool>[];
      index.addListener(() => seen.add(index.asking));

      await index.refresh();
      expect(seen, [true, false]);
    });

    test('clearing forgets what it knew', () async {
      writeAsset('ship.gltf', _gltf);
      final index = indexFor();
      await index.refresh();
      expect(index.counts, isNotEmpty);

      index.clear();
      expect(index.counts, isEmpty);
      expect(index[at(['assets', 'ship.gltf'])], isNull);
    });

    test('asking does not cook anything', () async {
      writeAsset('ship.gltf', _gltf);
      await indexFor().refresh();

      expect(
        Directory(at(['build'])).existsSync(),
        isFalse,
        reason: 'the browser describes a build, it does not run one',
      );
    });
  });

  group('how a state reads', () {
    test('every one of them has words and a reason', () {
      for (final state in CookState.values) {
        expect(state.label, isNotEmpty);
        expect(state.explanation, isNotEmpty);
      }
    });

    test('the three worth a dot have one, and the fourth does not', () {
      expect(CookState.cooked.mark, isNotNull);
      expect(CookState.stale.mark, isNotNull);
      expect(CookState.failed.mark, isNotNull);
      expect(
        CookState.ignored.mark,
        isNull,
        reason: 'a README is not a problem and never will be',
      );
    });

    test('they do not all look the same', () {
      final marks = {
        CookState.cooked.mark,
        CookState.stale.mark,
        CookState.failed.mark,
      };
      expect(marks, hasLength(3));
    });
  });
}

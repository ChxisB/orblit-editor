@TestOn('vm')
library;

import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/editor/export_folder.dart';
import 'package:orblit_editor/src/editor/export_run.dart';
import 'package:orblit_editor/src/editor/pack_contents.dart';
import 'package:path/path.dart' as p;

/// A glTF of a size the test picks, in pure Dart, so a test cooks for real
/// on a machine with no native tools built.
String _gltf([int padding = 0]) =>
    '{"asset": {"version": "2.0"}, "extras": {"pad": "${'x' * padding}"}}';

/// Counts the bytes asked of the pack, to show that listing reads a
/// directory and not the blobs.
final class _Counting implements PackBytes {
  _Counting(this._inner);

  final PackBytes _inner;
  int read_ = 0;

  @override
  Future<int> length() => _inner.length();

  @override
  Future<Uint8List> read(int at, int bytes) {
    read_ += bytes;
    return _inner.read(at, bytes);
  }
}

void main() {
  late Directory project;

  String at(List<String> parts) => p.joinAll([project.path, ...parts]);

  void writeAsset(String name, String contents) {
    final file = File(at(['assets', ...name.split('/')]));
    file.parent.createSync(recursive: true);
    file.writeAsStringSync(contents);
  }

  ExportRequest request(List<String> targets, {String? patchOf}) =>
      ExportRequest(
        projectDirectory: project.path,
        targets: targets,
        patchOf: patchOf,
        // Never the machine's own, as in the cook status tests.
        cachePath: at(['cache']),
      );

  Future<TargetExport> export(String target, {String? patchOf}) =>
      exportTarget(request([target], patchOf: patchOf), target);

  setUp(() {
    project = Directory.systemTemp.createTempSync('orblit_export');
    writeAsset('ship.gltf', _gltf());
    writeAsset('rock.gltf', _gltf(40));
  });
  tearDown(() => project.deleteSync(recursive: true));

  group('a whole export', () {
    test('writes one pack per target under exports', () async {
      final done = await runExport(request(['macos', 'android']));

      expect(done.map((d) => d.outcome), everyElement(ExportOutcome.packed));
      expect(done.map((d) => p.basename(d.path!)), [
        'game.macos.opack',
        'game.android.opack',
      ]);
      final listing = await listExports(project.path);
      expect(listing.packs.map((pack) => pack.target).toSet(), {
        'macos',
        'android',
      });
      expect(listing.packs.every((pack) => !pack.isPatch), isTrue);
    });

    test('reports the size, the assets and the sharing', () async {
      final done = await export('macos');

      final summary = done.summary!;
      expect(summary.assets, 2);
      expect(summary.added, 2);
      expect(summary.replaced, 0);
      expect(summary.bytes, File(done.path!).lengthSync());
      expect(done.cooked, isNotEmpty);
    });

    test('goes on to the next target after one cannot be done', () async {
      final done = await runExport(request(['nowhere', 'macos']));

      expect(done.first.outcome, ExportOutcome.failed);
      expect(done.first.message, contains('nowhere'));
      expect(done.last.outcome, ExportOutcome.packed);
    });

    test('a cook that dies is reported and the next target runs', () async {
      final seen = <String>[];
      final done = await runExport(
        request(['macos', 'android']),
        onTarget: (landed) => seen.add(landed.target),
        cook: (request, target) async {
          if (target == 'macos') throw StateError('out of memory');
          return exportTarget(request, target);
        },
      );

      expect(done.map((d) => d.outcome), [
        ExportOutcome.failed,
        ExportOutcome.packed,
      ]);
      expect(done.first.message, contains('stopped before it finished'));
      expect(done.first.message, contains('out of memory'));
      expect(seen, ['macos', 'android']);
    });

    test('says what did not cook and writes no pack for the target', () async {
      writeAsset('broken.gltf', '{ not json');

      final done = await export('macos');

      expect(done.outcome, ExportOutcome.cookFailed);
      expect(done.failures.map((f) => f.id), ['broken.gltf']);
      expect(done.failures.single.reason, isNotEmpty);
      expect(done.summary, isNull);
      expect(File(at(['exports', 'game.macos.opack'])).existsSync(), isFalse);
    });

    test('keeps the last good pack when the next cook fails', () async {
      await export('macos');
      final before = File(at(['exports', 'game.macos.opack']))
          .readAsBytesSync();
      writeAsset('broken.gltf', '{ not json');

      final done = await export('macos');

      expect(done.outcome, ExportOutcome.cookFailed);
      expect(
        File(at(['exports', 'game.macos.opack'])).readAsBytesSync(),
        before,
      );
    });

    test('a project with no assets folder says so', () async {
      Directory(at(['assets'])).deleteSync(recursive: true);

      final done = await export('macos');

      expect(done.outcome, ExportOutcome.failed);
      expect(done.message, contains('assets'));
    });

    test('the same project exported again is unchanged', () async {
      await export('macos');

      final again = await export('macos');

      expect(again.outcome, ExportOutcome.unchanged);
      final listing = await listExports(project.path);
      expect(listing.packs, hasLength(1));
      expect(
        Directory(at(['exports'])).listSync().map((f) => p.basename(f.path)),
        ['game.macos.opack'],
      );
    });

    test('sets the old pack aside under its hash when it changes', () async {
      final first = await export('macos');
      final firstHash = (await listExports(project.path)).packs.single.hash;
      writeAsset('ship.gltf', _gltf(10));

      final second = await export('macos');

      expect(second.outcome, ExportOutcome.packed);
      expect(first.path, second.path);
      final listing = await listExports(project.path);
      expect(listing.packs, hasLength(2));
      expect(
        File(at(['exports', 'game.macos.${firstHash.substring(0, 12)}.opack']))
            .existsSync(),
        isTrue,
      );
    });
  });

  group('a patch', () {
    late String whole;

    setUp(() async {
      whole = (await export('macos')).path!;
    });

    test('holds the one asset that changed', () async {
      writeAsset('ship.gltf', _gltf(10));

      final done = await export('macos', patchOf: whole);

      expect(done.outcome, ExportOutcome.packed);
      expect(done.patchOf, 'game.macos.opack');
      final summary = done.summary!;
      expect(summary.assets, 1);
      expect(summary.replaced, 1);
      expect(summary.added, 0);
      expect(summary.removed, 0);
      expect(p.basename(done.path!), isNot('game.macos.opack'));
      expect(File(whole).existsSync(), isTrue);
    });

    test('says what was added and what went', () async {
      writeAsset('tree.gltf', _gltf(20));
      File(at(['assets', 'rock.gltf'])).deleteSync();

      final done = await export('macos', patchOf: whole);

      final summary = done.summary!;
      expect(summary.added, 1);
      expect(summary.replaced, 0);
      expect(summary.removed, 1);
    });

    test('names the pack it stands on', () async {
      writeAsset('ship.gltf', _gltf(10));
      final done = await export('macos', patchOf: whole);

      final listing = await listExports(project.path);
      final patch = listing.packs.singleWhere((pack) => pack.path == done.path);
      final base = listing.packs.singleWhere((pack) => pack.path == whole);
      expect(patch.isPatch, isTrue);
      expect(patch.base, base.hash);
    });

    test('of nothing that changed writes nothing', () async {
      final done = await export('macos', patchOf: whole);

      expect(done.outcome, ExportOutcome.unchanged);
      expect(done.message, contains('game.macos.opack'));
      expect((await listExports(project.path)).packs, hasLength(1));
      expect(
        Directory(at(['exports'])).listSync().map((f) => p.basename(f.path)),
        ['game.macos.opack'],
      );
    });

    test('of a pack that is not there is a failure that names it', () async {
      final done = await export(
        'macos',
        patchOf: at(['exports', 'gone.opack']),
      );

      expect(done.outcome, ExportOutcome.failed);
      expect(done.message, contains('gone.opack'));
    });

    test('for another target than its base is refused', () async {
      final done = await export('android', patchOf: whole);

      expect(done.outcome, ExportOutcome.failed);
      expect(done.message, contains('macos'));
    });

    test('can stand on another patch', () async {
      writeAsset('ship.gltf', _gltf(10));
      final one = (await export('macos', patchOf: whole)).path!;
      writeAsset('rock.gltf', _gltf(60));

      final two = await export('macos', patchOf: one);

      expect(two.outcome, ExportOutcome.packed);
      expect(two.summary!.assets, 1);
      expect(two.patchOf, p.basename(one));
      final contents = await readContents(project.path, two.path!);
      expect(contents.packs, hasLength(3));
    });

    test('still finds its base after a newer whole pack replaces it', () async {
      writeAsset('ship.gltf', _gltf(10));
      final patch = (await export('macos', patchOf: whole)).path!;
      writeAsset('rock.gltf', _gltf(60));
      await export('macos');

      final contents = await readContents(project.path, patch);

      expect(contents.packs, hasLength(2));
      expect(contents.packs.first, isNot('game.macos.opack'));
    });
  });

  group('the folder', () {
    test('lists newest first and sets aside what is not a pack', () async {
      await export('macos');
      File(at(['exports', 'notes.opack'])).writeAsStringSync('not a pack');
      File(at(['exports', 'rock.obj'])).writeAsStringSync('v 0 0 0');

      final listing = await listExports(project.path);

      expect(listing.packs.map((pack) => p.basename(pack.path)), [
        'game.macos.opack',
      ]);
      expect(listing.unreadable.single, startsWith('notes.opack'));
    });

    test('an empty project has no exports', () async {
      final listing = await listExports(project.path);

      expect(listing.packs, isEmpty);
      expect(listing.unreadable, isEmpty);
    });

    test('says which pack is missing when a chain is broken', () async {
      final whole = (await export('macos')).path!;
      writeAsset('ship.gltf', _gltf(10));
      final patch = (await export('macos', patchOf: whole)).path!;
      File(whole).deleteSync();

      await expectLater(
        readContents(project.path, patch),
        throwsA(
          isA<ExportChainBroken>().having(
            (e) => e.message,
            'message',
            allOf(contains('was built on'), contains('exports folder')),
          ),
        ),
      );
    });
  });

  group('the contents', () {
    test('lists the largest asset first', () async {
      final whole = (await export('macos')).path!;

      final contents = await readContents(project.path, whole);

      expect(contents.rows.map((row) => row.id), [
        'rock.gltf.gltf',
        'ship.gltf.gltf',
      ]);
      expect(contents.rows.first.bytes, greaterThan(contents.rows.last.bytes));
      expect(contents.bytes, contents.rows.fold<int>(0, (a, r) => a + r.bytes));
      expect(contents.layered, isFalse);
    });

    test('names which pack each asset comes from', () async {
      final whole = (await export('macos')).path!;
      writeAsset('ship.gltf', _gltf(10));
      final patch = await export('macos', patchOf: whole);

      final contents = await readContents(project.path, patch.path!);

      final owners = {for (final row in contents.rows) row.id: row.pack};
      expect(owners['ship.gltf.gltf'], p.basename(patch.path!));
      expect(owners['rock.gltf.gltf'], 'game.macos.opack');
      expect(contents.layered, isTrue);
    });

    test('names the ids a patch takes away', () async {
      final whole = (await export('macos')).path!;
      File(at(['assets', 'rock.gltf'])).deleteSync();
      final patch = await export('macos', patchOf: whole);

      final contents = await readContents(project.path, patch.path!);

      expect(contents.removed, ['rock.gltf.gltf']);
      expect(contents.rows.map((row) => row.id), ['ship.gltf.gltf']);
    });

    test('reads the directory and not the blobs', () async {
      writeAsset('big.gltf', _gltf(300000));
      final whole = (await export('macos')).path!;
      final bytes = _Counting(MemoryPackBytes(File(whole).readAsBytesSync()));

      final pack = await AssetPack.open(bytes);
      final contents = contentsOf([('game.macos.opack', pack)]);

      expect(contents.rows.first.bytes, greaterThan(300000));
      expect(bytes.read_, lessThan(20000));
    });
  });
}

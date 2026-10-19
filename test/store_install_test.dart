import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_asset/orblit_asset.dart';
import 'package:orblit_editor/src/store/install_session.dart';
import 'package:path/path.dart' as p;

import 'support/store_fixture.dart';

const String into = 'assets/kits/crates';

late Directory scratch;
late StoreFixture fixture;
late String project;
late MapTransport transport;

AssetId id(String name) => AssetId.parse(name);

String inProject(String name) => p.join(project, name);

/// Every file in the project, as a path under it.
List<String> held() => [
  for (final file in Directory(project).listSync(recursive: true))
    if (file is File) p.relative(file.path, from: project),
]..sort();

/// Nothing reached the project, and no pack was filed as downloaded.
Future<void> expectNothingWritten() async {
  expect(held(), ['$into/models/crate.obj']);
  final kept = Directory(p.join(scratch.path, 'downloads')).listSync();
  expect(kept.map((file) => p.extension(file.path)), isNot(contains('.opack')));
}

Future<InstallSession> started() async {
  final (row, release) = await fixture.listing();
  transport = fixture.transport();
  final session = InstallSession(
    row: row,
    release: release,
    projectDirectory: project,
    downloads: p.join(scratch.path, 'downloads'),
    transport: transport,
    backoff: Duration.zero,
  );
  addTearDown(session.dispose);
  await session.start();
  return session;
}

void main() {
  setUp(() async {
    scratch = Directory.systemTemp.createTempSync('orblit_store_install_');
    fixture = await StoreFixture.build(scratch);
    project = p.join(scratch.path, 'project');
    File(inProject('$into/models/crate.obj'))
      ..parent.createSync(recursive: true)
      ..writeAsStringSync('my own crate');
  });
  tearDown(() => scratch.deleteSync(recursive: true));

  test('lists the scripts and the file that would be replaced', () async {
    final session = await started();
    expect(session.stage, InstallStage.choosing);
    expect(session.kit!.scripts, [id('scripts/spin.js')]);
    expect(await session.conflicts(into: into), [id('models/crate.obj')]);
    expect(await session.conflicts(), isEmpty);
  });

  test('leaves a conflicting file alone when it is not picked', () async {
    final session = await started();
    await session.install(
      into: into,
      only: {id('scripts/spin.js'), id('shots/a.png')},
    );
    expect(session.stage, InstallStage.done);
    expect(
      File(inProject('$into/models/crate.obj')).readAsStringSync(),
      'my own crate',
    );
    expect(
      File(inProject('$into/scripts/spin.js')).readAsStringSync(),
      'spin();',
    );
    final lock = KitLock.parse(File(inProject(kitLockName)).readAsBytesSync());
    final locked = lock.find('brass-lamp', 'crates')!;
    expect(locked.into, into);
    expect(locked.hash, fixture.hash);
    expect(locked.files.keys.map((file) => '$file'), [
      '$into/scripts/spin.js',
      '$into/shots/a.png',
    ]);
  });

  test('replaces a conflicting file only when the person agreed', () async {
    final session = await started();
    await session.install(into: into);
    expect(session.stage, InstallStage.choosing);
    expect(session.problem, contains('models/crate.obj'));
    expect(held(), ['$into/models/crate.obj']);

    await session.install(into: into, replacing: {id('models/crate.obj')});
    expect(session.stage, InstallStage.done);
    expect(
      File(inProject('$into/models/crate.obj')).readAsStringSync(),
      'a crate',
    );
    expect(held(), contains(kitLockName));
  });

  test('refuses a folder a kit may not go into, and writes nothing', () async {
    final session = await started();
    await session.install(into: '.git/hooks');
    expect(session.stage, InstallStage.choosing);
    expect(session.problem, contains('.git'));
    expect(held(), ['$into/models/crate.obj']);
  });

  test('writes nothing when kits.lock cannot be read', () async {
    File(inProject(kitLockName)).writeAsStringSync('{not json');
    final session = await started();
    await session.install(into: 'assets/kits/other');
    expect(session.stage, InstallStage.choosing);
    expect(session.problem, isNotNull);
    expect(held(), ['$into/models/crate.obj', kitLockName]);
  });

  test('gives the dialog back when the install throws an error', () async {
    final session = await started();
    await session.install(into: into, only: {id('nope.txt')});
    expect(session.stage, InstallStage.choosing);
    expect(session.problem, contains('nope.txt'));
    expect(held(), ['$into/models/crate.obj']);
  });

  test('writes no file when the lock has no room for the kit', () async {
    final full = KitLock([
      for (var i = 0; i < 500; i++)
        LockedKit(
          publisher: 'filler',
          slug: 'k$i',
          version: '1.0.0',
          hash: fixture.hash,
          files: const {},
        ),
    ]);
    File(inProject(kitLockName)).writeAsBytesSync(full.encode());
    final session = await started();
    await session.install(into: into, replacing: {id('models/crate.obj')});
    expect(session.stage, InstallStage.choosing);
    expect(session.problem, contains('500 kits'));
    expect(held(), ['$into/models/crate.obj', kitLockName]);
    expect(
      File(inProject('$into/models/crate.obj')).readAsStringSync(),
      'my own crate',
    );
  });

  test(
    'adds to the files listed when it installs into a folder again',
    () async {
      final session = await started();
      await session.install(into: into, only: {id('scripts/spin.js')});
      await session.install(into: into, only: {id('shots/a.png')});
      List<String> listed() => [
        for (final file in KitLock.parse(
          File(inProject(kitLockName)).readAsBytesSync(),
        ).find('brass-lamp', 'crates')!.files.keys)
          '$file',
      ];
      expect(
        listed(),
        unorderedEquals(['$into/scripts/spin.js', '$into/shots/a.png']),
      );

      await session.install(into: 'assets/other', only: {id('shots/a.png')});
      expect(listed(), ['assets/other/shots/a.png']);
    },
  );

  test('takes up a cut download where it stopped', () async {
    fixture.pages[crateUrl] = TransportPage(
      fixture.pack,
      stopAfter: fixture.pack.length ~/ 2,
    );
    final session = await started();
    expect(session.stage, InstallStage.choosing);
    final asked = transport.sent.where((request) => request.url == crateUrl);
    expect(asked.length, 2);
    expect(asked.last.from, fixture.pack.length ~/ 2);
    expect(session.progress!.received, fixture.pack.length);
  });

  test('refuses another pack than the one listed, with both hashes', () async {
    // A file renamed in the pack's directory, so it is still a pack but not
    // the one the entry hashed.
    final bad = Uint8List.fromList(fixture.pack);
    final header = ByteData.sublistView(bad);
    final at = header.getUint32(8, Endian.little);
    final end = at + header.getUint32(16, Endian.little);
    final text = utf8.decode(bad.sublist(at, end));
    bad.setRange(at, end, utf8.encode(text.replaceFirst('.obj', '.obx')));
    fixture.pages[crateUrl] = TransportPage(bad);
    final session = await started();
    expect(session.failed, isTrue);
    expect(session.problem, matches(RegExp(r'Expected \S+, got \S+')));
    await expectNothingWritten();
  });

  test('refuses a pack with a damaged file in it', () async {
    // The first byte after the header belongs to the first file.
    final bad = Uint8List.fromList(fixture.pack);
    bad[32] ^= 0xff;
    fixture.pages[crateUrl] = TransportPage(bad);
    final session = await started();
    expect(session.failed, isTrue);
    expect(session.problem, contains('do not match the hashes'));
    await expectNothingWritten();
  });

  test('refuses a pack that is not the release listed', () async {
    final (row, release) = await fixture.listing();
    final other = KitRow(
      manifest: KitManifest(
        slug: 'barrels',
        publisher: row.manifest.publisher,
        name: 'Barrels',
        summary: 'Some barrels.',
        version: '1.0.0',
        licence: row.manifest.licence,
        engine: row.manifest.engine,
        containsScripts: false,
      ),
      updated: row.updated,
    );
    final session = InstallSession(
      row: other,
      release: release,
      projectDirectory: project,
      downloads: p.join(scratch.path, 'downloads'),
      transport: fixture.transport(),
      backoff: Duration.zero,
    );
    addTearDown(session.dispose);
    await session.start();
    expect(session.failed, isTrue);
    expect(session.problem, contains('barrels'));
  });
}

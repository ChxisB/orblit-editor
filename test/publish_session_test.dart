import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/store/publish_session.dart';
import 'package:path/path.dart' as p;

import 'support/kit_folder.dart';

// Packing a kit of a project on the disk. The clock is the test's own.

late Directory project;

String inProject(String name) => p.join(project.path, name);

PublishSession session() => PublishSession(
  projectDirectory: project.path,
  clock: () => DateTime.utc(2026, 10, 19, 23, 30),
);

Future<PublishSession> run(
  String folder, {
  String url = hostedAt,
  String notes = '',
}) async {
  final made = session();
  await made.start(folder: folder, url: url, notes: notes);
  return made;
}

void main() {
  setUp(() {
    project = Directory.systemTemp.createTempSync('orblit_publish_');
    writeKitFolder(inProject('assets/kits/crates'));
  });
  tearDown(() => project.deleteSync(recursive: true));

  test('packs the kit into exports/kits and dates the release', () async {
    final made = await run('assets/kits/crates', notes: ' First release. ');
    expect(made.problem, isNull);
    expect(made.stage, PublishStage.done);
    expect(made.kitFile, inProject('exports/kits/crates-1.0.0.opack'));
    expect(File(made.kitFile!).lengthSync(), made.published!.summary.bytes);
    final release = made.published!.release;
    expect(release.url.toString(), hostedAt);
    expect(release.released, '2026-10-19');
    expect(release.notes, 'First release.');
    expect(made.entryText, contains('"version": "1.0.0"'));
    expect(made.entryText, contains(made.published!.summary.hash.hex));
    // The project's own folders are left as they were.
    expect(File(inProject('assets/kits/crates/kit.json')).existsSync(), isTrue);
  });

  test(
    'an address with no file name is refused before anything is made',
    () async {
      final made = await run('assets/kits/crates', url: 'https://example.com/');
      expect(made.stage, PublishStage.editing);
      expect(made.problem, contains('name of the kit file'));
      expect(Directory(inProject('exports')).existsSync(), isFalse);
    },
  );

  test('a folder outside the project is refused', () async {
    for (final folder in ['', '.', '..', '../elsewhere', '/tmp', 'a/../..']) {
      final made = await run(folder);
      expect(made.problem, contains('inside the project'), reason: folder);
    }
    final missing = await run('assets/nothing');
    expect(missing.problem, contains('no folder assets/nothing'));
    expect(Directory(inProject('exports')).existsSync(), isFalse);
  });

  test(
    'a kit that breaks a rule is named, and a second try can work',
    () async {
      File(inProject('assets/kits/crates/.DS_Store')).writeAsStringSync('x');
      final made = session();
      await made.start(folder: 'assets/kits/crates', url: hostedAt);
      expect(made.stage, PublishStage.editing);
      expect(made.problem, contains('.DS_Store'));

      File(inProject('assets/kits/crates/.DS_Store')).deleteSync();
      await made.start(folder: 'assets/kits/crates', url: hostedAt);
      expect(made.problem, isNull);
      expect(made.stage, PublishStage.done);
    },
  );

  test('a long note is refused and says so', () async {
    final made = await run('assets/kits/crates', notes: 'x' * 400);
    expect(made.stage, PublishStage.editing);
    expect(made.problem, isNotNull);
  });

  test('the entry is not there before a run is done', () {
    expect(() => session().entryText, throwsStateError);
  });
}

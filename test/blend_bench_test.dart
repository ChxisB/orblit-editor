import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/blend_bench.dart';
import 'package:orblit_editor/src/editor/blend_model.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_motion/orblit_motion.dart';

// The graph's side of the editor: a blend opened, changed through the undo
// stack, and written back as the file a game reads.

BlendDocument moves() => BlendDocument(
  name: 'Moves',
  inputs: const {'speed': 0},
  states: [
    BlendState('idle', plays: const BlendClip('idle')),
    BlendState('move', plays: const BlendClip('walk')),
  ],
  changes: const [
    BlendChange(
      from: 'idle',
      to: 'move',
      when: BlendCondition.above('speed', 0.1),
    ),
  ],
);

({History history, BlendBench bench}) rig() {
  final history = History(Workspace('/project'));
  final bench = BlendBench(history: history)
    ..openBlend('/project/moves.oblend', moves());
  return (history: history, bench: bench);
}

List<String> names(BlendDocument blend) => [
  for (final state in blend.writtenStates) state.name,
];

void main() {
  group('editing', () {
    test('changes the shown blend, and undo puts the old one back', () {
      final (:history, :bench) = rig();
      bench.edit('Add state', (blend) => blend.withStateAdded('jump'));
      expect(names(bench.blend!), ['idle', 'move', 'jump']);
      expect(history.labels, ['Add state']);

      history.undo();
      expect(names(bench.blend!), ['idle', 'move']);
      history.redo();
      expect(names(bench.blend!), ['idle', 'move', 'jump']);
    });

    test('a change that changes nothing is not a step', () {
      final (:history, :bench) = rig();
      bench.edit('Rename', (blend) => blend.withStateRenamed('idle', 'idle'));
      bench.edit('Remove', (blend) => blend.withStateRemoved('nothing'));
      expect(history.canUndo, isFalse);
    });

    test('ties a typed run into one step', () {
      final (:history, :bench) = rig();
      final gesture = Object();
      for (final to in ['r', 'ru', 'run']) {
        bench.edit(
          'Rename state',
          (blend) => blend.withStateRenamed(blend.writtenStates.last.name, to),
          gesture: gesture,
        );
      }
      expect(names(bench.blend!), ['idle', 'run']);
      history.undo();
      expect(names(bench.blend!), ['idle', 'move']);
      expect(history.canUndo, isFalse);
    });

    test('a choice follows its state through a rename and an undo', () {
      final (:history, :bench) = rig();
      bench.pick = const StatePicked('move');
      bench.edit(
        'Rename state',
        (blend) => blend.withStateRenamed('move', 'run'),
        pick: const StatePicked('run'),
      );
      expect(bench.pick, const StatePicked('run'));
      history.undo();
      expect(bench.pick, const StatePicked('move'));
    });

    test('a choice that is no longer there is cleared', () {
      final (:history, :bench) = rig();
      bench.pick = const StatePicked('move');
      bench.edit('Remove state', (blend) => blend.withStateRemoved('move'));
      expect(bench.pick, const NothingPicked());
    });

    test('undoing a step in a blend shows that blend', () {
      final (:history, :bench) = rig();
      bench.edit('Add state', (blend) => blend.withStateAdded('jump'));
      bench.openBlend('/project/other.oblend', moves());
      expect(bench.shown!.path, '/project/other.oblend');
      history.undo();
      expect(bench.shown!.path, '/project/moves.oblend');
    });

    test('closing a blend forgets its steps', () {
      final (:history, :bench) = rig();
      bench
        ..edit('Add state', (blend) => blend.withStateAdded('jump'))
        ..close('/project/moves.oblend');
      expect(history.canUndo, isFalse);
      expect(bench.shown, isNull);
    });
  });

  group('files', () {
    late Directory folder;
    late String path;

    setUp(() {
      folder = Directory.systemTemp.createTempSync('blend_bench');
      path = '${folder.path}/moves.oblend';
      File(path).writeAsStringSync(moves().encode());
    });

    tearDown(() => folder.deleteSync(recursive: true));

    test('writes a blend that changed, and it reads as saved', () {
      final history = History(Workspace(folder.path));
      final bench = BlendBench(history: history);
      expect(bench.openFile(path), isEmpty);
      expect(bench.anyUnsaved, isFalse);

      bench.edit('Add state', (blend) => blend.withStateAdded('jump'));
      expect(bench.anyUnsaved, isTrue);
      expect(bench.saveAll(), isEmpty);
      expect(bench.anyUnsaved, isFalse);

      final read = BlendDocument.decode(File(path).readAsStringSync()).blend;
      expect(names(read), ['idle', 'move', 'jump']);

      history.undo();
      expect(bench.anyUnsaved, isTrue);
    });

    test('says what could not be read, and refuses what is not a blend', () {
      final bench = BlendBench(history: History(Workspace(folder.path)));
      File(path).writeAsStringSync('{"kind":"orblit.clip"}');
      expect(() => bench.openFile(path), throwsA(isA<BlendFormatException>()));
      expect(bench.open, isEmpty);
    });

    test('asks nothing of a blend that is already open', () {
      final bench = BlendBench(history: History(Workspace(folder.path)));
      bench.openFile(path);
      File(path).deleteSync();
      expect(bench.openFile(path), isEmpty);
      expect(bench.shown!.path, path);
    });
  });
}

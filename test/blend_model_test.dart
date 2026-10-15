import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/blend_model.dart';
import 'package:orblit_motion/orblit_motion.dart';

// What the blend graph editor knows of a blend, without a widget.

BlendDocument moves() => BlendDocument(
  name: 'Moves',
  inputs: const {'speed': 0, 'jump': 0},
  states: [
    BlendState('idle', plays: const BlendClip('idle')),
    BlendState(
      'move',
      plays: BlendLine('speed', const [
        LinePoint(1, BlendClip('walk')),
        LinePoint(3, BlendClip('run')),
      ]),
    ),
    BlendState('jump', plays: const BlendClip('jump')),
    BlendState('lonely', plays: const BlendClip('wave')),
  ],
  changes: const [
    BlendChange(
      from: 'idle',
      to: 'move',
      when: BlendCondition.above('speed', 0.1),
      fade: 0.2,
    ),
    BlendChange(
      from: 'move',
      to: 'idle',
      when: BlendCondition.below('speed', 0.1),
    ),
    BlendChange(to: 'jump', when: BlendCondition.on('jump')),
    BlendChange(from: 'jump', to: 'idle', when: BlendCondition.through(1)),
  ],
);

List<String> names(BlendDocument blend) => [
  for (final state in blend.writtenStates) state.name,
];

void main() {
  group('the graph', () {
    test('has one arrow for each pair of states a change joins', () {
      final edges = blendEdges(moves());
      expect(
        [for (final edge in edges) '${edge.from}>${edge.to}${edge.changes}'],
        ['idle>move[0]', 'move>idle[1]', 'jump>idle[3]'],
      );
    });

    test('joins two changes between one pair into one arrow', () {
      final blend = moves().withChangeAdded('idle', 'move');
      final edge = blendEdges(blend).first;
      expect(edge.changes, [0, 4]);
    });

    test('leaves a change from anywhere to the state it enters', () {
      expect(changesFromAnywhere(moves()), {
        'jump': [2],
      });
    });

    test('puts each state a column further than the one that leads to it', () {
      final cells = layoutBlend(moves());
      expect(cells['idle'], (column: 0, row: 0));
      expect(cells['move'], (column: 1, row: 0));
    });

    test('puts a state nothing leads to after the last column', () {
      final cells = layoutBlend(moves());
      expect(cells['jump']!.column, 2);
      expect(cells['lonely']!.column, 2);
      expect(cells['jump']!.row, isNot(cells['lonely']!.row));
    });

    test('stands the start in the first column however it is named', () {
      final blend = moves().withStart('move');
      final cells = layoutBlend(blend);
      expect(cells['move'], (column: 0, row: 0));
      expect(cells['idle']!.column, 1);
    });

    test('draws a nested graph as the one state it is in the file', () {
      final inner = BlendDocument(
        name: 'Inner',
        states: [
          BlendState('a', plays: const BlendClip('a')),
          BlendState('b', plays: const BlendClip('b')),
        ],
      );
      final blend = BlendDocument(
        name: 'Outer',
        states: [
          BlendState('ground', plays: BlendGraph(inner)),
          BlendState('air', plays: const BlendClip('air')),
        ],
        changes: const [BlendChange(from: 'ground/b', to: 'air')],
      );
      expect(stateOwning(blend, 'ground/b'), 'ground');
      expect(blendEdges(blend).single.from, 'ground');
      expect(layoutBlend(blend).keys, ['ground', 'air']);
    });
  });

  group('a condition', () {
    test('says what kind of test it is', () {
      expect(conditionKind(BlendCondition.always), ConditionKind.always);
      expect(
        conditionKind(const BlendCondition.above('speed', 1)),
        ConditionKind.above,
      );
      expect(
        conditionKind(const BlendCondition.below('speed', 1)),
        ConditionKind.below,
      );
      expect(conditionKind(const BlendCondition.on('jump')), ConditionKind.on);
      expect(
        conditionKind(const BlendCondition.through(1)),
        ConditionKind.through,
      );
      expect(conditionKind(const BlendCondition.all([])), ConditionKind.all);
      expect(
        conditionKind(const BlendCondition.not(BlendCondition.always)),
        ConditionKind.not,
      );
    });

    test('is read as words', () {
      expect(
        describeCondition(const BlendCondition.above('speed', 0.1)),
        'speed above 0.1',
      );
      expect(describeCondition(const BlendCondition.on('jump')), 'jump is on');
      expect(
        describeCondition(const BlendCondition.through(1)),
        'when it is done',
      );
      expect(
        describeCondition(const BlendCondition.through(0.8)),
        'after 0.8 laps',
      );
      expect(
        describeCondition(
          const BlendCondition.all([
            BlendCondition.above('speed', 2),
            BlendCondition.on('ground'),
          ]),
        ),
        'speed above 2 and ground is on',
      );
      expect(describeCondition(const BlendCondition.all([])), 'always');
    });

    test('keeps its input and number when its kind changes', () {
      const old = BlendCondition.above('speed', 2);
      final below = conditionOfKind(ConditionKind.below, old);
      expect(conditionInput(below), 'speed');
      expect(conditionNumber(below), 2);
      expect(conditionKind(below), ConditionKind.below);
    });

    test('nests the condition it was when it becomes all of them', () {
      const old = BlendCondition.on('jump');
      final all = conditionOfKind(ConditionKind.all, old);
      expect(conditionParts(all).map(describeCondition), ['jump is on']);
      final none = conditionOfKind(ConditionKind.all, BlendCondition.always);
      expect(conditionParts(none), isEmpty);
    });

    test('reads another input, and not a name with nothing in it', () {
      const old = BlendCondition.above('speed', 2);
      expect(conditionInput(conditionReading(old, 'run')), 'run');
      expect(conditionInput(conditionReading(old, '')), 'speed');
      expect(conditionNumber(conditionReading(old, 'run')), 2);
    });

    test('takes a number, and not one that is not', () {
      const old = BlendCondition.above('speed', 2);
      expect(conditionNumber(conditionNumbered(old, 5)), 5);
      expect(conditionNumber(conditionNumbered(old, double.nan)), 2);
      expect(
        conditionKind(conditionNumbered(const BlendCondition.on('x'), 5)),
        ConditionKind.on,
      );
    });

    test('is rebuilt from the parts it is given', () {
      const old = BlendCondition.any([BlendCondition.on('a')]);
      final more = conditionMadeOf(old, [
        ...conditionParts(old),
        const BlendCondition.on('b'),
      ]);
      expect(describeCondition(more), 'a is on or b is on');
      expect(
        describeCondition(
          conditionMadeOf(const BlendCondition.not(BlendCondition.always), [
            const BlendCondition.on('c'),
          ]),
        ),
        'not c is on',
      );
    });
  });

  group('what a state plays', () {
    test('reads as a phrase', () {
      expect(describeSource(const BlendClip('walk')), 'walk');
      expect(
        describeSource(
          BlendLine('speed', const [
            LinePoint(0, BlendClip('a')),
            LinePoint(1, BlendClip('b')),
          ]),
        ),
        '2 points along speed',
      );
    });

    test('is only changed when it is a clip or a line of clips', () {
      expect(canEditSource(const BlendClip('a')), isTrue);
      expect(canEditSource(lineFrom('a', 'speed')), isTrue);
      expect(
        canEditSource(
          BlendLine('speed', [
            LinePoint(
              0,
              BlendLine('turn', const [LinePoint(0, BlendClip('a'))]),
            ),
          ]),
        ),
        isFalse,
      );
      expect(
        canEditSource(
          BlendPlane('x', 'y', const [PlanePoint(0, 0, BlendClip('a'))]),
        ),
        isFalse,
      );
    });

    test('is a line that moves a point and keeps them in order', () {
      final line = lineFrom('walk', 'speed');
      final moved = linePointChanged(line, 0, at: 5, clip: 'run');
      expect([for (final point in moved.points) point.at], [1, 5]);
      expect(firstClipOf(moved), 'walk');
      expect((moved.points.last.plays as BlendClip).clip, 'run');
    });

    test('keeps a point when it is moved to somewhere that is not', () {
      final line = lineFrom('walk', 'speed');
      final same = linePointChanged(line, 1, at: double.nan, clip: '');
      expect([for (final point in same.points) point.at], [0, 1]);
    });

    test('gets a point after the last and loses one but not the only', () {
      final line = linePointAdded(lineFrom('walk', 'speed'));
      expect([for (final point in line.points) point.at], [0, 1, 2]);
      expect(linePointRemoved(line, 1).points, hasLength(2));
      final one = BlendLine('speed', const [LinePoint(0, BlendClip('a'))]);
      expect(linePointRemoved(one, 0), same(one));
    });

    test('runs along another input', () {
      final line = lineAlong(lineFrom('walk', 'speed'), 'turn');
      expect(line.input, 'turn');
      expect(line.points, hasLength(2));
    });
  });

  group('a blend changed', () {
    test('names a new state after its clip, and numbers a second', () {
      final one = moves().withStateAdded('wave');
      expect(names(one).last, 'wave');
      final two = one.withStateAdded('wave');
      expect(names(two).last, 'wave 2');
    });

    test('names a new state for no clip as a clip, not as nothing', () {
      final blend = moves().withStateAdded('');
      expect((blend.writtenStates.last.plays as BlendClip).clip, 'clip');
    });

    test('renames a state in every change and the start', () {
      final blend = moves().withStateRenamed('idle', 'stand');
      expect(names(blend), ['stand', 'move', 'jump', 'lonely']);
      expect(blend.writtenStart, 'stand');
      expect(blend.writtenChanges[0].from, 'stand');
      expect(blend.writtenChanges[1].to, 'stand');
      expect(blend.writtenChanges[3].to, 'stand');
      expect(blend.writtenChanges[2].from, isNull);
    });

    test('refuses a name that is taken or empty', () {
      final blend = moves();
      expect(blend.withStateRenamed('idle', 'move'), same(blend));
      expect(blend.withStateRenamed('idle', ''), same(blend));
      expect(blend.withStateRenamed('idle', 'idle'), same(blend));
      expect(blend.withStateRenamed('nothing', 'x'), same(blend));
    });

    test('takes a state out with every change into and out of it', () {
      final blend = moves().withStateRemoved('jump');
      expect(names(blend), ['idle', 'move', 'lonely']);
      expect(blend.writtenChanges, hasLength(2));
    });

    test('moves the start on when the start is taken out', () {
      final blend = moves().withStateRemoved('idle');
      expect(blend.writtenStart, 'move');
      expect(blend.start, 'move');
    });

    test('keeps the last state', () {
      var blend = moves();
      for (final name in ['idle', 'move', 'jump', 'lonely']) {
        blend = blend.withStateRemoved(name);
      }
      expect(names(blend), ['lonely']);
    });

    test('changes one state and leaves the others', () {
      final blend = moves().withState(
        'idle',
        (state) => state.copyWith(speed: 2, whenDone: WhenDone.hold),
      );
      expect(blend.writtenStates.first.speed, 2);
      expect(blend.writtenStates.first.whenDone, WhenDone.hold);
      expect(blend.writtenStates[1].speed, 1);
    });

    test('refuses a speed a state cannot play at', () {
      final blend = moves();
      final result = blend.withState(
        'idle',
        (state) => state.copyWith(speed: -1),
      );
      expect(result, same(blend));
    });

    test('starts somewhere else', () {
      final blend = moves().withStart('jump');
      expect(blend.start, 'jump');
      expect(blend.withStart('jump'), same(blend));
      expect(blend.withStart('nothing'), same(blend));
    });

    test('adds a change, last in precedence', () {
      final blend = moves().withChangeAdded('lonely', 'idle');
      expect(blend.writtenChanges.last.from, 'lonely');
      expect(blend.writtenChanges, hasLength(5));
    });

    test('refuses a change to a state it does not have', () {
      final blend = moves();
      expect(blend.withChangeAdded('idle', 'nothing'), same(blend));
    });

    test('changes one change and leaves the others', () {
      final blend = moves().withChange(
        0,
        (change) => change.copyWith(fade: 0.5, inStep: true),
      );
      expect(blend.writtenChanges[0].fade, 0.5);
      expect(blend.writtenChanges[0].inStep, isTrue);
      expect(blend.writtenChanges[1].fade, 0);
    });

    test('takes a change out, and moves one up and down', () {
      final removed = moves().withChangeRemoved(1);
      expect(removed.writtenChanges, hasLength(3));
      final moved = moves().withChangeMoved(3, -3);
      expect(moved.writtenChanges.first.from, 'jump');
      expect(moved.writtenChanges[1].from, 'idle');
    });

    test('stops a change at either end of the precedence', () {
      final blend = moves();
      expect(blend.withChangeMoved(0, -1), same(blend));
      expect(blend.withChangeMoved(3, 1), same(blend));
      expect(blend.withChangeMoved(9, 1), same(blend));
    });

    test('sets an input and takes one out', () {
      final set = moves().withInput('turn', 0.5);
      expect(set.inputs['turn'], 0.5);
      expect(set.withInputRemoved('turn').inputs.containsKey('turn'), isFalse);
      expect(moves().withInput('', 1).inputs, moves().inputs);
      expect(moves().withInput('speed', double.nan).inputs['speed'], 0);
    });

    test('adds an input once, at nought', () {
      final added = moves().withInputAdded('turn');
      expect(added.inputs['turn'], 0);
      final set = moves().withInput('speed', 2);
      expect(set.withInputAdded('speed'), same(set));
      expect(moves().withInputAdded('').inputs, moves().inputs);
    });

    test('renames the blend, but not to nothing', () {
      expect(moves().withName('Walks').name, 'Walks');
      final blend = moves();
      expect(blend.withName(''), same(blend));
      expect(blend.withName('Moves'), same(blend));
    });

    test('writes and reads back as the blend it is', () {
      final blend = moves()
          .withStateRenamed('idle', 'stand')
          .withChange(0, (c) => c.copyWith(fade: 0.4))
          .withStateAdded('wave');
      final back = BlendDocument.decode(blend.encode());
      expect(back.problems, isEmpty);
      expect(back.blend.encode(), blend.encode());
    });
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/blend_bench.dart';
import 'package:orblit_editor/src/editor/blend_inspector.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_editor/src/theme/orblit_theme.dart';
import 'package:orblit_editor/src/widgets/controls.dart';
import 'package:orblit_motion/orblit_motion.dart'
    show BlendChange, BlendClip, BlendCondition, BlendDocument, BlendState;

// The fields of the blend graph's inspector: what is chosen decides what
// shows, and each edit is a step the undo stack can take back.

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

Future<({History history, BlendBench bench})> show(
  WidgetTester tester, {
  BlendPick pick = const NothingPicked(),
}) async {
  await tester.binding.setSurfaceSize(const Size(400, 1200));
  addTearDown(() => tester.binding.setSurfaceSize(null));
  final history = History(Workspace('/project'));
  final bench = BlendBench(history: history)
    ..openBlend('/project/moves.oblend', moves())
    ..pick = pick;
  await tester.pumpWidget(
    MaterialApp(
      theme: orblitTheme(),
      home: Scaffold(
        body: ListenableBuilder(
          listenable: bench,
          builder: (context, _) => BlendInspector(
            bench: bench,
            clips: () => const ['idle', 'walk', 'run'],
          ),
        ),
      ),
    ),
  );
  return (history: history, bench: bench);
}

Finder field(String text) => find.widgetWithText(TextField, text);

List<String> names(BlendDocument blend) => [
  for (final state in blend.writtenStates) state.name,
];

void main() {
  testWidgets('with nothing chosen it shows the blend and its inputs', (
    tester,
  ) async {
    await show(tester);
    expect(field('Moves'), findsOneWidget);
    expect(find.textContaining('2 states and 1 changes'), findsOneWidget);
    expect(field('0'), findsOneWidget);
  });

  testWidgets('typing a state a name is one step, and keeps the box', (
    tester,
  ) async {
    final (:history, :bench) = await show(
      tester,
      pick: const StatePicked('move'),
    );

    for (final text in ['r', 'ru', 'run']) {
      // The name box is the first, and its text is what is being changed.
      await tester.enterText(find.byType(TextField).first, text);
      await tester.pump();
    }

    expect(names(bench.blend!), ['idle', 'run']);
    expect(bench.pick, const StatePicked('run'));
    expect(history.labels, ['Rename state']);

    history.undo();
    await tester.pump();
    expect(names(bench.blend!), ['idle', 'move']);
  });

  testWidgets('choosing another state shows its own fields', (tester) async {
    final (:bench, history: _) = await show(
      tester,
      pick: const StatePicked('idle'),
    );
    expect(field('idle'), findsWidgets);

    bench.pick = const StatePicked('move');
    await tester.pump();

    expect(field('move'), findsWidgets);
    expect(field('walk'), findsWidgets);
  });

  testWidgets('adding a change from a state picks the new change', (
    tester,
  ) async {
    final (:bench, history: _) = await show(
      tester,
      pick: const StatePicked('move'),
    );

    final add = find.widgetWithText(OrblitButton, 'Add a change');
    await tester.ensureVisible(add);
    await tester.tap(add);
    await tester.pump();

    expect(bench.blend!.writtenChanges.length, 2);
    expect(bench.pick, const ChangePicked(1));
  });

  testWidgets('a change can be put earlier and later in precedence', (
    tester,
  ) async {
    final (:bench, history: _) = await show(
      tester,
      pick: const StatePicked('move'),
    );
    final add = find.widgetWithText(OrblitButton, 'Add a change');
    await tester.ensureVisible(add);
    await tester.tap(add);
    await tester.pump();
    expect(bench.pick, const ChangePicked(1));

    final earlier = find.widgetWithText(OrblitButton, 'Earlier');
    await tester.ensureVisible(earlier);
    await tester.tap(earlier);
    await tester.pump();

    expect(bench.pick, const ChangePicked(0));
    expect(bench.blend!.writtenChanges.first.from, 'move');
  });

  testWidgets('an input can be taken out', (tester) async {
    final (:bench, :history) = await show(tester);

    await tester.tap(find.byTooltip('Take out speed'));
    await tester.pump();
    expect(bench.blend!.inputs.containsKey('speed'), isFalse);
    expect(history.labels, ['Remove input speed']);
  });
}

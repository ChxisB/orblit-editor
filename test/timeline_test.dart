import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/clip_edits.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/timeline.dart';
import 'package:orblit_editor/src/editor/timeline_sheet.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_motion/orblit_motion.dart' show ClipDocument;

// The timeline panel and the inspector's key buttons, as widgets: what is on
// screen, and that a tap or a key reaches the bench. What the edits do to a
// clip is in clip_edits_test and clip_bench_test.

({EditorScene scene, SceneEntry entry, History history, ClipBench bench})
rig({bool open = true}) {
  final scene = EditorScene([
    SceneObject(id: 'lamp', name: 'Lamp', kind: ObjectKind.light, power: 100),
    SceneObject(id: 'box', name: 'Box', kind: ObjectKind.group),
  ]);
  final entry = SceneEntry(id: 'a', name: 'A', scene: scene);
  final workspace = Workspace('/project')..add(entry);
  final history = History(workspace);
  final bench = ClipBench(history: history, scene: () => scene);
  if (open) {
    bench.openClip(
      '/project/wave.oclip',
      ClipDocument(name: 'wave', duration: 2),
    );
  }
  return (scene: scene, entry: entry, history: history, bench: bench);
}

Future<void> showPanel(WidgetTester tester, ClipBench bench) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: TimelinePanel(bench: bench, selected: null, onProblem: (_) {}),
      ),
    ),
  );
  await tester.pump();
}

const ChannelAddress lampPower = (
  target: '',
  bone: null,
  property: 'light.power',
);

void main() {
  group('timeline panel', () {
    testWidgets('says what to do when no clip is open', (tester) async {
      await showPanel(tester, rig(open: false).bench);
      expect(find.textContaining('No clip open'), findsOneWidget);
      expect(find.byType(TimelineRuler), findsNothing);
    });

    testWidgets('a tap on the ruler moves the playhead onto a frame', (
      tester,
    ) async {
      final (:bench, scene: _, entry: _, history: _) = rig();
      await showPanel(tester, bench);
      expect(find.text('0.00 s   frame 0'), findsOneWidget);

      await tester.tap(find.byType(TimelineRuler));
      await tester.pump();

      final rate = bench.clip!.rate;
      final frame = (bench.at * rate).round();
      expect(bench.at, closeTo(1, 1 / rate));
      expect(bench.at * rate, closeTo(frame, 1e-9));
      expect(
        find.text('${bench.at.toStringAsFixed(2)} s   frame $frame'),
        findsOneWidget,
      );
    });

    testWidgets('Delete takes the picked keys only once the panel has focus', (
      tester,
    ) async {
      final (:scene, :history, :bench, entry: _) = rig();
      bench
        ..key(scene['lamp']!, 'light.power')
        ..selection = {(channel: lampPower, index: 0)};
      await showPanel(tester, bench);

      // Somewhere else has the keyboard: typing a length, say.
      await tester.sendKeyEvent(LogicalKeyboardKey.delete);
      await tester.pump();
      expect(channelAt(bench.clip!, lampPower), isNotNull);

      // Touching the panel gives it the keyboard.
      await tester.tap(find.byType(TimelineRuler));
      await tester.pump();
      await tester.sendKeyEvent(LogicalKeyboardKey.delete);
      await tester.pump();

      expect(channelAt(bench.clip!, lampPower), isNull);
      expect(history.labels.last, 'Delete key');
    });
  });

  group('key buttons', () {
    Future<void> showInspector(
      WidgetTester tester,
      ({EditorScene scene, SceneEntry entry, History history, ClipBench bench})
      rigged,
      String id,
    ) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Inspector(
              entry: rigged.entry,
              object: rigged.scene[id],
              history: rigged.history,
              onLoad: (_) {},
              keying: rigged.bench,
            ),
          ),
        ),
      );
      await tester.pump();
    }

    testWidgets('are not there with no clip open', (tester) async {
      await showInspector(tester, rig(open: false), 'box');
      expect(find.byIcon(Icons.diamond_outlined), findsNothing);
      expect(find.byIcon(Icons.diamond), findsNothing);
    });

    testWidgets('key a transform field, and show that it is keyed', (
      tester,
    ) async {
      final rigged = rig();
      await showInspector(tester, rigged, 'box');

      // Position, rotation and scale.
      expect(find.byIcon(Icons.diamond_outlined), findsNWidgets(3));

      await tester.tap(find.byIcon(Icons.diamond_outlined).first);
      await tester.pump();

      final box = rigged.scene['box']!;
      expect(rigged.bench.markFor(box, 'transform.position'), KeyMark.keyed);
      expect(find.byIcon(Icons.diamond), findsOneWidget);
      expect(find.byIcon(Icons.diamond_outlined), findsNWidgets(2));
    });
  });
}

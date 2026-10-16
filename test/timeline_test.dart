import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/clip_bench.dart';
import 'package:orblit_editor/src/editor/clip_edits.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/skeleton_view.dart';
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

Future<void> showPanel(
  WidgetTester tester,
  ClipBench bench, {
  VoidCallback? onNew,
  VoidCallback? onRetarget,
  ValueNotifier<SkeletonSettings>? skeleton,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: TimelinePanel(
          bench: bench,
          selected: null,
          skeleton: skeleton,
          onProblem: (_) {},
          onNew: onNew ?? () {},
          onRetarget: onRetarget,
        ),
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
      var made = 0;
      await showPanel(tester, rig(open: false).bench, onNew: () => made++);
      expect(find.text('No clip open'), findsOneWidget);
      expect(find.byType(TimelineRuler), findsNothing);

      // The button itself, rather than a file type to go and look for.
      await tester.tap(find.text('Make a clip'));
      expect(made, 1);
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

  group('clip menu', () {
    testWidgets('offers a retarget when the shell can make one', (
      tester,
    ) async {
      var asked = 0;
      await showPanel(tester, rig().bench, onRetarget: () => asked++);

      await tester.tap(find.text('wave'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Retarget to another model…'));
      await tester.pumpAndSettle();

      expect(asked, 1);
    });

    testWidgets('leaves it out for a clip that has no model to move', (
      tester,
    ) async {
      await showPanel(tester, rig().bench);

      await tester.tap(find.text('wave'));
      await tester.pumpAndSettle();

      expect(find.text('Make a clip'), findsOneWidget);
      expect(find.text('Retarget to another model…'), findsNothing);
    });
  });

  group('skeleton chips', () {
    testWidgets('are not there when the shell gives no settings', (
      tester,
    ) async {
      await showPanel(tester, rig().bench);
      expect(find.text('Bones'), findsNothing);
      expect(find.text('Onion'), findsNothing);
    });

    testWidgets('Bones switches the bones on and off', (tester) async {
      final settings = ValueNotifier(const SkeletonSettings());
      await showPanel(tester, rig().bench, skeleton: settings);

      await tester.tap(find.text('Bones'));
      await tester.pump();
      expect(settings.value.bones, isTrue);

      await tester.tap(find.text('Bones'));
      await tester.pump();
      expect(settings.value.bones, isFalse);
    });

    testWidgets('Onion brings the bones with it, and they take it back', (
      tester,
    ) async {
      final settings = ValueNotifier(const SkeletonSettings());
      await showPanel(tester, rig().bench, skeleton: settings);

      await tester.tap(find.text('Onion'));
      await tester.pump();
      expect(settings.value.onion, isTrue);
      expect(settings.value.bones, isTrue);

      await tester.tap(find.text('Bones'));
      await tester.pump();
      expect(settings.value.bones, isFalse);
      expect(settings.value.onion, isFalse);
    });

    testWidgets('the ghost menu appears with onion skin and says its count', (
      tester,
    ) async {
      final settings = ValueNotifier(const SkeletonSettings());
      await showPanel(tester, rig().bench, skeleton: settings);
      expect(find.text('2 each side, every 2 frames'), findsNothing);

      settings.value = settings.value.copyWith(bones: true, onion: true);
      await tester.pump();
      expect(find.text('2 each side, every 2 frames'), findsOneWidget);

      settings.value = settings.value.copyWith(before: 1, after: 1, stride: 1);
      await tester.pump();
      expect(find.text('1 each side, every frame'), findsOneWidget);
    });
    testWidgets('fit a narrow panel with onion skin on', (tester) async {
      tester.view.physicalSize = const Size(480, 300);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.reset);
      final settings = ValueNotifier(
        const SkeletonSettings(bones: true, onion: true),
      );

      await showPanel(tester, rig().bench, skeleton: settings);

      // An overflow is an exception, which the test would report.
      expect(tester.takeException(), isNull);
      expect(find.text('Bones'), findsOneWidget);
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

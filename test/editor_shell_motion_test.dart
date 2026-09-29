import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/dock_view.dart';
import 'package:orblit_editor/src/editor/game_view.dart';
import 'package:orblit_editor/src/editor/motion_section.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/timeline.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:path/path.dart' as p;

import 'support/editor_shell.dart';
import 'scene_playback_test.dart' show bounce;

EditorScene sceneOf(WidgetTester tester) => tester
    .widget<SceneViewport>(find.byType(SceneViewport))
    .workspace
    .loaded!
    .scene!;

void main() {
  useShell();

  Future<void> makeClip(WidgetTester tester) async {
    await tester.tap(row('Cube'));
    await tester.pumpAndSettle();
    await enterMode(tester, 'animation');
    await tester.tap(find.text('Make a clip'));
    await tester.pumpAndSettle();
  }

  testWidgets('a new clip belongs to its object and survives saving', (
    tester,
  ) async {
    await open(tester);
    await makeClip(tester);
    final cube = sceneOf(tester).objects
        .firstWhere((object) => object.name == 'Cube');
    expect(motionOf(cube)?.clips, ['clips/clip.oclip']);
    expect(motionOf(cube)?.autoplay, 'clips/clip.oclip');
    expect(
      tester.widget<TimelinePanel>(find.byType(TimelinePanel)).bench.owner,
      cube.id,
    );
    await save(tester);
    final files = Directory(p.join(root.path, 'scenes'))
        .listSync()
        .whereType<File>();
    expect(
      files.any((file) => file.readAsStringSync().contains('clips/clip.oclip')),
      isTrue,
    );
    await undo(tester);
    expect(motionOf(sceneOf(tester)[cube.id]!), isNull);
    expect(File(p.join(root.path, 'clips', 'clip.oclip')).existsSync(), isTrue);
  });

  testWidgets('Scene unfolds and folds the timeline with the selection', (
    tester,
  ) async {
    await open(tester);
    await makeClip(tester);
    await enterMode(tester, 'scene');
    expect(find.byType(TimelinePanel), findsOneWidget);
    expect(inMode(tester, 'scene'), isTrue);
    await add(tester, 'Camera');
    await tester.tap(row('Camera').last);
    await tester.pumpAndSettle();
    final dock = tester.widget<DockView>(find.byType(DockView));
    expect(dock.layout.groupOf('timeline')?.collapsed, isTrue);
    await tester.tap(row('Cube'));
    await tester.pumpAndSettle();
    expect(find.byType(TimelinePanel), findsOneWidget);
    final bench = tester
        .widget<TimelinePanel>(find.byType(TimelinePanel))
        .bench;
    expect(bench.shown?.path, endsWith('clips/clip.oclip'));
  });

  testWidgets(
    'Animation reads the first assigned clip and reports missing files',
    (tester) async {
      File(
        p.join(root.path, 'bounce.oclip'),
      ).writeAsStringSync(ClipDocument(name: 'Bounce', duration: 2).encode());
      await open(tester);
      final cube = sceneOf(tester).objects
          .firstWhere((object) => object.name == 'Cube');
      cube.components[doc.SceneComponents.motion] = const doc.MotionComponent(
        clips: ['bounce.oclip'],
      );
      await tester.tap(row('Cube'));
      await tester.pumpAndSettle();
      await enterMode(tester, 'animation');
      expect(find.text('Bounce'), findsOneWidget);
      cube.components[doc.SceneComponents.motion] = const doc.MotionComponent(
        clips: ['missing.oclip'],
      );
      await enterMode(tester, 'scene');
      expect(find.text('No clip open'), findsOneWidget);
      expect(find.textContaining('could not be read'), findsWidgets);
    },
  );

  testWidgets(
    'Play in four views previews unsaved animation without changing the scene',
    (tester) async {
      await open(tester);
      await makeClip(tester);
      final editing = sceneOf(tester);
      final cube = editing.objects.firstWhere(
        (object) => object.name == 'Cube',
      );
      final bench = tester
          .widget<TimelinePanel>(find.byType(TimelinePanel))
          .bench;
      bench.edit('Bounce', (_) => bounce());
      await tester.pumpAndSettle();
      final rest = cube.position.y;
      await viewMenu(tester, 'Four views');
      await tester.tap(find.byTooltip('Play scene animation.'));
      await tester.pump();
      await tester.pump(const Duration(milliseconds: 500));
      final playing = tester
          .widget<GameView>(find.byType(GameView))
          .workspace
          .loaded!
          .scene!;
      expect(playing[cube.id]!.position.y, closeTo(1, 0.05));
      expect(cube.position.y, rest);
      await tester.tap(find.byTooltip('Pause scene animation.'));
      await tester.pumpAndSettle();
      final paused = playing[cube.id]!.position.y;
      await tester.pump(const Duration(milliseconds: 500));
      expect(playing[cube.id]!.position.y, paused);
      await tester.tap(find.byTooltip('Stop animation and return to editing.'));
      await tester.pumpAndSettle();
      expect(
        tester.widget<GameView>(find.byType(GameView)).workspace.loaded!.scene,
        same(editing),
      );
      expect(cube.position.y, rest);
    },
  );
}

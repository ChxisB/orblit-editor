import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/gizmo.dart';
import 'package:orblit_editor/src/editor/gizmo_registry.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/sound_gizmo.dart';
import 'package:orblit_editor/src/editor/sound_section.dart';
import 'package:orblit_editor/src/editor/viewport.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// Sound on objects: carried through the scene file, offered where it can be
// used, edited as one step each, and drawn as the range it carries.

EditorScene scene({
  doc.SoundSourceComponent? source,
  doc.SoundListenerComponent? listener,
  doc.SoundSurfaceComponent? surface,
}) => EditorScene([
  SceneObject(
    id: 'fire',
    name: 'Fire',
    kind: ObjectKind.group,
    position: Vector3(0, 1, 0),
    components: {
      doc.SceneComponents.soundSource: ?source,
      doc.SceneComponents.soundListener: ?listener,
      doc.SceneComponents.soundSurface: ?surface,
    },
  ),
  SceneObject(id: 'rock', name: 'Rock', kind: ObjectKind.group),
  SceneObject(id: 'sky', name: 'Sky', kind: ObjectKind.weather),
]);

History historyOf(EditorScene scene) => History(
  Workspace('/project')..add(SceneEntry(id: 'a', name: 'A', scene: scene)),
);

InspectorTarget targetOf(EditorScene scene, String id, {History? history}) =>
    InspectorTarget(
      sceneId: 'a',
      scene: scene,
      object: scene[id]!,
      history: history ?? historyOf(scene),
    );

void main() {
  group('sound in a scene file', () {
    test('comes back as it was saved', () {
      final source = doc.SoundSourceComponent(
        clip: 'sounds/fire.ogg',
        bus: 'sfx',
        loop: false,
        volume: 0.5,
        near: 2,
        far: 30,
        curve: doc.SoundCurve.linear,
      );
      const surface = doc.SoundSurfaceComponent(name: 'wood');

      final back = SceneDocument.decode(
        SceneDocument.encode(
          scene(
            source: source,
            listener: const doc.SoundListenerComponent(),
            surface: surface,
          ),
        ),
      ).scene['fire']!;

      expect(soundSourceOf(back)!.toJson(), source.toJson());
      expect(back.components[doc.SceneComponents.soundListener], isNotNull);
      expect(
        (back.components[doc.SceneComponents.soundSurface]!
                as doc.SoundSurfaceComponent)
            .name,
        'wood',
      );
    });
  });

  group('the sound range', () {
    final source = doc.SoundSourceComponent(near: 2, far: 10);

    test('keeps the near end below the far end', () {
      expect(withNear(source, 50).near, closeTo(9.9, 1e-9));
      expect(withNear(source, 3).near, 3);
    });

    test('keeps the far end above the near end', () {
      expect(withFar(source, 0.5).far, closeTo(2.1, 1e-9));
      expect(withFar(source, 40).far, 40);
    });

    test('an empty clip is no clip, and the rest stays', () {
      final named = source.copyWith(clip: 'a.ogg', bus: 'sfx', loop: false);
      final cleared = withClip(named, '');

      expect(cleared.clip, isNull);
      expect(cleared.bus, 'sfx');
      expect(cleared.loop, isFalse);
      expect(cleared.far, 10);
      expect(withClip(named, 'b.ogg').clip, 'b.ogg');
    });
  });

  group('the sound section', () {
    test('is offered on an object, and not on the scene or the weather', () {
      final section = soundSection();
      final world = scene();

      expect(section.appliesTo(targetOf(world, 'fire')), isTrue);
      expect(section.appliesTo(targetOf(world, 'rock')), isTrue);
      expect(section.appliesTo(targetOf(world, 'sky')), isFalse);
    });
  });

  group('the sound rows', () {
    Future<void> show(WidgetTester tester, InspectorTarget target) =>
        tester.pumpWidget(
          MaterialApp(
            home: Material(
              child: SingleChildScrollView(child: soundSection().build(target)),
            ),
          ),
        );

    Finder choiceOf(String label, String option) => find.descendant(
      of: find.ancestor(of: find.text(label), matching: find.byType(ChoiceRow)),
      matching: find.text(option),
    );

    testWidgets('add a source, turn its loop off, and undo both', (
      tester,
    ) async {
      final world = scene();
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      await tester.tap(find.text('Add sound source'));
      await tester.pump();
      expect(soundSourceOf(world['fire']!), isNotNull);

      await show(tester, targetOf(world, 'fire', history: history));
      await tester.tap(choiceOf('Loop', 'No'));
      await tester.pump();

      final source = soundSourceOf(world['fire']!)!;
      expect(source.loop, isFalse);
      expect(source.far, 50, reason: 'nothing else moved');

      history.undo();
      expect(soundSourceOf(world['fire']!)!.loop, isTrue);
      history.undo();
      expect(soundSourceOf(world['fire']!), isNull);
    });

    testWidgets('a clip typed in is set, and an empty one clears it', (
      tester,
    ) async {
      final world = scene(source: doc.SoundSourceComponent());
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      await tester.enterText(find.byType(EditableText).first, 'a.ogg');
      await tester.pump();
      expect(soundSourceOf(world['fire']!)!.clip, 'a.ogg');

      await tester.enterText(find.byType(EditableText).first, '');
      await tester.pump();
      expect(soundSourceOf(world['fire']!)!.clip, isNull);
    });

    testWidgets('a surface typed in is added, and an empty one is removed', (
      tester,
    ) async {
      final world = scene();
      final history = historyOf(world);
      await show(tester, targetOf(world, 'rock', history: history));

      await tester.enterText(find.byType(EditableText).last, 'stone');
      await tester.pump();
      final surface =
          world['rock']!.components[doc.SceneComponents.soundSurface];
      expect((surface! as doc.SoundSurfaceComponent).name, 'stone');

      await tester.enterText(find.byType(EditableText).last, '');
      await tester.pump();
      expect(
        world['rock']!.components[doc.SceneComponents.soundSurface],
        isNull,
      );
    });

    testWidgets('the listener is switched on and off', (tester) async {
      final world = scene();
      final history = historyOf(world);
      await show(tester, targetOf(world, 'rock', history: history));

      await tester.tap(choiceOf('Listener', 'Yes'));
      await tester.pump();
      expect(
        world['rock']!.components[doc.SceneComponents.soundListener],
        isNotNull,
      );

      await show(tester, targetOf(world, 'rock', history: history));
      await tester.tap(choiceOf('Listener', 'No'));
      await tester.pump();
      expect(
        world['rock']!.components[doc.SceneComponents.soundListener],
        isNull,
      );
    });
  });

  group('the sound gizmo', () {
    const size = Size(800, 600);
    final projection = ViewportProjection(camera: OrbitCamera(), size: size);

    GizmoTarget targetOf(EditorScene scene, Set<String> selected) =>
        GizmoTarget(
          object: scene[selected.first]!,
          scene: scene,
          selected: selected,
          projection: projection,
        );

    test('is there for a selection with a source, and only then', () {
      final world = scene(source: doc.SoundSourceComponent());
      final gizmo = soundGizmo();

      expect(gizmo.appliesTo(targetOf(world, {'rock'})), isFalse);
      expect(gizmo.appliesTo(targetOf(world, {'fire'})), isTrue);
      expect(gizmo.appliesTo(targetOf(world, {'rock', 'fire'})), isTrue);
    });

    testWidgets('draws a ring for each end of the range', (tester) async {
      final world = scene(source: doc.SoundSourceComponent(near: 1, far: 4));
      await tester.pumpWidget(
        Directionality(
          textDirection: TextDirection.ltr,
          child: SizedBox.fromSize(
            size: size,
            child: Stack(
              children: [
                soundGizmo().overlay!(targetOf(world, {'fire'})),
              ],
            ),
          ),
        ),
      );

      // Each ring is stroked twice, a dark pass and then its colour. The far
      // ring is drawn first so the near one reads over it.
      final bounds = <Rect>[];
      expect(
        find.byType(CustomPaint),
        paints..something((method, arguments) {
          if (method != #drawPath) return false;
          bounds.add((arguments[0] as Path).getBounds());
          return bounds.length == 4;
        }),
      );
      final far = bounds[1];
      final near = bounds[3];
      final centre = projection.project(Vector3(0, 1, 0))!;

      expect(far.contains(centre), isTrue);
      expect(near.contains(centre), isTrue);
      expect(far.width, greaterThan(near.width * 2));
      expect(
        far.contains(near.topLeft) && far.contains(near.bottomRight),
        isTrue,
      );
    });
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/particles_section.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// Particles on objects: carried through the scene file, offered where they
// can be used, and edited as one step each.

EditorScene scene({doc.ParticleEmitterComponent? emitter}) => EditorScene([
  SceneObject(
    id: 'fire',
    name: 'Fire',
    kind: ObjectKind.group,
    position: Vector3(0, 1, 0),
    components: {doc.SceneComponents.particles: ?emitter},
  ),
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
  test('an emitter comes back from the scene file as it was saved', () {
    final emitter = doc.ParticleEmitterComponent(
      shape: doc.EmitterShape.cone,
      rate: 40,
      lifetimeMin: 0.5,
      lifetimeMax: 3,
      gravity: Vector3(0, -9.8, 0),
      space: doc.EmitterSpace.local,
    );

    final back = SceneDocument.decode(
      SceneDocument.encode(scene(emitter: emitter)),
    ).scene['fire']!;

    expect(particleEmitterOf(back)!.toJson(), emitter.toJson());
  });

  group('a range', () {
    final emitter = doc.ParticleEmitterComponent(
      lifetimeMin: 1,
      lifetimeMax: 2,
      speedMin: 3,
      speedMax: 4,
    );

    test('dragged past the other end takes it along', () {
      final longer = withLifetimeMin(emitter, 5);
      expect([longer.lifetimeMin, longer.lifetimeMax], [5, 5]);
      final shorter = withLifetimeMax(emitter, 0.5);
      expect([shorter.lifetimeMin, shorter.lifetimeMax], [0.5, 0.5]);
      final faster = withSpeedMin(emitter, 9);
      expect([faster.speedMin, faster.speedMax], [9, 9]);
      final slower = withSpeedMax(emitter, 1);
      expect([slower.speedMin, slower.speedMax], [1, 1]);
    });

    test('dragged within the range leaves the other end alone', () {
      final longer = withLifetimeMax(emitter, 6);
      expect([longer.lifetimeMin, longer.lifetimeMax], [1, 6]);
      final slower = withSpeedMin(emitter, 2);
      expect([slower.speedMin, slower.speedMax], [2, 4]);
    });
  });

  test('gravity is set along y and the sides are kept', () {
    final emitter = doc.ParticleEmitterComponent(gravity: Vector3(1, 2, 3));

    expect(withGravityY(emitter, -9).gravity, Vector3(1, -9, 3));
  });

  test('one box edge is set and the others are kept', () {
    final emitter = doc.ParticleEmitterComponent(extents: Vector3(1, 2, 3));
    final wider = withExtent(emitter, 1, 7);

    expect(wider.extents, Vector3(1, 7, 3));
    expect(emitter.extents, Vector3(1, 2, 3), reason: 'the old one is whole');
  });

  test('the section is offered on an object, and not on the weather', () {
    final section = particlesSection();
    final world = scene();

    expect(section.appliesTo(targetOf(world, 'fire')), isTrue);
    expect(section.appliesTo(targetOf(world, 'sky')), isFalse);
  });

  group('the rows', () {
    Future<void> show(WidgetTester tester, InspectorTarget target) =>
        tester.pumpWidget(
          MaterialApp(
            home: Material(
              child: SingleChildScrollView(
                child: particlesSection().build(target),
              ),
            ),
          ),
        );

    Finder choiceOf(String label, String option) => find.descendant(
      of: find.ancestor(of: find.text(label), matching: find.byType(ChoiceRow)),
      matching: find.text(option),
    );

    testWidgets('add an emitter, make it local, and undo both', (tester) async {
      final world = scene();
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      await tester.tap(find.text('Add particle emitter'));
      await tester.pump();
      expect(particleEmitterOf(world['fire']!), isNotNull);

      await show(tester, targetOf(world, 'fire', history: history));
      await tester.tap(choiceOf('Space', 'Local'));
      await tester.pump();

      final emitter = particleEmitterOf(world['fire']!)!;
      expect(emitter.space, doc.EmitterSpace.local);
      expect(emitter.rate, 10, reason: 'nothing else moved');

      history.undo();
      expect(particleEmitterOf(world['fire']!)!.space, doc.EmitterSpace.world);
      history.undo();
      expect(particleEmitterOf(world['fire']!), isNull);
    });

    testWidgets('a shape shows only the rows it uses', (tester) async {
      final world = scene(emitter: doc.ParticleEmitterComponent());
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      expect(find.text('Radius'), findsNothing);
      expect(find.text('Half X'), findsNothing);

      await tester.tap(choiceOf('Shape', 'Cone'));
      await tester.pump();
      await show(tester, targetOf(world, 'fire', history: history));
      expect(find.text('Radius'), findsOneWidget);
      expect(find.text('Angle'), findsOneWidget);
      expect(find.text('Half X'), findsNothing);

      await tester.tap(choiceOf('Shape', 'Box'));
      await tester.pump();
      await show(tester, targetOf(world, 'fire', history: history));
      expect(find.text('Half X'), findsOneWidget);
      expect(find.text('Radius'), findsNothing);
      expect(
        particleEmitterOf(world['fire']!)!.shape,
        doc.EmitterShape.box,
      );
    });

    testWidgets('choosing what is already chosen leaves nothing to undo', (
      tester,
    ) async {
      final world = scene(emitter: doc.ParticleEmitterComponent());
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      await tester.tap(choiceOf('Space', 'World'));
      await tester.tap(choiceOf('Shape', 'Point'));
      await tester.pump();

      expect(history.canUndo, isFalse);
    });

    testWidgets('remove takes the emitter off and undo puts it back', (
      tester,
    ) async {
      final emitter = doc.ParticleEmitterComponent(rate: 33);
      final world = scene(emitter: emitter);
      final history = historyOf(world);
      await show(tester, targetOf(world, 'fire', history: history));

      await tester.tap(find.text('Remove particle emitter'));
      await tester.pump();
      expect(particleEmitterOf(world['fire']!), isNull);

      history.undo();
      expect(particleEmitterOf(world['fire']!)!.rate, 33);
    });
  });
}

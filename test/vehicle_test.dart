import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_editor/src/editor/commands.dart';
import 'package:orblit_editor/src/editor/history.dart';
import 'package:orblit_editor/src/editor/inspector.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/scene_document.dart';
import 'package:orblit_editor/src/editor/vehicle_section.dart';
import 'package:orblit_editor/src/editor/workspace.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

// A car and its wheels on objects: carried through the editor, offered where
// the scene would use them, and edited as one step each.

/// A car with a wheel under it, a plain crate with a wheel-shaped child, and a
/// fixed slab. Only the wheel under the car has a wheel component.
EditorScene scene({doc.VehicleComponent? vehicle, doc.WheelComponent? wheel}) =>
    EditorScene([
      SceneObject(
        id: 'car',
        name: 'Car',
        kind: ObjectKind.group,
        components: {
          doc.SceneComponents.body: doc.BodyComponent(mass: 1200),
          doc.SceneComponents.vehicle: ?vehicle,
        },
      ),
      SceneObject(
        id: 'wheel',
        name: 'Wheel',
        kind: ObjectKind.group,
        parentId: 'car',
        position: Vector3(0.8, -0.2, -1.2),
        components: {doc.SceneComponents.wheel: ?wheel},
      ),
      SceneObject(
        id: 'crate',
        name: 'Crate',
        kind: ObjectKind.group,
        components: {doc.SceneComponents.body: doc.BodyComponent()},
      ),
      SceneObject(
        id: 'hub',
        name: 'Hub',
        kind: ObjectKind.group,
        parentId: 'crate',
      ),
      SceneObject(
        id: 'slab',
        name: 'Slab',
        kind: ObjectKind.group,
        components: {
          doc.SceneComponents.body: doc.BodyComponent(
            motion: doc.BodyMotion.fixed,
          ),
        },
      ),
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
  group('a car in a scene file', () {
    test('comes back as it was saved', () {
      final vehicle = doc.VehicleComponent(
        maxSteer: 35,
        antiRoll: 4000,
        ratios: [3.0, 1.8, 1.1],
        lock: 300,
      );
      const wheel = doc.WheelComponent(
        radius: 0.4,
        steers: true,
        handbrake: true,
        hertz: 2,
      );

      final back = SceneDocument.decode(
        SceneDocument.encode(scene(vehicle: vehicle, wheel: wheel)),
      ).scene;

      expect(vehicleOf(back['car']!)!.toJson(), vehicle.toJson());
      expect(wheelOf(back['wheel']!)!.toJson(), wheel.toJson());
    });
  });

  group('the vehicle section', () {
    test('is offered on a body that moves, and not on one that stays', () {
      final section = vehicleSection();
      final world = scene();

      expect(section.appliesTo(targetOf(world, 'car')), isTrue);
      expect(section.appliesTo(targetOf(world, 'slab')), isFalse);
      expect(section.appliesTo(targetOf(world, 'wheel')), isFalse);
    });

    test('is still shown for a vehicle a file put on a fixed body', () {
      final slab = scene()
        ..['slab']!.components[doc.SceneComponents.vehicle] =
            doc.VehicleComponent();
      expect(vehicleSection().appliesTo(targetOf(slab, 'slab')), isTrue);
    });
  });

  group('the wheel section', () {
    test(
      'is offered on a child of a car and on a wheel, and not elsewhere',
      () {
        final section = wheelSection();

        final plain = scene();
        expect(section.appliesTo(targetOf(plain, 'wheel')), isFalse);

        final car = scene(vehicle: doc.VehicleComponent());
        expect(section.appliesTo(targetOf(car, 'wheel')), isTrue);
        expect(section.appliesTo(targetOf(car, 'car')), isFalse);
        expect(section.appliesTo(targetOf(car, 'hub')), isFalse);

        final fitted = scene(wheel: const doc.WheelComponent());
        expect(section.appliesTo(targetOf(fitted, 'wheel')), isTrue);
      },
    );
  });

  group('editing a car', () {
    SetObjectComponent putVehicle(
      EditorScene scene,
      doc.VehicleComponent? to,
    ) => SetObjectComponent(
      sceneId: 'a',
      id: 'car',
      label: 'Set Car vehicle',
      type: doc.SceneComponents.vehicle,
      from: vehicleOf(scene['car']!),
      to: to,
    );

    test('adding one is undone by taking it off again', () {
      final world = scene();
      final history = historyOf(world);

      history.run(putVehicle(world, doc.VehicleComponent()));
      expect(vehicleOf(world['car']!), isNotNull);

      history.undo();
      expect(vehicleOf(world['car']!), isNull);
    });

    test('a drag through a field is one step', () {
      final world = scene(vehicle: doc.VehicleComponent());
      final history = historyOf(world);

      for (var i = 1; i <= 20; i++) {
        final vehicle = vehicleOf(world['car']!)!;
        history.run(putVehicle(world, vehicle.copyWith(maxSteer: 30.0 + i)));
      }
      expect(vehicleOf(world['car']!)!.maxSteer, 50);

      history.undo();
      expect(vehicleOf(world['car']!)!.maxSteer, 30);
      expect(vehicleOf(world['car']!), isNotNull);
    });
  });

  group('the wheel rows', () {
    Future<void> show(WidgetTester tester, InspectorTarget target) =>
        tester.pumpWidget(
          MaterialApp(
            home: Material(
              child: SingleChildScrollView(child: wheelSection().build(target)),
            ),
          ),
        );

    testWidgets('add a wheel, then say it steers and drives', (tester) async {
      final world = scene(vehicle: doc.VehicleComponent());
      final history = historyOf(world);
      await show(tester, targetOf(world, 'wheel', history: history));

      await tester.tap(find.text('Add wheel'));
      await tester.pump();
      expect(wheelOf(world['wheel']!), isNotNull);

      await show(tester, targetOf(world, 'wheel', history: history));
      final steers = find.descendant(
        of: find.ancestor(
          of: find.text('Steers'),
          matching: find.byType(ToggleRow),
        ),
        matching: find.text('Yes'),
      );
      await tester.tap(steers);
      await tester.pump();

      final wheel = wheelOf(world['wheel']!)!;
      expect(wheel.steers, isTrue);
      expect(wheel.drives, isFalse);
      expect(wheel.radius, 0.35, reason: 'nothing else moved');

      history.undo();
      expect(wheelOf(world['wheel']!)!.steers, isFalse);
    });
  });
}

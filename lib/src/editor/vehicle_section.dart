import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'body_section.dart';
import 'component_rows.dart';
import 'inspector.dart';
import 'scene.dart';

/// The vehicle on [object], if it has one.
doc.VehicleComponent? vehicleOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.vehicle]) {
      final doc.VehicleComponent vehicle => vehicle,
      _ => null,
    };

/// The wheel on [object], if it has one.
doc.WheelComponent? wheelOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.wheel]) {
      final doc.WheelComponent wheel => wheel,
      _ => null,
    };

/// The inspector's section for a car.
///
/// Offered on a body the solver moves, which is the only kind a car can be.
/// A vehicle a file already put on another kind is still shown, so it can be
/// seen and taken off. The torque curve and the gears are in the file; the
/// rows here are the ones a car is tuned by.
InspectorSection vehicleSection() => InspectorSection(
  name: 'vehicle',
  appliesTo: (target) {
    final body = physicsBodyOf(target.object);
    return vehicleOf(target.object) != null ||
        (body != null && movesFreely(body));
  },
  build: (target) => _VehicleSection(target: target),
);

/// The inspector's section for a wheel.
///
/// Offered on a direct child of a car, because that is where the scene looks
/// for a wheel, and on anything that already has one.
InspectorSection wheelSection() => InspectorSection(
  name: 'wheel',
  appliesTo: (target) {
    final parent = target.scene[target.object.parentId ?? ''];
    return wheelOf(target.object) != null ||
        (parent != null && vehicleOf(parent) != null);
  },
  build: (target) => _WheelSection(target: target),
);

class _VehicleSection extends StatelessWidget {
  const _VehicleSection({required this.target});

  final InspectorTarget target;

  @override
  Widget build(BuildContext context) {
    final rows = ComponentRows<doc.VehicleComponent>(
      target,
      type: doc.SceneComponents.vehicle,
      of: vehicleOf,
    );
    final name = target.object.name;
    if (vehicleOf(target.object) == null) {
      return OrblitSection(
        title: 'Vehicle',
        child: OrblitButton(
          label: 'Add vehicle',
          tooltip: 'Hold this body up on the wheels that are its children.',
          icon: Icons.add,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: () => rows.put(
            doc.VehicleComponent(),
            label: 'Add vehicle to $name',
            alone: true,
          ),
        ),
      );
    }
    return OrblitSection(
      title: 'Vehicle',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          rows.drag(
            'Steer lock',
            (vehicle) => vehicle.maxSteer,
            (vehicle, value) => vehicle.copyWith(maxSteer: value),
            minimum: 0,
            step: 0.2,
            decimals: 1,
          ),
          rows.drag(
            'Anti-roll',
            (vehicle) => vehicle.antiRoll,
            (vehicle, value) => vehicle.copyWith(antiRoll: value),
            minimum: 0,
            step: 20,
            decimals: 0,
          ),
          rows.drag(
            'Idle',
            (vehicle) => vehicle.idle,
            (vehicle, value) => vehicle.copyWith(idle: value),
            minimum: 100,
            step: 5,
            decimals: 0,
          ),
          rows.drag(
            'Final drive',
            (vehicle) => vehicle.finalDrive,
            (vehicle, value) => vehicle.copyWith(finalDrive: value),
            minimum: 0.1,
          ),
          rows.drag(
            'Reverse',
            (vehicle) => vehicle.reverse,
            (vehicle, value) => vehicle.copyWith(reverse: value),
            minimum: 0.1,
          ),
          rows.drag(
            'Diff lock',
            (vehicle) => vehicle.lock,
            (vehicle, value) => vehicle.copyWith(lock: value),
            minimum: 0,
            step: 2,
            decimals: 0,
          ),
          const SizedBox(height: Space.xs),
          OrblitButton(
            label: 'Remove vehicle',
            tooltip: 'Make this an ordinary body again.',
            icon: Icons.remove,
            tone: ButtonTone.quiet,
            expand: true,
            onPressed: () =>
                rows.put(null, label: 'Remove vehicle from $name', alone: true),
          ),
        ],
      ),
    );
  }
}

class _WheelSection extends StatelessWidget {
  const _WheelSection({required this.target});

  final InspectorTarget target;

  @override
  Widget build(BuildContext context) {
    final rows = ComponentRows<doc.WheelComponent>(
      target,
      type: doc.SceneComponents.wheel,
      of: wheelOf,
    );
    final name = target.object.name;
    if (wheelOf(target.object) == null) {
      return OrblitSection(
        title: 'Wheel',
        child: OrblitButton(
          label: 'Add wheel',
          tooltip:
              'Make this object a wheel. Put it where the wheel sits at rest.',
          icon: Icons.add,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: () => rows.put(
            const doc.WheelComponent(),
            label: 'Add wheel to $name',
            alone: true,
          ),
        ),
      );
    }
    return OrblitSection(
      title: 'Wheel',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          rows.toggle(
            'Steers',
            (wheel) => wheel.steers,
            (wheel, on) => wheel.copyWith(steers: on),
          ),
          rows.toggle(
            'Drives',
            (wheel) => wheel.drives,
            (wheel, on) => wheel.copyWith(drives: on),
          ),
          rows.toggle(
            'Handbrake',
            (wheel) => wheel.handbrake,
            (wheel, on) => wheel.copyWith(handbrake: on),
          ),
          rows.drag(
            'Radius',
            (wheel) => wheel.radius,
            (wheel, value) => wheel.copyWith(radius: value),
            minimum: 0.05,
          ),
          rows.drag(
            'Travel',
            (wheel) => wheel.travel,
            (wheel, value) => wheel.copyWith(travel: value),
            minimum: 0.02,
          ),
          rows.drag(
            'Spring',
            (wheel) => wheel.hertz,
            (wheel, value) => wheel.copyWith(hertz: value),
            minimum: 0.5,
          ),
          rows.drag(
            'Damping',
            (wheel) => wheel.damping,
            (wheel, value) => wheel.copyWith(damping: value),
            minimum: 0.05,
          ),
          rows.drag(
            'Grip',
            (wheel) => wheel.grip,
            (wheel, value) => wheel.copyWith(grip: value),
            minimum: 0.05,
          ),
          rows.drag(
            'Brake',
            (wheel) => wheel.brake,
            (wheel, value) => wheel.copyWith(brake: value),
            minimum: 0,
            step: 20,
            decimals: 0,
          ),
          const SizedBox(height: Space.xs),
          OrblitButton(
            label: 'Remove wheel',
            tooltip: 'Make this an ordinary object again.',
            icon: Icons.remove,
            tone: ButtonTone.quiet,
            expand: true,
            onPressed: () =>
                rows.put(null, label: 'Remove wheel from $name', alone: true),
          ),
        ],
      ),
    );
  }
}

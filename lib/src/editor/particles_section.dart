import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' show Vector3;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'component_rows.dart';
import 'inspector.dart';
import 'scene.dart';

/// The particle emitter on [object], if it has one.
doc.ParticleEmitterComponent? particleEmitterOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.particles]) {
      final doc.ParticleEmitterComponent emitter => emitter,
      _ => null,
    };

/// How short a life may be. A particle that lives no time is no particle, and
/// the scene file does not accept one.
const double _shortest = 0.05;

/// [emitter] with the shortest life at [value], and the longest raised to meet
/// it, so dragging one end past the other moves both and leaves a range.
doc.ParticleEmitterComponent withLifetimeMin(
  doc.ParticleEmitterComponent emitter,
  double value,
) => emitter.copyWith(
  lifetimeMin: value,
  lifetimeMax: math.max(emitter.lifetimeMax, value),
);

/// [emitter] with the longest life at [value], and the shortest lowered to
/// meet it.
doc.ParticleEmitterComponent withLifetimeMax(
  doc.ParticleEmitterComponent emitter,
  double value,
) => emitter.copyWith(
  lifetimeMax: value,
  lifetimeMin: math.min(emitter.lifetimeMin, value),
);

doc.ParticleEmitterComponent withSpeedMin(
  doc.ParticleEmitterComponent emitter,
  double value,
) => emitter.copyWith(
  speedMin: value,
  speedMax: math.max(emitter.speedMax, value),
);

doc.ParticleEmitterComponent withSpeedMax(
  doc.ParticleEmitterComponent emitter,
  double value,
) => emitter.copyWith(
  speedMax: value,
  speedMin: math.min(emitter.speedMin, value),
);

/// [emitter] with the box half-extent on [axis] at [value].
doc.ParticleEmitterComponent withExtent(
  doc.ParticleEmitterComponent emitter,
  int axis,
  double value,
) => emitter.copyWith(extents: emitter.extents.clone()..[axis] = value);

/// [emitter] with gravity along y at [value]. The sideways parts stay as the
/// file has them.
doc.ParticleEmitterComponent withGravityY(
  doc.ParticleEmitterComponent emitter,
  double value,
) => emitter.copyWith(
  gravity: Vector3(emitter.gravity.x, value, emitter.gravity.z),
);

const _shapeNames = {
  doc.EmitterShape.point: 'Point',
  doc.EmitterShape.sphere: 'Sphere',
  doc.EmitterShape.cone: 'Cone',
  doc.EmitterShape.box: 'Box',
};

const _axes = ['X', 'Y', 'Z'];

/// The inspector's section for particles: what an object gives off, how
/// fast, how long it lasts and where it goes.
///
/// Offered on every object, since any of them can be a source.
InspectorSection particlesSection() => InspectorSection(
  name: 'particles',
  appliesTo: (target) =>
      target.object.kind != ObjectKind.scene &&
      target.object.kind != ObjectKind.weather,
  build: (target) => _ParticlesSection(target: target),
);

class _ParticlesSection extends StatelessWidget {
  const _ParticlesSection({required this.target});

  final InspectorTarget target;

  @override
  Widget build(BuildContext context) {
    final name = target.object.name;
    final rows = ComponentRows<doc.ParticleEmitterComponent>(
      target,
      type: doc.SceneComponents.particles,
      of: particleEmitterOf,
    );
    final emitter = particleEmitterOf(target.object);
    return OrblitSection(
      title: 'Particles',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: emitter == null
            ? [
                OrblitButton(
                  label: 'Add particle emitter',
                  tooltip: 'Make this object give off particles.',
                  icon: Icons.add,
                  tone: ButtonTone.quiet,
                  expand: true,
                  onPressed: () => rows.put(
                    doc.ParticleEmitterComponent(),
                    label: 'Add particle emitter to $name',
                    alone: true,
                  ),
                ),
              ]
            : [
                ..._shape(rows, emitter),
                ..._motion(rows),
                rows.drag(
                  'Max',
                  (e) => e.maxParticles.toDouble(),
                  (e, value) => e.copyWith(maxParticles: value.round()),
                  minimum: 1,
                  step: 10,
                  decimals: 0,
                ),
                const SizedBox(height: Space.xs),
                OrblitButton(
                  label: 'Remove particle emitter',
                  tooltip: 'Stop this object giving off particles.',
                  icon: Icons.remove,
                  tone: ButtonTone.quiet,
                  expand: true,
                  onPressed: () => rows.put(
                    null,
                    label: 'Remove particle emitter from $name',
                    alone: true,
                  ),
                ),
              ],
      ),
    );
  }

  List<Widget> _shape(
    ComponentRows<doc.ParticleEmitterComponent> rows,
    doc.ParticleEmitterComponent emitter,
  ) => [
    rows.choice(
      'Shape',
      [..._shapeNames.values],
      (e) => _shapeNames[e.shape]!,
      (e, option) => e.copyWith(
        shape: _shapeNames.entries.firstWhere((s) => s.value == option).key,
      ),
    ),
    if (emitter.shape == doc.EmitterShape.sphere ||
        emitter.shape == doc.EmitterShape.cone)
      rows.drag(
        'Radius',
        (e) => e.radius,
        (e, value) => e.copyWith(radius: value),
        minimum: 0,
      ),
    if (emitter.shape == doc.EmitterShape.cone)
      rows.drag(
        'Angle',
        (e) => e.angle,
        (e, value) => e.copyWith(angle: math.min(value, math.pi)),
        minimum: 0,
      ),
    if (emitter.shape == doc.EmitterShape.box)
      for (var axis = 0; axis < 3; axis++)
        rows.drag(
          'Half ${_axes[axis]}',
          (e) => e.extents[axis],
          (e, value) => withExtent(e, axis, value),
          minimum: 0,
        ),
  ];

  List<Widget> _motion(ComponentRows<doc.ParticleEmitterComponent> rows) => [
    rows.drag(
      'Rate',
      (e) => e.rate,
      (e, value) => e.copyWith(rate: value),
      minimum: 0,
      step: 0.5,
      decimals: 1,
    ),
    rows.drag(
      'Life min',
      (e) => e.lifetimeMin,
      withLifetimeMin,
      minimum: _shortest,
      step: 0.05,
    ),
    rows.drag(
      'Life max',
      (e) => e.lifetimeMax,
      withLifetimeMax,
      minimum: _shortest,
      step: 0.05,
    ),
    rows.drag('Speed min', (e) => e.speedMin, withSpeedMin, minimum: 0),
    rows.drag('Speed max', (e) => e.speedMax, withSpeedMax, minimum: 0),
    rows.drag(
      'Gravity',
      (e) => e.gravity.y,
      withGravityY,
      step: 0.1,
      decimals: 1,
    ),
    rows.drag(
      'Drag',
      (e) => e.drag,
      (e, value) => e.copyWith(drag: value),
      minimum: 0,
    ),
    rows.drag(
      'Noise',
      (e) => e.noiseStrength,
      (e, value) => e.copyWith(noiseStrength: value),
      minimum: 0,
    ),
    rows.choice(
      'Space',
      const ['World', 'Local'],
      (e) => e.space == doc.EmitterSpace.local ? 'Local' : 'World',
      (e, option) => e.copyWith(
        space: option == 'Local'
            ? doc.EmitterSpace.local
            : doc.EmitterSpace.world,
      ),
    ),
  ];
}

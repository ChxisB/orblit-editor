import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';

/// The body on [object], if it has one: what the physics does with it.
///
/// A body lives among the components the row carries rather than as fields of
/// its own. The row's `body` is already the sun or the moon a light stands
/// for, which is a different thing that happens to share the word.
doc.BodyComponent? physicsBodyOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.body]) {
      final doc.BodyComponent body => body,
      _ => null,
    };

/// How big an object's mesh is in its own space, for fitting a body to it.
typedef BoundsOf = ({Vector3 min, Vector3 max}) Function(SceneObject object);

/// The inspector's section for a body.
///
/// Registered by the shell through the same registry as any other section,
/// because the inspector has no idea what a body is and should not need one.
/// [boundsOf] is how big each mesh is, which the shell knows and the
/// inspector does not: an imported model's size comes from the file.
InspectorSection bodySection({BoundsOf? boundsOf}) => InspectorSection(
  name: 'body',
  // What something can stand on or knock over: anything drawn, and a place in
  // the tree for a trigger with nothing to draw. Lights, cameras and the
  // weather are left alone unless a file already gave one a body.
  appliesTo: (target) =>
      physicsBodyOf(target.object) != null ||
      target.object.isDrawable ||
      target.object.kind == ObjectKind.group,
  build: (target) => _BodySection(target: target, boundsOf: boundsOf),
);

class _BodySection extends StatelessWidget {
  const _BodySection({required this.target, required this.boundsOf});

  final InspectorTarget target;
  final BoundsOf? boundsOf;

  SceneObject get _object => target.object;

  static const _shapes = {
    doc.BodyShape.box: 'Box',
    doc.BodyShape.sphere: 'Ball',
    doc.BodyShape.capsule: 'Capsule',
    doc.BodyShape.plane: 'Ground',
  };

  static const _motions = {
    doc.BodyMotion.fixed: 'Fixed',
    doc.BodyMotion.driven: 'Driven',
    doc.BodyMotion.free: 'Free',
  };

  @override
  Widget build(BuildContext context) {
    final body = physicsBodyOf(_object);
    return OrblitSection(
      title: 'Physics body',
      icon: Icons.sports_baseball_outlined,
      child: body == null ? _absent() : _present(body),
    );
  }

  Widget _absent() => OrblitButton(
    label: 'Add body',
    icon: Icons.add,
    tone: ButtonTone.quiet,
    expand: true,
    onPressed: () => _put(
      // A drawn thing gets a body its own shape, which is what somebody adding
      // one to a crate means. A place in the tree gets a metre box to start.
      _object.isDrawable ? _fitted(doc.BodyComponent()) : doc.BodyComponent(),
      label: 'Add body to ${_object.name}',
      alone: true,
    ),
  );

  Widget _present(doc.BodyComponent body) {
    final ground = body.shape == doc.BodyShape.plane;
    final free = body.motion == doc.BodyMotion.free && !ground;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ChoiceRow(
          label: 'Shape',
          options: _shapes.values.toList(),
          selected: _shapes[body.shape]!,
          onSelect: (label) =>
              _choose(body.copyWith(shape: _key(_shapes, label))),
        ),
        // Ground never moves, whatever the file says, so there is nothing to
        // choose and the row says so instead of offering a choice it ignores.
        ground
            ? ChoiceRow(
                label: 'Motion',
                options: const ['Fixed'],
                selected: 'Fixed',
              )
            : ChoiceRow(
                label: 'Motion',
                options: _motions.values.toList(),
                selected: _motions[body.motion]!,
                onSelect: (label) =>
                    _choose(body.copyWith(motion: _key(_motions, label))),
              ),
        if (body.shape == doc.BodyShape.box)
          _drag(
            'Size',
            (body) => body.size.storage,
            (body, values) => body.copyWith(size: Vector3.array(values)),
            minimum: 0.01,
          ),
        if (body.shape == doc.BodyShape.sphere ||
            body.shape == doc.BodyShape.capsule)
          _drag(
            'Radius',
            (body) => [body.radius],
            (body, values) => body.copyWith(radius: values.single),
            minimum: 0.01,
          ),
        if (body.shape == doc.BodyShape.capsule)
          _drag(
            'Height',
            (body) => [body.height],
            (body, values) => body.copyWith(height: values.single),
            minimum: 0.01,
          ),
        _drag(
          ground ? 'Surface at' : 'Centre',
          (body) => body.centre.storage,
          (body, values) => body.copyWith(centre: Vector3.array(values)),
        ),
        if (free)
          _drag(
            'Mass',
            (body) => [body.mass],
            (body, values) => body.copyWith(mass: values.single),
            minimum: 0.01,
            step: 0.1,
          ),
        _slider(
          'Friction',
          body.friction,
          (body, value) => body.copyWith(friction: value),
        ),
        _slider(
          'Bounce',
          body.restitution,
          (body, value) => body.copyWith(restitution: value),
        ),
        if (free) ...[
          _slider(
            'Drag',
            body.linearDamping,
            (body, value) => body.copyWith(linearDamping: value),
          ),
          _slider(
            'Spin drag',
            body.angularDamping,
            (body, value) => body.copyWith(angularDamping: value),
          ),
          ChoiceRow(
            label: 'Starts',
            options: const ['Awake', 'Asleep'],
            selected: body.startsAsleep ? 'Asleep' : 'Awake',
            onSelect: (label) =>
                _choose(body.copyWith(startsAsleep: label == 'Asleep')),
          ),
        ],
        const SizedBox(height: Space.xs),
        Row(
          children: [
            if (_object.isDrawable) ...[
              Expanded(
                child: OrblitButton(
                  label: 'Fit to mesh',
                  icon: Icons.fit_screen_outlined,
                  tone: ButtonTone.quiet,
                  expand: true,
                  onPressed: () => _put(
                    _fitted(body),
                    label: 'Fit ${_object.name} body to its mesh',
                    alone: true,
                  ),
                ),
              ),
              const SizedBox(width: Space.xs),
            ],
            Expanded(
              child: OrblitButton(
                label: 'Remove body',
                icon: Icons.remove,
                tone: ButtonTone.quiet,
                expand: true,
                onPressed: () => _put(
                  null,
                  label: 'Remove body from ${_object.name}',
                  alone: true,
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  /// A row of numbers read off the body and dragged into a new one.
  ///
  /// Both directions take the body as it is at that moment rather than the
  /// one this section was built with, because a drag changes it every frame
  /// and the section is not rebuilt for each.
  Widget _drag(
    String label,
    List<double> Function(doc.BodyComponent body) read,
    doc.BodyComponent Function(doc.BodyComponent body, List<double> values)
    change, {
    double? minimum,
    double step = 0.01,
  }) => DragRow(
    label: label,
    listenable: target.history,
    read: () {
      final body = physicsBodyOf(_object);
      return body == null ? const [0.0] : read(body);
    },
    onChanged: (values) {
      final body = physicsBodyOf(_object);
      if (body != null) _put(change(body, values));
    },
    onSettled: target.history.seal,
    minimum: minimum,
    step: step,
  );

  Widget _slider(
    String label,
    double value,
    doc.BodyComponent Function(doc.BodyComponent body, double value) change,
  ) => SliderRow(
    label: label,
    value: value,
    min: 0,
    max: 1,
    decimals: 2,
    onChanged: (value) {
      final body = physicsBodyOf(_object);
      if (body != null) _put(change(body, value));
    },
    onSettled: target.history.seal,
  );

  void _choose(doc.BodyComponent next) => _put(next, alone: true);

  /// Replaces the body with [next], or takes it off for null.
  ///
  /// [alone] for a click: sealed at once, so it is its own step. A drag is
  /// sealed by the row when the pointer lifts.
  void _put(doc.BodyComponent? next, {String? label, bool alone = false}) {
    target.history.run(
      SetObjectComponent(
        sceneId: target.sceneId,
        id: _object.id,
        label: label ?? 'Set ${_object.name} body',
        type: doc.SceneComponents.body,
        from: physicsBodyOf(_object),
        to: next,
      ),
    );
    if (alone) target.history.seal();
  }

  /// [body] reshaped round the object's mesh: the same shape, as big as the
  /// mesh and centred on it.
  ///
  /// In the object's own units, as a body is, so the fit is the mesh's size
  /// whatever the object is scaled to.
  doc.BodyComponent _fitted(doc.BodyComponent body) {
    final bounds = boundsOf?.call(_object) ?? _object.localBounds();
    final size = bounds.max - bounds.min;
    final middle = (bounds.min + bounds.max) * 0.5;

    return switch (body.shape) {
      doc.BodyShape.box => body.copyWith(size: size, centre: middle),
      doc.BodyShape.sphere => body.copyWith(
        radius: math.max(size.x, math.max(size.y, size.z)) / 2,
        centre: middle,
      ),
      doc.BodyShape.capsule => body.copyWith(
        radius: math.max(size.x, size.z) / 2,
        height: size.y,
        centre: middle,
      ),
      // Ground is the top of the mesh: a floor tile is stood on, not in.
      doc.BodyShape.plane => body.copyWith(
        centre: Vector3(middle.x, bounds.max.y, middle.z),
      ),
    };
  }

  static T _key<T>(Map<T, String> labels, String label) =>
      labels.entries.firstWhere((entry) => entry.value == label).key;
}

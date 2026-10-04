import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'commands.dart';
import 'body_parts.dart';
import 'convex_outline.dart';
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

/// The zone on [object], if it has one: a region that changes how the bodies
/// inside it move. It uses the object's body as the region.
doc.ZoneComponent? zoneOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.zone]) {
      final doc.ZoneComponent zone => zone,
      _ => null,
    };

/// Whether the solver moves [body]. Ground never moves, whatever its file
/// says.
bool movesFreely(doc.BodyComponent body) =>
    body.motion == doc.BodyMotion.free && body.shape != doc.BodyShape.plane;

/// Whether [object] is a place rather than a thing: a body that asks to be a
/// trigger, or that is a zone's region. The solver moves a free body and
/// cannot make one a place, so a free body is never one.
bool isPlace(SceneObject object) {
  final body = physicsBodyOf(object);
  if (body == null || movesFreely(body)) return false;
  return body.trigger || zoneOf(object) != null;
}

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
    doc.BodyShape.cylinder: 'Cylinder',
    doc.BodyShape.hull: 'Hull',
    doc.BodyShape.compound: 'Compound',
    doc.BodyShape.plane: 'Ground',
  };

  /// The most corners the physics world keeps of a hull.
  static const _keptCorners = 255;

  static const _motions = {
    doc.BodyMotion.fixed: 'Fixed',
    doc.BodyMotion.driven: 'Driven',
    doc.BodyMotion.free: 'Free',
  };

  static final _materials = {
    for (final material in doc.BodyMaterial.presets) material: material.name,
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
    tooltip: 'Give this object a shape for physics.',
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
    final free = movesFreely(body);
    final place = isPlace(_object);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        ..._shapeRows(body),
        // Ground never moves, whatever the file says, so there is nothing to
        // choose and the row says so instead of offering a choice it ignores.
        ground
            ? const ChoiceRow(
                label: 'Motion',
                options: ['Fixed'],
                selected: 'Fixed',
              )
            : ChoiceRow(
                label: 'Motion',
                options: _motions.values.toList(),
                selected: _motions[body.motion]!,
                onSelect: (label) =>
                    _choose(body.copyWith(motion: _key(_motions, label))),
              ),
        ..._role(body, free: free),
        if (body.shape == doc.BodyShape.box)
          _drag(
            'Size',
            (body) => body.size.storage,
            (body, values) => body.copyWith(size: Vector3.array(values)),
            minimum: 0.01,
          ),
        if (body.shape == doc.BodyShape.sphere ||
            body.shape == doc.BodyShape.capsule ||
            body.shape == doc.BodyShape.cylinder)
          _drag(
            'Radius',
            (body) => [body.radius],
            (body, values) => body.copyWith(radius: values.single),
            minimum: 0.01,
          ),
        if (body.shape == doc.BodyShape.capsule ||
            body.shape == doc.BodyShape.cylinder)
          _drag(
            'Height',
            (body) => [body.height],
            (body, values) => body.copyWith(height: values.single),
            minimum: 0.01,
          ),
        if (body.shape == doc.BodyShape.hull) _hullNote(body),
        if (body.shape == doc.BodyShape.compound)
          BodyParts(
            read: () => physicsBodyOf(_object) ?? body,
            put: _put,
            settle: target.history.seal,
            listenable: target.history,
            hull: _fitted(body.copyWith(shape: doc.BodyShape.hull)).hull,
          ),
        if (!ground)
          _drag(
            'Shape scale',
            (body) => body.shapeScale.storage,
            (body, values) => body.copyWith(shapeScale: Vector3.array(values)),
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
        PickRow(
          label: 'Material',
          shown: doc.BodyMaterial.of(body)?.name ?? 'Custom',
          options: _materials,
          onSelect: (material) => _choose(body.madeOf(material)),
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
        // A place pushes nothing and a free body is not fixed to anything, so
        // only a solid body that stays put can be a belt.
        if (!free && !place)
          _drag(
            'Belt speed',
            (body) => body.surface.storage,
            (body, values) => body.copyWith(surface: Vector3.array(values)),
            step: 0.05,
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
          ..._movement(body),
        ],
        ..._layers(body),
        const SizedBox(height: Space.xs),
        OrblitButtonRow(
          buttons: [
            if (_object.isDrawable)
              OrblitButton(
                label: 'Fit to mesh',
                tooltip: 'Resize the physics shape to fit this object.',
                icon: Icons.fit_screen_outlined,
                tone: ButtonTone.quiet,
                expand: true,
                onPressed: () => _put(
                  _fitted(body),
                  label: 'Fit ${_object.name} body to its mesh',
                  alone: true,
                ),
              ),
            OrblitButton(
              label: 'Remove body',
              tooltip: 'Stop simulating physics for this object.',
              icon: Icons.remove,
              tone: ButtonTone.quiet,
              expand: true,
              onPressed: () => _put(
                null,
                label: 'Remove body from ${_object.name}',
                alone: true,
              ),
            ),
          ],
        ),
      ],
    );
  }

  /// Every shape as a toggle, in rows of three because the labels do not fit
  /// across the panel.
  List<Widget> _shapeRows(doc.BodyComponent body) {
    const across = 3;
    final labels = _shapes.values.toList();
    return [
      for (var first = 0; first < labels.length; first += across)
        ChoiceRow(
          label: first == 0 ? 'Shape' : '',
          options: labels.sublist(
            first,
            math.min(first + across, labels.length),
          ),
          selected: _shapes[body.shape]!,
          onSelect: (label) => _choose(_reshaped(body, _key(_shapes, label))),
        ),
    ];
  }

  /// What a hull is made of. Its points are only in the file, so this says how
  /// many there are, or what is missing.
  Widget _hullNote(doc.BodyComponent body) {
    final outline = ConvexOutline.of(body.hull);
    final corners = outline.corners.length;
    if (!outline.hasVolume) {
      return const _Note(
        'Needs four points that enclose a volume. Until then the body does '
        'nothing.',
      );
    }
    return _Note(
      corners > _keptCorners
          ? '$corners corners. The simulation keeps the $_keptCorners that '
                'stand out most.'
          : '$corners corners.',
    );
  }

  /// Whether the body is a thing or a place, and whether it hears every step
  /// of what touches it or is inside it.
  List<Widget> _role(doc.BodyComponent body, {required bool free}) => [
    // The solver moves a free body and cannot move a place, so there is
    // nothing to choose for one.
    if (!free)
      // A zone is a place whatever the body says, so that row reads as fact.
      zoneOf(_object) != null
          ? const ChoiceRow(
              label: 'Acts as',
              options: ['Trigger'],
              selected: 'Trigger',
            )
          : ChoiceRow(
              label: 'Acts as',
              options: const ['Solid', 'Trigger'],
              selected: body.trigger ? 'Trigger' : 'Solid',
              onSelect: (label) =>
                  _choose(body.copyWith(trigger: label == 'Trigger')),
            ),
    ChoiceRow(
      label: 'Stay events',
      options: const ['Off', 'On'],
      selected: body.stay ? 'On' : 'Off',
      onSelect: (label) => _choose(body.copyWith(stay: label == 'On')),
    ),
  ];

  /// How a free body is held and limited, and where its weight sits.
  List<Widget> _movement(doc.BodyComponent body) => [
    _drag(
      'Gravity',
      (body) => [body.gravityScale],
      (body, values) => body.copyWith(gravityScale: values.single),
      step: 0.02,
    ),
    _locks('Lock move', body, [
      doc.BodyLock.moveX,
      doc.BodyLock.moveY,
      doc.BodyLock.moveZ,
    ]),
    _locks('Lock turn', body, [
      doc.BodyLock.turnX,
      doc.BodyLock.turnY,
      doc.BodyLock.turnZ,
    ]),
    _drag(
      'Max speed',
      (body) => [body.maxSpeed],
      (body, values) => body.copyWith(maxSpeed: values.single),
      minimum: 0,
      step: 0.1,
    ),
    _drag(
      'Max spin',
      (body) => [body.maxSpin],
      (body, values) => body.copyWith(maxSpin: values.single),
      minimum: 0,
      step: 0.1,
    ),
    const _Note('Zero is no limit.'),
    _drag(
      'Weight at',
      (body) => body.centreOfMass.storage,
      (body, values) => body.copyWith(centreOfMass: Vector3.array(values)),
    ),
    _drag(
      'Inertia',
      (body) => body.inertia.storage,
      (body, values) => body.copyWith(inertia: Vector3.array(values)),
      minimum: 0,
    ),
    const _Note('Give all three or the shape\'s own inertia is used.'),
  ];

  /// One switch for each of [axes], which a lock holds the body from.
  Widget _locks(
    String label,
    doc.BodyComponent body,
    List<doc.BodyLock> axes,
  ) => ToggleRow(
    label: label,
    cells: [
      for (final (index, lock) in axes.indexed)
        ToggleCell(
          label: 'XYZ'[index],
          on: body.locks.contains(lock),
          onTap: () => _choose(
            body.copyWith(
              locks: body.locks.contains(lock)
                  ? (body.locks.toSet()..remove(lock))
                  : {...body.locks, lock},
            ),
          ),
        ),
    ],
  );

  /// The layers a body is in and the layers it looks for. A pair meets when
  /// either one looks for the other's layer.
  List<Widget> _layers(doc.BodyComponent body) => [
    LayerGrid(
      label: 'Is in',
      bits: body.layers,
      names: target.scene.layerNames,
      onToggle: (index) =>
          _choose(body.copyWith(layers: body.layers ^ (1 << index))),
    ),
    LayerGrid(
      label: 'Sees',
      bits: body.cares,
      names: target.scene.layerNames,
      onToggle: (index) =>
          _choose(body.copyWith(cares: body.cares ^ (1 << index))),
    ),
    const _Note('A pair meets when either one sees the other\'s layer.'),
  ];

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
    final bounds = _boundsOfObject();
    final size = bounds.max - bounds.min;
    final middle = (bounds.min + bounds.max) * 0.5;

    return switch (body.shape) {
      doc.BodyShape.compound => body.copyWith(
        parts: [doc.BodyPart(size: size)],
        centre: middle,
      ),
      doc.BodyShape.box => body.copyWith(size: size, centre: middle),
      doc.BodyShape.sphere => body.copyWith(
        radius: math.max(size.x, math.max(size.y, size.z)) / 2,
        centre: middle,
      ),
      doc.BodyShape.capsule || doc.BodyShape.cylinder => body.copyWith(
        radius: math.max(size.x, size.z) / 2,
        height: size.y,
        centre: middle,
      ),
      doc.BodyShape.hull => body.copyWith(
        hull: _hullAround(bounds, middle),
        centre: middle,
      ),
      // Ground is the top of the mesh: a floor tile is stood on, not in.
      doc.BodyShape.plane => body.copyWith(
        centre: Vector3(middle.x, bounds.max.y, middle.z),
      ),
    };
  }

  /// [body] with [shape], and a hull that has no points yet cut from the mesh
  /// so that choosing Hull gives a body that works.
  doc.BodyComponent _reshaped(doc.BodyComponent body, doc.BodyShape shape) {
    final next = body.copyWith(shape: shape);
    if (shape == doc.BodyShape.compound && next.parts.isEmpty) {
      return _fitted(next);
    }
    return shape == doc.BodyShape.hull && next.hull.isEmpty
        ? _fitted(next)
        : next;
  }

  /// How big the object is in its own space. A place with nothing drawn is
  /// the metre box a new body starts as, not the two metre placeholder.
  ({Vector3 min, Vector3 max}) _boundsOfObject() {
    if (!_object.isDrawable) {
      return (min: Vector3.all(-0.5), max: Vector3.all(0.5));
    }
    return boundsOf?.call(_object) ?? _object.localBounds();
  }

  /// The corners of the mesh's hull, written flat and measured from [middle].
  ///
  /// The corners of [bounds] when the editor holds no geometry for the object,
  /// as for an imported model, or when its points enclose no volume.
  List<double> _hullAround(
    ({Vector3 min, Vector3 max}) bounds,
    Vector3 middle,
  ) {
    final mesh = _object.isDrawable ? _object.currentMesh : null;
    final fromMesh = mesh == null || mesh.isEmpty
        ? ConvexOutline.none
        : ConvexOutline.around(mesh.positions);
    final corners = fromMesh.hasVolume
        ? fromMesh.corners
        : [
            for (final x in [bounds.min.x, bounds.max.x])
              for (final y in [bounds.min.y, bounds.max.y])
                for (final z in [bounds.min.z, bounds.max.z]) Vector3(x, y, z),
          ];
    return [
      for (final corner in corners) ...[
        corner.x - middle.x,
        corner.y - middle.y,
        corner.z - middle.z,
      ],
    ];
  }

  static T _key<T>(Map<T, String> labels, String label) =>
      labels.entries.firstWhere((entry) => entry.value == label).key;
}

/// A sentence under a group of rows, saying what a value means where the
/// row's own label cannot.
class _Note extends StatelessWidget {
  const _Note(this.text);

  final String text;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: Space.xs),
      child: Text(text, style: OrblitText.caption),
    );
  }
}

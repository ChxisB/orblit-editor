import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'body_section.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';

/// The joint on [object], if it has one.
doc.JointComponent? jointOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.joint]) {
      final doc.JointComponent joint => joint,
      _ => null,
    };

/// The two bodies a joint on [id] holds, found the way a running scene finds
/// them: the nearest body at or above it, and the nearest body above that,
/// or null for the world.
///
/// Asked of the editor's own tree rather than a saved document, so what the
/// inspector names and the gizmo joins up is what will be simulated.
({String? body, String? holder}) jointEndsIn(EditorScene scene, String id) =>
    doc.JointComponent.endsOf(
      id,
      parentOf: (id) => scene[id]?.parentId,
      hasBody: (id) => switch (scene[id]) {
        final object? => physicsBodyOf(object) != null,
        null => false,
      },
    );

/// Where the middle of the body on [id] is in the world: the point a joint
/// draws its line to and a distance joint measures to.
Vector3? bodyMiddleIn(EditorScene scene, String id) {
  final object = scene[id];
  final body = object == null ? null : physicsBodyOf(object);
  if (body == null) return null;
  return scene.worldOf(id).transformed3(body.centre.clone());
}

/// The inspector's section for a joint.
///
/// Offered on anything a joint on it would hold: a body, or something under
/// one, which is how a hinge sits at a door's edge rather than its middle.
/// Anything with no body at or above it would hold nothing, so it is not
/// offered there unless a file already put one on.
InspectorSection jointSection() => InspectorSection(
  name: 'joint',
  appliesTo: (target) =>
      jointOf(target.object) != null ||
      jointEndsIn(target.scene, target.object.id).body != null,
  build: (target) => _JointSection(target: target),
);

class _JointSection extends StatelessWidget {
  const _JointSection({required this.target});

  final InspectorTarget target;

  SceneObject get _object => target.object;

  static const _kinds = {
    doc.JointKind.fixed: 'Fixed',
    doc.JointKind.point: 'Point',
    doc.JointKind.hinge: 'Hinge',
    doc.JointKind.slider: 'Slider',
    doc.JointKind.distance: 'Distance',
    doc.JointKind.cone: 'Cone',
    doc.JointKind.sixAxis: 'Six axes',
    doc.JointKind.gear: 'Gear',
    doc.JointKind.rack: 'Rack',
    doc.JointKind.pulley: 'Pulley',
    doc.JointKind.path: 'Path',
  };

  /// Where a spring starts from when it is switched on: a few cycles a
  /// second, which is slow enough to see and stiff enough to hold a body up.
  static const _firstHertz = 3.0;
  static const _newGive = doc.JointGive(_firstHertz);

  /// A strength to give a motor that is asked to hold a place and has none.
  static const _firstStrength = 50.0;

  @override
  Widget build(BuildContext context) {
    final joint = jointOf(_object);
    return OrblitSection(
      title: 'Joint',
      child: joint == null ? _absent() : _present(joint),
    );
  }

  Widget _absent() => OrblitButton(
    label: 'Add joint',
    tooltip: 'Connect this object to another physics body.',
    icon: Icons.add,
    tone: ButtonTone.quiet,
    expand: true,
    onPressed: () => _put(
      doc.JointComponent(),
      label: 'Add joint to ${_object.name}',
      alone: true,
    ),
  );

  Widget _present(doc.JointComponent joint) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        FieldRow(label: 'Holds', child: _holds()),
        PickRow(
          label: 'Kind',
          shown: _kinds[joint.kind]!,
          options: _kinds,
          onSelect: (kind) =>
              _put(_seeded(joint.copyWith(kind: kind)), alone: true),
        ),
        for (final axis in joint.axes) ..._limitRows(joint, axis),
        ..._softRows(joint),
        if (joint.kind == doc.JointKind.cone)
          SliderRow(
            label: 'Swing',
            value: joint.swing.clamp(0, 180).toDouble(),
            min: 0,
            max: 180,
            unit: '°',
            onChanged: (value) =>
                _change((joint) => joint.copyWith(swing: value)),
            onSettled: target.history.seal,
          ),
        ..._machineRows(joint),
        ..._driveRows(joint),
        for (final axis in _giveAxes(joint)) ..._giveRows(joint, axis),
        _drag(
          'Breaks at',
          (joint) => [joint.breakingForce],
          (joint, values) => joint.copyWith(breakingForce: values.single),
          unit: 'N',
          minimum: 0,
          step: 1,
        ),
        _drag(
          '',
          (joint) => [joint.breakingTorque],
          (joint, values) => joint.copyWith(breakingTorque: values.single),
          unit: 'N·m',
          minimum: 0,
          step: 1,
        ),
        ChoiceRow(
          label: 'Collide',
          options: const ['No', 'Yes'],
          selected: joint.collide ? 'Yes' : 'No',
          onSelect: (label) =>
              _put(joint.copyWith(collide: label == 'Yes'), alone: true),
        ),
        const SizedBox(height: Space.xs),
        OrblitButton(
          label: 'Remove joint',
          tooltip: 'Remove the connection between these bodies.',
          icon: Icons.remove,
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: () => _put(
            null,
            label: 'Remove joint from ${_object.name}',
            alone: true,
          ),
        ),
      ],
    );
  }

  /// A kind that needs points is given some to start from: a pulley with
  /// none is not one, and a path with one point has no direction.
  static doc.JointComponent _seeded(doc.JointComponent joint) {
    switch (joint.kind) {
      case doc.JointKind.pulley when joint.over.length != 2:
        return joint.copyWith(over: [Vector3(0, 1, 0), Vector3(0, 1, 1)]);
      case doc.JointKind.path when joint.path.length < 2:
        return joint.copyWith(path: [Vector3.zero(), Vector3(1, 0, 0)]);
      default:
        return joint;
    }
  }

  /// Which body the joint holds to which, in the names the outliner uses,
  /// because the answer comes from where the entity sits and is otherwise
  /// invisible until the scene runs.
  Widget _holds() {
    final ends = jointEndsIn(target.scene, _object.id);
    String nameOf(String id) => target.scene[id]?.name ?? id;
    final text = switch (ends) {
      (body: null, holder: _) => 'Nothing: no body at or above it',
      (body: final body?, holder: null) => '${nameOf(body)} to the world',
      (body: final body?, holder: final holder?) =>
        '${nameOf(body)} to ${nameOf(holder)}',
    };
    return Text(
      text,
      style: OrblitText.label.copyWith(
        fontSize: 11.5,
        color: ends.body == null ? OrblitColors.warn : OrblitColors.ink,
      ),
    );
  }

  /// The rows for one limit: whether it is free, locked or a range, and
  /// then the number or two numbers.
  ///
  /// A distance joint with no limit is not free but a rod as long as it was
  /// placed, so it says so, and a set length is what locked means there.
  List<Widget> _limitRows(doc.JointComponent joint, doc.JointAxis axis) {
    final range = joint.limits[axis];
    final distance = joint.kind == doc.JointKind.distance;
    final modes = distance
        ? const ['As placed', 'Set', 'Range']
        : const ['Free', 'Locked', 'Range'];
    final mode = range == null ? 0 : (range.locked ? 1 : 2);
    final unit = axis.turns ? '°' : 'm';
    final step = axis.turns ? 1.0 : 0.01;

    return [
      ChoiceRow(
        label: _labelOf(joint.kind, axis),
        options: modes,
        selected: modes[mode],
        onSelect: (label) {
          final next = switch (modes.indexOf(label)) {
            1 => joint.limit(axis, _lockedFor(axis, distance)),
            2 => joint.limit(axis, _rangeFor(axis, distance)),
            _ => joint.free(axis),
          };
          _put(next, alone: true);
        },
      ),
      if (range != null && range.locked)
        _drag(
          '',
          (joint) => [joint.limits[axis]?.low ?? 0],
          (joint, values) =>
              joint.limit(axis, doc.JointRange.at(values.single)),
          unit: unit,
          step: step,
          minimum: distance ? 0 : null,
        ),
      if (range != null && !range.locked)
        _drag(
          '',
          (joint) {
            final range = joint.limits[axis];
            return range == null ? const [0.0, 0.0] : [range.low, range.high];
          },
          (joint, values) => joint.limit(axis, _ordered(joint, axis, values)),
          unit: unit,
          step: step,
          minimum: distance ? 0 : null,
        ),
    ];
  }

  /// What a limit is called on this kind of joint: a hinge's turn, a
  /// slider's travel, a rope's length.
  static String _labelOf(doc.JointKind kind, doc.JointAxis axis) =>
      switch ((kind, axis)) {
        (doc.JointKind.hinge, _) => 'Turn',
        (doc.JointKind.slider, _) => 'Travel',
        (doc.JointKind.distance, _) => 'Length',
        (doc.JointKind.cone, _) => 'Twist',
        (_, doc.JointAxis.alongX) => 'Along x',
        (_, doc.JointAxis.alongY) => 'Along y',
        (_, doc.JointAxis.alongZ) => 'Along z',
        (_, doc.JointAxis.aboutX) => 'About x',
        (_, doc.JointAxis.aboutY) => 'About y',
        (_, doc.JointAxis.aboutZ) => 'About z',
      };

  /// Locked where it was placed, which is nought for everything measured
  /// from there, and the placed length for a distance.
  doc.JointRange _lockedFor(doc.JointAxis axis, bool distance) =>
      doc.JointRange.at(distance ? _placedLength() : 0);

  /// A range to start from: a rope as long as it was placed, a quarter turn
  /// either way, or half a metre either way.
  doc.JointRange _rangeFor(doc.JointAxis axis, bool distance) {
    if (distance) return doc.JointRange(0, _placedLength());
    return axis.turns
        ? const doc.JointRange(-45, 45)
        : const doc.JointRange(-0.5, 0.5);
  }

  /// How far this entity is from the middle of the body it holds, which is
  /// the length a distance joint with no limit keeps.
  double _placedLength() {
    final body = jointEndsIn(target.scene, _object.id).body;
    final middle = body == null ? null : bodyMiddleIn(target.scene, body);
    if (middle == null) return 1;
    final length =
        (middle - target.scene.worldOf(_object.id).getTranslation()).length;
    // Rounded to the centimetre the field shows, so what is written is what
    // is read.
    return length < 0.01 ? 1 : (length * 100).roundToDouble() / 100;
  }

  /// [values] as a range, with whichever end was dragged stopped at the
  /// other rather than passing it.
  static doc.JointRange _ordered(
    doc.JointComponent joint,
    doc.JointAxis axis,
    List<double> values,
  ) {
    final [low, high] = values;
    if (low <= high) return doc.JointRange(low, high);
    final lowMoved = low != joint.limits[axis]?.low;
    return lowMoved ? doc.JointRange(high, high) : doc.JointRange(low, low);
  }

  /// How soft the ends of the limits are. Offered where a limit is a range,
  /// because a lock stays hard and a free way has no end to soften.
  List<Widget> _softRows(doc.JointComponent joint) {
    final ranged = joint.axes.any((axis) {
      final range = joint.limits[axis];
      return range != null && !range.locked;
    });
    if (!ranged && joint.kind != doc.JointKind.cone) return const [];

    return [
      _drag(
        'Soft ends',
        (joint) => [joint.softHertz],
        (joint, values) => joint.copyWith(softHertz: values.single),
        unit: 'Hz',
        minimum: 0,
        step: 0.1,
      ),
      if (joint.softHertz > 0)
        _drag(
          'Damping',
          (joint) => [joint.softRatio],
          (joint, values) => joint.copyWith(softRatio: values.single),
          unit: '',
          minimum: 0,
          step: 0.05,
        ),
    ];
  }

  /// What a gear, a rack, a pulley or a path adds to a joint: the numbers
  /// that tie it to the body above, or the line it runs along.
  List<Widget> _machineRows(doc.JointComponent joint) => switch (joint.kind) {
    doc.JointKind.gear || doc.JointKind.rack => [
      _ratioRow(joint),
      _drag(
        'Turns about',
        (joint) => _numbersOf(joint.axisB),
        (joint, values) => joint.copyWith(axisB: _vectorOf(values)),
        unit: '',
      ),
    ],
    doc.JointKind.pulley => [
      _ratioRow(joint),
      for (var i = 0; i < 2; i++)
        _drag(
          'Over ${i + 1}',
          (joint) => _pointOf(joint.over, i),
          (joint, values) =>
              joint.copyWith(over: _withPoint(joint.over, i, values, take: 2)),
          unit: 'm',
        ),
    ],
    doc.JointKind.path => _pathRows(joint),
    _ => const [],
  };

  Widget _ratioRow(doc.JointComponent joint) => _drag(
    'Ratio',
    (joint) => [joint.ratio],
    (joint, values) => joint.copyWith(ratio: values.single),
    unit: joint.kind == doc.JointKind.rack ? 'm/rad' : '×',
    step: 0.05,
  );

  List<Widget> _pathRows(doc.JointComponent joint) {
    final points = joint.path;
    return [
      for (var i = 0; i < points.length; i++)
        _drag(
          'Point ${i + 1}',
          (joint) => _pointOf(joint.path, i),
          (joint, values) =>
              joint.copyWith(path: _withPoint(joint.path, i, values)),
          unit: 'm',
        ),
      OrblitButtonRow(
        buttons: [
          OrblitButton(
            label: 'Add point',
            tooltip: 'Carry the path on past its last point.',
            icon: Icons.add,
            tone: ButtonTone.quiet,
            expand: true,
            onPressed: () => _put(
              joint.copyWith(path: [...points, _beyond(points)]),
              alone: true,
            ),
          ),
          OrblitButton(
            label: 'Remove point',
            tooltip: 'Take off the last point. A path keeps two.',
            icon: Icons.remove,
            tone: ButtonTone.quiet,
            expand: true,
            onPressed: points.length > 2
                ? () => _put(
                    joint.copyWith(path: points.sublist(0, points.length - 1)),
                    alone: true,
                  )
                : null,
          ),
        ],
      ),
      ChoiceRow(
        label: 'Follow',
        options: const ['No', 'Yes'],
        selected: joint.follow ? 'Yes' : 'No',
        onSelect: (label) =>
            _put(joint.copyWith(follow: label == 'Yes'), alone: true),
      ),
    ];
  }

  /// The next point of a path: as far past the last as the last is past
  /// the one before, or a metre along x for a path of one.
  static Vector3 _beyond(List<Vector3> points) {
    final last = points.last;
    return points.length < 2
        ? last + Vector3(1, 0, 0)
        : last * 2 - points[points.length - 2];
  }

  /// How the joint's body is driven: at a speed, or to a place. Both are a
  /// motor, so both are held to the strength below them.
  List<Widget> _driveRows(doc.JointComponent joint) {
    final turning = joint.kind == doc.JointKind.hinge;
    final driven =
        turning ||
        joint.kind == doc.JointKind.slider ||
        joint.kind == doc.JointKind.path;
    if (!driven) return const [];
    final toPlace = joint.goalHertz > 0;

    return [
      ChoiceRow(
        label: 'Drive',
        options: const ['Speed', 'Place'],
        selected: toPlace ? 'Place' : 'Speed',
        onSelect: (label) =>
            _put(_driven(joint, toPlace: label == 'Place'), alone: true),
      ),
      if (!toPlace)
        _drag(
          'Motor',
          (joint) => [joint.speed],
          (joint, values) => joint.copyWith(speed: values.single),
          unit: turning ? '°/s' : 'm/s',
          step: turning ? 1 : 0.01,
        )
      else ...[
        _drag(
          'Hold at',
          (joint) => [joint.goal],
          (joint, values) => joint.copyWith(goal: values.single),
          unit: turning ? '°' : 'm',
          step: turning ? 1 : 0.01,
        ),
        _drag(
          'Stiffness',
          (joint) => [joint.goalHertz],
          (joint, values) => joint.copyWith(goalHertz: values.single),
          unit: 'Hz',
          minimum: 0.1,
          step: 0.1,
        ),
        _drag(
          'Damping',
          (joint) => [joint.goalRatio],
          (joint, values) => joint.copyWith(goalRatio: values.single),
          unit: '',
          minimum: 0,
          step: 0.05,
        ),
      ],
      _drag(
        'Strength',
        (joint) => [joint.strength],
        (joint, values) => joint.copyWith(strength: values.single),
        unit: turning ? 'N·m' : 'N',
        minimum: 0,
        step: 0.1,
      ),
    ];
  }

  /// [joint] driven to a place or at a speed. A motor with no strength does
  /// nothing, so a place is given one to start from rather than left to hold
  /// nothing.
  static doc.JointComponent _driven(
    doc.JointComponent joint, {
    required bool toPlace,
  }) {
    if (!toPlace) return joint.copyWith(goalHertz: 0);
    return joint.copyWith(
      goalHertz: _firstHertz,
      strength: joint.strength > 0 ? joint.strength : _firstStrength,
    );
  }

  /// The ways a spring may be put on. A joint's own limited ways, and for
  /// the kinds that limit none: all six for a weld, which then gives in
  /// every way, the turns of a ball, which nothing else holds, and the tie
  /// of a gear, a rack or a pulley.
  static List<doc.JointAxis> _giveAxes(doc.JointComponent joint) =>
      switch (joint.kind) {
        doc.JointKind.fixed => doc.JointAxis.values,
        doc.JointKind.point => const [
          doc.JointAxis.aboutX,
          doc.JointAxis.aboutY,
          doc.JointAxis.aboutZ,
        ],
        doc.JointKind.gear ||
        doc.JointKind.rack ||
        doc.JointKind.pulley => const [doc.JointAxis.alongX],
        _ => joint.axes.toList(),
      };

  /// The rows for the spring on one way: whether there is one, and when
  /// there is, how it is tuned.
  List<Widget> _giveRows(doc.JointComponent joint, doc.JointAxis axis) {
    final give = joint.springs[axis];
    return [
      ChoiceRow(
        label: _springLabel(joint, axis),
        options: const ['Off', 'On'],
        selected: give == null ? 'Off' : 'On',
        onSelect: (label) => _put(
          label == 'On'
              ? joint.spring(axis, _newGive)
              : joint.unspring(axis),
          alone: true,
        ),
      ),
      if (give != null) ..._tuneRows(joint, axis, give),
    ];
  }

  /// How stiff and how damped a spring is, and where it rests.
  List<Widget> _tuneRows(
    doc.JointComponent joint,
    doc.JointAxis axis,
    doc.JointGive give,
  ) => [
    _giveDrag(
      axis,
      'Stiffness',
      read: (give) => give.hertz,
      make: (give, value) =>
          doc.JointGive(value, ratio: give.ratio, rest: give.rest),
      unit: 'Hz',
      minimum: 0.1,
      step: 0.1,
    ),
    _giveDrag(
      axis,
      'Damping',
      read: (give) => give.ratio,
      make: (give, value) =>
          doc.JointGive(give.hertz, ratio: value, rest: give.rest),
      unit: '',
      minimum: 0,
      step: 0.05,
    ),
    ChoiceRow(
      label: 'Rest',
      options: const ['As made', 'Set'],
      selected: give.rest == null ? 'As made' : 'Set',
      onSelect: (label) => _put(
        joint.spring(
          axis,
          doc.JointGive(
            give.hertz,
            ratio: give.ratio,
            rest: label == 'Set' ? 0 : null,
          ),
        ),
        alone: true,
      ),
    ),
    if (give.rest != null)
      _giveDrag(
        axis,
        '',
        read: (give) => give.rest ?? 0,
        make: (give, value) =>
            doc.JointGive(give.hertz, ratio: give.ratio, rest: value),
        unit: axis.turns ? '°' : 'm',
        step: axis.turns ? 1 : 0.01,
      ),
  ];

  /// One number of the spring on [axis], dragged. The spring is read as it
  /// is at each step, or as a new one if a drag outlives it.
  Widget _giveDrag(
    doc.JointAxis axis,
    String label, {
    required double Function(doc.JointGive give) read,
    required doc.JointGive Function(doc.JointGive give, double value) make,
    required String unit,
    double? minimum,
    double step = 0.01,
  }) => _drag(
    label,
    (joint) => [read(joint.springs[axis] ?? _newGive)],
    (joint, values) => joint.spring(
      axis,
      make(joint.springs[axis] ?? _newGive, values.single),
    ),
    unit: unit,
    minimum: minimum,
    step: step,
  );

  /// What a spring is called: one word where a joint has one way to give,
  /// and which way where it has several.
  static String _springLabel(doc.JointComponent joint, doc.JointAxis axis) =>
      _giveAxes(joint).length == 1
      ? 'Spring'
      : 'Spring ${_labelOf(joint.kind, axis).toLowerCase()}';

  static List<double> _numbersOf(Vector3 v) => [v.x, v.y, v.z];

  static Vector3 _vectorOf(List<double> values) =>
      Vector3(values[0], values[1], values[2]);

  /// Point [i] of [points] as numbers, nought where there is no such point.
  static List<double> _pointOf(List<Vector3> points, int i) =>
      i < points.length ? _numbersOf(points[i]) : const [0.0, 0.0, 0.0];

  /// [points] with point [i] set to [values], padded with the origin to reach
  /// it and cut to [take] points when that is given.
  static List<Vector3> _withPoint(
    List<Vector3> points,
    int i,
    List<double> values, {
    int? take,
  }) {
    final count = take ?? points.length;
    final next = [
      for (var k = 0; k < count; k++)
        k == i
            ? _vectorOf(values)
            : (k < points.length ? points[k] : Vector3.zero()),
    ];
    return next;
  }

  /// A row of numbers read off the joint and dragged into a new one, reading
  /// the joint as it is at each step for the reason the body's rows do.
  Widget _drag(
    String label,
    List<double> Function(doc.JointComponent joint) read,
    doc.JointComponent Function(doc.JointComponent joint, List<double> values)
    change, {
    required String unit,
    double? minimum,
    double step = 0.01,
  }) => DragRow(
    label: label,
    listenable: target.history,
    read: () {
      final joint = jointOf(_object);
      return joint == null ? const [0.0] : read(joint);
    },
    onChanged: (values) => _change((joint) => change(joint, values)),
    onSettled: target.history.seal,
    minimum: minimum,
    step: step,
    trailing: SizedBox(
      // Wide enough for m/rad, which is the longest unit a row carries.
      width: unit.length > 3 ? 38 : 28,
      child: Text(
        unit,
        style: OrblitText.label.copyWith(
          fontSize: 11,
          color: OrblitColors.inkDim,
        ),
      ),
    ),
  );

  void _change(doc.JointComponent Function(doc.JointComponent joint) change) {
    final joint = jointOf(_object);
    if (joint != null) _put(change(joint));
  }

  /// Replaces the joint with [next], or takes it off for null. [alone] for a
  /// click, sealed at once; a drag is sealed when the pointer lifts.
  void _put(doc.JointComponent? next, {String? label, bool alone = false}) {
    target.history.run(
      SetObjectComponent(
        sceneId: target.sceneId,
        id: _object.id,
        label: label ?? 'Set ${_object.name} joint',
        type: doc.SceneComponents.joint,
        from: jointOf(_object),
        to: next,
      ),
    );
    if (alone) target.history.seal();
  }
}

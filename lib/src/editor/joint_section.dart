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
  };

  @override
  Widget build(BuildContext context) {
    final joint = jointOf(_object);
    return OrblitSection(
      title: 'Joint',
      icon: Icons.link,
      child: joint == null ? _absent() : _present(joint),
    );
  }

  Widget _absent() => OrblitButton(
    label: 'Add joint',
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
    final motor =
        joint.kind == doc.JointKind.hinge || joint.kind == doc.JointKind.slider;
    final turning = joint.kind == doc.JointKind.hinge;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        FieldRow(label: 'Holds', child: _holds()),
        _KindChoice(
          options: _kinds.values.toList(),
          selected: _kinds[joint.kind]!,
          onSelect: (label) =>
              _put(joint.copyWith(kind: _key(_kinds, label)), alone: true),
        ),
        for (final axis in joint.axes) ..._limitRows(joint, axis),
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
        if (motor) ...[
          _drag(
            'Motor',
            (joint) => [joint.speed],
            (joint, values) => joint.copyWith(speed: values.single),
            unit: turning ? '°/s' : 'm/s',
            step: turning ? 1 : 0.01,
          ),
          _drag(
            'Strength',
            (joint) => [joint.strength],
            (joint, values) => joint.copyWith(strength: values.single),
            unit: turning ? 'N·m' : 'N',
            minimum: 0,
            step: 0.1,
          ),
        ],
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
      width: 28,
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

  static T _key<T>(Map<T, String> labels, String label) =>
      labels.entries.firstWhere((entry) => entry.value == label).key;
}

/// The kind of joint, from a menu: seven are too many to lay side by side in
/// a row as narrow as the inspector.
class _KindChoice extends StatelessWidget {
  const _KindChoice({
    required this.options,
    required this.selected,
    required this.onSelect,
  });

  final List<String> options;
  final String selected;
  final ValueChanged<String> onSelect;

  @override
  Widget build(BuildContext context) {
    return FieldRow(
      label: 'Kind',
      child: PopupMenuButton<String>(
        tooltip: '',
        color: OrblitColors.raised,
        onSelected: onSelect,
        itemBuilder: (context) => [
          for (final option in options)
            PopupMenuItem(
              value: option,
              height: 28,
              child: Text(option, style: OrblitText.label),
            ),
        ],
        child: Container(
          height: 24,
          padding: const EdgeInsets.symmetric(horizontal: Space.sm),
          decoration: BoxDecoration(
            color: OrblitColors.raised,
            borderRadius: BorderRadius.circular(4),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  selected,
                  overflow: TextOverflow.ellipsis,
                  style: OrblitText.label.copyWith(
                    fontSize: 11.5,
                    color: OrblitColors.ink,
                  ),
                ),
              ),
              const Icon(
                Icons.expand_more,
                size: 14,
                color: OrblitColors.inkDim,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

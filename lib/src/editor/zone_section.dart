import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'body_section.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';

/// The inspector's section for a zone.
///
/// Offered on a body that stays where it is, because the body is the region
/// and the solver cannot hold a free one in place. A zone a file already put
/// on a free body is still shown, so it can be seen and taken off.
InspectorSection zoneSection() => InspectorSection(
  name: 'zone',
  appliesTo: (target) {
    final body = physicsBodyOf(target.object);
    return zoneOf(target.object) != null ||
        (body != null && !movesFreely(body));
  },
  build: (target) => _ZoneSection(target: target),
);

class _ZoneSection extends StatelessWidget {
  const _ZoneSection({required this.target});

  final InspectorTarget target;

  SceneObject get _object => target.object;

  /// What a field starts at when it is switched on: the world's gravity and a
  /// body's own damping, so nothing inside changes until the field is dragged.
  static final _gravity = Vector3(0, -9.81, 0);
  static final _defaults = doc.BodyComponent();

  @override
  Widget build(BuildContext context) {
    final zone = zoneOf(_object);
    return OrblitSection(
      title: 'Zone',
      child: zone == null ? _absent() : _present(zone),
    );
  }

  Widget _absent() => OrblitButton(
    label: 'Add zone',
    tooltip: 'Change gravity and drag for what is inside this body.',
    icon: Icons.add,
    tone: ButtonTone.quiet,
    expand: true,
    onPressed: () => _put(
      doc.ZoneComponent(),
      label: 'Add zone to ${_object.name}',
      alone: true,
    ),
  );

  Widget _present(doc.ZoneComponent zone) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      ..._field(
        'Gravity',
        own: zone.gravity != null,
        take: (zone) => zone.copyWith(gravity: _gravity.clone()),
        leave: (zone) => doc.ZoneComponent(
          linearDamping: zone.linearDamping,
          angularDamping: zone.angularDamping,
          priority: zone.priority,
        ),
        value: _drag(
          'Pull',
          (zone) => zone.gravity?.storage ?? const [0.0, 0.0, 0.0],
          (zone, values) => zone.copyWith(gravity: Vector3.array(values)),
          step: 0.1,
        ),
      ),
      ..._field(
        'Drag',
        own: zone.linearDamping != null,
        take: (zone) => zone.copyWith(linearDamping: _defaults.linearDamping),
        leave: (zone) => doc.ZoneComponent(
          gravity: zone.gravity,
          angularDamping: zone.angularDamping,
          priority: zone.priority,
        ),
        value: _drag(
          'Amount',
          (zone) => [zone.linearDamping ?? 0],
          (zone, values) => zone.copyWith(linearDamping: values.single),
          minimum: 0,
          step: 0.05,
        ),
      ),
      ..._field(
        'Spin drag',
        own: zone.angularDamping != null,
        take: (zone) => zone.copyWith(angularDamping: _defaults.angularDamping),
        leave: (zone) => doc.ZoneComponent(
          gravity: zone.gravity,
          linearDamping: zone.linearDamping,
          priority: zone.priority,
        ),
        value: _drag(
          'Amount',
          (zone) => [zone.angularDamping ?? 0],
          (zone, values) => zone.copyWith(angularDamping: values.single),
          minimum: 0,
          step: 0.05,
        ),
      ),
      // A whole number, so a drag has to move by one to change anything: the
      // row reads the zone back at every step and rounds what it gets.
      _drag(
        'Priority',
        (zone) => [zone.priority.toDouble()],
        (zone, values) => zone.copyWith(priority: values.single.round()),
        step: 1,
        decimals: 0,
      ),
      const SizedBox(height: Space.xs),
      OrblitButton(
        label: 'Remove zone',
        tooltip: 'Stop changing how bodies move inside this one.',
        icon: Icons.remove,
        tone: ButtonTone.quiet,
        expand: true,
        onPressed: () =>
            _put(null, label: 'Remove zone from ${_object.name}', alone: true),
      ),
    ],
  );

  /// One field a zone may leave to the body inside, as a choice between the
  /// body's own and this zone's, with [value] shown under it when it is the
  /// zone's.
  List<Widget> _field(
    String label, {
    required bool own,
    required doc.ZoneComponent Function(doc.ZoneComponent zone) take,
    required doc.ZoneComponent Function(doc.ZoneComponent zone) leave,
    required Widget value,
  }) => [
    ChoiceRow(
      label: label,
      options: const ['Same', 'Own'],
      selected: own ? 'Own' : 'Same',
      onSelect: (choice) =>
          _change(choice == 'Own' ? take : leave, alone: true),
    ),
    if (own) value,
  ];

  /// A row of numbers read off the zone and dragged into a new one, reading
  /// the zone as it is at each step for the reason the body's rows do.
  Widget _drag(
    String label,
    List<double> Function(doc.ZoneComponent zone) read,
    doc.ZoneComponent Function(doc.ZoneComponent zone, List<double> values)
    change, {
    double? minimum,
    double step = 0.01,
    int decimals = 2,
  }) => DragRow(
    label: label,
    listenable: target.history,
    read: () {
      final zone = zoneOf(_object);
      return zone == null ? const [0.0] : read(zone);
    },
    onChanged: (values) => _change((zone) => change(zone, values)),
    onSettled: target.history.seal,
    minimum: minimum,
    step: step,
    decimals: decimals,
  );

  void _change(
    doc.ZoneComponent Function(doc.ZoneComponent zone) change, {
    bool alone = false,
  }) {
    final zone = zoneOf(_object);
    if (zone != null) _put(change(zone), alone: alone);
  }

  /// Replaces the zone with [next], or takes it off for null. [alone] for a
  /// click, sealed at once; a drag is sealed when the pointer lifts.
  void _put(doc.ZoneComponent? next, {String? label, bool alone = false}) {
    target.history.run(
      SetObjectComponent(
        sceneId: target.sceneId,
        id: _object.id,
        label: label ?? 'Set ${_object.name} zone',
        type: doc.SceneComponents.zone,
        from: zoneOf(_object),
        to: next,
      ),
    );
    if (alone) target.history.seal();
  }
}

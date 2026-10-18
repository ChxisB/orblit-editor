import 'package:flutter/widgets.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;

import '../widgets/controls.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';

/// Rows that read and write the component of type [T] on one object.
///
/// A drag reads the component as it is at every step, because the drag is
/// what is changing it.
final class ComponentRows<T extends doc.SceneComponent> {
  const ComponentRows(this.target, {required this.type, required this.of});

  final InspectorTarget target;

  /// The key the component has on the object.
  final String type;
  final T? Function(SceneObject object) of;

  SceneObject get _object => target.object;

  Widget drag(
    String label,
    double Function(T component) read,
    T Function(T component, double value) change, {
    double? minimum,
    double step = 0.01,
    int decimals = 2,
  }) => DragRow(
    label: label,
    listenable: target.history,
    read: () {
      final component = of(_object);
      return [component == null ? 0.0 : read(component)];
    },
    onChanged: (values) => _change((component) => change(component, values[0])),
    onSettled: target.history.seal,
    minimum: minimum,
    step: step,
    decimals: decimals,
  );

  Widget toggle(
    String label,
    bool Function(T component) read,
    T Function(T component, bool on) change,
  ) {
    final component = of(_object);
    final on = component != null && read(component);
    return ChoiceRow(
      label: label,
      options: const ['No', 'Yes'],
      selected: on ? 'Yes' : 'No',
      onSelect: (choice) => _change(
        (component) => change(component, choice == 'Yes'),
        alone: true,
      ),
    );
  }

  /// A text field, which settles when it loses focus.
  Widget text(
    String label,
    String Function(T component) read,
    T Function(T component, String value) change, {
    String? hint,
  }) {
    final component = of(_object);
    return FieldRow(
      label: label,
      child: ValueField(
        value: component == null ? '' : read(component),
        hint: hint,
        mono: true,
        onChanged: (value) => _change((component) => change(component, value)),
        onDone: target.history.seal,
      ),
    );
  }

  /// One of [options], by the name [read] gives it and [change] takes.
  Widget choice(
    String label,
    List<String> options,
    String Function(T component) read,
    T Function(T component, String option) change,
  ) {
    final component = of(_object);
    return ChoiceRow(
      label: label,
      options: options,
      selected: component == null ? options.first : read(component),
      onSelect: (option) =>
          _change((component) => change(component, option), alone: true),
    );
  }

  void _change(T Function(T component) change, {bool alone = false}) {
    final component = of(_object);
    if (component != null) put(change(component), alone: alone);
  }

  /// Replaces the component with [next], or takes it off for null. [alone]
  /// for a click, sealed at once; a drag is sealed when the pointer lifts.
  void put(T? next, {String? label, bool alone = false}) {
    target.history.run(
      SetObjectComponent(
        sceneId: target.sceneId,
        id: _object.id,
        label: label ?? 'Set ${_object.name} $type',
        type: type,
        from: of(_object),
        to: next,
      ),
    );
    if (alone) target.history.seal();
  }
}

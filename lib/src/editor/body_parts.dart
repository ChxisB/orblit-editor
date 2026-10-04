import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../widgets/controls.dart';
import 'inspector.dart';

/// Edits each convex part through the same undo history as its owning body.
final class BodyParts extends StatelessWidget {
  const BodyParts({
    super.key,
    required this.read,
    required this.put,
    required this.settle,
    required this.listenable,
    required this.hull,
  });

  final doc.BodyComponent Function() read;
  final ValueChanged<doc.BodyComponent> put;
  final VoidCallback settle;
  final Listenable listenable;
  final List<double> hull;

  void _edit(int index, doc.BodyPart Function(doc.BodyPart) change) {
    final body = read();
    if (index >= body.parts.length) return;
    final parts = body.parts.toList();
    parts[index] = change(parts[index]);
    put(body.copyWith(parts: parts));
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      for (var i = 0; i < read().parts.length; i++)
        _PartRows(
          index: i,
          read: () => read().parts[i],
          edit: (change) => _edit(i, change),
          remove: () {
            final body = read();
            put(body.copyWith(parts: body.parts.toList()..removeAt(i)));
            settle();
          },
          settle: settle,
          listenable: listenable,
          hull: hull,
        ),
      OrblitButton(
        label: 'Add part',
        icon: Icons.add,
        tone: ButtonTone.quiet,
        expand: true,
        onPressed: read().parts.length < 64
            ? () {
                final body = read();
                put(body.copyWith(parts: [...body.parts, doc.BodyPart()]));
                settle();
              }
            : null,
      ),
    ],
  );
}

final class _PartRows extends StatelessWidget {
  const _PartRows({
    required this.index,
    required this.read,
    required this.edit,
    required this.remove,
    required this.settle,
    required this.listenable,
    required this.hull,
  });

  final int index;
  final doc.BodyPart Function() read;
  final void Function(doc.BodyPart Function(doc.BodyPart)) edit;
  final VoidCallback remove;
  final VoidCallback settle;
  final Listenable listenable;
  final List<double> hull;

  static const shapes = {
    doc.BodyShape.box: 'Box',
    doc.BodyShape.sphere: 'Ball',
    doc.BodyShape.capsule: 'Capsule',
    doc.BodyShape.cylinder: 'Cylinder',
    doc.BodyShape.hull: 'Hull',
  };

  @override
  Widget build(BuildContext context) {
    final part = read();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (var first = 0; first < shapes.length; first += 3)
          ChoiceRow(
            label: first == 0 ? 'Part ${index + 1}' : '',
            options: shapes.values.toList().sublist(
              first,
              math.min(first + 3, shapes.length),
            ),
            selected: shapes[part.shape] ?? 'Box',
            onSelect: (label) {
              final shape = shapes.entries
                  .firstWhere((e) => e.value == label)
                  .key;
              edit(
                (p) => p.copyWith(
                  shape: shape,
                  hull: shape == doc.BodyShape.hull && p.hull.isEmpty
                      ? hull
                      : p.hull,
                ),
              );
              settle();
            },
          ),
        ..._dimensions(part.shape),
        _drag(
          'Part centre',
          (p) => p.centre.storage,
          (p, v) => p.copyWith(centre: Vector3.array(v)),
        ),
        _drag(
          'Part scale',
          (p) => p.scale.storage,
          (p, v) => p.copyWith(scale: Vector3.array(v)),
          minimum: 0.01,
        ),
        _drag(
          'Part turn',
          (p) => _angles(p.rotation),
          (p, v) => p.copyWith(rotation: _rotation(v)),
          step: 1,
        ),
        OrblitButton(
          label: 'Remove part ${index + 1}',
          tone: ButtonTone.quiet,
          expand: true,
          onPressed: remove,
        ),
      ],
    );
  }

  List<Widget> _dimensions(doc.BodyShape shape) => [
    if (shape == doc.BodyShape.box)
      _drag(
        'Part size',
        (p) => p.size.storage,
        (p, v) => p.copyWith(size: Vector3.array(v)),
        minimum: 0.01,
      ),
    if (shape == doc.BodyShape.sphere ||
        shape == doc.BodyShape.capsule ||
        shape == doc.BodyShape.cylinder)
      _drag(
        'Part radius',
        (p) => [p.radius],
        (p, v) => p.copyWith(radius: v.single),
        minimum: 0.01,
      ),
    if (shape == doc.BodyShape.capsule || shape == doc.BodyShape.cylinder)
      _drag(
        'Part height',
        (p) => [p.height],
        (p, v) => p.copyWith(height: v.single),
        minimum: 0.01,
      ),
  ];

  List<double> _angles(Quaternion rotation) {
    final q = rotation.clone()..normalize();
    return [
      math.atan2(2 * (q.w * q.x + q.y * q.z), 1 - 2 * (q.x * q.x + q.y * q.y)),
      math.asin((2 * (q.w * q.y - q.z * q.x)).clamp(-1.0, 1.0)),
      math.atan2(2 * (q.w * q.z + q.x * q.y), 1 - 2 * (q.y * q.y + q.z * q.z)),
    ].map((angle) => angle * 180 / math.pi).toList();
  }

  Quaternion _rotation(List<double> degrees) {
    final x = Quaternion.axisAngle(
      Vector3(1, 0, 0),
      degrees[0] * math.pi / 180,
    );
    final y = Quaternion.axisAngle(
      Vector3(0, 1, 0),
      degrees[1] * math.pi / 180,
    );
    final z = Quaternion.axisAngle(
      Vector3(0, 0, 1),
      degrees[2] * math.pi / 180,
    );
    return z * y * x;
  }

  Widget _drag(
    String label,
    List<double> Function(doc.BodyPart) values,
    doc.BodyPart Function(doc.BodyPart, List<double>) change, {
    double? minimum,
    double step = 0.01,
  }) => DragRow(
    label: label,
    listenable: listenable,
    read: () => values(read()),
    onChanged: (v) => edit((p) => change(p, v)),
    onSettled: settle,
    minimum: minimum,
    step: step,
  );
}

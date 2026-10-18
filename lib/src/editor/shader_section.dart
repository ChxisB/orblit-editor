import 'package:flutter/material.dart';
import 'package:orblit_stage/orblit_stage.dart';
import 'package:path/path.dart' as p;

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'commands.dart';
import 'inspector.dart';
import 'scene.dart';
import 'surface_cook.dart';

/// The inspector's section for an object wearing an author-written shader.
///
/// Offers the uniforms the author marked `instance`, as the control their
/// hints ask for: a colour for `source_color`, a slider for `hint_range`.
/// Pictures are named in the scene file; there is no picker for one yet.
InspectorSection shaderSection({required SurfaceCooks cooks}) =>
    InspectorSection(
      name: 'shader',
      appliesTo: (target) => target.object.wearsSurface,
      build: (target) => ListenableBuilder(
        listenable: cooks,
        builder: (_, _) => _ShaderSection(target: target, cooks: cooks),
      ),
    );

class _ShaderSection extends StatelessWidget {
  const _ShaderSection({required this.target, required this.cooks});

  final InspectorTarget target;
  final SurfaceCooks cooks;

  SceneObject get _object => target.object;

  @override
  Widget build(BuildContext context) {
    final asset = _object.materialAsset!;
    final cooked = cooks.cookedOf(asset);
    final problem = cooks.problemWith(asset);
    final params = [
      for (final param in cooked?.params ?? const <SurfaceParam>[])
        if (param.instance && param.type != 'sampler2D') param,
    ];
    return OrblitSection(
      title: 'Shader',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextRow(label: 'File', value: p.basename(asset)),
          if (problem != null)
            _Note(problem, colour: OrblitColors.bad)
          else if (cooked == null)
            const _Note('Cooking the shader.')
          else if (params.isEmpty)
            const _Note('Nothing in this shader is set for each object.'),
          if (cooked != null)
            for (final param in params) _row(param, cooked),
        ],
      ),
    );
  }

  Widget _row(SurfaceParam param, CookedSurface cooked) {
    final width = cooked.widths[param.name] ?? 1;
    if (param.isColour && width >= 3) return _colour(param, cooked, width);
    if (param.type == 'bool') return _flag(param, cooked);
    if (param.range case final range? when width == 1) {
      return _slider(param, cooked, range);
    }
    return DragRow(
      label: param.name,
      listenable: target.history,
      read: () => cooked.valueOf(param.name, _object.materialValues),
      onChanged: (values) => _put(param, values),
      onSettled: target.history.seal,
      step: param.type == 'int' ? 1 : 0.01,
      decimals: param.type == 'int' ? 0 : 2,
    );
  }

  Widget _colour(SurfaceParam param, CookedSurface cooked, int width) {
    final v = cooked.valueOf(param.name, _object.materialValues);
    return ColourRow(
      label: param.name,
      value: Color.from(
        alpha: width == 4 ? v[3] : 1,
        red: v[0],
        green: v[1],
        blue: v[2],
      ),
      onChanged: (c) {
        _put(param, [c.r, c.g, c.b, if (width == 4) c.a]);
        target.history.seal();
      },
    );
  }

  Widget _flag(SurfaceParam param, CookedSurface cooked) {
    final on = cooked.valueOf(param.name, _object.materialValues).single != 0;
    return ChoiceRow(
      label: param.name,
      options: const ['Off', 'On'],
      selected: on ? 'On' : 'Off',
      onSelect: (choice) {
        _put(param, [choice == 'On' ? 1.0 : 0.0]);
        target.history.seal();
      },
    );
  }

  Widget _slider(
    SurfaceParam param,
    CookedSurface cooked,
    ({double min, double max, double step}) range,
  ) {
    final whole = param.type == 'int';
    return SliderRow(
      label: param.name,
      value: cooked.valueOf(param.name, _object.materialValues).single,
      min: range.min,
      max: range.max,
      decimals: whole ? 0 : 2,
      onChanged: (value) {
        final snapped = range.step == 0
            ? value
            : range.min +
                  ((value - range.min) / range.step).round() * range.step;
        _put(param, [snapped]);
      },
      onSettled: target.history.seal,
    );
  }

  /// States [values] for [param]. A drag is sealed when the pointer lifts, a
  /// click by the caller.
  void _put(SurfaceParam param, List<double> values) {
    final from = _object.materialValues;
    final Object stored = switch (param.type) {
      'float' => values.single,
      'int' => values.single.round(),
      'bool' => values.single != 0,
      _ => values,
    };
    target.history.run(
      SetMaterialValue(
        sceneId: target.sceneId,
        id: _object.id,
        name: _object.name,
        uniform: param.name,
        from: from,
        to: {...from, param.name: stored},
      ),
    );
  }
}

class _Note extends StatelessWidget {
  const _Note(this.text, {this.colour = OrblitColors.inkDim});

  final String text;
  final Color colour;

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: Text(text, style: OrblitText.label.copyWith(color: colour)),
  );
}

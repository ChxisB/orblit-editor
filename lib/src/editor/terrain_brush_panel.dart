import 'package:flutter/material.dart';
import 'package:orblit_terrain/orblit_terrain.dart';

import '../theme/orblit_theme.dart';
import '../widgets/controls.dart';
import 'inspector.dart' show ColourRow, SliderRow;
import 'terrain_bench.dart';
import 'terrain_edits.dart';
import 'terrain_mode.dart';

/// The brush, as a place: which terrain it is working on, what the tool
/// does and lays, and the settings every tool shares.
///
/// Not undoable, any of it. The brush is how somebody is working, like the
/// grid a drag snaps to, and not part of anything that is saved.
class TerrainBrushPanel extends StatelessWidget {
  const TerrainBrushPanel({
    super.key,
    required this.bench,
    required this.target,
  });

  final TerrainBench bench;

  /// The terrain a stroke would land on, or null when there is none.
  final OpenTerrain? target;

  /// Tints that read as ground. The colour multiplies what the textures
  /// paint, so white is no tint and anything darker darkens.
  static const _tints = [
    Color(0xFF967C60),
    Color(0xFFC9B48A),
    Color(0xFF6E5A40),
    Color(0xFF8FA05A),
    Color(0xFFB07050),
    Color(0xFF8A8A8A),
  ];

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) => Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [_terrainSection(), _toolSection(), _brushSection()],
    ),
  );

  Widget _terrainSection() {
    final open = target;
    return OrblitSection(
      title: 'Terrain',
      icon: Icons.landscape_outlined,
      child: open == null
          ? Text(
              'Nothing to shape. Select a terrain, or add one with '
              'Add › Terrain.',
              style: OrblitText.caption.copyWith(fontSize: 11),
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(open.name, style: OrblitText.body),
                const SizedBox(height: 2),
                Text(
                  _extent(open.terrain),
                  style: OrblitText.caption.copyWith(fontSize: 11),
                ),
              ],
            ),
    );
  }

  Widget _toolSection() {
    final tool = bench.tool;
    return OrblitSection(
      title: toolLabel(tool),
      icon: toolIcon(tool),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            toolHint(tool),
            style: OrblitText.caption.copyWith(fontSize: 11),
          ),
          const SizedBox(height: Space.xs),
          ...switch (tool) {
            BrushTool.cover => [_sets()],
            BrushTool.colour => [
              ColourRow(
                label: 'Tint',
                value: Color.fromARGB(
                  255,
                  bench.colour.red,
                  bench.colour.green,
                  bench.colour.blue,
                ),
                swatches: _tints,
                onChanged: (colour) => bench.colour = bench.colour.withColour(
                  (colour.r * 255).round(),
                  (colour.g * 255).round(),
                  (colour.b * 255).round(),
                ),
              ),
            ],
            BrushTool.roughness => [
              SliderRow(
                label: 'Rougher by',
                value: bench.roughness,
                min: -1,
                max: 1,
                decimals: 2,
                onChanged: (value) => bench.roughness = value,
              ),
            ],
            _ => const <Widget>[],
          },
        ],
      ),
    );
  }

  /// The sets to lay, one row each: there can be thirty-two of them, which
  /// is too many to share one row the way a choice of three does.
  Widget _sets() {
    final sets = target?.terrain.sets ?? const <TerrainSet>[];
    if (sets.isEmpty) {
      return Text(
        'This terrain has no sets to lay. Add one in the inspector.',
        style: OrblitText.caption.copyWith(fontSize: 11),
      );
    }
    final chosen = bench.set.clamp(0, sets.length - 1);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        for (final (index, set) in sets.indexed)
          _SetRow(
            name: set.name,
            detail:
                '${_metres(set.tileSize)} m'
                '${set.triplanar ? ', on cliffs' : ''}',
            selected: index == chosen,
            onTap: () => bench.set = index,
          ),
      ],
    );
  }

  Widget _brushSection() {
    final brush = bench.brush;
    return OrblitSection(
      title: 'Brush',
      icon: Icons.brush_outlined,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SliderRow(
            label: 'Size',
            value: brush.size,
            min: 1,
            max: 128,
            unit: ' m',
            onChanged: (value) => bench.brush = brush.copyWith(size: value),
          ),
          SliderRow(
            label: 'Strength',
            value: brush.strength,
            min: 0,
            max: 1,
            decimals: 2,
            onChanged: (value) => bench.brush = brush.copyWith(strength: value),
          ),
          SliderRow(
            label: 'Falloff',
            value: brush.falloff,
            min: 0,
            max: 1,
            decimals: 2,
            onChanged: (value) => bench.brush = brush.copyWith(falloff: value),
          ),
          SliderRow(
            label: 'Jitter',
            value: brush.jitter,
            min: 0,
            max: 1,
            decimals: 2,
            onChanged: (value) => bench.brush = brush.copyWith(jitter: value),
          ),
          SliderRow(
            label: 'Spacing',
            value: brush.spacing,
            min: Brush.minSpacing,
            max: 1,
            decimals: 2,
            onChanged: (value) => bench.brush = brush.copyWith(spacing: value),
          ),
        ],
      ),
    );
  }

  static String _metres(double value) => value == value.roundToDouble()
      ? '${value.round()}'
      : value.toStringAsFixed(1);

  static String _extent(Terrain terrain) {
    final count = terrain.regions.length;
    final metres = _metres(terrain.regionSize * terrain.spacing);
    return switch (count) {
      0 => 'No ground yet. Add a region in the inspector.',
      1 => 'One region, $metres m across',
      _ => '$count regions, each $metres m across',
    };
  }
}

class _SetRow extends StatelessWidget {
  const _SetRow({
    required this.name,
    required this.detail,
    required this.selected,
    required this.onTap,
  });

  final String name;
  final String detail;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) => MouseRegion(
    cursor: SystemMouseCursors.click,
    child: GestureDetector(
      onTap: onTap,
      child: Container(
        height: 24,
        margin: const EdgeInsets.only(bottom: 2),
        padding: const EdgeInsets.symmetric(horizontal: Space.sm),
        decoration: BoxDecoration(
          color: selected ? OrblitColors.emberWash : OrblitColors.raised,
          borderRadius: BorderRadius.circular(Radii.control),
        ),
        child: Row(
          children: [
            Expanded(
              child: Text(
                name,
                overflow: TextOverflow.ellipsis,
                style: OrblitText.label.copyWith(
                  fontSize: 11.5,
                  color: selected ? OrblitColors.ember : OrblitColors.ink,
                ),
              ),
            ),
            Text(detail, style: OrblitText.mono.copyWith(fontSize: 10.5)),
          ],
        ),
      ),
    ),
  );
}

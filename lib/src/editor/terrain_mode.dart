import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../theme/orblit_theme.dart';
import 'dock.dart';
import 'editor_mode.dart';
import 'gizmo.dart';
import 'history.dart';
import 'terrain_bench.dart';
import 'terrain_edits.dart';
import 'viewport_input.dart';

/// The panel with the brush's settings, beside the inspector while the
/// ground is being shaped.
const PanelKind terrainBrushPanel = PanelKind(
  'terrainBrush',
  'Brush',
  Icons.brush_outlined,
);

/// Shaping and painting the ground.
///
/// [target] is the terrain a brush works on. It is asked again for each
/// gesture, since which one it is follows the selection.
EditorMode terrainMode({
  required TerrainBench bench,
  required History history,
  required OpenTerrain? Function() target,
}) {
  final input = TerrainBrushInput(
    bench: bench,
    history: history,
    target: target,
  );
  return EditorMode(
    name: 'terrain',
    label: 'Terrain',
    icon: Icons.landscape_outlined,
    layout: terrainLayout,
    tools: (_) => TerrainToolShelf(bench: bench),
    input: input.handle,
    overlay: (_, projection) =>
        BrushRing(bench: bench, projection: projection, target: target),
  );
}

/// The scene's arrangement with the brush in front on the right, since the
/// brush's settings are what is reached for most while shaping, and without
/// the panels that have nothing to say about the ground.
DockLayout terrainLayout() => const DockLayout(
  root: DockSplit(
    id: 'root',
    axis: Axis.vertical,
    weights: [0.74, 0.26],
    children: [
      DockSplit(
        id: 'middle',
        axis: Axis.horizontal,
        weights: [0.19, 0.58, 0.23],
        children: [
          DockGroup(
            id: 'left',
            panels: [DockPanel(id: 'outliner', kind: PanelKind.outliner)],
          ),
          DockGroup(
            id: 'centre',
            panels: [
              DockPanel(id: 'scene', kind: PanelKind.viewport),
              DockPanel(id: 'game', kind: PanelKind.game),
            ],
          ),
          DockGroup(
            id: 'right',
            panels: [
              DockPanel(id: 'brush', kind: terrainBrushPanel),
              DockPanel(id: 'inspector', kind: PanelKind.inspector),
            ],
          ),
        ],
      ),
      DockGroup(
        id: 'bottom',
        panels: [
          DockPanel(id: 'project', kind: PanelKind.project),
          DockPanel(id: 'console', kind: PanelKind.console),
        ],
      ),
    ],
  ),
);

/// Each tool's icon on the shelf.
IconData toolIcon(BrushTool tool) => switch (tool) {
  BrushTool.raise => Icons.arrow_upward,
  BrushTool.lower => Icons.arrow_downward,
  BrushTool.smooth => Icons.blur_on,
  BrushTool.flatten => Icons.horizontal_rule,
  BrushTool.slope => Icons.trending_up,
  BrushTool.cover => Icons.texture,
  BrushTool.colour => Icons.format_paint_outlined,
  BrushTool.roughness => Icons.grain,
  BrushTool.hole => Icons.adjust,
};

/// What a tool does, and what it does with Shift or ⌘ held.
String toolHint(BrushTool tool) => switch (tool) {
  BrushTool.raise => 'Lifts the ground. Shift lowers it.',
  BrushTool.lower => 'Sinks the ground. Shift raises it.',
  BrushTool.smooth => 'Evens out the bumps.',
  BrushTool.flatten => 'Draws the ground to the height the stroke began at.',
  BrushTool.slope =>
    'Drag from one end of the ramp to the other, then back over it: the '
        'ramp runs between the ground at the two ends.',
  BrushTool.cover =>
    'Lays the chosen set. Shift hands the ground back to automatic cover.',
  BrushTool.colour => 'Tints the ground. Shift washes the tint out.',
  BrushTool.roughness =>
    'Makes the ground rougher or glossier. Shift takes it back off.',
  BrushTool.hole => 'Cuts holes. Shift fills them.',
};

/// What a stroke is called next to Undo.
String strokeLabel(BrushTool tool, String terrain) => switch (tool) {
  BrushTool.cover ||
  BrushTool.colour ||
  BrushTool.roughness => '${toolLabel(tool)} on $terrain',
  BrushTool.hole => 'Punch holes in $terrain',
  _ => '${toolLabel(tool)} $terrain',
};

/// What the brush does with the pointer in a scene view.
///
/// Takes a gesture only over the ground, so a click beside the terrain still
/// selects what was clicked, and the camera is still the right button and
/// the wheel. A drag that leaves the ground carries on at the height it was
/// last at, so a stroke across a hole or off an edge does not stop dead.
class TerrainBrushInput {
  TerrainBrushInput({
    required this.bench,
    required this.history,
    required this.target,
  });

  final TerrainBench bench;
  final History history;
  final OpenTerrain? Function() target;

  OpenTerrain? _on;
  TerrainStroke? _stroke;
  Object? _gesture;
  Vector3? _last;

  bool handle(ViewportGesture gesture) {
    switch (gesture.phase) {
      case ViewportPhase.hover:
        final hit = _hit(gesture, target());
        bench.hover.value = hit;
        return hit != null;
      case ViewportPhase.leave:
        bench.hover.value = null;
        return false;
      case ViewportPhase.tap || ViewportPhase.dragStart:
        final open = target();
        final hit = _hit(gesture, open);
        if (open == null || hit == null) return false;
        _on = open;
        _stroke = bench.strokeOn(open, invert: gesture.add);
        _gesture = Object();
        bench.hover.value = hit;
        _move(hit);
        if (gesture.phase == ViewportPhase.tap) _end();
        return true;
      case ViewportPhase.dragUpdate:
        final open = _on;
        if (open == null) return false;
        final hit = _hit(gesture, open) ?? _level(gesture);
        bench.hover.value = hit;
        if (hit != null) _move(hit);
        return true;
      case ViewportPhase.dragEnd || ViewportPhase.dragCancel:
        if (_on == null) return false;
        _end();
        return true;
    }
  }

  void _move(Vector3 at) {
    final open = _on!;
    final stroke = _stroke!;
    _last = at;
    history.run(
      TerrainStrokeStep(
        bench: bench,
        file: open.file,
        stroke: stroke,
        x: at.x,
        z: at.z,
        gesture: _gesture!,
        label: strokeLabel(stroke.tool, open.name),
      ),
    );
  }

  void _end() {
    history.seal();
    _on = null;
    _stroke = null;
    _gesture = null;
    _last = null;
  }

  static Vector3? _hit(ViewportGesture gesture, OpenTerrain? open) {
    final projection = gesture.projection;
    if (open == null || projection == null) return null;
    final ray = projection.rayThrough(gesture.at);
    return open.terrain.raycast(ray.origin, ray.direction);
  }

  /// Where the pointer meets the level the stroke was last at, for a drag
  /// that has gone past the ground.
  Vector3? _level(ViewportGesture gesture) {
    final last = _last;
    final projection = gesture.projection;
    if (last == null || projection == null) return null;
    return projection
        .rayThrough(gesture.at)
        .meets(point: last, normal: Vector3(0, 1, 0));
  }
}

/// The brush's reach, drawn on the ground under the pointer: its edge, and
/// fainter, where it starts to ease off.
class BrushRing extends StatelessWidget {
  const BrushRing({
    super.key,
    required this.bench,
    required this.projection,
    required this.target,
  });

  final TerrainBench bench;
  final ViewportProjection projection;
  final OpenTerrain? Function() target;

  @override
  Widget build(BuildContext context) => CustomPaint(
    size: Size.infinite,
    painter: _RingPainter(bench, projection, target),
  );
}

class _RingPainter extends CustomPainter {
  _RingPainter(this.bench, this.projection, this.target)
    : super(repaint: Listenable.merge([bench, bench.hover]));

  final TerrainBench bench;
  final ViewportProjection projection;
  final OpenTerrain? Function() target;

  static const int _steps = 64;

  @override
  void paint(Canvas canvas, Size size) {
    final centre = bench.hover.value;
    final terrain = target()?.terrain;
    if (centre == null || terrain == null) return;
    final brush = bench.brush;
    final edge = _ring(terrain, centre, brush.radius);
    canvas
      ..drawPath(edge, _line(3, const Color(0x66000000)))
      ..drawPath(edge, _line(1.5, OrblitColors.ember));
    // Where the falloff begins. Left out when it would sit on the edge or
    // the centre, where it says nothing the edge and the dot do not.
    final core = brush.radius * (1 - brush.falloff);
    if (core > brush.radius * 0.05 && core < brush.radius * 0.95) {
      canvas.drawPath(
        _ring(terrain, centre, core),
        _line(1, OrblitColors.ember.withValues(alpha: 0.45)),
      );
    }
    if (projection.project(centre) case final at?) {
      canvas.drawCircle(at, 2, Paint()..color = OrblitColors.ember);
    }
  }

  /// A circle [radius] metres round [centre], lying on the ground.
  Path _ring(Terrain terrain, Vector3 centre, double radius) {
    final path = Path();
    var drawing = false;
    for (var i = 0; i <= _steps; i++) {
      final angle = i / _steps * 2 * math.pi;
      final x = centre.x + math.cos(angle) * radius;
      final z = centre.z + math.sin(angle) * radius;
      final y = terrain.heightAt(x, z) ?? centre.y;
      final at = projection.project(Vector3(x, y, z));
      if (at == null) {
        drawing = false;
      } else if (drawing) {
        path.lineTo(at.dx, at.dy);
      } else {
        path.moveTo(at.dx, at.dy);
        drawing = true;
      }
    }
    return path;
  }

  static Paint _line(double width, Color colour) => Paint()
    ..style = PaintingStyle.stroke
    ..strokeWidth = width
    ..color = colour;

  @override
  bool shouldRepaint(_RingPainter old) =>
      !identical(old.projection, projection) || !identical(old.bench, bench);
}

/// The tools, and the two settings changed most, on the shelf under the
/// menus.
class TerrainToolShelf extends StatelessWidget {
  const TerrainToolShelf({super.key, required this.bench});

  final TerrainBench bench;

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: bench,
    builder: (context, _) => SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (final tool in BrushTool.values) ...[
            _ToolButton(
              icon: toolIcon(tool),
              tooltip: '${toolLabel(tool)}\n${toolHint(tool)}',
              active: bench.tool == tool,
              onTap: () => bench.tool = tool,
            ),
            // Shaping, then painting, then holes.
            if (tool == BrushTool.slope || tool == BrushTool.roughness)
              const _Divider(),
          ],
          const _Divider(),
          _ShelfSlider(
            label: 'Size',
            value: bench.brush.size,
            min: 1,
            max: 128,
            text: '${bench.brush.size.round()} m',
            onChanged: (value) =>
                bench.brush = bench.brush.copyWith(size: value),
          ),
          _ShelfSlider(
            label: 'Strength',
            value: bench.brush.strength,
            min: 0,
            max: 1,
            text: bench.brush.strength.toStringAsFixed(2),
            onChanged: (value) =>
                bench.brush = bench.brush.copyWith(strength: value),
          ),
        ],
      ),
    ),
  );
}

class _Divider extends StatelessWidget {
  const _Divider();

  @override
  Widget build(BuildContext context) => Container(
    width: 1,
    height: 18,
    margin: const EdgeInsets.symmetric(horizontal: Space.sm),
    color: OrblitColors.line,
  );
}

class _ShelfSlider extends StatelessWidget {
  const _ShelfSlider({
    required this.label,
    required this.value,
    required this.min,
    required this.max,
    required this.text,
    required this.onChanged,
  });

  final String label;
  final double value;
  final double min;
  final double max;
  final String text;
  final ValueChanged<double> onChanged;

  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Text(label, style: OrblitText.label),
      SizedBox(
        width: 110,
        child: Slider(
          value: value.clamp(min, max),
          min: min,
          max: max,
          onChanged: onChanged,
        ),
      ),
      SizedBox(width: 40, child: Text(text, style: OrblitText.monoValue)),
    ],
  );
}

class _ToolButton extends StatefulWidget {
  const _ToolButton({
    required this.icon,
    required this.tooltip,
    required this.active,
    required this.onTap,
  });

  final IconData icon;
  final String tooltip;
  final bool active;
  final VoidCallback onTap;

  @override
  State<_ToolButton> createState() => _ToolButtonState();
}

class _ToolButtonState extends State<_ToolButton> {
  bool _hovering = false;

  @override
  Widget build(BuildContext context) => Tooltip(
    message: widget.tooltip,
    child: MouseRegion(
      cursor: SystemMouseCursors.click,
      onEnter: (_) => setState(() => _hovering = true),
      onExit: (_) => setState(() => _hovering = false),
      child: GestureDetector(
        onTap: widget.onTap,
        child: Container(
          width: 30,
          height: 28,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            color: widget.active
                ? OrblitColors.emberWash
                : (_hovering ? OrblitColors.raised : Colors.transparent),
            borderRadius: BorderRadius.circular(Radii.control),
          ),
          child: Icon(
            widget.icon,
            size: 17,
            color: widget.active
                ? OrblitColors.ember
                : (_hovering ? OrblitColors.ink : OrblitColors.inkMid),
          ),
        ),
      ),
    ),
  );
}

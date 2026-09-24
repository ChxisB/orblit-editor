import 'package:orblit_terrain/orblit_terrain.dart';

import 'history.dart';
import 'terrain_bench.dart';

// Every change the terrain mode makes, as steps on the undo stack. None of
// them holds a copy of a region: a stroke keeps the tiles it touched, a
// settings edit the settings, and taking a region away keeps that one region
// so putting it back is the same object.

/// A terrain's settings, apart from its regions.
typedef TerrainSettings = ({
  List<TerrainSet> sets,
  AutoCover autoCover,
  double blendSharpness,
});

TerrainSettings settingsOf(Terrain terrain) => (
  sets: List.unmodifiable(terrain.sets),
  autoCover: terrain.autoCover,
  blendSharpness: terrain.blendSharpness,
);

/// What each tool is called on the shelf and next to Undo.
String toolLabel(BrushTool tool) => switch (tool) {
  BrushTool.raise => 'Raise',
  BrushTool.lower => 'Lower',
  BrushTool.smooth => 'Smooth',
  BrushTool.flatten => 'Flatten',
  BrushTool.slope => 'Slope',
  BrushTool.cover => 'Paint cover',
  BrushTool.colour => 'Paint colour',
  BrushTool.roughness => 'Paint roughness',
  BrushTool.hole => 'Punch holes',
};

/// One move of the brush across a terrain.
///
/// The first time it is applied it moves the stroke, which writes the ground
/// and says which tiles it touched; after that it writes those tiles back.
/// Every move of one drag shares a [gesture], so a stroke is one step to
/// undo however many frames it took, and what that step keeps is the tiles
/// the whole stroke touched as they were before it and after.
class TerrainStrokeStep extends EditorCommand {
  TerrainStrokeStep({
    required this.bench,
    required this.file,
    required this.stroke,
    required this.x,
    required this.z,
    required this.gesture,
    required this.label,
  });

  final TerrainBench bench;

  final String file;

  final TerrainStroke stroke;

  /// Where in the world the brush was carried to.
  final double x;
  final double z;

  final Object gesture;

  @override
  final String label;

  TerrainPatch? _patch;

  /// What the stroke has changed so far, or null before it is applied.
  TerrainPatch? get patch => _patch;

  @override
  String get sceneId => terrainSceneId(file);

  @override
  Object? get mergeKey => (file, gesture);

  /// The ground is not something any panel but a view shows, so a stroke is
  /// redrawn and nothing else is rebuilt.
  @override
  bool get onlyMoves => true;

  @override
  void apply(SceneHost host) {
    final terrain = bench[file]?.terrain;
    if (terrain == null) return;
    final patch = _patch;
    if (patch == null) {
      _patch = stroke.moveTo(x, z);
    } else {
      patch.apply(terrain);
    }
  }

  @override
  void revert(SceneHost host) {
    final terrain = bench[file]?.terrain;
    if (terrain != null) _patch?.revert(terrain);
  }

  @override
  void absorb(EditorCommand later) {
    if (later is! TerrainStrokeStep) return;
    final theirs = later._patch;
    if (theirs == null) return;
    _patch = _patch?.followedBy(theirs) ?? theirs;
  }
}

/// A change to a terrain's sets, its automatic cover or its blend.
class TerrainSettingsEdit extends EditorCommand {
  TerrainSettingsEdit({
    required this.bench,
    required this.file,
    required this.label,
    required this.from,
    required this.to,
    this.gesture,
  });

  final TerrainBench bench;

  final String file;

  @override
  final String label;

  final TerrainSettings from;

  /// Not final: a merged slider drag rewrites where it ends up.
  TerrainSettings to;

  /// What ties a drag's worth of these together, or null for one alone.
  final Object? gesture;

  @override
  String get sceneId => terrainSceneId(file);

  @override
  Object? get mergeKey => gesture == null ? null : (file, gesture);

  @override
  void apply(SceneHost host) => bench.putSettings(file, to);

  @override
  void revert(SceneHost host) => bench.putSettings(file, from);

  @override
  void absorb(EditorCommand later) {
    if (later is TerrainSettingsEdit) to = later.to;
  }
}

/// A region of ground added to a terrain, or taken away from one.
///
/// Holds the region itself, so taking one away and undoing it puts back the
/// ground that was there and not a flat square.
class TerrainRegionEdit extends EditorCommand {
  TerrainRegionEdit({
    required this.bench,
    required this.file,
    required this.region,
    required this.adding,
    required this.label,
  });

  final TerrainBench bench;

  final String file;

  final TerrainRegion region;

  /// Whether this adds [region]; false takes it away.
  final bool adding;

  @override
  final String label;

  @override
  String get sceneId => terrainSceneId(file);

  @override
  void apply(SceneHost host) => bench.putRegion(file, region, present: adding);

  @override
  void revert(SceneHost host) =>
      bench.putRegion(file, region, present: !adding);
}

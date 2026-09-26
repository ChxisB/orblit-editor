import 'dart:io';
import 'dart:math' as math;
import 'dart:ui' as ui;

import 'package:flutter/foundation.dart';
import 'package:orblit_filament/orblit_filament.dart'
    show OrblitPopulation, OrblitTerrain;
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_stage/orblit_stage.dart' show scatterFrom, terrainFrom;
import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:path/path.dart' as p;
import 'package:vector_math/vector_math_64.dart';

import 'history.dart';
import 'scene.dart';
import 'terrain_edits.dart';

/// What the undo stack files a terrain's changes under.
///
/// A terrain is not a scene, but it is a document that changes and is saved,
/// like a clip. Prefixed so no scene or clip can ever be given the same one.
String terrainSceneId(String file) => 'terrain:$file';

/// The terrain on [object], if it has one: the ground it puts in the scene.
doc.TerrainComponent? terrainComponentOf(SceneObject object) =>
    switch (object.components[doc.SceneComponents.terrain]) {
      final doc.TerrainComponent terrain => terrain,
      _ => null,
    };

/// A terrain read from the project, as it is now.
class OpenTerrain {
  OpenTerrain({required this.file, required this.path, required this.terrain});

  /// Project-relative, as a [doc.TerrainComponent] names it.
  final String file;

  /// Where the settings file is on this machine. Its regions are beside it.
  final String path;

  /// Edited in place: a brush writes straight into its regions, and the
  /// commands that change it keep what they need to change it back.
  final Terrain terrain;

  /// The undo stack's stamp for this terrain when it was last written.
  int savedStamp = 0;

  /// Each region's revision when it was written, which is also every region
  /// file there is. A region whose revision has moved on since is written
  /// again; one no longer in the terrain has its file taken away.
  final Map<RegionKey, int> savedRevisions = {};

  String get id => terrainSceneId(file);

  String get name => p.basenameWithoutExtension(file);

  /// What the renderer knows this terrain by from one frame to the next.
  int get key => file.hashCode & 0x3fffffff;
}

/// The terrains being edited, and the brush they are edited with.
///
/// A notifier rather than state in a panel: the tool shelf, the brush panel,
/// the ring on the ground and the inspector all read the same brush, and a
/// terrain has to stay open whichever of them is showing.
///
/// Terrains are opened the first time a scene asks to draw one and stay open,
/// which is what lets a stroke be undone after the object that showed it was
/// deleted and brought back.
class TerrainBench extends ChangeNotifier {
  TerrainBench({
    required this.history,
    required this.projectRoot,
    this.onProblem,
  });

  final History history;

  /// What a terrain's file and its pictures are relative to.
  final String projectRoot;

  /// Told what could not be read, drawn or written, once each. Can be called
  /// while a frame is being built, so it must not build anything itself.
  final void Function(String message)? onProblem;

  final Map<String, OpenTerrain> _open = {};

  /// Files that could not be read. Not tried again, so a broken file is one
  /// message and not one a frame.
  final Set<String> _unreadable = {};

  final Set<String> _said = {};

  bool _disposed = false;

  OpenTerrain? operator [](String file) => _open[file];

  Iterable<OpenTerrain> get open => _open.values;

  // ---- the brush ----

  BrushTool _tool = BrushTool.raise;

  BrushTool get tool => _tool;

  set tool(BrushTool value) {
    if (value == _tool) return;
    _tool = value;
    notifyListeners();
  }

  Brush _brush = const Brush();

  /// The settings every tool shares.
  Brush get brush => _brush;

  set brush(Brush value) {
    if (value == _brush) return;
    _brush = value;
    notifyListeners();
  }

  int _set = 0;

  /// The set [BrushTool.cover] lays, by its index in the terrain's sets.
  int get set => _set;

  set set(int value) {
    if (value == _set) return;
    _set = value;
    notifyListeners();
  }

  GroundColour _colour = GroundColour.of(red: 150, green: 124, blue: 96);

  /// The tint [BrushTool.colour] paints. Its roughness is not painted with it.
  GroundColour get colour => _colour;

  set colour(GroundColour value) {
    if (value == _colour) return;
    _colour = value;
    notifyListeners();
  }

  double _roughness = 0.5;

  /// The nudge [BrushTool.roughness] paints, −1 to +1.
  double get roughness => _roughness;

  set roughness(double value) {
    if (value == _roughness) return;
    _roughness = value;
    notifyListeners();
  }

  /// Where the brush is over the ground, or null when it is not. Apart from
  /// the rest so a ring following the pointer repaints itself and nothing
  /// else.
  final ValueNotifier<Vector3?> hover = ValueNotifier(null);

  /// A stroke across [open] with the brush as it is now.
  ///
  /// [invert] turns the tool round, which is what holding a modifier does.
  TerrainStroke strokeOn(OpenTerrain open, {bool invert = false, int? seed}) {
    final sets = open.terrain.sets.length;
    return TerrainStroke(
      open.terrain,
      tool: _tool,
      brush: _brush,
      invert: invert,
      set: sets == 0 ? 0 : _set.clamp(0, sets - 1),
      colour: _colour,
      roughness: _roughness,
      seed: seed ?? math.Random().nextInt(1 << 30),
    );
  }

  // ---- opening ----

  /// The terrain at [file], read now if it is not open, or null when there
  /// is none or it cannot be read. Says what could not be read, once.
  OpenTerrain? terrainFor(String? file) {
    if (file == null || file.isEmpty) return null;
    final had = _open[file];
    if (had != null || _unreadable.contains(file)) return had;
    try {
      return _open[file] = _read(file);
    } on TerrainFormatException catch (error) {
      _cannotRead(file, error.message);
    } on FileSystemException catch (error) {
      _cannotRead(file, error.message);
    }
    return null;
  }

  void _cannotRead(String file, String why) {
    _unreadable.add(file);
    _report('The terrain $file could not be read: $why');
  }

  OpenTerrain _read(String file) {
    final path = p.join(projectRoot, file);
    final load = Terrain.decode(File(path).readAsStringSync());
    final open = OpenTerrain(file: file, path: path, terrain: load.terrain);
    final problems = [...load.problems];
    final folder = p.dirname(path);
    for (final key in load.regions) {
      final problem = _readRegion(open, p.join(folder, key.fileName), key);
      if (problem != null) problems.add(problem);
    }
    for (final problem in problems) {
      _report('${open.name}: $problem');
    }
    return open;
  }

  /// Reads one region into [open], or says why it was left out.
  String? _readRegion(OpenTerrain open, String path, RegionKey key) {
    final RegionLoad load;
    try {
      load = TerrainRegion.decode(File(path).readAsBytesSync());
    } on RegionFormatException catch (error) {
      return 'Region ${key.fileName} was left out: ${error.message}';
    } on FileSystemException catch (error) {
      return 'Region ${key.fileName} was left out: ${error.message}';
    }
    final region = load.region;
    if (region.key != key || region.size != open.terrain.regionSize) {
      return 'Region ${key.fileName} was left out: it is not the region its '
          'name says, or not the size of the terrain\'s.';
    }
    open.terrain.putRegion(region);
    open.savedRevisions[key] = region.revision;
    for (final problem in load.problems) {
      _report('${open.name}, ${key.fileName}: $problem');
    }
    return null;
  }

  /// Writes [terrain] as a new file at [file] and opens it, or says why it
  /// could not be written.
  String? create(String file, Terrain terrain) {
    final open = OpenTerrain(
      file: file,
      path: p.join(projectRoot, file),
      terrain: terrain,
    );
    final problem = _write(open);
    if (problem != null) return '${open.name} could not be written: $problem';
    _open[file] = open;
    _unreadable.remove(file);
    notifyListeners();
    return null;
  }

  // ---- saving ----

  bool isUnsaved(OpenTerrain open) =>
      history.stampFor(open.id) != open.savedStamp;

  bool get anyUnsaved => _open.values.any(isUnsaved);

  /// Writes every terrain with changes, and says which could not be written.
  List<String> saveAll() => [
    for (final one in _open.values)
      if (isUnsaved(one)) ?save(one),
  ];

  /// Writes [open], or says why it could not be.
  String? save(OpenTerrain open) {
    final problem = _write(open);
    if (problem != null) return '${open.name} could not be saved: $problem';
    open.savedStamp = history.stampFor(open.id);
    notifyListeners();
    return null;
  }

  /// Writes the regions that changed, then the settings that list them, then
  /// takes away the files of regions that are gone. In that order so a save
  /// cut short leaves a settings file whose regions are all there.
  static String? _write(OpenTerrain open) {
    final folder = p.dirname(open.path);
    final written = <RegionKey, int>{};
    try {
      Directory(folder).createSync(recursive: true);
      for (final region in open.terrain.regions) {
        if (open.savedRevisions[region.key] != region.revision) {
          File(p.join(folder, region.key.fileName))
              .writeAsBytesSync(region.encode());
        }
        written[region.key] = region.revision;
      }
      File(open.path).writeAsStringSync(open.terrain.encode());
      for (final key in open.savedRevisions.keys) {
        if (written.containsKey(key)) continue;
        final gone = File(p.join(folder, key.fileName));
        if (gone.existsSync()) gone.deleteSync();
      }
    } on FileSystemException catch (error) {
      return error.message;
    }
    open.savedRevisions
      ..clear()
      ..addAll(written);
    return null;
  }

  // ---- editing ----

  /// Changes [open]'s settings through the undo stack. A [gesture] ties a
  /// slider's worth of these into one step.
  void edit(
    OpenTerrain open,
    String label,
    TerrainSettings Function(TerrainSettings settings) change, {
    Object? gesture,
  }) {
    final from = settingsOf(open.terrain);
    history.run(
      TerrainSettingsEdit(
        bench: this,
        file: open.file,
        label: label,
        from: from,
        to: change(from),
        gesture: gesture,
      ),
    );
  }

  /// Where a [TerrainSettingsEdit] puts a terrain's settings.
  void putSettings(String file, TerrainSettings settings) {
    final terrain = _open[file]?.terrain;
    if (terrain == null) return;
    terrain.sets
      ..clear()
      ..addAll(settings.sets);
    terrain
      ..autoCover = settings.autoCover
      ..blendSharpness = settings.blendSharpness;
    // A set's pictures may be different ones now, and the renderer takes
    // pictures again only when told to.
    _picturesRevision++;
    notifyListeners();
  }

  /// Where a [TerrainRegionEdit] adds or takes away a region.
  void putRegion(String file, TerrainRegion region, {required bool present}) {
    final terrain = _open[file]?.terrain;
    if (terrain == null) return;
    if (present) {
      terrain.putRegion(region);
      // Touched so the renderer, which let it go, takes it again.
      region.touch();
    } else {
      terrain.removeRegion(region.key);
    }
    notifyListeners();
  }

  // ---- drawing ----

  /// Pixels a side every set's pictures are made, since the renderer keeps
  /// them as layers of one array and those are all one size.
  static const int pictureSize = 512;

  final Map<String, Uint8List?> _pictures = {};

  int _picturesRevision = 0;

  /// Bumped each time a picture arrives or the sets change.
  int get picturesRevision => _picturesRevision;

  /// The terrains [objects] put in the scene, as the renderer takes them.
  ///
  /// One per file however many objects name it: two copies of the same
  /// ground would be drawn into each other.
  List<OrblitTerrain> renderFor(Iterable<SceneObject> objects) {
    final seen = <String>{};
    final terrains = <OrblitTerrain>[];
    for (final object in objects) {
      final component = terrainComponentOf(object);
      final file = component?.file;
      if (component == null || file == null || !seen.add(file)) continue;
      final open = terrainFor(file);
      if (open == null) continue;
      try {
        terrains.add(
          terrainFrom(
            open.terrain,
            key: open.key,
            pixels: _picture,
            picturesRevision: _picturesRevision,
            castShadows: component.castShadows,
            receiveShadows: component.receiveShadows,
          ),
        );
      } on ArgumentError catch (error) {
        _report('${open.name} cannot be drawn: ${error.message}');
      }
    }
    return terrains;
  }

  /// Where each terrain's scatter stands, kept between frames so that a
  /// stroke places again only the regions it touched, and what each last
  /// drew, against the placer's revision when it did.
  final Map<String, ScatterPlacer> _placers = {};
  final Map<String, (int, List<OrblitPopulation>)> _scattered = {};

  /// What the terrains [objects] put in the scene have scattered over them,
  /// as the renderer takes it: a population a region and layer.
  ///
  /// Blocks only. A layer that names a model is left out here, since drawing
  /// it means resolving its mesh and material the way the scene's own objects
  /// are, and nothing does that for a terrain yet.
  List<OrblitPopulation> scatterFor(Iterable<SceneObject> objects) {
    final seen = <String>{};
    final drawn = <OrblitPopulation>[];
    for (final object in objects) {
      final file = terrainComponentOf(object)?.file;
      if (file == null || !seen.add(file)) continue;
      final open = terrainFor(file);
      if (open == null) continue;
      final placer = _placers.putIfAbsent(file, ScatterPlacer.new);
      placer.update(open.terrain);
      var held = _scattered[file];
      if (held == null || held.$1 != placer.revision) {
        final blocks = scatterFrom(
          placer,
          key: open.key,
          models: false,
        ).populations;
        held = _scattered[file] = (placer.revision, blocks);
      }
      drawn.addAll(held.$2);
    }
    return drawn;
  }

  /// The picture at [path] as RGBA bytes, or null while it is being read or
  /// when it cannot be.
  Uint8List? _picture(String path) {
    if (_pictures.containsKey(path)) return _pictures[path];
    _pictures[path] = null;
    _decode(path);
    return null;
  }

  Future<void> _decode(String path) async {
    try {
      final bytes = await File(p.join(projectRoot, path)).readAsBytes();
      final codec = await ui.instantiateImageCodec(
        bytes,
        targetWidth: pictureSize,
        targetHeight: pictureSize,
      );
      final frame = await codec.getNextFrame();
      codec.dispose();
      // Straight, not premultiplied: alpha is the albedo's height and the
      // normal map's roughness, and multiplying the colour by either would
      // darken the ground wherever it is low or glossy.
      final data = await frame.image.toByteData(
        format: ui.ImageByteFormat.rawStraightRgba,
      );
      frame.image.dispose();
      if (_disposed || data == null) return;
      _pictures[path] = data.buffer.asUint8List(
        data.offsetInBytes,
        data.lengthInBytes,
      );
      _picturesRevision++;
      notifyListeners();
    } on Exception catch (error) {
      _report('The picture $path could not be read: $error');
    }
  }

  void _report(String message) {
    if (_said.add(message)) onProblem?.call(message);
  }

  @override
  void dispose() {
    _disposed = true;
    hover.dispose();
    super.dispose();
  }
}

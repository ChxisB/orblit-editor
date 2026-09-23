import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:path/path.dart' as p;
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_weather/orblit_weather.dart';

import 'package:orblit_mesh/orblit_mesh.dart';

import 'boundary.dart';
import 'grid.dart';
import 'surface.dart';

import 'colour.dart';

import 'package:vector_math/vector_math_64.dart' hide Colors;

part 'scene_object.dart';
part 'scene_render.dart';

/// What kind of thing an object is, which decides what components it has and
/// therefore what the inspector shows.
enum ObjectKind { scene, mesh, light, camera, group, weather, canvas, shape }

/// An object being dragged.
///
/// A type of its own rather than the bare id, because an asset path is also a
/// string and the outliner, the viewport and the project browser all take
/// drops. With one type for both, dragging a crate into the browser would look
/// exactly like dragging a file, and each target would have to guess.
class ObjectDrag {
  const ObjectDrag(this.id, this.name);

  final String id;

  /// What to call it while it is in the air, and what to name the file it
  /// lands in.
  final String name;
}

/// Something an edit could not do, worth saying out loud.
class SceneError extends StateError {
  SceneError(super.message);
}

/// The scene being edited.
///
/// A flat list plus a parent link rather than nested children: reparenting is
/// then one field, ordering survives it, and there is no second copy of the
/// tree to keep in step with the first.
class EditorScene {
  EditorScene(
    List<SceneObject> objects, {
    this.name = 'Scene',
    Color? skyColour,
    this.ambient = 28000,
    this.timeOfDay = 10,
    this.dayCycle = false,
    this.hoursPerSecond = 0.5,
  }) : _objects = objects,
       skyColour = skyColour ?? const Color(0xFF1A2029) {
    for (final object in objects) {
      if (_byId.containsKey(object.id)) {
        throw SceneError('Two objects share the id "${object.id}".');
      }
      _byId[object.id] = object;
    }
  }

  /// Where an object sits among its siblings, for reordering.
  int indexOf(String id) {
    final object = _byId[id];
    if (object == null) return -1;
    return siblingsOf(object.parentId).indexWhere((o) => o.id == id);
  }

  /// The objects sharing a parent, in the order they are drawn in the tree.
  List<SceneObject> siblingsOf(String? parentId) => [
    for (final o in _objects)
      if (o.parentId == parentId) o,
  ];

  /// Moves an object to a new parent at a given position among its siblings.
  ///
  /// Order is the list's own order rather than a number on each object: an
  /// index stored per object has to be renumbered on every move, and the
  /// renumbering is what goes wrong.
  void moveTo(String id, {String? parentId, required int index}) {
    final object = _byId[id];
    if (object == null) return;

    if (parentId != null) {
      if (parentId == id || isAncestorOf(id, parentId)) {
        throw SceneError(
          'Cannot put "${object.name}" inside itself or its own children.',
        );
      }
      if (!_byId.containsKey(parentId)) return;
    }

    _objects.remove(object);
    object.parentId = parentId;

    // Placed relative to its new siblings rather than at an absolute position
    // in the flat list, which is what makes the tree read in the right order.
    final siblings = siblingsOf(parentId);
    final at = index.clamp(0, siblings.length);
    if (at >= siblings.length) {
      // After the last sibling and everything nested under it, so it does not
      // land in the middle of somebody else's children.
      final last = siblings.isEmpty ? null : siblings.last;
      final tail = last == null
          ? null
          : (descendantsOf(last.id).isEmpty
                ? last
                : descendantsOf(last.id).last);
      final anchor = tail == null ? -1 : _objects.indexOf(tail);
      _objects.insert(anchor + 1, object);
    } else {
      _objects.insert(_objects.indexOf(siblings[at]), object);
    }

    invalidate();
  }

  /// A default scene, so a new project opens on something rather than nothing.
  ///
  /// The ground is a flattened box rather than a plane because the renderer
  /// draws boxes and nothing else yet; when meshes load it becomes a mesh.
  factory EditorScene.starter() => EditorScene([
    // Watts per square metre, because that is what a sun's strength is
    // stated in. A hundred and ten of them is about seventy-five thousand
    // lux, which is a bright but not blinding afternoon.
    SceneObject(
      id: 'sun',
      name: 'Sun',
      kind: ObjectKind.light,
      rotation: Vector3(-55, 35, 0),
      colour: const Color(0xFFFFF3E0),
      power: 110,
    ),
    SceneObject(
      id: 'ground',
      name: 'Ground',
      kind: ObjectKind.mesh,
      position: Vector3(0, -1.05, 0),
      scale: Vector3(8, 0.05, 8),
      colour: const Color(0xFF3B424C),
    ),
    SceneObject(id: 'props', name: 'Props', kind: ObjectKind.group),
    SceneObject(
      id: 'cube',
      name: 'Cube',
      kind: ObjectKind.mesh,
      parentId: 'props',
      rotation: Vector3(0, 25, 0),
      colour: const Color(0xFFD9634F),
    ),
    SceneObject(
      id: 'crate',
      name: 'Crate',
      kind: ObjectKind.mesh,
      parentId: 'props',
      position: Vector3(2.2, -0.65, 0.6),
      scale: Vector3(0.7, 0.7, 0.7),
      colour: const Color(0xFFE5B84F),
    ),
    SceneObject(
      id: 'camera',
      name: 'Camera',
      kind: ObjectKind.camera,
      position: Vector3(6, 4, 8),
      rotation: Vector3(-20, 35, 0),
    ),
    // A fair day rather than a clear one, so the object in the tree is
    // visibly doing something the moment somebody selects it.
    SceneObject(
      id: 'weather',
      name: 'Weather',
      kind: ObjectKind.weather,
      condition: WeatherCondition.fair,
    ),
  ]);

  /// What the scene is called, which need not match its file name.
  String name;

  /// The sky, and by the same setting the light it casts.
  ///
  /// One property rather than two, because a backdrop that lights nothing
  /// reads as a photograph behind the scene rather than the sky it stands
  /// under.
  Color skyColour;

  /// How much light the sky casts, in lux.
  double ambient;

  /// The hour the scene is set at, from zero to twenty-four.
  ///
  /// What is authored, not what is showing: with a cycle running, this is
  /// where the day starts from and [currentTimeOfDay] is where it has got to.
  /// Keeping them apart means a cycle left running does not quietly rewrite
  /// the scene somebody saved.
  double timeOfDay;

  /// Whether the day runs on its own, and how fast.
  ///
  /// Off, the scene sits at its hour and the body above it is whichever one
  /// the light says it is. On, the hour advances and the sky decides.
  bool dayCycle;

  double hoursPerSecond;

  /// Seconds since the editor started animating this scene.
  ///
  /// Not saved, and not part of the document: it is the editor's clock, and a
  /// scene reopened tomorrow should be where it was left rather than wherever
  /// the ticker had got to.
  double clock = 0;

  /// Whether anything here moves without somebody moving it.
  ///
  /// Drifting cloud is not in this list. The renderer moves that on its own
  /// clock, so it keeps going at sixty frames a second without the editor
  /// republishing the scene to say so — which is the difference between mist
  /// costing a message a frame and costing nothing.
  bool get isAnimated =>
      dayCycle || _weatherIsChanging || (weatherNow?.lightning ?? 0) > 0;

  /// The object that decides what the air is doing, if there is one.
  ///
  /// The first of them. A scene with two would be two answers to one
  /// question, and the same rule the light above the scene follows: the first
  /// one is it, and the rest are reported rather than silently obeyed.
  SceneObject? get weather {
    for (final object in _objects) {
      if (object.kind == ObjectKind.weather) return object;
    }
    return null;
  }

  /// Whether more than one thing is claiming to be the weather.
  bool get hasSpareWeather =>
      _objects.where((o) => o.kind == ObjectKind.weather).length > 1;

  /// What the air is doing this instant, part of the way through whatever
  /// change it is in the middle of.
  WeatherState? get weatherNow {
    final object = weather;
    if (object == null || !isShown(object.id)) return null;

    final from = object.blendFrom;
    if (from == null || object.transitionSeconds <= 0) return object.weather;

    final t = (clock - object.blendSince) / object.transitionSeconds;
    if (t >= 1) return object.weather;
    return WeatherState.lerp(from, object.weather, t);
  }

  bool get _weatherIsChanging {
    final object = weather;
    if (object == null || object.blendFrom == null) return false;
    return clock - object.blendSince < object.transitionSeconds;
  }

  /// The hour the scene is showing, which is the authored one until a cycle
  /// starts carrying it forward.
  double get currentTimeOfDay =>
      dayCycle ? (timeOfDay + clock * hoursPerSecond) % 24 : timeOfDay;

  /// The sky at that hour.
  SkyState get skyState => DayCycle.at(currentTimeOfDay);

  /// The light everything is lit from above by, if there is one.
  ///
  /// The first directional light in the scene. A renderer draws one, so a
  /// second is a light that would be quietly ignored — and this is where the
  /// choice of which is made rather than left to chance.
  SceneObject? get celestial {
    for (final object in _objects) {
      if (object.kind == ObjectKind.light &&
          object.lightType == LightType.sun) {
        return object;
      }
    }
    return null;
  }

  /// Which body is up: the cycle's, or the one the light was told to be.
  CelestialBody get activeBody =>
      dayCycle ? skyState.body : (celestial?.body ?? CelestialBody.sun);

  /// What an object is called on screen.
  ///
  /// A celestial light that still has a body's name for a name follows the
  /// body, so a scene that runs into the night says Moon in the tree without
  /// anybody editing the document. Give it a name of your own and it keeps
  /// that instead — a rename is somebody saying they want it called that.
  String displayNameOf(SceneObject object) {
    if (!identical(object, celestial)) return object.name;
    final named = CelestialBody.values.any((b) => b.label == object.name);
    return named ? activeBody.label : object.name;
  }

  /// The icon that goes with that name.
  IconData displayIconOf(SceneObject object) =>
      identical(object, celestial) && activeBody == CelestialBody.moon
      ? Icons.nightlight_outlined
      : object.icon;

  final List<SceneObject> _objects;

  final Map<String, SceneObject> _byId = {};

  /// Bumped by every structural change, so derived work can tell whether the
  /// answer it cached is still the answer.
  int _generation = 0;

  int _cachedGeneration = -1;

  final Map<String, Matrix4> _worldCache = {};

  List<SceneObject> get objects => List.unmodifiable(_objects);

  int get length => _objects.length;

  SceneObject? operator [](String id) => _byId[id];

  bool contains(String id) => _byId.containsKey(id);

  /// Call after mutating an object, so cached world matrices are recomputed.
  void invalidate() => _generation++;

  /// Objects with no parent, in order.
  List<SceneObject> get roots => [
    for (final o in _objects)
      if (o.parentId == null) o,
  ];

  List<SceneObject> childrenOf(String id) => [
    for (final o in _objects)
      if (o.parentId == id) o,
  ];

  /// Every object under [id], deepest last.
  List<SceneObject> descendantsOf(String id) {
    final found = <SceneObject>[];
    void walk(String parent) {
      for (final child in childrenOf(parent)) {
        found.add(child);
        walk(child.id);
      }
    }

    walk(id);
    return found;
  }

  /// How deep an object sits, for indenting.
  int depthOf(String id) {
    var depth = 0;
    var current = _byId[id]?.parentId;
    // Bounded rather than trusted: a cycle here would hang the outliner while
    // it drew, which is the worst place to discover one.
    while (current != null && depth < _maxDepth) {
      depth++;
      current = _byId[current]?.parentId;
    }
    return depth;
  }

  static const _maxDepth = 256;

  /// Whether an object is shown, which means it and everything above it is.
  ///
  /// Hiding a group has to hide what is inside it. A flag that applied only to
  /// the thing it was set on would make hiding a folder do nothing visible,
  /// which reads as a broken toggle rather than as a deliberate limit.
  bool isShown(String id) {
    var current = _byId[id];
    var steps = 0;
    while (current != null && steps < _maxDepth) {
      if (!current.visible) return false;
      final parentId = current.parentId;
      if (parentId == null) return true;
      current = _byId[parentId];
      steps++;
    }
    return true;
  }

  /// Whether [ancestor] is above [id] in the tree.
  ///
  /// The check that stops somebody dragging a parent onto its own child, which
  /// would detach the whole branch from the scene and leave it unreachable.
  bool isAncestorOf(String ancestor, String id) {
    var current = _byId[id]?.parentId;
    var steps = 0;
    while (current != null && steps < _maxDepth) {
      if (current == ancestor) return true;
      current = _byId[current]?.parentId;
      steps++;
    }
    return false;
  }

  /// Where an object ends up, with every parent applied.
  Matrix4 worldOf(String id) {
    if (_cachedGeneration != _generation) {
      _worldCache.clear();
      _cachedGeneration = _generation;
    }

    final cached = _worldCache[id];
    if (cached != null) return cached;

    final object = _byId[id];
    if (object == null) return Matrix4.identity();

    final parentId = object.parentId;
    final world = parentId == null || !_byId.containsKey(parentId)
        ? object.localTransform
        : worldOf(parentId).multiplied(object.localTransform);

    return _worldCache[id] = world;
  }

  // ---- structural edits, called by commands rather than by widgets ----

  void add(SceneObject object, {int? at}) {
    if (_byId.containsKey(object.id)) {
      throw SceneError('There is already an object with id "${object.id}".');
    }
    _byId[object.id] = object;
    _objects.insert(at ?? _objects.length, object);
    invalidate();
  }

  /// Removes an object and everything under it, and says where it was.
  ///
  /// Returns the removed objects with their positions so an undo can put them
  /// back exactly, rather than appending them to the end where they would
  /// silently reorder the outliner.
  List<({SceneObject object, int index})> remove(String id) {
    final object = _byId[id];
    if (object == null) return const [];

    final going = [object, ...descendantsOf(id)];
    final removed = <({SceneObject object, int index})>[];
    for (final gone in going) {
      final index = _objects.indexOf(gone);
      if (index < 0) continue;
      removed.add((object: gone, index: index));
    }
    // Highest index first, so each removal leaves the earlier indices valid.
    removed.sort((a, b) => b.index.compareTo(a.index));
    for (final entry in removed) {
      _objects.removeAt(entry.index);
      _byId.remove(entry.object.id);
    }

    invalidate();
    // Back into insertion order, which is the order an undo has to replay.
    return removed.reversed.toList();
  }

  /// Swaps every object in this scene for [objects], keeping the scene itself.
  ///
  /// For the one caller that has a whole new set of them: a change stated as a
  /// difference between two documents, which is worked out on documents and
  /// then has to land somewhere. Everything else edits the objects it already
  /// has, and should — this is the blunt instrument, and using it for a drag
  /// would rebuild the outliner sixty times a second.
  void replaceAll(List<SceneObject> objects) {
    _objects
      ..clear()
      ..addAll(objects);
    _byId
      ..clear()
      ..addEntries([for (final object in objects) MapEntry(object.id, object)]);
    invalidate();
  }

  /// Puts [object] where the object with its id is, in the same place.
  ///
  /// For a change worked out on a document and read back as a new row, one
  /// object at a time — a clip's preview, which changes a handful of fields a
  /// frame and would rebuild every other row through [replaceAll].
  void replace(SceneObject object) {
    final was = _byId[object.id];
    if (was == null) {
      throw SceneError('There is no object with id "${object.id}".');
    }
    _objects[_objects.indexOf(was)] = object;
    _byId[object.id] = object;
    invalidate();
  }

  void restore(List<({SceneObject object, int index})> entries) {
    for (final entry in entries) {
      _byId[entry.object.id] = entry.object;
      _objects.insert(math.min(entry.index, _objects.length), entry.object);
    }
    invalidate();
  }

  void reparent(String id, String? parentId) {
    final object = _byId[id];
    if (object == null) return;
    if (parentId != null) {
      if (parentId == id || isAncestorOf(id, parentId)) {
        throw SceneError(
          'Cannot put "${object.name}" inside itself or its own children.',
        );
      }
      if (!_byId.containsKey(parentId)) return;
    }
    object.parentId = parentId;
    invalidate();
  }

  /// The nearest drawable object a ray runs into, or null for empty space.
  ///
  /// Against the unit cube the renderer draws, in each object's own space, so
  /// an object that has been rotated and squashed is hit where it looks rather
  /// than inside the upright box that would contain it. A real mesh is a finer
  /// question than this can answer — that wants the geometry itself, which
  /// lives on the other side of the channel.
  String? objectAlong(
    Vector3 origin,
    Vector3 direction, {
    ({Vector3 min, Vector3 max})? Function(SceneObject)? boundsOf,
  }) {
    String? nearest;
    var closest = double.infinity;

    for (final object in _objects) {
      if (!object.isDrawable || !isShown(object.id)) continue;

      final world = worldOf(object.id);
      final inverse = Matrix4.tryInvert(world);
      // A zero scale on any axis leaves nothing to hit.
      if (inverse == null) continue;

      final from = inverse.transformed3(origin.clone());
      // As the difference of two transformed points, so the translation
      // cancels and what is left is the direction in the object's space —
      // still measured in world units, which is what makes the distances
      // comparable between objects.
      final along = inverse.transformed3(origin + direction) - from;

      // Nothing to hit, by choice: decoration somebody should walk straight
      // through is decoration they should not be able to click either.
      if (object.boundary.isNothing) continue;

      final box = object.boundary.boxFrom(
        object.localBounds(reported: boundsOf?.call(object)),
      );
      final hit = _boxHit(from, along, box.min, box.max);
      if (hit == null || hit >= closest) continue;

      // The box got the ray into the neighbourhood; the boundary decides.
      // Clicking the gap in an L-shaped room should select what is behind it,
      // not the room — which is the whole difference between a box round a
      // thing and the thing.
      final shell = object.boundaryMesh;
      final where = shell == null || shell.isEmpty
          ? hit
          : _meshHit(shell, from, along);
      if (where == null || where >= closest) continue;

      closest = where;
      nearest = object.id;
    }
    return nearest;
  }

  /// How far along a ray a box is first met, or null for a miss.
  static double? _meshHit(Mesh mesh, Vector3 origin, Vector3 direction) {
    var nearest = double.infinity;

    for (final face in mesh.faces) {
      final points = mesh.pointsOf(face);
      if (points.length < 3) continue;
      // The same fan the renderer draws it with, so what is clicked is what
      // is on screen. Ear clipping would be exact for a concave face, and the
      // difference is a click in the dent of one — worth having, and not
      // worth walking every face twice for.
      for (var i = 1; i + 1 < points.length; i++) {
        final hit = _triangleHit(
          origin,
          direction,
          points.first,
          points[i],
          points[i + 1],
        );
        if (hit != null && hit < nearest) nearest = hit;
      }
    }
    return nearest.isFinite ? nearest : null;
  }

  /// Möller–Trumbore. Both sides count: clicking the far wall of a room from
  /// inside it should select the room.
  static double? _triangleHit(
    Vector3 origin,
    Vector3 direction,
    Vector3 a,
    Vector3 b,
    Vector3 c,
  ) {
    final edge1 = b - a;
    final edge2 = c - a;
    final h = direction.cross(edge2);
    final det = edge1.dot(h);
    if (det.abs() < 1e-12) return null;

    final f = 1 / det;
    final s = origin - a;
    final u = f * s.dot(h);
    if (u < 0 || u > 1) return null;

    final q = s.cross(edge1);
    final v = f * direction.dot(q);
    if (v < 0 || u + v > 1) return null;

    final t = f * edge2.dot(q);
    return t > 1e-6 ? t : null;
  }

  /// How far along a ray a box is first met, or null for a miss.
  ///
  /// The slab method: the span of the ray inside each pair of parallel faces,
  /// intersected. If what is left is empty the ray goes past.
  static double? _boxHit(
    Vector3 origin,
    Vector3 direction,
    Vector3 low,
    Vector3 high,
  ) {
    var near = -double.infinity;
    var far = double.infinity;

    for (var axis = 0; axis < 3; axis++) {
      final o = origin[axis];
      final d = direction[axis];

      if (d.abs() < 1e-9) {
        // Parallel to this pair of faces: either between them for the whole
        // ray, or never.
        if (o < low[axis] || o > high[axis]) return null;
        continue;
      }

      final first = (low[axis] - o) / d;
      final second = (high[axis] - o) / d;
      near = math.max(near, math.min(first, second));
      far = math.min(far, math.max(first, second));
      if (near > far) return null;
    }

    // Behind the eye is not in front of it.
    if (far < 0) return null;
    return near >= 0 ? near : far;
  }

  /// Roughly where an object sits and how big it is, with its children.
  ///
  /// Built from the unit cube the renderer draws for everything, so it is only
  /// as accurate as the geometry is — which is exact today and becomes an
  /// approximation the moment real meshes load. Good enough to frame by, which
  /// is all it is for.
  ({Vector3 centre, double radius}) boundsOf(String id) {
    final objects = [
      if (this[id] != null) this[id]!,
      ...descendantsOf(id),
    ].where((o) => o.isDrawable).toList();

    // A group of nothing, or a light: frame its own position rather than
    // refusing, so F always does something.
    if (objects.isEmpty) {
      final lone = this[id];
      return (
        centre: lone == null
            ? Vector3.zero()
            : worldOf(lone.id).getTranslation(),
        radius: 1,
      );
    }

    var minimum = Vector3.all(double.infinity);
    var maximum = Vector3.all(double.negativeInfinity);

    for (final object in objects) {
      final world = worldOf(object.id);
      for (final x in const [-1.0, 1.0]) {
        for (final y in const [-1.0, 1.0]) {
          for (final z in const [-1.0, 1.0]) {
            final corner = world.transformed3(Vector3(x, y, z));
            minimum = Vector3(
              math.min(minimum.x, corner.x),
              math.min(minimum.y, corner.y),
              math.min(minimum.z, corner.z),
            );
            maximum = Vector3(
              math.max(maximum.x, corner.x),
              math.max(maximum.y, corner.y),
              math.max(maximum.z, corner.z),
            );
          }
        }
      }
    }

    final centre = (minimum + maximum)..scale(0.5);
    // Floored, so framing something flat — a ground plane — does not put the
    // camera inside it.
    final radius = math.max((maximum - minimum).length / 2, 0.5);
    return (centre: centre, radius: radius);
  }

  /// What the whole scene occupies, for framing with nothing selected.
  ({Vector3 centre, double radius}) boundsOfEverything() {
    final roots = this.roots;
    if (roots.isEmpty) return (centre: Vector3.zero(), radius: 4);

    var minimum = Vector3.all(double.infinity);
    var maximum = Vector3.all(double.negativeInfinity);
    for (final root in roots) {
      final bounds = boundsOf(root.id);
      final low = bounds.centre - Vector3.all(bounds.radius);
      final high = bounds.centre + Vector3.all(bounds.radius);
      minimum = Vector3(
        math.min(minimum.x, low.x),
        math.min(minimum.y, low.y),
        math.min(minimum.z, low.z),
      );
      maximum = Vector3(
        math.max(maximum.x, high.x),
        math.max(maximum.y, high.y),
        math.max(maximum.z, high.z),
      );
    }

    return (
      centre: (minimum + maximum)..scale(0.5),
      radius: math.max((maximum - minimum).length / 2, 1),
    );
  }

  /// sRGB to linear, because the shading maths is linear and a colour handed
  /// over unconverted is washed out in a way that reads as a lighting bug.
  static Vector3 linearFromColour(Color colour) =>
      Vector3(_linear(colour.r), _linear(colour.g), _linear(colour.b));

  static double _linear(double channel) => channel <= 0.04045
      ? channel / 12.92
      : math.pow((channel + 0.055) / 1.055, 2.4).toDouble();
}

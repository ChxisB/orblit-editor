import 'package:flutter/material.dart';
import 'package:orblit_light/orblit_light.dart' show LightType;
import 'package:orblit_scene/orblit_scene.dart' as doc;
import 'package:orblit_weather/orblit_weather.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import 'boundary.dart';
import 'colour.dart';
import 'scene.dart';
import 'surface.dart';

/// The extension a scene file carries.
const String sceneExtension = doc.sceneExtension;

/// A scene read back off disk, with anything that could not be read.
///
/// Problems are returned rather than thrown. A scene with one broken object is
/// still worth opening — losing the other ninety-nine because of it is the
/// worse outcome, and the editor can say what it dropped.
class SceneLoad {
  const SceneLoad({required this.scene, this.name, this.problems = const []});

  final EditorScene scene;

  /// What the scene calls itself, which need not match the file name.
  final String? name;

  final List<String> problems;

  bool get hasProblems => problems.isNotEmpty;
}

/// Something that made a file unreadable as a whole.
typedef SceneFormatException = doc.SceneFormatException;

/// Reading and writing a scene, as the editor holds one.
///
/// The format itself is not here any more. It is `orblit_scene`, which has no
/// Flutter in it and is read by the runtime, the gallery and anything else
/// that opens a scene. What is left is the translation between a document and
/// the shape the editor edits: a document's entity is a set of components, and
/// a [SceneObject] is one row of the outliner with every kind's fields on it.
///
/// Keeping the editor's shape is deliberate for now. Every panel, command and
/// gizmo in this application reads a [SceneObject], and changing the format
/// and the editing model in one go would mean nothing could be tested until
/// all of it worked. The format is the half that other programs have to agree
/// on, so it went first.
abstract final class SceneDocument {
  /// Bumped when the shape changes in a way an older editor could misread.
  ///
  /// Two was a light's power changing units. Three moved the air out of the
  /// scene and into an object. Four replaced an object's kind with the set of
  /// components it has. The list of conversions lives with the format, in
  /// `orblit_scene`, and so does the refusal to read anything newer.
  static const int formatVersion = doc.SceneDocument.formatVersion;

  static String encode(EditorScene scene, {String? name}) =>
      documentOf(scene, name: name).encode();

  /// The editor's scene as a document.
  static doc.SceneDocument documentOf(EditorScene scene, {String? name}) =>
      doc.SceneDocument(
        name: name ?? scene.name,
        settings: doc.SceneSettings(
          sky: scene.skyColour.tint,
          ambient: scene.ambient,
          // The hour is what the scene was authored at, not wherever a running
          // cycle had carried it to. A clock left going should not rewrite
          // somebody's scene every time it is saved.
          timeOfDay: scene.timeOfDay,
          dayCycle: scene.dayCycle,
          hoursPerSecond: scene.hoursPerSecond,
        ),
        entities: [for (final object in scene.objects) entityOf(object)],
      );

  static SceneLoad decode(String text) {
    final load = doc.SceneDocument.decode(text);
    final document = load.document;

    return SceneLoad(
      scene: EditorScene(
        [for (final entity in document.entities) objectOf(entity)],
        name: document.name,
        skyColour: document.settings.sky.colour,
        ambient: document.settings.ambient,
        timeOfDay: document.settings.timeOfDay,
        dayCycle: document.settings.dayCycle,
        hoursPerSecond: document.settings.hoursPerSecond,
      ),
      name: document.name,
      problems: load.problems,
    );
  }

  /// One object as JSON. Shared with the clipboard and with prefabs, so what
  /// is copied, what is saved and what a prefab holds are the same shape.
  static Map<String, Object?> objectToJson(SceneObject object) =>
      entityOf(object).toJson();

  /// One object from JSON, or null if it cannot be read.
  ///
  /// Anything older than the current format is run through the same list of
  /// migrations a file is. A clipboard payload and a prefab both carry the
  /// version they were written at, and a second, nearly-identical conversion
  /// for them is how pasting an object comes to lose a field that saving it
  /// keeps.
  static SceneObject? objectFromJson(
    Map<String, Object?> entry, {
    int version = formatVersion,
  }) {
    final migrated = doc.SceneMigrations.entity(entry, version: version);
    if (migrated == null) return null;
    final entity = doc.SceneEntity.fromJson(migrated);
    return entity == null ? null : objectOf(entity);
  }

  /// An outliner row as an entity with components.
  static doc.SceneEntity entityOf(SceneObject object) {
    final components = <String, doc.SceneComponent>{
      doc.SceneComponents.transform: doc.TransformComponent(
        position: object.position,
        rotation: object.rotation,
        scale: object.scale,
      ),
    };

    if (object.isDrawable) {
      final boundary = object.boundary.toJson();
      components[doc.SceneComponents.mesh] = doc.MeshComponent(
        asset: object.meshAsset,
        shape: object.shape,
        geometry: object.geometry,
        outline: object.outline,
        boundary: boundary.isEmpty ? null : boundary,
        surfaces: object.surfaces.isEmpty
            ? null
            : [for (final one in object.surfaces) one.toJson()],
        colour: object.colour.tint,
        castShadows: object.castShadows,
        receiveShadows: object.receiveShadows,
        sway: object.sway,
        // What used to be the difference between the two drawable kinds.
        authored: object.kind == ObjectKind.shape,
      );
    }

    if (object.kind == ObjectKind.light) {
      components[doc.SceneComponents.light] = doc.LightComponent(
        kind: object.lightType,
        power: object.power,
        colour: object.colour.tint,
        spotSize: object.spotSize,
        spotBlend: object.spotBlend,
        sourceRadius: object.sourceRadius,
        sunAngle: object.sunAngle,
        body: object.body,
        castShadows: object.castShadows,
      );
    }

    if (object.kind == ObjectKind.camera) {
      components[doc.SceneComponents.camera] = const doc.CameraComponent();
    }

    if (object.kind == ObjectKind.weather) {
      components[doc.SceneComponents.weather] = doc.WeatherComponent(
        condition: object.condition,
        // Only written when somebody has chosen one, so a scene that follows
        // its condition keeps following it when the mapping changes rather
        // than being frozen at whatever it was.
        cloudKind: object.cloudKind,
        air: object.weather,
        windDirection: object.windDirection,
        transitionSeconds: object.transitionSeconds,
      );
    }

    if (object.materialAsset != null) {
      components[doc.SceneComponents.material] = doc.MaterialComponent(
        asset: object.materialAsset,
      );
    }
    if (object.kind == ObjectKind.canvas || object.interfaceAsset != null) {
      components[doc.SceneComponents.canvas] = doc.CanvasComponent(
        asset: object.interfaceAsset,
      );
    }
    if (object.prefab != null) {
      components[doc.SceneComponents.prefab] = doc.PrefabComponent(
        asset: object.prefab,
      );
    }
    if (object.data.isNotEmpty) {
      components[doc.SceneComponents.data] = doc.DataComponent(
        paths: object.data,
      );
    }

    // What the row carries without understanding it. Its own fields win: the
    // row is what the editor has been changing, and what it carries is only
    // what was there when it was read.
    for (final MapEntry(:key, :value) in object.components.entries) {
      components.putIfAbsent(key, () => value);
    }

    return doc.SceneEntity(
      id: object.id,
      name: object.name,
      parent: object.parentId,
      visible: object.visible,
      components: components,
    );
  }

  /// Brings [scene] in line with [document], in place.
  ///
  /// In place because a scene is held by everything that is looking at it —
  /// the outliner, the viewport, every open panel — and handing back a new one
  /// would mean finding all of them. What the renderer knows each object by is
  /// carried across wherever an id survives, so an undo that moves one crate
  /// does not make it rebuild the other four thousand.
  static void reconcile(EditorScene scene, doc.SceneDocument document) {
    final keys = {
      for (final object in scene.objects) object.id: object.renderKey,
    };

    scene.replaceAll([
      for (final entity in document.entities)
        objectOf(entity, renderKey: keys[entity.id]),
    ]);
    scene
      ..name = document.name
      ..skyColour = document.settings.sky.colour
      ..ambient = document.settings.ambient
      ..timeOfDay = document.settings.timeOfDay
      ..dayCycle = document.settings.dayCycle
      ..hoursPerSecond = document.settings.hoursPerSecond;
  }

  /// An entity as the row the editor edits.
  static SceneObject objectOf(doc.SceneEntity entity, {int? renderKey}) {
    final transform = entity[doc.SceneComponents.transform];
    final mesh = entity[doc.SceneComponents.mesh];
    final light = entity[doc.SceneComponents.light];
    final weather = entity[doc.SceneComponents.weather];
    final material = entity[doc.SceneComponents.material];
    final canvas = entity[doc.SceneComponents.canvas];
    final prefab = entity[doc.SceneComponents.prefab];
    final data = entity[doc.SceneComponents.data];

    final object = SceneObject(
      id: entity.id,
      name: entity.name,
      kind: _kindOf(entity),
      renderKey: renderKey,
      parentId: entity.parent,
      position: transform is doc.TransformComponent
          ? transform.position.clone()
          : Vector3.zero(),
      rotation: transform is doc.TransformComponent
          ? transform.rotation.clone()
          : Vector3.zero(),
      scale: transform is doc.TransformComponent
          ? transform.scale.clone()
          : Vector3.all(1),
      // A light's colour and a mesh's are one field on the row and two fields
      // in the document, which is the honest shape: a lamp that is both has
      // two colours, and they are not the same colour.
      colour: switch ((mesh, light)) {
        (final doc.MeshComponent one, _) => one.colour.colour,
        (_, final doc.LightComponent one) => one.colour.colour,
        _ => const Color(0xFFD9634F),
      },
      power: light is doc.LightComponent ? light.power : 1000,
      lightType: light is doc.LightComponent ? light.kind : LightType.sun,
      spotSize: light is doc.LightComponent ? light.spotSize : 45,
      spotBlend: light is doc.LightComponent ? light.spotBlend : 0.15,
      sourceRadius: light is doc.LightComponent ? light.sourceRadius : 0.1,
      sunAngle: light is doc.LightComponent ? light.sunAngle : 0.526,
      body: light is doc.LightComponent ? light.body : CelestialBody.sun,
      condition: weather is doc.WeatherComponent
          ? weather.condition
          : WeatherCondition.clear,
      cloudKind: weather is doc.WeatherComponent ? weather.cloudKind : null,
      weather: weather is doc.WeatherComponent
          ? weather.air
          : WeatherState.of(WeatherCondition.clear),
      windDirection: weather is doc.WeatherComponent
          ? weather.windDirection
          : 135,
      transitionSeconds: weather is doc.WeatherComponent
          ? weather.transitionSeconds
          : 8,
      sway: mesh is doc.MeshComponent ? mesh.sway : 0,
      // A light states its own, and everything else is a mesh's.
      castShadows: switch ((mesh, light)) {
        (_, final doc.LightComponent one) => one.castShadows,
        (final doc.MeshComponent one, _) => one.castShadows,
        _ => true,
      },
      receiveShadows: mesh is doc.MeshComponent ? mesh.receiveShadows : true,
      visible: entity.visible,
      meshAsset: mesh is doc.MeshComponent ? mesh.asset : null,
      materialAsset: material is doc.MaterialComponent ? material.asset : null,
      interfaceAsset: canvas is doc.CanvasComponent ? canvas.asset : null,
      shape: mesh is doc.MeshComponent ? mesh.shape : null,
      geometry: mesh is doc.MeshComponent ? mesh.geometry : null,
      outline: mesh is doc.MeshComponent ? mesh.outline : null,
      boundary: mesh is doc.MeshComponent
          ? Boundary.fromJson(mesh.boundary)
          : Boundary.fromJson(null),
      surfaces: mesh is doc.MeshComponent && mesh.surfaces != null
          ? [for (final one in mesh.surfaces!) ?Surface.fromJson(one)]
          : null,
      prefab: prefab is doc.PrefabComponent ? prefab.asset : null,
      data: data is doc.DataComponent ? data.paths : null,
    );

    // Whatever the row has no way to say goes with it. Worked out from what
    // the row writes back rather than from a list of the kinds it models: a
    // lamp is a light and a mesh, the row is only a light, and a list of
    // modelled kinds would drop its mesh just as surely as it once dropped a
    // body.
    final said = entityOf(object).components;
    for (final MapEntry(:key, :value) in entity.components.entries) {
      if (!said.containsKey(key)) object.components[key] = value;
    }
    return object;
  }

  /// What an entity is, in the one word the outliner needs.
  ///
  /// The document does not have a kind and does not want one — an entity is
  /// what it has, and a lamp is a mesh and a light at once. The editor still
  /// shows one row per thing with one icon on it, so the row has to pick, and
  /// it picks by what changes most about how the row behaves. Weather first,
  /// because a weather object is not drawn and not lit; then light; then
  /// camera; then geometry; then an interface; and a thing with none of those
  /// is a place in the tree.
  static ObjectKind _kindOf(doc.SceneEntity entity) {
    if (entity.has(doc.SceneComponents.weather)) return ObjectKind.weather;
    if (entity.has(doc.SceneComponents.light)) return ObjectKind.light;
    if (entity.has(doc.SceneComponents.camera)) return ObjectKind.camera;
    final mesh = entity[doc.SceneComponents.mesh];
    if (mesh is doc.MeshComponent) {
      return mesh.authored ? ObjectKind.shape : ObjectKind.mesh;
    }
    if (entity.has(doc.SceneComponents.canvas)) return ObjectKind.canvas;
    return ObjectKind.group;
  }
}

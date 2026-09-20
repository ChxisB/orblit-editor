import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_light/orblit_light.dart';
import 'package:orblit_weather/orblit_weather.dart';
import 'package:orblit_editor/src/editor/scene.dart';
import 'package:orblit_editor/src/editor/viewport.dart';

void main() {
  group('the edited scene', () {
    test('sends only meshes to the renderer', () {
      final scene = EditorScene.starter();
      final rendered = scene.toRenderScene(OrbitCamera().toRenderCamera());

      expect(
        rendered.objects.length,
        scene.objects.where((o) => o.kind == ObjectKind.mesh).length,
      );
      // The sun, the camera and the scene root have no geometry; sending them
      // would draw a cube where the light is.
      expect(rendered.objects.length, lessThan(scene.objects.length));
    });

    test('converts colour out of sRGB, because shading is linear', () {
      // Mid grey: 0.5 in sRGB is about 0.21 in linear, and a renderer handed
      // 0.5 shows a scene that reads as washed out rather than wrong.
      final linear = EditorScene.linearFromColour(const Color(0xFF808080));
      expect(linear.x, closeTo(0.2158, 0.001));
    });

    test('points a light where its rotation points', () {
      final scene = EditorScene([
        SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light),
      ]);
      // Unrotated, forward is -Z: light falling straight down the view axis.
      final direction = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .lights
          .single
          .direction;
      expect(direction.z, closeTo(-1, 1e-9));
    });

    test('states a sun in lux and a bulb in lumens', () {
      final scene = EditorScene([
        SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light, power: 100),
        SceneObject(
          id: 'bulb',
          name: 'Bulb',
          kind: ObjectKind.light,
          lightType: LightType.point,
          power: 100,
        ),
      ]);

      final lights = scene.toRenderScene(OrbitCamera().toRenderCamera()).lights;
      final sun = lights.firstWhere(
        (l) => l.kind == OrblitLightKind.directional,
      );
      final bulb = lights.firstWhere((l) => l.kind == OrblitLightKind.point);

      // The same number of watts means two different things, and the units
      // are the whole reason the conversion lives in one place.
      expect(sun.intensity, closeTo(100 * 683, 1));
      expect(bulb.intensity, closeTo(100 * 683, 1));
      // A sun does not fall off, so it is sent no radius to fall off within.
      expect(sun.falloffRadius, 0);
      expect(bulb.falloffRadius, greaterThan(0));
    });

    test('a surface that sways takes the wind from the weather', () {
      final scene = EditorScene([
        SceneObject(
          id: 'air',
          name: 'Weather',
          kind: ObjectKind.weather,
          condition: WeatherCondition.storm,
          windDirection: 90,
        ),
        SceneObject(
          id: 'hedge',
          name: 'Hedge',
          kind: ObjectKind.mesh,
          materialAsset: 'leaves.png',
          sway: 1,
        ),
      ]);

      final material = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .materials
          .single;

      // The bearing comes from the weather object and the speed from the
      // condition, so the trees lean the way the rain already falls.
      expect(material.wind.moves, isTrue);
      expect(material.wind.bearing, 90);
      expect(material.wind.strength, 1);
      expect(material.wind.speed, greaterThan(0));
    });

    test('a scene with no weather leaves everything rigid', () {
      final scene = EditorScene([
        SceneObject(
          id: 'hedge',
          name: 'Hedge',
          kind: ObjectKind.mesh,
          materialAsset: 'leaves.png',
          sway: 1,
        ),
      ]);

      final material = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .materials
          .single;

      // Nothing is doing any weather, so there is no wind to answer — and a
      // surface that claims to sway in still air would sway for ever.
      expect(material.wind, OrblitWind.none);
    });

    test('one texture on two swaying differently is two materials', () {
      // The trap this guards: materials used to be keyed by texture alone, so
      // the trunk and the canopy cut from the same bark would have shared one
      // instance — and whichever was built second would have decided how both
      // of them moved.
      final scene = EditorScene([
        SceneObject(
          id: 'air',
          name: 'Weather',
          kind: ObjectKind.weather,
          condition: WeatherCondition.storm,
        ),
        SceneObject(
          id: 'trunk',
          name: 'Trunk',
          kind: ObjectKind.mesh,
          materialAsset: 'bark.png',
          sway: 0.1,
        ),
        SceneObject(
          id: 'canopy',
          name: 'Canopy',
          kind: ObjectKind.mesh,
          materialAsset: 'bark.png',
          sway: 1,
        ),
      ]);

      final rendered = scene.toRenderScene(OrbitCamera().toRenderCamera());
      expect(rendered.materials.length, 2);

      final strengths = rendered.materials.map((m) => m.wind.strength).toList()
        ..sort();
      expect(strengths, [0.1, 1]);

      // And each object names the one that describes it.
      final trunk = rendered.objects.firstWhere(
        (o) => o.key == scene['trunk']!.renderKey,
      );
      final canopy = rendered.objects.firstWhere(
        (o) => o.key == scene['canopy']!.renderKey,
      );
      expect(trunk.material, isNot(canopy.material));
    });

    test('two rigid objects on one texture still share a material', () {
      // The other half of the same rule: keying by more than the texture must
      // not stop the ordinary case from sharing, or a scene of a hundred
      // identical crates becomes a hundred material instances.
      final scene = EditorScene([
        SceneObject(
          id: 'a',
          name: 'A',
          kind: ObjectKind.mesh,
          materialAsset: 'crate.png',
        ),
        SceneObject(
          id: 'b',
          name: 'B',
          kind: ObjectKind.mesh,
          materialAsset: 'crate.png',
        ),
      ]);

      final rendered = scene.toRenderScene(OrbitCamera().toRenderCamera());
      expect(rendered.materials.length, 1);
    });

    test('an area light arrives as a rectangle, not as a point', () {
      final scene = EditorScene([
        SceneObject(
          id: 'panel',
          name: 'Panel',
          kind: ObjectKind.light,
          lightType: LightType.area,
          power: 100,
          sourceRadius: 0.5,
        ),
      ]);

      final light = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .lights
          .single;

      // It used to arrive as a point with a wide source, because the renderer
      // had nowhere to put a rectangle. It has one now, and the difference is
      // not cosmetic: a point of the same power is integrated against a
      // direction, a rectangle against its own area, so the shape of the
      // highlight and the gradient of the shadow edge both come out of the
      // panel's proportions rather than out of one radius.
      expect(light.kind, OrblitLightKind.area);
      expect(light.intensity, closeTo(100 * 683, 1));
    });

    test('a light that has no size is still given one', () {
      // `orblit_light` leaves width and height at zero for every kind that has
      // no size, and zero would reach the renderer as a panel with no area to
      // integrate — a light that emits nothing. Anything that is not an area
      // light carries the renderer's own default instead.
      final scene = EditorScene([
        SceneObject(
          id: 'bulb',
          name: 'Bulb',
          kind: ObjectKind.light,
          lightType: LightType.point,
          power: 100,
        ),
      ]);

      final light = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .lights
          .single;
      expect(light.width, greaterThan(0));
      expect(light.height, greaterThan(0));
    });

    test('hiding a group hides what is inside it', () {
      final scene = EditorScene.starter();
      scene['props']!.visible = false;

      final rendered = scene.toRenderScene(OrbitCamera().toRenderCamera());
      final cube = rendered.objects.firstWhere(
        (o) => o.key == scene['cube']!.renderKey,
      );

      // The cube's own flag was never touched. It is hidden because the thing
      // it sits in is.
      expect(scene['cube']!.visible, isTrue);
      expect(cube.visible, isFalse);
    });

    test('a hidden light is left out rather than sent dark', () {
      final scene = EditorScene([
        SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light)
          ..visible = false,
      ]);

      expect(
        scene.toRenderScene(OrbitCamera().toRenderCamera()).lights,
        isEmpty,
      );
    });

    test('an object keeps the key the renderer knows it by', () {
      final scene = EditorScene.starter();
      final before = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .objects
          .map((o) => o.key)
          .toList();

      scene['cube']!.position.setValues(3, 0, 0);
      scene.invalidate();

      final after = scene
          .toRenderScene(OrbitCamera().toRenderCamera())
          .objects
          .map((o) => o.key)
          .toList();

      // The whole point: an edit is the same objects in new places, so the
      // renderer moves them rather than building the scene again.
      expect(after, before);
    });

    test('a copy is a new object, not the same one somewhere else', () {
      final original = SceneObject(id: 'a', name: 'A', kind: ObjectKind.mesh);
      expect(original.copyAs(id: 'b').renderKey, isNot(original.renderKey));
    });

    test('an edit to an object reaches the next rendered scene', () {
      final scene = EditorScene.starter();
      final cube = scene['cube']!;
      cube.position.setValues(3, 0, 0);
      scene.invalidate();

      final rendered = scene.toRenderScene(OrbitCamera().toRenderCamera());
      final translations = rendered.objects.map(
        (o) => o.transform.getTranslation().x,
      );
      expect(translations, contains(3.0));
    });
  });

  group('what every scene has', () {
    final camera = OrbitCamera().toRenderCamera();

    EditorScene sharedWith(List<SceneObject> objects) => EditorScene(objects);

    test('shared objects are drawn alongside the open scene', () {
      final open = EditorScene([
        SceneObject(id: 'cube', name: 'Cube', kind: ObjectKind.mesh),
      ]);
      final shared = sharedWith([
        SceneObject(id: 'prop', name: 'Prop', kind: ObjectKind.mesh),
      ]);

      final drawn = open.toRenderScene(camera, shared: shared).objects;
      expect(drawn.length, 2);
      // By key, because that is the only thing the renderer knows either of
      // them by — and two scenes' objects must never collide in it.
      expect(drawn.map((o) => o.key).toSet(), {
        open['cube']!.renderKey,
        shared['prop']!.renderKey,
      });
    });

    test('a shared light lights the open scene', () {
      final open = EditorScene([
        SceneObject(id: 'cube', name: 'Cube', kind: ObjectKind.mesh),
      ]);
      final shared = sharedWith([
        SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light),
      ]);

      expect(open.toRenderScene(camera, shared: shared).lights, hasLength(1));
      // And is what the sky draws its disk for.
      expect(open.toRenderScene(camera, shared: shared).sky.showBody, isTrue);
    });

    test('shared weather is what the scene is in, until it has its own', () {
      final open = EditorScene([]);
      final shared = sharedWith([
        SceneObject(
          id: 'weather',
          name: 'Weather',
          kind: ObjectKind.weather,
          weather: WeatherState.of(WeatherCondition.storm),
        ),
      ]);

      expect(
        open.toRenderScene(camera, shared: shared).precipitation.isVisible,
        isTrue,
      );

      // A scene that has its own overrules it, so a level can be dry inside a
      // project that rains.
      final dry = EditorScene([
        SceneObject(
          id: 'weather',
          name: 'Weather',
          kind: ObjectKind.weather,
          weather: WeatherState.of(WeatherCondition.clear),
        ),
      ]);
      expect(
        dry.toRenderScene(camera, shared: shared).precipitation.isVisible,
        isFalse,
      );
    });

    test('a scene with its own sun keeps it', () {
      final open = EditorScene([
        SceneObject(
          id: 'stage',
          name: 'Stage',
          kind: ObjectKind.light,
          power: 400,
        ),
      ]);
      final shared = sharedWith([
        SceneObject(id: 'sun', name: 'Sun', kind: ObjectKind.light, power: 50),
      ]);

      final lights = open.toRenderScene(camera, shared: shared).lights;
      // Both are sent — two lights are two lights — but the scene's own is
      // the one the sky and the day cycle are about.
      expect(lights, hasLength(2));
      expect(lights.first.intensity, greaterThan(lights.last.intensity));
    });

    test('hiding something shared hides it everywhere', () {
      final open = EditorScene([]);
      final shared = sharedWith([
        SceneObject(id: 'prop', name: 'Prop', kind: ObjectKind.mesh)
          ..visible = false,
      ]);

      final drawn = open.toRenderScene(camera, shared: shared).objects;
      expect(drawn.single.visible, isFalse);
    });
  });
}

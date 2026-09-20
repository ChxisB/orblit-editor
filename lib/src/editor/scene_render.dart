part of 'scene.dart';

// Turning the edited scene into the one the renderer draws. It is a
// translation and nothing else: nothing here changes the scene, and the
// result is rebuilt every frame rather than kept.

/// Component-wise interpolation, which vector_math does not offer for
/// colours and which reads worse written out three times.
Vector3 _mix(Vector3 from, Vector3 to, double t) => Vector3(
  from.x + (to.x - from.x) * t,
  from.y + (to.y - from.y) * t,
  from.z + (to.z - from.z) * t,
);

/// A colour dragged towards the flat grey of a covered sky.
Tint _greyed(Tint colour, double amount) =>
    Tint.lerp(colour, const Tint.hex(0x9BA3AB), amount);

/// A stored mesh reference as a path the renderer can open.
/// The key a texture's material is kept under.
///
/// Derived from the path rather than handed out in order, because the
/// renderer keeps a material for as long as its key is mentioned: a key
/// that shifted when another object lost its texture would rebuild every
/// instance after it. Non-negative, because the grid's material sits below
/// zero and must not collide.
int _materialKeyOf(String texture, double sway) =>
    Object.hash(texture, sway) & 0x3fffffff;

/// The wind a surface of this compliance feels, from whatever the air is
/// doing. Still air and a rigid surface both come out as [OrblitWind.none],
/// which is the early return in the vertex stage.
OrblitWind _windFor(double sway, WeatherState? air, double bearing) {
  if (sway <= 0 || air == null || air.windSpeed <= 0) return OrblitWind.none;
  return OrblitWind(bearing: bearing, speed: air.windSpeed, strength: sway);
}

String? _resolveMesh(String? reference, String? root) {
  if (reference == null) return null;
  if (root == null || p.isAbsolute(reference)) return reference;
  return p.join(root, reference);
}

extension SceneRendering on EditorScene {
  /// Everything the renderer draws, viewed from [camera].
  ///
  /// The viewport's camera is passed in rather than taken from the scene's
  /// Camera object: the scene view and the game camera are separate things,
  /// and moving one should not move the other.
  ///
  /// [projectRoot] resolves mesh references, which are stored relative to the
  /// project so a scene file survives the folder being moved or shared, and
  /// have to be absolute by the time the renderer opens them.
  /// Everything the renderer draws, viewed from [camera].
  ///
  /// [shared] is what every scene in the project has in it: its objects and
  /// its lights are drawn alongside this scene's own, and its weather and its
  /// sun stand in where this scene has none. The loaded scene wins wherever
  /// both have something to say, which is the rule that makes a shared set
  /// useful rather than something to work around — put a manager there once
  /// and every scene has it, and any scene can still overrule it.
  OrblitScene toRenderScene(
    OrblitCamera camera, {
    String? projectRoot,
    EditorScene? shared,
    String? Function(SceneObject)? geometryOf,
    GridPlan? grid,
  }) {
    final sky = skyState;
    final driven = dayCycle;

    // What is above the scene, and what the air is doing, from whichever of
    // the two has one.
    final lit = celestial ?? shared?.celestial;
    final air = weatherNow ?? shared?.weatherNow;
    // Which way it blows, from whichever object is the weather — the same
    // source the fog and the rain already read, so a scene's trees lean the
    // way its rain falls.
    final bearing = (weather ?? shared?.weather)?.windDirection ?? 135;
    final flash = air == null || air.lightning <= 0
        ? 0.0
        : WeatherState.flashAt(clock, air.lightning);

    final lights = [
      for (final scene in [this, ?shared])
        for (final object in scene._objects)
          // A hidden light is left out rather than sent dark. Filament shades
          // one directional light and a budget of punctual ones, and a light
          // nobody can see should not be the one that fills the budget.
          if (object.kind == ObjectKind.light && scene.isShown(object.id))
            scene._lightFor(
              object,
              sky: driven && identical(object, lit) ? sky : null,
              air: identical(object, lit) ? air : null,
              flash: identical(object, lit) ? flash : 0,
            ),
    ];

    // A covered sky is one enormous diffuser: less of the light arrives from
    // one direction and more of it from everywhere. A strike lights the whole
    // of it at once, which is why lightning has no shadows worth the name.
    final ambientLux =
        (driven ? sky.ambient : ambient) *
        (air?.scattered ?? 1) *
        (1 + flash * 40);

    return OrblitScene(
      materials: [
        if (grid != null) grid.material,
        // One material per distinct texture rather than one per object, keyed
        // by the texture so two objects sharing a colour map share the
        // instance — and so the key is stable from frame to frame, which is
        // what lets the renderer keep the instance rather than rebuild it.
        // Keyed by the texture *and* how much the surface sways, because
        // two objects sharing a colour map do not necessarily share a
        // response to the wind: the same bark is on the trunk that barely
        // moves and the branch that does. Sharing by texture alone would
        // make one of them wrong.
        for (final surface in {
          for (final scene in [this, ?shared])
            for (final object in scene._objects)
              if (object.isDrawable && object.materialAsset != null)
                (
                  texture: _resolveMesh(object.materialAsset, projectRoot)!,
                  sway: object.sway,
                ),
        })
          OrblitMaterial(
            key: _materialKeyOf(surface.texture, surface.sway),
            baseColourMap: OrblitTexture(surface.texture),
            wind: _windFor(surface.sway, air, bearing),
          ),
      ],
      objects: [
        // First, so it is under everything in the list as well as in the
        // world. Not a scene object: it is never saved, never selected and
        // never in the outliner, because it is a drawing aid rather than a
        // thing somebody put there.
        if (grid != null) grid.object,
        for (final scene in [this, ?shared])
          for (final object in scene._objects)
            if (object.isDrawable)
              OrblitObject(
                key: object.renderKey,
                transform: scene.worldOf(object.id),
                colour: EditorScene.linearFromColour(object.colour),
                // Geometry built here first, then whatever file the object
                // names. A shape somebody is dragging a face on should draw
                // as what it is now, not as the mesh it used to reference.
                mesh: _resolveMesh(
                  (object.kind == ObjectKind.shape
                          ? geometryOf?.call(object)
                          : null) ??
                      object.meshAsset,
                  projectRoot,
                ),
                material: object.materialAsset == null
                    ? null
                    : _materialKeyOf(
                        _resolveMesh(object.materialAsset, projectRoot)!,
                        object.sway,
                      ),
                castShadows: object.castShadows,
                receiveShadows: object.receiveShadows,
                visible: scene.isShown(object.id),
              ),
      ],
      lights: lights,
      sky: _skyFrom(
        base: _greyed(
          driven ? sky.skyColour : skyColour.tint,
          (air?.greying ?? 0) * 0.8,
        ),
        ambientLux: ambientLux,
        lights: lights,
        lit: lit,
        body: driven ? sky : null,
        air: air,
        weather: weather ?? shared?.weather,
        flash: flash,
      ),
      fog: _fogFrom(air, weather ?? shared?.weather),
      precipitation: _precipitationFrom(air, weather ?? shared?.weather),
      camera: driven ? _metered(camera, lights, ambientLux) : camera,
    );
  }

  /// The camera, set for the light this scene actually has in it.
  OrblitCamera _metered(
    OrblitCamera camera,
    List<OrblitLight> lights,
    double ambientLux,
  ) {
    final exposure = CameraExposure.forIlluminance(
      _incidentLux(lights, ambientLux),
    );
    return camera.copyWith(
      aperture: exposure.aperture,
      shutterSpeed: exposure.shutterSpeed,
      sensitivity: exposure.sensitivity,
    );
  }

  /// How much light is actually falling on this scene, in lux.
  ///
  /// Read off the lights being sent rather than off what the day cycle
  /// intends, because those are not always the same thing. A scene whose light
  /// is a bulb rather than a sun, or one somebody has turned up, still has to
  /// be exposed for what it has — metering off the hour instead is how a night
  /// ends up a white rectangle with the shapes barely showing through it.
  ///
  /// Directional light only. A lamp lights the corner it is in rather than the
  /// scene, and a camera set for the corner would blow out everywhere else —
  /// which is exactly what a real one does, too.
  double _incidentLux(List<OrblitLight> lights, double ambientLux) {
    var total = ambientLux;
    for (final light in lights) {
      if (light.kind != OrblitLightKind.directional) continue;
      // Angled by how high it is: a sun on the horizon lays far less on the
      // ground than one overhead, and metering as though it did would leave
      // every dusk under-exposed.
      total += light.intensity * math.max(0, -light.direction.y);
    }
    return total;
  }

  /// The air, as the weather has it this instant.
  ///
  /// Two things through one setting. The even haze is what distance looks
  /// like; the sheets are what a bank of cloud looks like lying in a valley.
  /// A condition asks for both, because weather with no haze behind it reads
  /// as cut-outs hanging in clear air.
  OrblitFog _fogFrom(WeatherState? now, SceneObject? object) {
    if (now == null || object == null) return OrblitFog.none;

    final heading = WeatherState.windFrom(object.windDirection);

    return OrblitFog(
      colour: now.fogColour.linear,
      density: now.fogDensity,
      height: now.fogHeight,
      heightFalloff: now.fogFalloff,
      structure: now.mist,
      // Metres a second, which is what wind is measured in. Turning that into
      // how fast a pattern scrolls is the renderer's business, because only it
      // knows how big the pattern is.
      wind: Vector2(heading.x * now.windSpeed, heading.z * now.windSpeed),
      // Turns of the noise per metre: the reciprocal of how big a cloud is,
      // stated the way somebody would measure it rather than the way the
      // shader wants it.
      featureSize: 1 / math.max(now.mistSize, 0.5),
      // How deep the bank is, out of how fast the haze thins with altitude.
      // The two describe the same layer, and authoring them apart would let
      // somebody set a shallow haze with a bank standing out of the top of it.
      thickness: (1 / math.max(now.fogFalloff, 0.05)).clamp(1.0, 40.0),
    );
  }

  /// What is coming down, if anything is.
  ///
  /// Rain and snow are the same curtain at different settings, so a scene
  /// with some of each — which is what the temperature between them looks
  /// like — is one curtain part of the way from streaks to flakes rather than
  /// two curtains fighting.
  OrblitPrecipitation _precipitationFrom(
    WeatherState? now,
    SceneObject? object,
  ) {
    if (now == null || object == null || !now.isWet) {
      return OrblitPrecipitation.none;
    }

    final total = now.rain + now.snow;
    final asSnow = (now.snow / total).clamp(0.0, 1.0);
    double between(double wet, double white) => wet + (white - wet) * asSnow;

    final heading = WeatherState.windFrom(object.windDirection);

    return OrblitPrecipitation(
      colour: EditorScene.linearFromColour(
        Color.lerp(const Color(0xFFB8C6D6), const Color(0xFFF2F5F8), asSnow)!,
      ),
      amount: total.clamp(0.0, 1.0),
      // Nine metres a second for rain, under one for snow. It is the whole
      // difference in how the two read.
      fall: between(9, 0.8),
      // Snow is taken by the wind far more than rain is: it weighs nothing
      // and it has all day.
      wind: Vector2(
        heading.x * now.windSpeed * between(0.6, 1.6),
        heading.z * now.windSpeed * between(0.6, 1.6),
      ),
      dropsPerMetre: between(8, 3.5),
      // How far a drop travels while the shutter is open. A streak, or a
      // flake.
      stretch: between(30, 5),
      threshold: between(0.7, 0.55),
    );
  }

  /// The sky: its gradient, the body in it, the cloud, and any strike.
  ///
  /// One object because it is one shader on one dome. Splitting it was the
  /// mistake behind two rounds of cloud that did not read as sky: the cloud
  /// was tinted a colour somebody chose, while the sun was drawn somewhere
  /// else entirely, and nothing in the picture agreed with anything else.
  /// Here the cloud is lit by the same direction the scene is.
  OrblitSky _skyFrom({
    required Tint base,
    required double ambientLux,
    required List<OrblitLight> lights,
    required SceneObject? lit,
    required SkyState? body,
    required WeatherState? air,
    required SceneObject? weather,
    required double flash,
  }) {
    final ground = base.linear;
    final strike = _strikeFrom(air, weather);

    // Which way the body is, taken from the light that is actually lighting
    // the scene rather than from the clock. A sun drawn in one place and a
    // cloud lit from another is the single thing that gives a sky away.
    final beam = lights
        .where((light) => light.kind == OrblitLightKind.directional)
        .firstOrNull;
    final toBody = beam == null ? Vector3(0.35, 0.78, 0.52) : (-beam.direction)
      ..normalize();

    // The body's own colour, at a brightness that says which body it is. The
    // moon is the sun's light at a millionth of the strength and the exposure
    // opens right up for it, so it needs saying here or the night has a
    // second sun in it.
    final night =
        toBody.y < 0.999 && body != null && body.body == CelestialBody.moon;
    final bodyColour =
        (beam == null ? Vector3(1.0, 0.96, 0.90) : beam.colour.clone())
          ..scale(night ? 0.30 : 1.0);

    // Overhead is the deepest part of a sky and the horizon the palest,
    // because the horizon is where the most air is and every metre of it
    // scatters. When the body is low the horizon takes its colour, which is
    // the whole of a sunset.
    final zenith = ground.clone()..scale(0.82);
    final glow = body == null
        ? 0.30
        : (1 - (body.altitude / 0.45)).clamp(0.0, 1.0).toDouble();
    final horizon = _mix(
      _mix(ground, Vector3(0.72, 0.80, 0.92), 0.30),
      bodyColour,
      glow * 0.55,
    );

    return OrblitSky(
      colour: ground,
      zenith: zenith,
      horizon: horizon,
      ambient: ambientLux,
      // Nothing to draw a disk for if the scene has no light above it, and
      // one nobody can see should not appear in the sky either.
      showBody: lit != null,
      bodyDirection: toBody,
      bodyColour: bodyColour,
      // A degree across rather than the sun's own half-degree. A physically
      // sized disc is four pixels on a normal screen, and a sun nobody can
      // pick out of the glare is not worth drawing.
      bodySize: 0.011,
      flash: strike.flash,
      flashDirection: strike.direction,
      flashSeed: strike.seed,
      clouds: _cloudsFrom(air, weather, bodyColour),
    );
  }

  /// The strike this instant, or none if the sky is not that kind of sky.
  Strike _strikeFrom(WeatherState? now, SceneObject? object) =>
      now == null || object == null || now.lightning <= 0
      ? Strike.none
      : WeatherState.strikeAt(clock, now.lightning);

  /// The cloud in the sky, which is not the same thing as the fog.
  ///
  /// Fog is the air between here and the horizon; cloud is a layer a long way
  /// overhead that the light comes through. A scene can have either without
  /// the other, and one setting doing both would be wrong for every scene
  /// that wants one of them.
  ///
  /// The kind is a shape, not a preset: which one is chosen decides how high
  /// the base sits, how deep the layer is and how far its noise is folded,
  /// and none of those can be reached by turning a cover slider.
  OrblitClouds _cloudsFrom(
    WeatherState? now,
    SceneObject? object,
    Vector3 bodyColour,
  ) {
    if (now == null || object == null || now.cloudCover <= 0.01) {
      return OrblitClouds.none;
    }

    // A condition that has no cloud of its own still gets one if somebody
    // has turned the cover up, because the alternative is a slider that does
    // nothing until the condition is changed too. The chosen kind wins over
    // both, including when it is None.
    final kind =
        object.cloudKind ??
        switch (CloudKind.forCondition(object.condition)) {
          CloudKind.none => CloudKind.cumulus,
          final chosen => chosen,
        };
    if (kind == CloudKind.none) return OrblitClouds.none;

    final heading = WeatherState.windFrom(object.windDirection);

    // Carried faster than anything at ground level, because there is nothing
    // up there to slow the wind down.
    final wind = Vector2(
      heading.x * now.windSpeed * 2.5,
      heading.z * now.windSpeed * 2.5,
    );

    final clouds = switch (kind) {
      CloudKind.none => OrblitClouds.none,
      CloudKind.cumulus => OrblitClouds.cumulus(
        cover: now.cloudCover,
        wind: wind,
      ),
      CloudKind.stratocumulus => OrblitClouds.stratocumulus(
        cover: now.cloudCover,
        wind: wind,
      ),
      CloudKind.stratus => OrblitClouds.stratus(
        cover: now.cloudCover,
        wind: wind,
      ),
      CloudKind.cirrus => OrblitClouds.cirrus(
        cover: now.cloudCover,
        wind: wind,
      ),
      CloudKind.cumulonimbus => OrblitClouds.cumulonimbus(
        cover: now.cloudCover,
        wind: wind,
      ),
    };

    // The kind is the shape; the height is a setting on top of it, and the
    // scene always has one.
    return clouds.copyWith(
      altitude: now.cloudHeight,
      // What the sky puts back into the shadowed side, warmed by whatever is
      // above it. A cloud lit only from one side has a black underside, and
      // no real one does.
      colour: _mix(clouds.colour, bodyColour, 0.18),
    );
  }

  /// One authored light, in the units the renderer takes.
  ///
  /// The conversion happens in `orblit_light` rather than here. Watts, metres
  /// and degrees are what a light is stated in; lumens, lux and radians are
  /// what a renderer is told. Doing that arithmetic in the editor as well
  /// would be a second place for it to drift.
  /// One light, in the units the renderer takes.
  ///
  /// Told what is happening to it rather than working it out. Which light the
  /// sky is standing in for, and what the weather is, are questions about the
  /// project rather than about the scene this light happens to live in — a sun
  /// in the shared set is still the sun of whichever scene is open.
  OrblitLight _lightFor(
    SceneObject object, {
    SkyState? sky,
    WeatherState? air,
    double flash = 0,
  }) {
    final world = worldOf(object.id);

    // A day cycle owns the one light everything is lit from above by: where it
    // is, what colour it is and how strong. The object keeps what it was
    // authored with, so turning the cycle off puts it back rather than leaving
    // it wherever the clock stopped.
    final driven = sky != null;

    // Cloud sits between the scene and whatever is above it, so it only
    // touches that one light. A lamp indoors does not care what the sky is
    // doing, and neither should a stage light somebody has aimed by hand.
    final now = air;

    final described = Light(
      type: object.lightType,
      color: _greyed(
        sky?.lightColour ?? object.colour.tint,
        now?.greying ?? 0,
      ).linear,
      // Cloud does not switch the sun off. A heavy overcast still passes a
      // good tenth of it, which is why a wet afternoon is grey rather than
      // dark: the camera opens up and the world stays legible.
      //
      // A strike goes the other way, briefly and by a lot. It comes through
      // the light that is already above the scene rather than as a second
      // one: a flash is the sky lighting up, and the sky is what that light
      // is standing in for.
      power:
          (sky?.power ?? object.power) *
          (now?.transmitted ?? 1) *
          (1 + flash * 60),
      radius: object.sourceRadius,
      spotSize: object.spotSize,
      spotBlend: object.spotBlend,
      // The whole difference between a bright day and a dull one. The sun is
      // a disc half a degree across; cloud turns it into a source the size of
      // the sky, and shadows lose their edges long before they lose their
      // depth.
      sunAngle: object.sunAngle * (now?.spread ?? 1),
      castShadows: object.castShadows,
      // An area light arrives as a point of the same luminous power, so the
      // size it would have emitted from becomes the size of the source that
      // stands in for it — the falloff and the total are right, and the
      // penumbra is at least a believable width.
      sizeX: object.sourceRadius * 2,
      sizeY: object.sourceRadius * 2,
    );
    final light = described.toRenderer();

    // Down the local -Z axis, which is where a light points: the same
    // convention as a camera, so a light parented to a rig turns with it.
    final direction =
        sky?.direction ??
        (world.getRotation().transformed(Vector3(0, 0, -1))..normalize());

    // What tells a sun from a moon at a glance, once both are white discs of
    // the same width: a sun is wrapped in glare and a moon is not.
    final body = driven ? sky.body : object.body;
    final isMoon = body == CelestialBody.moon;

    return OrblitLight(
      key: object.renderKey,
      kind: switch (light.kind) {
        RendererLightKind.directional => OrblitLightKind.directional,
        RendererLightKind.point => OrblitLightKind.point,
        RendererLightKind.spot => OrblitLightKind.spot,
        RendererLightKind.area => OrblitLightKind.area,
      },
      colour: light.color,
      intensity: light.intensity,
      position: world.getTranslation(),
      direction: direction,
      // A sun's influence is infinite, which is not a number a renderer can
      // be given. It ignores the falloff of a directional light anyway, so
      // zero here means "not asked" rather than "no reach".
      falloffRadius: light.falloffRadius.isFinite ? light.falloffRadius : 0,
      innerConeAngle: light.innerConeAngle,
      outerConeAngle: light.outerConeAngle,
      sunAngularRadius: light.sunAngularRadius,
      sourceRadius: light.sourceRadius,
      haloSize: isMoon ? 3 : 12,
      haloFalloff: isMoon ? 240 : 70,
      castShadows: light.castShadows,
      // Only an area light has a size, and `orblit_light` leaves both at zero
      // for the kinds that do not. Passing that zero through would give the
      // renderer a panel with no area to integrate, which is a light that
      // emits nothing — so the renderer's own default stands in instead.
      width: light.width > 0 ? light.width : 1,
      height: light.height > 0 ? light.height : 1,
    );
  }
}

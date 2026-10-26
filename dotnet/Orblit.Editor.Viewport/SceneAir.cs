namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;
using Orblit.Weather;

/// <summary>The sky, the fog and what falls through them, as the renderer takes them.</summary>
internal static class SceneAir
{
    /// <summary>A colour dragged towards the flat grey of a covered sky.</summary>
    public static Tint Greyed(Tint colour, double amount) => Tint.Lerp(colour, Tint.Hex(0x9BA3AB), amount);

    /// <summary>
    /// The air, as the weather has it this instant.
    ///
    /// Two things through one setting. The even haze is what distance looks like; the sheets are
    /// what a bank of cloud looks like lying in a valley. A condition asks for both, because
    /// weather with no haze behind it reads as cut-outs hanging in clear air.
    /// </summary>
    public static Fog FogFrom(WeatherState? now, SceneObject? weather)
    {
        if (now is null || weather is null)
        {
            return Fog.None;
        }

        var heading = WeatherState.WindFrom(weather.WindDirection);
        return new Fog
        {
            Colour = now.FogColour.Linear,
            Density = now.FogDensity,
            Height = now.FogHeight,
            HeightFalloff = now.FogFalloff,
            Structure = now.Mist,
            // Metres a second, which is what wind is measured in. Turning that into how fast a
            // pattern scrolls is the renderer's business, because only it knows how big the
            // pattern is.
            Wind = new Vector2(heading.X * now.WindSpeed, heading.Z * now.WindSpeed),
            // Turns of the noise per metre: the reciprocal of how big a cloud is, stated the way
            // somebody would measure it rather than the way the shader wants it.
            FeatureSize = 1 / Math.Max(now.MistSize, 0.5),
            // How deep the bank is, out of how fast the haze thins with altitude. The two
            // describe the same layer, and authoring them apart would let somebody set a shallow
            // haze with a bank standing out of the top of it.
            Thickness = Doubles.Clamp(1 / Math.Max(now.FogFalloff, 0.05), 1, 40),
        };
    }

    /// <summary>
    /// What is coming down, if anything is.
    ///
    /// Rain and snow are the same curtain at different settings, so a scene with some of each,
    /// which is what the temperature between them looks like, is one curtain part of the way from
    /// streaks to flakes rather than two curtains fighting.
    /// </summary>
    public static Precipitation PrecipitationFrom(WeatherState? now, SceneObject? weather)
    {
        if (now is null || weather is null || !now.IsWet)
        {
            return Precipitation.None;
        }

        var total = now.Rain + now.Snow;
        var asSnow = Doubles.Clamp(now.Snow / total, 0, 1);
        double Between(double wet, double white) => wet + ((white - wet) * asSnow);

        var heading = WeatherState.WindFrom(weather.WindDirection);
        var carried = now.WindSpeed * Between(0.6, 1.6);
        return new Precipitation
        {
            Colour = Tint.Lerp(Tint.Hex(0xB8C6D6), Tint.Hex(0xF2F5F8), asSnow).Linear,
            Amount = Doubles.Clamp(total, 0, 1),
            // Nine metres a second for rain, under one for snow. It is the whole difference in
            // how the two read.
            Fall = Between(9, 0.8),
            // Snow is taken by the wind far more than rain is: it weighs nothing and it has all
            // day.
            Wind = new Vector2(heading.X * carried, heading.Z * carried),
            DropsPerMetre = Between(8, 3.5),
            // How far a drop travels while the shutter is open. A streak, or a flake.
            Stretch = Between(30, 5),
            Threshold = Between(0.7, 0.55),
        };
    }

    /// <summary>
    /// The sky: its gradient, the body in it, the cloud, and any strike.
    ///
    /// One object because it is one shader on one dome. Splitting it was the mistake behind two
    /// rounds of cloud that did not read as sky: the cloud was tinted a colour somebody chose,
    /// while the sun was drawn somewhere else entirely, and nothing in the picture agreed with
    /// anything else. Here the cloud is lit by the same direction the scene is.
    /// </summary>
    public static Sky SkyFrom(Conditions conditions, IReadOnlyList<RenderLight> lights, double ambientLux, Tint baseColour)
    {
        var ground = baseColour.Linear;
        var strike = StrikeFrom(conditions);

        // Which way the body is, taken from the light that is actually lighting the scene rather
        // than from the clock. A sun drawn in one place and a cloud lit from another is the single
        // thing that gives a sky away.
        var beam = lights.FirstOrDefault(light => light.Kind == RenderLightKind.Directional);
        var toBody = beam is null ? new Vector3(0.35, 0.78, 0.52) : -beam.Direction;
        toBody.Normalize();

        var bodyColour = BodyColour(beam, conditions.Body, toBody);

        // Overhead is the deepest part of a sky and the horizon the palest, because the horizon is
        // where the most air is and every metre of it scatters. When the body is low the horizon
        // takes its colour, which is the whole of a sunset.
        var glow = conditions.Body is { } body ? Doubles.Clamp(1 - (body.Altitude / 0.45), 0, 1) : 0.30;
        return new Sky
        {
            Colour = ground,
            Zenith = ground.Scaled(0.82),
            Horizon = Mix(Mix(ground, new Vector3(0.72, 0.80, 0.92), 0.30), bodyColour, glow * 0.55),
            Ambient = ambientLux,
            // Nothing to draw a disk for if the scene has no light above it, and one nobody can
            // see should not appear in the sky either.
            ShowBody = conditions.Lit is not null,
            BodyDirection = toBody,
            BodyColour = bodyColour,
            // A degree across rather than the sun's own half-degree. A physically sized disc is
            // four pixels on a normal screen, and a sun nobody can pick out of the glare is not
            // worth drawing.
            BodySize = 0.011,
            Flash = strike.Flash,
            FlashDirection = strike.Direction,
            FlashSeed = strike.Seed,
            Clouds = CloudsFrom(conditions, bodyColour),
        };
    }

    // The body's own colour, at a brightness that says which body it is. The moon is the sun's
    // light at a millionth of the strength and the exposure opens right up for it, so it needs
    // saying here or the night has a second sun in it.
    private static Vector3 BodyColour(RenderLight? beam, SkyState? body, Vector3 toBody)
    {
        var night = toBody.Y < 0.999 && body is { Body: CelestialBody.Moon };
        var colour = beam is null ? new Vector3(1.0, 0.96, 0.90) : beam.Colour.Clone();
        colour.Scale(night ? 0.30 : 1.0);
        return colour;
    }

    // The strike this instant, or none if the sky is not that kind of sky.
    private static Strike StrikeFrom(Conditions conditions) =>
        conditions.Now is null || conditions.WeatherObject is null || conditions.Now.Lightning <= 0
            ? Strike.None
            : WeatherState.StrikeAt(conditions.Clock, conditions.Now.Lightning);

    /// <summary>
    /// The cloud in the sky, which is not the same thing as the fog.
    ///
    /// Fog is the air between here and the horizon; cloud is a layer a long way overhead that the
    /// light comes through. A scene can have either without the other, and one setting doing both
    /// would be wrong for every scene that wants one of them.
    ///
    /// The kind is a shape, not a preset: which one is chosen decides how high the base sits, how
    /// deep the layer is and how far its noise is folded, and none of those can be reached by
    /// turning a cover slider.
    /// </summary>
    private static Clouds CloudsFrom(Conditions conditions, Vector3 bodyColour)
    {
        var now = conditions.Now;
        var weather = conditions.WeatherObject;
        if (now is null || weather is null || now.CloudCover <= 0.01)
        {
            return Clouds.None;
        }

        // A condition that has no cloud of its own still gets one if somebody has turned the cover
        // up, because the alternative is a slider that does nothing until the condition is changed
        // too. The chosen kind wins over both, including when it is None.
        var kind = weather.CloudKind ?? DefaultKind(weather.Condition);
        if (kind == CloudKind.None)
        {
            return Clouds.None;
        }

        // Carried faster than anything at ground level, because there is nothing up there to slow
        // the wind down.
        var heading = WeatherState.WindFrom(weather.WindDirection);
        var wind = new Vector2(heading.X * now.WindSpeed * 2.5, heading.Z * now.WindSpeed * 2.5);
        var clouds = CloudShapes.Of(kind) with { Cover = now.CloudCover, Wind = wind };

        // The kind is the shape; the height is a setting on top of it, and the scene always has
        // one. The colour is what the sky puts back into the shadowed side, warmed by whatever is
        // above it. A cloud lit only from one side has a black underside, and no real one does.
        return clouds with { Altitude = now.CloudHeight, Colour = Mix(clouds.Colour, bodyColour, 0.18) };
    }

    private static CloudKind DefaultKind(WeatherCondition condition) =>
        WeatherConditionKind(condition) is var chosen && chosen == CloudKind.None ? CloudKind.Cumulus : chosen;

    private static CloudKind WeatherConditionKind(WeatherCondition condition) => CloudKindExtensions.ForCondition(condition);

    /// <summary>
    /// Component-wise interpolation, which the vector type does not offer for colours and which
    /// reads worse written out three times.
    /// </summary>
    private static Vector3 Mix(Vector3 from, Vector3 to, double t) => new(
        from.X + ((to.X - from.X) * t),
        from.Y + ((to.Y - from.Y) * t),
        from.Z + ((to.Z - from.Z) * t));
}

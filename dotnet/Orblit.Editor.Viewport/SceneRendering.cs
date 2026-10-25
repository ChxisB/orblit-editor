namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Orblit.Editor.Core;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;
using Orblit.Weather;

/// <summary>What <see cref="SceneRendering.ToRenderScene"/> is told about the project around the scene.</summary>
public sealed record SceneRenderOptions
{
    /// <summary>
    /// Resolves mesh references, which are stored relative to the project so a scene file
    /// survives the folder being moved or shared, and have to be absolute by the time the renderer
    /// opens them.
    /// </summary>
    public string? ProjectRoot { get; init; }

    /// <summary>
    /// What every scene in the project has in it: its objects and its lights are drawn alongside
    /// this scene's own, and its weather and its sun stand in where this scene has none. The loaded
    /// scene wins wherever both have something to say, which is the rule that makes a shared set
    /// useful rather than something to work around. Put a manager there once and every scene has
    /// it, and any scene can still overrule it.
    /// </summary>
    public EditorScene? Shared { get; init; }

    /// <summary>The geometry a shape has been given, as a file, before whatever mesh it names.</summary>
    public Func<SceneObject, string?>? GeometryOf { get; init; }

    /// <summary>The grid, drawn under everything and never part of the scene.</summary>
    public GridPlan? Grid { get; init; }

    /// <summary>
    /// Turns the objects that are shown into the ground they put in the scene. Asked rather than
    /// worked out here, because a terrain is a file of its own that whoever calls this has open,
    /// and reading it again every frame would be reading megabytes to draw what has not changed.
    /// </summary>
    public Func<IEnumerable<SceneObject>, IReadOnlyList<RenderTerrain>>? TerrainOf { get; init; }

    /// <summary>The same for what is scattered over that ground.</summary>
    public Func<IEnumerable<SceneObject>, IReadOnlyList<Population>>? ScatterOf { get; init; }

    /// <summary>
    /// The surface an object wearing a shader draws, or null while that shader has not cooked, when
    /// the object keeps its mesh's own.
    /// </summary>
    public Func<SceneObject, CustomSurface?>? SurfaceOf { get; init; }
}

/// <summary>
/// What the sky and the air are doing for one frame, worked out once and read by everything that
/// draws a part of them.
/// </summary>
/// <param name="Lit">The light everything is lit from above by, from whichever scene has one.</param>
/// <param name="Now">What the air is doing this instant.</param>
/// <param name="WeatherObject">The object that is the weather, from whichever scene has one.</param>
/// <param name="Bearing">Which way it blows, so a scene's trees lean the way its rain falls.</param>
/// <param name="Flash">How bright a strike is this instant, or nought.</param>
/// <param name="Clock">The scene's clock.</param>
/// <param name="Driven">Whether a day cycle owns the sky.</param>
/// <param name="Sky">The sky at the cycle's hour.</param>
internal sealed record Conditions(
    SceneObject? Lit,
    WeatherState? Now,
    SceneObject? WeatherObject,
    double Bearing,
    double Flash,
    double Clock,
    bool Driven,
    SkyState Sky)
{
    /// <summary>The sky when the cycle owns it, and nothing when the scene's own colours stand.</summary>
    public SkyState? Body => Driven ? Sky : null;
}

/// <summary>
/// Turning the edited scene into the one the renderer draws. It is a translation and nothing
/// else: nothing here changes the scene, and the result is rebuilt every frame rather than kept.
/// </summary>
public static class SceneRendering
{
    private const int KeyMask = 0x3fffffff;

    /// <summary>
    /// Everything the renderer draws, viewed from <paramref name="camera"/>.
    ///
    /// The viewport's camera is passed in rather than taken from the scene's Camera object: the
    /// scene view and the game camera are separate things, and moving one should not move the
    /// other.
    /// </summary>
    public static RenderScene ToRenderScene(
        this EditorScene scene,
        RenderCamera camera,
        SceneRenderOptions? options = null)
    {
        var chosen = options ?? new SceneRenderOptions();
        var scenes = ScenesOf(scene, chosen.Shared);
        var conditions = ConditionsOf(scene, chosen.Shared);
        var lights = LightsOf(scenes, conditions);

        var ambientLux = AmbientOf(scene, conditions);
        var surfaces = SurfacesOf(scenes, chosen);
        var shown = ShownIn(scenes);

        return new RenderScene
        {
            Materials = MaterialsOf(scenes, surfaces, conditions, chosen),
            Objects = ObjectsOf(scenes, surfaces, chosen),
            Lights = lights,
            Sky = SceneAir.SkyFrom(conditions, lights, ambientLux, SkyColourOf(scene, conditions)),
            Fog = SceneAir.FogFrom(conditions.Now, conditions.WeatherObject),
            Precipitation = SceneAir.PrecipitationFrom(conditions.Now, conditions.WeatherObject),
            Camera = conditions.Driven ? Metered(camera, lights, ambientLux) : camera,
            Terrain = chosen.TerrainOf?.Invoke(shown) ?? [],
            Populations = chosen.ScatterOf?.Invoke(shown) ?? [],
        };
    }

    // A covered sky is one enormous diffuser: less of the light arrives from one direction and more
    // of it from everywhere. A strike lights the whole of it at once, which is why lightning has no
    // shadows worth the name.
    private static double AmbientOf(EditorScene scene, Conditions conditions) =>
        (conditions.Driven ? conditions.Sky.Ambient : scene.Ambient)
        * (conditions.Now?.Scattered ?? 1)
        * (1 + (conditions.Flash * 40));

    private static Tint SkyColourOf(EditorScene scene, Conditions conditions) => SceneAir.Greyed(
        conditions.Driven ? conditions.Sky.SkyColour : scene.SkyColour.ToTint(),
        (conditions.Now?.Greying ?? 0) * 0.8);

    private static List<EditorScene> ScenesOf(EditorScene scene, EditorScene? shared) =>
        shared is null ? [scene] : [scene, shared];

    private static Conditions ConditionsOf(EditorScene scene, EditorScene? shared)
    {
        var weather = scene.Weather ?? shared?.Weather;
        var now = scene.WeatherNow ?? shared?.WeatherNow;
        return new Conditions(
            scene.Celestial ?? shared?.Celestial,
            now,
            weather,
            weather?.WindDirection ?? 135,
            FlashOf(now, scene.Clock),
            scene.Clock,
            scene.DayCycle,
            scene.SkyState);
    }

    private static double FlashOf(WeatherState? now, double clock) =>
        now is null || now.Lightning <= 0 ? 0.0 : WeatherState.FlashAt(clock, now.Lightning);

    // A hidden light is left out rather than sent dark. Filament shades one directional light and
    // a budget of punctual ones, and a light nobody can see should not be the one that fills the
    // budget.
    private static List<RenderLight> LightsOf(List<EditorScene> scenes, Conditions conditions) =>
    [
        .. scenes.SelectMany(scene => scene.Objects
            .Where(one => one.Kind == ObjectKind.Light && scene.IsShown(one.Id))
            .Select(one => LightFor(scene, one, conditions))),
    ];

    /// <summary>
    /// What the objects that wear a shader draw, by the key each is kept under. Objects that state
    /// nothing share a key, so the renderer keeps one instance for them.
    /// </summary>
    private static (Dictionary<SceneObject, int> Keys, OrderedDictionary<int, CustomSurface> Surfaces) SurfacesOf(
        List<EditorScene> scenes,
        SceneRenderOptions options)
    {
        var keys = new Dictionary<SceneObject, int>(ReferenceEqualityComparer.Instance);
        var surfaces = new OrderedDictionary<int, CustomSurface>();
        foreach (var one in scenes.SelectMany(scene => scene.Objects).Where(one => one.IsDrawable && one.WearsSurface))
        {
            if (options.SurfaceOf?.Invoke(one) is not { } surface)
            {
                continue;
            }

            var key = SurfaceKeyOf(one);
            keys[one] = key;
            surfaces[key] = surface;
        }

        return (keys, surfaces);
    }

    private static List<RenderMaterial> MaterialsOf(
        List<EditorScene> scenes,
        (Dictionary<SceneObject, int> Keys, OrderedDictionary<int, CustomSurface> Surfaces) surfaces,
        Conditions conditions,
        SceneRenderOptions options)
    {
        var materials = new List<RenderMaterial>();
        if (options.Grid is { } grid)
        {
            materials.Add(grid.ToMaterial());
        }

        materials.AddRange(surfaces.Surfaces.Select(one => new RenderMaterial { Key = one.Key, Surface = one.Value }));

        // One material per distinct texture rather than one per object, keyed by the texture so
        // two objects sharing a colour map share the instance, and so the key is stable from
        // frame to frame, which is what lets the renderer keep the instance rather than rebuild
        // it. Keyed by the texture and how much the surface sways, because two objects sharing a
        // colour map do not necessarily share a response to the wind: the same bark is on the
        // trunk that barely moves and the branch that does.
        var textured = scenes
            .SelectMany(scene => scene.Objects)
            .Where(one => one.IsDrawable && one.MaterialAsset is not null && !one.WearsSurface)
            .Select(one => (Texture: ResolveMesh(one.MaterialAsset, options.ProjectRoot)!, one.Sway))
            .Distinct();
        materials.AddRange(textured.Select(one => new RenderMaterial
        {
            Key = MaterialKeyOf(one.Texture, one.Sway),
            BaseColourMap = new Texture(one.Texture),
            Wind = WindFor(one.Sway, conditions.Now, conditions.Bearing),
        }));
        return materials;
    }

    private static List<RenderObject> ObjectsOf(
        List<EditorScene> scenes,
        (Dictionary<SceneObject, int> Keys, OrderedDictionary<int, CustomSurface> Surfaces) surfaces,
        SceneRenderOptions options)
    {
        // The grid is first, so it is under everything in the list as well as in the world. It is
        // not a scene object: never saved, never selected and never in the outliner, because it is
        // a drawing aid rather than a thing somebody put there.
        var objects = new List<RenderObject>();
        if (options.Grid is { } grid)
        {
            objects.Add(grid.ToObject());
        }

        foreach (var scene in scenes)
        {
            objects.AddRange(scene.Objects
                .Where(one => one.IsDrawable)
                .Select(one => ObjectFor(scene, one, surfaces.Keys, options)));
        }

        return objects;
    }

    private static RenderObject ObjectFor(
        EditorScene scene,
        SceneObject one,
        Dictionary<SceneObject, int> surfaceKeys,
        SceneRenderOptions options)
    {
        // Geometry built here first, then whatever file the object names. A shape somebody is
        // dragging a face on should draw as what it is now, not as the mesh it used to reference.
        var built = one.Kind == ObjectKind.Shape ? options.GeometryOf?.Invoke(one) : null;
        return new RenderObject
        {
            Key = one.RenderKey,
            Transform = scene.WorldOf(one.Id),
            Colour = EditorScene.LinearFromColour(one.Colour),
            Mesh = ResolveMesh(built ?? one.MeshAsset, options.ProjectRoot),
            Material = MaterialFor(one, surfaceKeys, options.ProjectRoot),
            CastShadows = one.CastShadows,
            ReceiveShadows = one.ReceiveShadows,
            Visible = scene.IsShown(one.Id),
        };
    }

    private static long? MaterialFor(SceneObject one, Dictionary<SceneObject, int> surfaceKeys, string? root)
    {
        if (surfaceKeys.TryGetValue(one, out var worn))
        {
            return worn;
        }

        return one.MaterialAsset is null || one.WearsSurface
            ? null
            : MaterialKeyOf(ResolveMesh(one.MaterialAsset, root)!, one.Sway);
    }

    // The key a texture's material is kept under. Derived from the path rather than handed out in
    // order, because the renderer keeps a material for as long as its key is mentioned: a key that
    // shifted when another object lost its texture would rebuild every instance after it.
    // Non-negative, because the grid's material sits below zero and must not collide.
    private static int MaterialKeyOf(string texture, double sway) =>
        Hash($"{texture}|{Normalised(sway)}") & KeyMask;

    // The key an author-written surface is kept under. Objects that state nothing share one. An
    // object that states values gets a key of its own, which stays the same while the values
    // change, so the renderer updates that instance rather than building another. Made from the
    // render key and not the id, because the scene and the shared scene may both hold an id.
    private static int SurfaceKeyOf(SceneObject one)
    {
        var own = one.MaterialValues.Count == 0 ? string.Empty : one.RenderKey.ToString(CultureInfo.InvariantCulture);
        return Hash($"surface|{one.MaterialAsset}|{own}") & KeyMask;
    }

    // FNV-1a over the text, because the key has to be the same in every run and a string's own
    // hash code is not.
    private static int Hash(string text)
    {
        var hash = 2166136261u;
        foreach (var one in Encoding.UTF8.GetBytes(text))
        {
            hash = (hash ^ one) * 16777619u;
        }

        return (int)hash;
    }

    private static string Normalised(double sway) =>
        (sway == 0 ? 0.0 : sway).ToString("R", CultureInfo.InvariantCulture);

    // The wind a surface of this compliance feels, from whatever the air is doing. Still air and
    // a rigid surface both come out as Wind.None, which is the early return in the vertex stage.
    private static Wind WindFor(double sway, WeatherState? air, double bearing) =>
        sway <= 0 || air is null || air.WindSpeed <= 0
            ? Wind.None
            : new Wind { Bearing = bearing, Speed = air.WindSpeed, Strength = sway };

    private static string? ResolveMesh(string? reference, string? root)
    {
        if (reference is null)
        {
            return null;
        }

        return root is null || Path.IsPathFullyQualified(reference) ? reference : Path.Combine(root, reference);
    }

    private static List<SceneObject> ShownIn(List<EditorScene> scenes) =>
        [.. scenes.SelectMany(scene => scene.Objects.Where(one => scene.IsShown(one.Id)))];

    /// <summary>The camera, set for the light this scene actually has in it.</summary>
    private static RenderCamera Metered(RenderCamera camera, List<RenderLight> lights, double ambientLux)
    {
        var exposure = CameraExposure.ForIlluminance(IncidentLux(lights, ambientLux));
        return camera with
        {
            Aperture = exposure.Aperture,
            ShutterSpeed = exposure.ShutterSpeed,
            Sensitivity = exposure.Sensitivity,
        };
    }

    // How much light is actually falling on this scene, in lux.
    //
    // Read off the lights being sent rather than off what the day cycle intends, because those are
    // not always the same thing. A scene whose light is a bulb rather than a sun, or one somebody
    // has turned up, still has to be exposed for what it has. Metering off the hour instead is how
    // a night ends up a white rectangle with the shapes barely showing through it.
    //
    // Directional light only. A lamp lights the corner it is in rather than the scene, and a
    // camera set for the corner would blow out everywhere else.
    //
    // Angled by how high it is: a sun on the horizon lays far less on the ground than one overhead,
    // and metering as though it did would leave every dusk under-exposed.
    private static double IncidentLux(List<RenderLight> lights, double ambientLux) =>
        ambientLux
        + lights
            .Where(light => light.Kind == RenderLightKind.Directional)
            .Sum(light => light.Intensity * Math.Max(0, -light.Direction.Y));

    /// <summary>
    /// One light, in the units the renderer takes.
    ///
    /// Told what is happening to it rather than working it out. Which light the sky is standing in
    /// for, and what the weather is, are questions about the project rather than about the scene
    /// this light happens to live in: a sun in the shared set is still the sun of whichever scene
    /// is open.
    /// </summary>
    private static RenderLight LightFor(EditorScene scene, SceneObject one, Conditions conditions)
    {
        // A day cycle owns the one light everything is lit from above by: where it is, what colour
        // it is and how strong. The object keeps what it was authored with, so turning the cycle
        // off puts it back rather than leaving it wherever the clock stopped.
        //
        // Cloud sits between the scene and whatever is above it, so it only touches that one
        // light. A lamp indoors does not care what the sky is doing, and neither should a stage
        // light somebody has aimed by hand.
        var isLit = ReferenceEquals(one, conditions.Lit);
        var sky = conditions.Driven && isLit ? conditions.Sky : null;
        var now = isLit ? conditions.Now : null;
        var light = Described(one, sky, now, isLit ? conditions.Flash : 0).ToRenderer();
        var world = scene.WorldOf(one.Id);
        var halo = HaloOf(one, sky);
        return new RenderLight
        {
            Key = one.RenderKey,
            Kind = KindOf(light.Kind),
            Colour = light.Color,
            Intensity = light.Intensity,
            Position = world.GetTranslation(),
            Direction = sky?.Direction ?? Pointing(world),
            FalloffRadius = ReachOf(light.FalloffRadius),
            InnerConeAngle = light.InnerConeAngle,
            OuterConeAngle = light.OuterConeAngle,
            SunAngularRadius = light.SunAngularRadius,
            SourceRadius = light.SourceRadius,
            HaloSize = halo.Size,
            HaloFalloff = halo.Falloff,
            CastShadows = light.CastShadows,
            Width = PanelSide(light.Width),
            Height = PanelSide(light.Height),
        };
    }

    // What tells a sun from a moon at a glance, once both are white discs of the same width: a sun
    // is wrapped in glare and a moon is not.
    private static (double Size, double Falloff) HaloOf(SceneObject one, SkyState? sky) =>
        (sky?.Body ?? one.Body) == CelestialBody.Moon ? (3, 240) : (12, 70);

    // A sun's influence is infinite, which is not a number a renderer can be given. It ignores the
    // falloff of a directional light anyway, so zero here means "not asked" rather than "no reach".
    private static double ReachOf(double falloff) => double.IsFinite(falloff) ? falloff : 0;

    // Only an area light has a size, and the light package leaves both at zero for the kinds that
    // do not. Passing that zero through would give the renderer a panel with no area to integrate,
    // which is a light that emits nothing, so the renderer's own default stands in instead.
    private static double PanelSide(double side) => side > 0 ? side : 1;

    private static LightSource Described(SceneObject one, SkyState? sky, WeatherState? now, double flash) => new(
        type: one.LightType,
        color: SceneAir.Greyed(sky?.LightColour ?? one.Colour.ToTint(), now?.Greying ?? 0).Linear,
        power: PowerOf(one, sky, now, flash),
        radius: one.SourceRadius,
        // The whole difference between a bright day and a dull one. The sun is a disc half a
        // degree across; cloud turns it into a source the size of the sky, and shadows lose their
        // edges long before they lose their depth.
        sunAngle: one.SunAngle * (now?.Spread ?? 1),
        spotSize: one.SpotSize,
        spotBlend: one.SpotBlend,
        // An area light arrives as a point of the same luminous power, so the size it would have
        // emitted from becomes the size of the source that stands in for it. The falloff and the
        // total are right, and the penumbra is at least a believable width.
        sizeX: one.SourceRadius * 2,
        sizeY: one.SourceRadius * 2,
        castShadows: one.CastShadows);

    // Cloud does not switch the sun off. A heavy overcast still passes a good tenth of it, which is
    // why a wet afternoon is grey rather than dark: the camera opens up and the world stays legible.
    //
    // A strike goes the other way, briefly and by a lot. It comes through the light that is already
    // above the scene rather than as a second one: a flash is the sky lighting up, and the sky is
    // what that light is standing in for.
    private static double PowerOf(SceneObject one, SkyState? sky, WeatherState? now, double flash) =>
        (sky?.Power ?? one.Power) * (now?.Transmitted ?? 1) * (1 + (flash * 60));

    // Down the local -Z axis, which is where a light points: the same convention as a camera, so a
    // light parented to a rig turns with it.
    private static Vector3 Pointing(Matrix4 world)
    {
        var direction = world.GetRotation().Transformed(new Vector3(0, 0, -1));
        direction.Normalize();
        return direction;
    }

    private static RenderLightKind KindOf(RendererLightKind kind) => kind switch
    {
        RendererLightKind.Directional => RenderLightKind.Directional,
        RendererLightKind.Point => RenderLightKind.Point,
        RendererLightKind.Spot => RenderLightKind.Spot,
        RendererLightKind.Area => RenderLightKind.Area,
        _ => throw new UnreachableException(),
    };
}

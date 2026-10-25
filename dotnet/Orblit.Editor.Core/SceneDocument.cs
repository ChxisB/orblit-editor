namespace Orblit.Editor.Core;

using System.Diagnostics.CodeAnalysis;
using Orblit.Light;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;
using Doc = Orblit.Scene;

/// <summary>A scene read back off disk, with anything that could not be read.</summary>
/// <remarks>
/// Problems are returned rather than thrown. A scene with one broken object is still worth
/// opening, because losing the other ninety-nine to it is the worse outcome, and the editor can say
/// what it dropped.
/// </remarks>
public sealed class SceneLoad(EditorScene scene, string? name = null, IReadOnlyList<string>? problems = null)
{
    public EditorScene Scene { get; } = scene;

    /// <summary>What the scene calls itself, which need not match the file name.</summary>
    public string? Name { get; } = name;

    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Reading and writing a scene, as the editor holds one.
/// </summary>
/// <remarks>
/// The format itself is not here. It is <c>Orblit.Scene</c>, which has no UI in it and is read by
/// the runtime, the gallery and anything else that opens a scene. What is left is the translation
/// between a document and the shape the editor edits: a document's entity is a set of components,
/// and a <see cref="SceneObject"/> is one row of the outliner with every kind's fields on it.
/// </remarks>
public static class SceneDocument
{
    /// <summary>
    /// Bumped when the shape changes in a way an older editor could misread. The list of
    /// conversions lives with the format, in <c>Orblit.Scene</c>, and so does the refusal to read
    /// anything newer.
    /// </summary>
    public const int FormatVersion = Doc.SceneDocument.FormatVersion;

    /// <summary>
    /// The scene as the text of a file.
    /// </summary>
    /// <remarks>
    /// Every instance is folded back to its link and what is different about it, against the prefab
    /// <paramref name="prefabs"/> hands back for it. That has to be the one it was opened against,
    /// or the difference between two versions of the prefab is saved as a change to every instance.
    /// An instance whose prefab cannot be had is written out whole rather than lost.
    /// </remarks>
    public static string Encode(EditorScene scene, string? name = null, PrefabSource? prefabs = null)
    {
        var document = DocumentOf(scene, name);
        return (prefabs is null ? document : Instances.Fold(document, prefabs)).Encode();
    }

    /// <summary>The editor's scene as a document.</summary>
    public static Doc.SceneDocument DocumentOf(EditorScene scene, string? name = null) =>
        new(
            name ?? scene.Name,
            new SceneSettings(
                scene.SkyColour.ToTint(),
                scene.Ambient,
                // The hour is what the scene was authored at, not wherever a running cycle had
                // carried it to. A clock left going should not rewrite somebody's scene every time
                // it is saved.
                scene.TimeOfDay,
                scene.DayCycle,
                scene.HoursPerSecond,
                scene.LayerNames),
            [.. scene.Objects.Select(EntityOf)]);

    /// <summary>
    /// A scene out of a file's text, its instances opened.
    /// </summary>
    /// <remarks>
    /// <paramref name="prefabs"/> hands over each prefab an instance links to. Without it, or for
    /// one it cannot find, the instance stays folded: a row with the link on it and nothing under
    /// it, saved back exactly as it was read.
    /// </remarks>
    public static SceneLoad Decode(string text, PrefabSource? prefabs = null)
    {
        var read = Doc.SceneDocument.Decode(text);
        var opened = prefabs is null ? null : Instances.Expand(read.Document, prefabs);
        var document = opened?.Document ?? read.Document;
        var settings = document.Settings;

        return new SceneLoad(
            new EditorScene(
                [.. document.Entities.Select(entity => ObjectOf(entity))],
                document.Name,
                Colour.FromTint(settings.Sky),
                settings.Ambient,
                settings.TimeOfDay,
                settings.DayCycle,
                settings.HoursPerSecond,
                settings.LayerNames),
            document.Name,
            [.. read.Problems, .. opened?.Problems ?? []]);
    }

    /// <summary>
    /// One object as JSON. Shared with the clipboard and with prefabs, so what is copied, what is
    /// saved and what a prefab holds are the same shape.
    /// </summary>
    public static IReadOnlyDictionary<string, object?> ObjectToJson(SceneObject target) => EntityOf(target).ToJson();

    /// <summary>
    /// One object from JSON, or null if it cannot be read.
    /// </summary>
    /// <remarks>
    /// Anything older than the current format is run through the same list of migrations a file is.
    /// A clipboard payload and a prefab both carry the version they were written at, and a second,
    /// nearly identical conversion for them is how pasting an object comes to lose a field that
    /// saving it keeps.
    /// </remarks>
    public static SceneObject? ObjectFromJson(IReadOnlyDictionary<string, object?> entry, long version = FormatVersion)
    {
        var migrated = SceneMigrations.Entity(entry, version);
        if (migrated is null)
        {
            return null;
        }

        return SceneEntity.FromJson(migrated) is { } entity ? ObjectOf(entity) : null;
    }

    /// <summary>An outliner row as an entity with components.</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of components is data.")]
    public static SceneEntity EntityOf(SceneObject target)
    {
        var components = new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Transform] = new TransformComponent(target.Position, target.Rotation, target.Scale),
        };

        if (target.IsDrawable)
        {
            var boundary = target.Boundary.ToJson();
            components[SceneComponents.Mesh] = new MeshComponent(
                asset: target.MeshAsset,
                shape: target.Shape,
                geometry: target.Geometry,
                outline: target.Outline,
                boundary: boundary.Count == 0 ? null : boundary,
                surfaces: target.Surfaces.Count == 0 ? null : [.. target.Surfaces.Select(one => (object?)one.ToJson())],
                colour: target.Colour.ToTint(),
                castShadows: target.CastShadows,
                receiveShadows: target.ReceiveShadows,
                sway: target.Sway,
                // What used to be the difference between the two drawable kinds.
                authored: target.Kind == ObjectKind.Shape);
        }

        if (target.Kind == ObjectKind.Light)
        {
            components[SceneComponents.Light] = new LightComponent(
                target.LightType,
                target.Power,
                target.Colour.ToTint(),
                target.SpotSize,
                target.SpotBlend,
                target.SourceRadius,
                target.SunAngle,
                target.Body,
                target.CastShadows);
        }

        if (target.Kind == ObjectKind.Camera)
        {
            components[SceneComponents.Camera] = new CameraComponent();
        }

        if (target.Kind == ObjectKind.Weather)
        {
            components[SceneComponents.Weather] = new WeatherComponent(
                target.Weather,
                target.Condition,
                // Only written when somebody has chosen one, so a scene that follows its condition
                // keeps following it when the mapping changes rather than being frozen at whatever
                // it was.
                target.CloudKind,
                target.WindDirection,
                target.TransitionSeconds);
        }

        if (target.MaterialAsset is not null)
        {
            components[SceneComponents.Material] = new MaterialComponent(
                target.MaterialAsset,
                // Read only when the asset is a shader, so a texture put on after one does not carry
                // the shader's values into the file.
                values: target.WearsSurface ? target.MaterialValues : new OrderedDictionary<string, object>());
        }

        if (target.Kind == ObjectKind.Canvas || target.InterfaceAsset is not null)
        {
            components[SceneComponents.Canvas] = new CanvasComponent(target.InterfaceAsset);
        }

        if (target.Prefab is { } link)
        {
            components[SceneComponents.Prefab] = link;
        }

        if (target.Data.Count > 0)
        {
            components[SceneComponents.Data] = new DataComponent(target.Data);
        }

        // What the row carries without understanding it. Its own fields win: the row is what the
        // editor has been changing, and what it carries is only what was there when it was read.
        foreach (var (key, value) in target.Components)
        {
            components.TryAdd(key, value);
        }

        return new SceneEntity(target.Id, target.Name, target.ParentId, target.Visible, components);
    }

    /// <summary>
    /// Brings <paramref name="scene"/> in line with <paramref name="document"/>, in place.
    /// </summary>
    /// <remarks>
    /// In place because a scene is held by everything that is looking at it, the outliner, the
    /// viewport and every open panel, and handing back a new one would mean finding all of them.
    /// What the renderer knows each object by is carried across wherever an id survives, so an undo
    /// that moves one crate does not make it rebuild the other four thousand.
    /// </remarks>
    public static void Reconcile(EditorScene scene, Doc.SceneDocument document)
    {
        var keys = scene.Objects.ToDictionary(one => one.Id, one => one.RenderKey);

        scene.ReplaceAll(
            [.. document.Entities.Select(entity => ObjectOf(entity, keys.TryGetValue(entity.Id, out var key) ? key : null))]);

        var settings = document.Settings;
        scene.Name = document.Name;
        scene.SkyColour = Colour.FromTint(settings.Sky);
        scene.Ambient = settings.Ambient;
        scene.TimeOfDay = settings.TimeOfDay;
        scene.DayCycle = settings.DayCycle;
        scene.HoursPerSecond = settings.HoursPerSecond;
        scene.LayerNames = settings.LayerNames;
    }

    /// <summary>An entity as the row the editor edits.</summary>
    [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of fields is data.")]
    public static SceneObject ObjectOf(SceneEntity entity, int? renderKey = null)
    {
        var transform = entity[SceneComponents.Transform] as TransformComponent;
        var mesh = entity[SceneComponents.Mesh] as MeshComponent;
        var light = entity[SceneComponents.Light] as LightComponent;
        var weather = entity[SceneComponents.Weather] as WeatherComponent;
        var material = entity[SceneComponents.Material] as MaterialComponent;
        var canvas = entity[SceneComponents.Canvas] as CanvasComponent;
        var data = entity[SceneComponents.Data] as DataComponent;

        var result = new SceneObject(
            entity.Id,
            entity.Name,
            KindOf(entity),
            renderKey,
            entity.Parent,
            transform?.Position.Clone() ?? Vector3.Zero(),
            transform?.Rotation.Clone() ?? Vector3.Zero(),
            transform?.Scale.Clone() ?? Vector3.All(1),
            // A light's colour and a mesh's are one field on the row and two fields in the
            // document, which is the honest shape: a lamp that is both has two colours, and they
            // are not the same colour.
            colour: mesh is not null ? Colour.FromTint(mesh.Colour) : light is not null ? Colour.FromTint(light.Colour) : null,
            power: light?.Power ?? 1000,
            lightType: light?.Kind ?? LightType.Sun,
            spotSize: light?.SpotSize ?? 45,
            spotBlend: light?.SpotBlend ?? 0.15,
            sourceRadius: light?.SourceRadius ?? 0.1,
            sunAngle: light?.SunAngle ?? 0.526,
            body: light?.Body ?? CelestialBody.Sun,
            weather: weather?.Air ?? WeatherState.Of(WeatherCondition.Clear),
            condition: weather?.Condition ?? WeatherCondition.Clear,
            cloudKind: weather?.CloudKind,
            windDirection: weather?.WindDirection ?? 135,
            transitionSeconds: weather?.TransitionSeconds ?? 8,
            sway: mesh?.Sway ?? 0,
            // A light states its own, and everything else is a mesh's.
            castShadows: light?.CastShadows ?? mesh?.CastShadows ?? true,
            receiveShadows: mesh?.ReceiveShadows ?? true,
            visible: entity.Visible,
            meshAsset: mesh?.Asset,
            materialAsset: material?.Asset,
            materialValues: material is null ? null : Copied(material.Values),
            shape: mesh?.Shape,
            geometry: mesh?.Geometry,
            outline: mesh?.Outline,
            boundary: Boundary.FromJson(mesh?.Boundary),
            interfaceAsset: canvas?.Asset,
            prefab: entity[SceneComponents.Prefab] as PrefabComponent,
            surfaces: mesh?.Surfaces is { } slots ? [.. slots.Select(Surface.FromJson).OfType<Surface>()] : null,
            data: data is null ? null : [.. data.Paths]);

        // Whatever the row has no way to say goes with it. Worked out from what the row writes back
        // rather than from a list of the kinds it models: a lamp is a light and a mesh, the row is
        // only a light, and a list of modelled kinds would drop its mesh just as surely as it once
        // dropped a body.
        var said = EntityOf(result).Components;
        foreach (var (key, value) in entity.Components)
        {
            if (!said.ContainsKey(key))
            {
                result.Components[key] = value;
            }
        }

        return result;
    }

    private static OrderedDictionary<string, object> Copied(IReadOnlyDictionary<string, object> values)
    {
        var copy = new OrderedDictionary<string, object>();
        foreach (var (key, value) in values)
        {
            copy[key] = value;
        }

        return copy;
    }

    // The document does not have a kind and does not want one: an entity is what it has, and a
    // lamp is a mesh and a light at once. The editor still shows one row per thing with one icon on
    // it, so the row has to pick, and it picks by what changes most about how the row behaves.
    // Weather first, because a weather object is not drawn and not lit. Then light, then camera,
    // then geometry, then an interface. A thing with none of those is a place in the tree.
    private static ObjectKind KindOf(SceneEntity entity)
    {
        if (entity.Has(SceneComponents.Weather))
        {
            return ObjectKind.Weather;
        }

        if (entity.Has(SceneComponents.Light))
        {
            return ObjectKind.Light;
        }

        if (entity.Has(SceneComponents.Camera))
        {
            return ObjectKind.Camera;
        }

        if (entity[SceneComponents.Mesh] is MeshComponent mesh)
        {
            return mesh.Authored ? ObjectKind.Shape : ObjectKind.Mesh;
        }

        return entity.Has(SceneComponents.Canvas) ? ObjectKind.Canvas : ObjectKind.Group;
    }
}

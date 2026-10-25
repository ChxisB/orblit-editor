namespace Orblit.Editor.Core;

using Orblit.Mesh;

// How an object looks without changing its shape.
//
// F2: a command takes the scene, the object, its name for the label and the change, so a step is
// built by name from what the panel already holds.

/// <summary>Recolours one object.</summary>
public sealed class SetColour(string sceneId, string id, string name, Colour from, Colour to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    /// <summary>Kept for the label so it survives the object being deleted and restored.</summary>
    public string Name { get; } = name;

    public Colour From { get; } = from;

    public Colour To { get; } = to;

    public override string Label => $"Recolour {Name}";

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Colour = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Colour = From;
}

/// <summary>Changes how much an object answers the wind.</summary>
public sealed class SetSway(string sceneId, string id, string name, double from, double to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    /// <summary>Kept for the label so it survives the object being deleted and restored.</summary>
    public string Name { get; } = name;

    public double From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public double To { get; private set; } = to;

    public override string Label => To <= 0 ? $"Stop {Name} swaying" : $"Set {Name} sway";

    public override object? MergeKey => (Id, "sway");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetSway other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Sway = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Sway = From;
}

/// <summary>Swaps the texture or shader an object wears.</summary>
public sealed class SetMaterialAsset(string sceneId, string id, string name, string? from, string? to)
    : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? From { get; } = from;

    public string? To { get; } = to;

    public override string Label => To is null ? $"Clear texture on {Name}" : $"Texture {Name}";

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.MaterialAsset = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.MaterialAsset = From;
}

/// <summary>
/// Changes what an object states for one uniform of its shader.
/// <para>
/// Holds the whole map on each side rather than the one entry, so undo puts back exactly what was
/// there, including the absence of a value.
/// </para>
/// </summary>
public sealed class SetMaterialValue(
    string sceneId,
    string id,
    string name,
    string uniform,
    OrderedDictionary<string, object> from,
    OrderedDictionary<string, object> to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    /// <summary>The uniform being changed, for the label and so that a drag on one is a step of its own.</summary>
    public string Uniform { get; } = uniform;

    public OrderedDictionary<string, object> From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public OrderedDictionary<string, object> To { get; private set; } = to;

    public override string Label => $"Set {Name} {Uniform}";

    public override object? MergeKey => (Id, "materialValue", Uniform);

    public override void Absorb(EditorCommand later)
    {
        if (later is SetMaterialValue other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.MaterialValues = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.MaterialValues = From;
}

/// <summary>Shows or hides an object.</summary>
public sealed class SetVisible(string sceneId, string id, string name, bool to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public bool To { get; } = to;

    public override string Label => To ? $"Show {Name}" : $"Hide {Name}";

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Visible = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Visible = !To;
}

/// <summary>Turns shadow casting on or off.</summary>
public sealed class SetCastShadows(string sceneId, string id, string name, bool to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public bool To { get; } = to;

    public override string Label => To ? $"Cast shadows from {Name}" : $"Stop {Name} casting";

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.CastShadows = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.CastShadows = !To;
}

/// <summary>Turns shadow receiving on or off.</summary>
public sealed class SetReceiveShadows(string sceneId, string id, string name, bool to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public bool To { get; } = to;

    public override string Label => To ? $"Let shadows fall on {Name}" : $"Keep shadows off {Name}";

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.ReceiveShadows = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.ReceiveShadows = !To;
}

/// <summary>
/// Replaces a shape's material slots.
/// <para>
/// The whole list rather than one slot, for the same reason geometry is the whole mesh: a slot's
/// position in the list is what a face points at, so an edit to one is a fact about all of them.
/// </para>
/// </summary>
public sealed class SetSurfaces(
    string sceneId,
    string id,
    string name,
    List<Surface> to,
    string what,
    object? gesture = null) : EditorCommand
{
    private List<Surface>? _was;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public List<Surface> To { get; private set; } = to;

    public string What { get; } = what;

    /// <summary>Set while a slider is being dragged, so a gesture is one step.</summary>
    public object? Gesture { get; } = gesture;

    public override object? MergeKey => Gesture;

    public override string Label => $"{What} on {Name}";

    public override void Absorb(EditorCommand later)
    {
        if (later is SetSurfaces other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        _was ??= [.. target.Surfaces];
        Put(target, To);
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target || _was is not { } was)
        {
            return;
        }

        Put(target, was);
        scene.Invalidate();
    }

    private static void Put(SceneObject target, List<Surface> surfaces)
    {
        // Copied first: the list being put may be the very one being cleared.
        var next = surfaces.ToList();
        target.Surfaces.Clear();
        target.Surfaces.AddRange(next);
    }
}

/// <summary>Changes the outline a shape was drawn from.</summary>
public sealed class SetOutline(string sceneId, string id, string name, PolyShape to, object? gesture = null)
    : EditorCommand
{
    private PolyShape? _was;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public PolyShape To { get; private set; } = to;

    /// <summary>Set while a slider is moving, so the run is one step.</summary>
    public object? Gesture { get; } = gesture;

    public override object? MergeKey => Gesture;

    public override string Label => $"Reshape {Name}";

    public override void Absorb(EditorCommand later)
    {
        if (later is SetOutline other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        _was ??= target.Outline;
        target.Outline = To;
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        target.Outline = _was;
        scene.Invalidate();
    }
}

namespace Orblit.Editor.Core;

// How an object looks without changing its shape.

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

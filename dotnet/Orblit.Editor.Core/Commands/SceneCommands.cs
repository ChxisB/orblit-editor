namespace Orblit.Editor.Core;

// Settings that belong to the scene rather than to anything in it.

/// <summary>Changes the sky colour and the ambient light together.</summary>
public sealed class SetSceneSky(
    string sceneId,
    Colour fromColour,
    Colour toColour,
    double fromAmbient,
    double toAmbient) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public Colour FromColour { get; } = fromColour;

    public Colour ToColour { get; } = toColour;

    public double FromAmbient { get; } = fromAmbient;

    /// <summary>Not fixed: a drag on the ambient slider ends holding where it stopped.</summary>
    public double ToAmbient { get; private set; } = toAmbient;

    public override string Label => "Change the sky";

    public override object? MergeKey => (SceneId, "sky");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetSceneSky other)
        {
            ToAmbient = other.ToAmbient;
        }
    }

    public override void Apply(ISceneHost host) => Set(host, ToColour, ToAmbient);

    public override void Revert(ISceneHost host) => Set(host, FromColour, FromAmbient);

    private void Set(ISceneHost host, Colour colour, double ambient)
    {
        if (host.SceneFor(SceneId) is not { } scene)
        {
            return;
        }

        scene.SkyColour = colour;
        scene.Ambient = ambient;
    }
}

/// <summary>Renames the scene itself.</summary>
public sealed class RenameScene(string sceneId, string from, string to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string From { get; } = from;

    /// <summary>Not fixed: typing merges into one step rather than one per keystroke.</summary>
    public string To { get; private set; } = to;

    public override string Label => $"Rename {From}";

    public override object? MergeKey => (SceneId, "sceneName");

    public override void Absorb(EditorCommand later)
    {
        if (later is RenameScene other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?.Name = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?.Name = From;
}

/// <summary>Names the scene's physics layers.</summary>
public sealed class SetLayerNames(string sceneId, IReadOnlyList<string> from, IReadOnlyList<string> to)
    : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public IReadOnlyList<string> From { get; } = from;

    /// <summary>
    /// Not fixed: typing in one name merges into one step, which ends holding the names as they
    /// stood when the last key went down.
    /// </summary>
    public IReadOnlyList<string> To { get; private set; } = to;

    public override string Label => "Name the layers";

    public override object? MergeKey => (SceneId, "layerNames");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetLayerNames other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?.LayerNames = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?.LayerNames = From;
}

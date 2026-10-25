namespace Orblit.Editor.Core;

using Doc = Orblit.Scene;

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

/// <summary>
/// A change stated as the difference between two documents.
/// </summary>
/// <remarks>
/// Every other command here names the field it touches. That fits an edit somebody makes with a
/// mouse, and not one that arrives whole: a scene pasted in, a file reloaded from under somebody, a
/// generated layout, an import. For those the change is "this document became that one", and a diff
/// says so exactly. It is also the shape an edit has to be in to leave this machine, because a diff
/// is a list of operations that serialise.
/// <para>
/// The blunt instrument, on purpose. It rebuilds the objects rather than mutating the ones there.
/// What the renderer knows each by is carried across, so that costs no frame, but the outliner is
/// rebuilt and a drag should not go through here.
/// </para>
/// </remarks>
public sealed class ApplySceneDiff(string sceneId, string label, Doc.SceneDiff diff) : EditorCommand
{
    /// <summary>
    /// The change from what is there now to what should be. Both directions are in it: undo applies
    /// the inverse, so there is no second description of the change to fall out of step with the
    /// first.
    /// </summary>
    public Doc.SceneDiff Diff { get; } = diff;

    public override string SceneId { get; } = sceneId;

    public override string Label { get; } = label;

    public override void Apply(ISceneHost host) => Move(host, Diff);

    public override void Revert(ISceneHost host) => Move(host, Diff.Inverse);

    private void Move(ISceneHost host, Doc.SceneDiff by)
    {
        if (host.SceneFor(SceneId) is not { } scene)
        {
            return;
        }

        SceneDocument.Reconcile(scene, by.ApplyTo(SceneDocument.DocumentOf(scene)));
    }
}

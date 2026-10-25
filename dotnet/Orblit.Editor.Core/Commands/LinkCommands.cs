namespace Orblit.Editor.Core;

// Links out of the scene, to a data file or an interface, and the breaking of them. A prefab's
// link is a change to the whole document, and goes through ApplySceneDiff.

/// <summary>
/// Points an object at a data object, or stops pointing at one.
/// <para>
/// Attaching rather than copying: what changes here is which file the object reads its settings
/// from, and the settings themselves stay in the file where everything else using them can see the
/// same values.
/// </para>
/// </summary>
public sealed class SetDataLinks(string sceneId, string id, string name, IReadOnlyList<string> paths, string what)
    : EditorCommand
{
    private List<string> _was = [];

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    /// <summary>
    /// The whole list afterwards, rather than one path added or removed. An undo then puts back
    /// exactly what was there, including the order.
    /// </summary>
    public IReadOnlyList<string> Paths { get; } = paths;

    /// <summary>What the step is called: "Add weight" or "Remove weight".</summary>
    public string What { get; } = what;

    public override string Label => $"{What} on {Name}";

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        _was = [.. target.Data];
        Put(target, Paths);
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        Put(target, _was);
        scene.Invalidate();
    }

    private static void Put(SceneObject target, IReadOnlyList<string> paths)
    {
        target.Data.Clear();
        target.Data.AddRange(paths);
    }
}

/// <summary>
/// Changes which interface a canvas object shows.
/// <para>
/// A reference rather than a copy, like a mesh: the .oui is the document and the scene says which
/// one is on screen, so one interface can be on two scenes and editing it changes both.
/// </para>
/// </summary>
public sealed class SetInterface(string sceneId, string id, string name, string? to) : EditorCommand
{
    private string? _was;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? To { get; } = to;

    public override string Label => To is null ? $"Clear {Name}" : $"Set {Name}";

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        _was = target.InterfaceAsset;
        target.InterfaceAsset = To;
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        target.InterfaceAsset = _was;
        scene.Invalidate();
    }
}

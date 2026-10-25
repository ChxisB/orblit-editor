namespace Orblit.Editor.Core;

using Orblit.Mesh;

// Edits that replace an object's geometry.
//
// F2: a command takes the scene, the object, its name for the label and the change, so a step is
// built by name from what the panel already holds.

/// <summary>
/// Changes the numbers a shape is made from.
/// <para>
/// One command for the whole shape rather than one per field: the fields are dragged, they interact
/// (a cylinder's sides and its radius are the same decision), and an undo stack with "width" and
/// "sides" as separate steps is one somebody has to walk back through twice.
/// </para>
/// </summary>
public sealed class SetShape(string sceneId, string id, string name, Shape to) : EditorCommand
{
    private Shape? _was;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public Shape To { get; } = to;

    public override string Label => $"Change {Name}";

    /// <summary>
    /// Dragging a slider produces one of these a frame. They collapse into one step, the way a
    /// dragged transform does.
    /// </summary>
    public override object? MergeKey => $"shape/{Id}";

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        _was ??= target.Shape;
        target.Shape = To;
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        target.Shape = _was;
        scene.Invalidate();
    }
}

/// <summary>
/// Replaces an object's geometry.
/// <para>
/// What every mesh edit runs through. The whole mesh rather than the change: an extrude adds vertices
/// and faces and moves others, and describing that as a diff is more code than copying a few thousand
/// doubles, which is what a mesh is, and is nothing next to a frame.
/// </para>
/// </summary>
public sealed class SetGeometry(
    string sceneId,
    string id,
    string name,
    Mesh to,
    string what,
    object? gesture = null) : EditorCommand
{
    private Mesh? _was;
    private Shape? _wasShape;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public Mesh To { get; private set; } = to;

    /// <summary>What the step is called: "Extrude", "Inset".</summary>
    public string What { get; } = what;

    /// <summary>
    /// Set while a drag is running, so the hundred commands a gesture produces are one step to
    /// undo. Null for an edit that stands alone: a menu item, a tool button.
    /// </summary>
    public object? Gesture { get; } = gesture;

    public override object? MergeKey => Gesture;

    public override string Label => $"{What} {Name}";

    public override void Absorb(EditorCommand later)
    {
        // The mesh the drag has reached now, over the one it started from. The one this command is
        // holding is from before the gesture began, which is where undo has to land.
        if (later is SetGeometry other)
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

        _was ??= target.Geometry;
        _wasShape ??= target.Shape;
        target.Geometry = To;
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        target.Geometry = _was;
        target.Shape = _wasShape;
        scene.Invalidate();
    }
}

/// <summary>Changes where an object begins and ends.</summary>
public sealed class SetBoundary(string sceneId, string id, string name, Boundary to, object? gesture = null)
    : EditorCommand
{
    private Boundary? _was;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public Boundary To { get; private set; } = to;

    /// <summary>Set while a slider is moving, so the run is one step.</summary>
    public object? Gesture { get; } = gesture;

    public override object? MergeKey => Gesture;

    public override string Label => $"Boundary of {Name}";

    public override void Absorb(EditorCommand later)
    {
        if (later is SetBoundary other)
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

        _was ??= target.Boundary;
        target.Boundary = To;
        scene.Invalidate();
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target || _was is not { } was)
        {
            return;
        }

        target.Boundary = was;
        scene.Invalidate();
    }
}

namespace Orblit.Editor.Core;

using Orblit.VectorMath;
using Doc = Orblit.Scene;

// Edits to the tree itself: what exists, what it is called, and where it sits.

/// <summary>Renames an object.</summary>
public sealed class Rename(string sceneId, string id, string from, string to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string From { get; } = from;

    /// <summary>Not fixed: typing merges into one step rather than one per keystroke.</summary>
    public string To { get; private set; } = to;

    public override string Label => $"Rename {From}";

    public override object? MergeKey => (Id, "name");

    public override void Absorb(EditorCommand later)
    {
        if (later is Rename other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Name = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Name = From;
}

/// <summary>
/// Moves an object: to a new parent, to a new place among its siblings, or both.
/// </summary>
/// <remarks>
/// One command rather than two, because dragging in a tree is one gesture and the answer to "where
/// did that go" should be one step of undo.
/// <para>
/// The local transform is rewritten so the object does not jump when its parent changes. Dropping
/// something into a folder should not move it: an editor that teleported things on reparent would be
/// unusable for layout.
/// </para>
/// </remarks>
// F2: a move is its scene, the object, and both ends of the change.
public sealed class MoveObject(
    string sceneId,
    string id,
    string name,
    string? from,
    string? to,
    int fromIndex,
    int toIndex) : EditorCommand
{
    private Vector3? _oldPosition;
    private Vector3? _oldRotation;
    private Vector3? _oldScale;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? From { get; } = from;

    public string? To { get; } = to;

    public int FromIndex { get; } = fromIndex;

    public int ToIndex { get; } = toIndex;

    public override string Label => From == To ? $"Reorder {Name}" : $"Move {Name}";

    public override void Apply(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        var target = scene?[Id];
        if (scene is null || target is null)
        {
            return;
        }

        _oldPosition = target.Position.Clone();
        _oldRotation = target.Rotation.Clone();
        _oldScale = target.Scale.Clone();

        var world = scene.WorldOf(Id).Clone();
        scene.MoveTo(Id, ToIndex, To);

        // Only when the parent actually changed: a reorder among siblings leaves the transform
        // alone, and recomposing it would introduce rounding for no reason.
        if (From != To)
        {
            SceneMath.PlaceInWorld(scene, target, world);
        }
    }

    public override void Revert(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        var target = scene?[Id];
        if (scene is null || target is null)
        {
            return;
        }

        scene.MoveTo(Id, FromIndex, From);
        target.Position.SetFrom(_oldPosition ?? target.Position);
        target.Rotation.SetFrom(_oldRotation ?? target.Rotation);
        target.Scale.SetFrom(_oldScale ?? target.Scale);
        scene.Invalidate();
    }
}

/// <summary>
/// Moves an object and everything under it from one scene into another.
/// </summary>
/// <remarks>
/// A separate command from <see cref="MoveObject"/> because it is a different operation: one scene
/// loses a subtree and another gains it, and an undo has to put it back in the scene it came from at
/// the index it came from. Trying to do both through one command means every field is nullable and
/// neither case is clear.
/// <para>
/// What makes it worth having at all is the shared set, the objects every scene has. Without this,
/// the only way to put something in it was to build it there, and the obvious gesture of dragging a
/// prop onto the Shared row did nothing at all.
/// </para>
/// </remarks>
/// <param name="fromSceneId">Where it came from.</param>
/// <param name="sceneId">
/// Where it is going, which is the scene the history records this against: an undo should show the
/// object where it will reappear.
/// </param>
/// <param name="id">The object that moves.</param>
/// <param name="name">What to call the step.</param>
/// <param name="parentId">The parent it lands under in the destination, or null for a root.</param>
/// <param name="index">Where among its new siblings.</param>
// F2: a move between scenes is both scenes, the object, its new parent and its new place.
public sealed class MoveBetweenScenes(
    string fromSceneId,
    string sceneId,
    string id,
    string name,
    string? parentId,
    int index) : EditorCommand
{
    // What was taken out, with the indices to put it back at.
    private List<(SceneObject Object, int Index)> _removed = [];

    // What actually went into the destination. The very same objects when nothing had to change, so
    // the renderer keeps the key it knows them by and an undo puts back exactly what was there.
    // Copies only when an id collided.
    private List<SceneObject> _moved = [];

    public string FromSceneId { get; } = fromSceneId;

    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public string? ParentId { get; } = parentId;

    public int Index { get; } = index;

    public override IReadOnlySet<string> Touches => new HashSet<string> { SceneId, FromSceneId };

    public override string Label => $"Move {Name}";

    public override void Apply(ISceneHost host)
    {
        var from = host.SceneFor(FromSceneId);
        var to = host.SceneFor(SceneId);
        if (from is null || to is null || from[Id] is null)
        {
            return;
        }

        // Read before the move, so the thing lands where it looked rather than wherever its old
        // local transform points under a new parent.
        var world = from.WorldOf(Id).Clone();

        _removed = from.Remove(Id);
        _moved = Carried(_removed.Select(entry => entry.Object).ToList(), to);

        var root = _moved[0];
        root.ParentId = ParentId;
        foreach (var moving in _moved)
        {
            to.Add(moving);
        }

        to.MoveTo(root.Id, Index, ParentId);
        SceneMath.PlaceInWorld(to, root, world);
    }

    public override void Revert(ISceneHost host)
    {
        var from = host.SceneFor(FromSceneId);
        var to = host.SceneFor(SceneId);
        if (from is null || to is null || _removed.Count == 0)
        {
            return;
        }

        to.Remove(_moved[0].Id);
        from.Restore(_removed);
    }

    // Two scenes can hold the same id (the starter scene names its objects outright), and adding a
    // second one would throw. Renaming on the way across rather than refusing keeps the drag
    // working. A part of an instance is named by the path into it, so it follows the instance rather
    // than being renamed on its own.
    private static List<SceneObject> Carried(List<SceneObject> objects, EditorScene to)
    {
        var heads = objects
            .Where(moving => !Doc.EntityPath.IsPart(moving.Id) && to.Contains(moving.Id))
            .ToDictionary(moving => moving.Id, moving => $"{moving.Id}~{to.Length}");
        var clash = new Dictionary<string, string>();
        foreach (var moving in objects)
        {
            if (Renamed(moving.Id, heads) is { } renamed)
            {
                clash[moving.Id] = renamed;
            }
        }

        return clash.Count == 0 ? objects : [.. objects.Select(moving => Renamed(moving, clash))];
    }

    private static SceneObject Renamed(SceneObject moving, Dictionary<string, string> clash) =>
        moving.CopyAs(
            clash.GetValueOrDefault(moving.Id) ?? moving.Id,
            moving.ParentId is null ? null : clash.GetValueOrDefault(moving.ParentId) ?? moving.ParentId);

    // What an id is called once the heads have been renamed, or null when it keeps its name.
    private static string? Renamed(string id, Dictionary<string, string> heads)
    {
        if (heads.TryGetValue(id, out var renamed))
        {
            return renamed;
        }

        if (Doc.EntityPath.InstanceOf(id) is not { } head || !heads.TryGetValue(head, out var moved))
        {
            return null;
        }

        return $"{moved}{id[head.Length..]}";
    }
}

/// <summary>Adds an object to the scene.</summary>
public sealed class AddObject(SceneObject added, string sceneId, string? parentId = null) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public SceneObject Added { get; } = added;

    public string? ParentId { get; } = parentId;

    public override string Label => $"Add {Added.Name}";

    public override void Apply(ISceneHost host)
    {
        Added.ParentId = ParentId;
        host.SceneFor(SceneId)?.Add(Added);
    }

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?.Remove(Added.Id);
}

/// <summary>
/// Puts objects into a scene, keeping the shape they had.
/// </summary>
/// <remarks>
/// One command for the whole paste rather than one per object: a paste is one thing somebody did, and
/// undoing it halfway would leave a subtree with its parent missing.
/// </remarks>
/// <param name="sceneId">The scene pasted into.</param>
/// <param name="objects">
/// In insertion order: a parent is always added before its children, so no object is ever briefly
/// pointing at something that is not there.
/// </param>
/// <param name="roots">The tops of what was pasted, which is what a delete has to take.</param>
/// <param name="what">What to call the step.</param>
/// <param name="worlds">
/// Where each top sat in the world when it was copied. Pasting into a scene whose parent chain is
/// different would otherwise put the object somewhere else entirely, because a local transform only
/// means anything relative to the parent it was measured against.
/// </param>
// F2: a paste is the scene, what went in, which are the tops, a label and where they stood.
public sealed class PasteObjects(
    string sceneId,
    IReadOnlyList<SceneObject> objects,
    IReadOnlyList<string> roots,
    string what,
    IReadOnlyDictionary<string, Matrix4>? worlds = null) : EditorCommand
{
    private readonly IReadOnlyDictionary<string, Matrix4> _worlds = worlds ?? new Dictionary<string, Matrix4>();

    public override string SceneId { get; } = sceneId;

    public IReadOnlyList<SceneObject> Objects { get; } = objects;

    public IReadOnlyList<string> Roots { get; } = roots;

    public string What { get; } = what;

    public override string Label => $"Paste {What}";

    public override void Apply(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        if (scene is null)
        {
            return;
        }

        foreach (var added in Objects.Where(added => !scene.Contains(added.Id)))
        {
            scene.Add(added);
        }

        // After every object is in, so a root's new parent can be resolved.
        foreach (var root in Roots)
        {
            if (_worlds.TryGetValue(root, out var world) && scene[root] is { } target)
            {
                SceneMath.PlaceInWorld(scene, target, world);
            }
        }
    }

    public override void Revert(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        if (scene is null)
        {
            return;
        }

        // Roots only: removing one takes everything under it, and asking for a child that has
        // already gone is not an error worth having.
        foreach (var root in Roots)
        {
            scene.Remove(root);
        }
    }
}

/// <summary>
/// Deletes objects and everything under them.
/// </summary>
/// <remarks>
/// One command however many are selected: deleting three things is one thing somebody did, and
/// undoing it a third at a time would be tedious and would let a subtree come back without its
/// parent.
/// </remarks>
public sealed class DeleteObjects(string sceneId, IReadOnlyList<string> ids, string what) : EditorCommand
{
    private readonly List<List<(SceneObject Object, int Index)>> _removed = [];

    public override string SceneId { get; } = sceneId;

    public IReadOnlyList<string> Ids { get; } = ids;

    public string What { get; } = what;

    public override string Label => $"Delete {What}";

    public override void Apply(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        if (scene is null)
        {
            return;
        }

        _removed.Clear();
        foreach (var id in Ids.Where(scene.Contains))
        {
            // Already gone if it was inside something removed a moment ago, which is not a mistake:
            // the selection simply held a parent and its child. The check runs as each id is
            // reached, not up front.
            _removed.Add(scene.Remove(id));
        }
    }

    public override void Revert(ISceneHost host)
    {
        var scene = host.SceneFor(SceneId);
        if (scene is null)
        {
            return;
        }

        // Backwards, so each restore puts things back at indices the ones after it have not yet
        // shifted.
        for (var i = _removed.Count - 1; i >= 0; i--)
        {
            scene.Restore(_removed[i]);
        }
    }
}

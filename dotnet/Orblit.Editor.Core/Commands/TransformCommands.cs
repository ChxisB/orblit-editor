namespace Orblit.Editor.Core;

using Orblit.VectorMath;

// Moving, rotating and scaling: one object or a selection.

/// <summary>Which of an object's three vectors an edit is touching.</summary>
public enum TransformField
{
    Position,
    Rotation,
    Scale,
}

public static class TransformFields
{
    /// <summary>The word that appears next to Undo.</summary>
    public static string Verb(this TransformField field) => field switch
    {
        TransformField.Position => "Move",
        TransformField.Rotation => "Rotate",
        TransformField.Scale => "Scale",
        _ => throw new System.Diagnostics.UnreachableException(),
    };

    public static Vector3 Of(this TransformField field, SceneObject target) => field switch
    {
        TransformField.Position => target.Position,
        TransformField.Rotation => target.Rotation,
        TransformField.Scale => target.Scale,
        _ => throw new System.Diagnostics.UnreachableException(),
    };
}

/// <summary>A position and a rotation, the pair a steer sets together.</summary>
public readonly record struct Placement(Vector3 Position, Vector3 Rotation)
{
    public Placement Copy() => new(Position.Clone(), Rotation.Clone());
}

/// <summary>
/// Moves, turns or resizes one object.
/// </summary>
/// <remarks>
/// Holds the whole vector either side rather than one component, so a drag on X and a later drag on
/// Y merge into one "Move" step the way somebody nudging a thing into place expects.
/// </remarks>
public sealed class SetTransform : EditorCommand
{
    private readonly Vector3 _from;
    private Vector3 _to;

    // F2: a command is its scene, its target and both ends of the change.
    public SetTransform(string sceneId, string id, TransformField field, string name, Vector3 from, Vector3 to)
    {
        SceneId = sceneId;
        Id = id;
        Field = field;
        Name = name;
        _from = from.Clone();
        _to = to.Clone();
    }

    public override string SceneId { get; }

    public string Id { get; }

    public TransformField Field { get; }

    /// <summary>Kept for the label so it survives the object being deleted and restored.</summary>
    public string Name { get; }

    public override string Label => $"{Field.Verb()} {Name}";

    public override object? MergeKey => (Id, Field);

    public override void Absorb(EditorCommand later)
    {
        if (later is SetTransform other)
        {
            _to = other._to.Clone();
        }
    }

    public override void Apply(ISceneHost host) => Set(host, _to);

    public override void Revert(ISceneHost host) => Set(host, _from);

    private void Set(ISceneHost host, Vector3 value)
    {
        var scene = host.SceneFor(SceneId);
        var target = scene?[Id];
        if (scene is null || target is null)
        {
            return;
        }

        Field.Of(target).SetFrom(value);
        scene.Invalidate();
    }
}

/// <summary>
/// Moves and turns one object together, the way steering a camera from the scene view does. A move
/// and a turn run apart would be two undo steps for one gesture.
/// </summary>
public sealed class PlaceObject : EditorCommand
{
    private readonly Placement _from;
    private Placement _to;

    public PlaceObject(string sceneId, string id, string name, Placement from, Placement to)
    {
        SceneId = sceneId;
        Id = id;
        Name = name;
        _from = from.Copy();
        _to = to.Copy();
    }

    public override string SceneId { get; }

    public string Id { get; }

    /// <summary>Kept for the label so it survives the object being deleted and restored.</summary>
    public string Name { get; }

    public override string Label => $"Steer {Name}";

    public override object? MergeKey => (Id, "place");

    public override bool OnlyMoves => true;

    public override void Absorb(EditorCommand later)
    {
        if (later is PlaceObject other)
        {
            _to = other._to.Copy();
        }
    }

    public override void Apply(ISceneHost host) => Set(host, _to);

    public override void Revert(ISceneHost host) => Set(host, _from);

    private void Set(ISceneHost host, Placement place)
    {
        var scene = host.SceneFor(SceneId);
        var target = scene?[Id];
        if (scene is null || target is null)
        {
            return;
        }

        target.Position.SetFrom(place.Position);
        target.Rotation.SetFrom(place.Rotation);
        scene.Invalidate();
    }
}

/// <summary>Where one object started a gesture and where it has got to.</summary>
public readonly record struct TransformChange(Vector3 From, Vector3 To);

/// <summary>
/// Moves or turns several objects at once, as one step.
/// </summary>
/// <remarks>
/// A gizmo drag with three things selected is one gesture and has to be one undo. Running a command
/// per object would merge each into its own run and leave somebody pressing undo once per thing they
/// moved together.
/// </remarks>
public sealed class TransformMany : EditorCommand
{
    private readonly Dictionary<string, TransformChange> _changes;

    public TransformMany(string sceneId, TransformField field, string what, IReadOnlyDictionary<string, TransformChange> changes)
    {
        SceneId = sceneId;
        Field = field;
        What = what;
        _changes = changes.ToDictionary(
            entry => entry.Key,
            entry => new TransformChange(entry.Value.From.Clone(), entry.Value.To.Clone()));
    }

    public override string SceneId { get; }

    public TransformField Field { get; }

    /// <summary>What is being moved, for the label: a name, or how many there are.</summary>
    public string What { get; }

    public override string Label => $"{Field.Verb()} {What}";

    /// <summary>
    /// One key for the whole gesture rather than one per object, which is what collapses a drag into
    /// a single step however many things it moved.
    /// </summary>
    public override object? MergeKey => (SceneId, "gizmo", Field);

    /// <summary>
    /// Nothing about what these objects are has changed, only where they stand. Which is what a drag
    /// does sixty times a second.
    /// </summary>
    public override bool OnlyMoves => true;

    public override void Absorb(EditorCommand later)
    {
        if (later is not TransformMany other)
        {
            return;
        }

        foreach (var entry in other._changes)
        {
            // Keeps where each object started and takes where it has got to, so undoing the run puts
            // everything back where the drag began.
            var from = _changes.TryGetValue(entry.Key, out var existing) ? existing.From : entry.Value.From.Clone();
            _changes[entry.Key] = new TransformChange(from, entry.Value.To.Clone());
        }
    }

    public override void Apply(ISceneHost host) => Write(host, change => change.To);

    public override void Revert(ISceneHost host) => Write(host, change => change.From);

    private void Write(ISceneHost host, Func<TransformChange, Vector3> pick)
    {
        var scene = host.SceneFor(SceneId);
        if (scene is null)
        {
            return;
        }

        foreach (var entry in _changes)
        {
            if (scene[entry.Key] is { } target)
            {
                Field.Of(target).SetFrom(pick(entry.Value));
            }
        }

        scene.Invalidate();
    }
}

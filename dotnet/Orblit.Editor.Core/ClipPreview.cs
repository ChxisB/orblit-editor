namespace Orblit.Editor.Core;

using Orblit.Motion;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>What posing the scene changed, which decides how much is built again.</summary>
public enum PoseChange
{
    /// <summary>Nothing.</summary>
    None,

    /// <summary>Only where things are, which the views that show movement follow.</summary>
    Moved,

    /// <summary>Something else about an object, which any panel might be showing.</summary>
    Rebuilt,
}

/// <summary>
/// The clip on the timeline, shown on the scene where the playhead is.
/// </summary>
/// <remarks>
/// Written into the objects directly, not through the undo stack: scrubbing is looking, not editing,
/// and a scene should not read as changed because somebody looked at it. What each field was before
/// the clip took it is kept, and <see cref="Lift"/> puts it all back. That is done before anything
/// that reads the scene as a document, so a saved scene is the scene as it rests and not wherever the
/// playhead happened to be.
/// <para>
/// A field the clip moves belongs to the clip while it is shown. Changing one by hand shows until the
/// playhead moves, and keying it is how the change is kept.
/// </para>
/// </remarks>
public sealed class ClipPreview
{
    // Each field the clip has taken, and what it was before.
    private readonly OrderedDictionary<PosedField, object?> _rest = [];

    private readonly record struct PosedField(string Id, string Type, string Field);

    public bool Posing => _rest.Count > 0;

    /// <summary>
    /// Shows <paramref name="clip"/> at <paramref name="at"/> on <paramref name="scene"/>, as played by
    /// <paramref name="owner"/>.
    /// <para>
    /// Fields the clip moved last time and does not now go back to how they were, so choosing
    /// something else to play on, or deleting the last key of a channel, leaves nothing behind.
    /// </para>
    /// </summary>
    public PoseChange Pose(EditorScene scene, ClipDocument? clip, string? owner, double at)
    {
        var playing = owner is null ? null : scene[owner];
        if (clip is null || owner is null || playing is null)
        {
            return Lift(scene);
        }

        var scope = ClipScope.InScene(new Doc.SceneDocument(entities: [SceneDocument.EntityOf(playing)]), owner);
        return Show(scene, clip.SampleAt(at), scope);
    }

    /// <summary>
    /// Shows <paramref name="clip"/> at <paramref name="at"/> on <paramref name="scene"/>, naming
    /// things by the scene's own ids the way a cutscene does.
    /// </summary>
    public PoseChange PoseAll(EditorScene scene, ClipDocument? clip, double at) =>
        clip is null ? Lift(scene) : Show(scene, clip.SampleAt(at), ClipScope.WholeScene);

    /// <summary>Puts back every field the clip has taken.</summary>
    public PoseChange Lift(EditorScene scene)
    {
        if (_rest.Count == 0)
        {
            return PoseChange.None;
        }

        var ops = _rest.Select(entry => Back(entry.Key, entry.Value)).ToList();
        _rest.Clear();
        return Write(scene, ops);
    }

    /// <summary>
    /// Lets go without putting anything back, for a scene that is going away and taking the fields
    /// with it.
    /// </summary>
    public void Forget() => _rest.Clear();

    private static Doc.SetField Back(PosedField field, object? rest) =>
        new(field.Id, field.Type, field.Field, from: null, to: rest);

    // The fields frame sets that document has somewhere to put.
    private static List<PosedField> DrivenIn(ClipFrame frame, Doc.SceneDocument document, ClipScope scope)
    {
        var driven = new List<PosedField>();
        foreach (var (target, properties) in frame.Values)
        {
            if (document[scope.Resolve(target)] is not { } entity)
            {
                continue;
            }

            foreach (var property in properties.Keys)
            {
                var dot = property.IndexOf('.', StringComparison.Ordinal);
                if (dot <= 0 || entity[property[..dot]] is null)
                {
                    continue;
                }

                var field = new PosedField(entity.Id, property[..dot], property[(dot + 1)..]);
                if (!driven.Contains(field))
                {
                    driven.Add(field);
                }
            }
        }

        return driven;
    }

    private PoseChange Show(EditorScene scene, ClipFrame frame, ClipScope scope)
    {
        var ids = frame.Values.Keys.Select(scope.Resolve).Distinct().ToList();
        var document = new Doc.SceneDocument(
            entities: [.. ids.Select(id => scene[id]).OfType<SceneObject>().Select(SceneDocument.EntityOf)]);
        var driven = DrivenIn(frame, document, scope);

        var restoring = new List<Doc.SetField>();
        foreach (var field in _rest.Keys.ToList())
        {
            if (!driven.Contains(field))
            {
                _rest.Remove(field, out var rest);
                restoring.Add(Back(field, rest));
            }
        }

        foreach (var field in driven.Where(field => !_rest.ContainsKey(field)))
        {
            _rest[field] = document[field.Id]![field.Type]!.ToJson().GetValueOrDefault(field.Field);
        }

        return Write(scene, [.. restoring, .. ClipScene.OpsFor(frame, document, scope)]);
    }

    // Where an op puts a transform field, or null when it is not one.
    private static Vector3? VectorOf(SceneObject target, Doc.SetField op)
    {
        if (op.Type != Doc.SceneComponents.Transform)
        {
            return null;
        }

        return op.Field switch
        {
            "position" => target.Position,
            "rotation" => target.Rotation,
            "scale" => target.Scale,
            _ => null,
        };
    }

    // Writes ops into scene.
    //
    // Where something is goes straight into the row, which is all a drag does too. Anything else goes
    // through the document and back, the way an undo does, one object at a time.
    private static PoseChange Write(EditorScene scene, List<Doc.SetField> ops)
    {
        if (ops.Count == 0)
        {
            return PoseChange.None;
        }

        var change = PoseChange.Moved;
        var others = new OrderedDictionary<string, List<Doc.SetField>>();
        foreach (var op in ops.Where(op => scene[op.Id] is not null))
        {
            if (VectorOf(scene[op.Id]!, op) is { } vector)
            {
                vector.SetFrom(Doc.Values.Vector(op.To, op.Field == "scale" ? 1 : 0));
            }
            else if (others.TryGetValue(op.Id, out var list))
            {
                list.Add(op);
            }
            else
            {
                others[op.Id] = [op];
            }
        }

        foreach (var (id, changes) in others)
        {
            var target = scene[id]!;
            var document = new Doc.SceneDocument(entities: [SceneDocument.EntityOf(target)]);
            document = changes.Aggregate(document, (current, op) => op.ApplyTo(current));
            scene.Replace(SceneDocument.ObjectOf(document[id]!, target.RenderKey));
            change = PoseChange.Rebuilt;
        }

        scene.Invalidate();
        return change;
    }
}

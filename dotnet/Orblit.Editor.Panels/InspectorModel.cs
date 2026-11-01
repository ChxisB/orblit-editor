namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>What an inspector section is showing: one object, and what its fields need in order to change it.</summary>
public sealed class InspectorTarget
{
    /// <summary>The scene <see cref="Item"/> is in, by id, which is how a command names it.</summary>
    public required string SceneId { get; init; }

    public required EditorScene Scene { get; init; }

    public required SceneObject Item { get; init; }

    /// <summary>Where every change goes, so that undo knows about all of them.</summary>
    public required History History { get; init; }

    /// <summary>Shows one of the object's data objects in the project browser.</summary>
    public Action<string>? OnOpenData { get; init; }

    /// <summary>Takes one of the object's data objects off it, given the object's id and the path.</summary>
    public Action<string, string>? OnDetachData { get; init; }

    /// <summary>Goes to the modelling workspace, where a shape's geometry is changed.</summary>
    public Action? OnOpenTools { get; init; }

    /// <summary>How big a mesh is in its own space where the editor holds no geometry for it, such as an imported model.</summary>
    public Func<SceneObject, (Vector3 Min, Vector3 Max)?>? BoundsOf { get; init; }

    /// <summary>Reads the triangles an object draws, for a body that collides with them. Null where there is no project to read a model from.</summary>
    public Func<SceneObject, Task<CollisionMesh>>? CollisionOf { get; init; }

    /// <summary>What keys one of the object's fields into the clip being edited. Null where no clip can be.</summary>
    public IKeying? Keying { get; init; }

    /// <summary>Opens one of the object's own clips on the timeline.</summary>
    public Action<string>? OnOpenClip { get; init; }

    /// <summary>Gives the object the clip open on the timeline. Null while none is open.</summary>
    public Action? OnAttachClip { get; init; }

    /// <summary>
    /// Whether something it is inside hides it while it is shown itself. A different state from hidden
    /// here, and an object that says "shown" while nothing appears is worse than no control at all.
    /// </summary>
    public bool HiddenAbove => Item.Visible && !Scene.IsShown(Item.Id);
}

/// <summary>
/// One block of the inspector, and the objects it is shown for.
/// </summary>
/// <remarks>
/// Keyed by what an object is rather than listed in the inspector, so a body or a terrain brings its own
/// fields and the inspector does not have to know either of them exists. <typeparamref name="TView"/> is
/// whatever a window draws a section as, so the model holds no control.
/// </remarks>
/// <param name="Name">Unique among sections.</param>
/// <param name="AppliesTo">Whether the target's object has anything to show here.</param>
/// <param name="Build">The section itself.</param>
public sealed record InspectorSection<TView>(
    string Name,
    Func<InspectorTarget, bool> AppliesTo,
    Func<InspectorTarget, TView> Build) : IRegistered;

/// <summary>What the inspector decides about the fields it shows, apart from drawing them.</summary>
public static class InspectorModel
{
    /// <summary>The fewest layer names shown, so a scene that names nothing still offers somewhere to start.</summary>
    private const int LeastLayerRows = 4;

    /// <summary>
    /// A light level, at a precision that says something at both ends of the day. A night rounded to the
    /// nearest lux is a night that reads as zero.
    /// </summary>
    public static string Lux(double lux) => lux >= 10
        ? $"{Math.Round(lux, MidpointRounding.AwayFromZero).ToString("F0", CultureInfo.InvariantCulture)} lx"
        : $"{lux.ToString("F2", CultureInfo.InvariantCulture)} lx";

    /// <summary>An hour as a clock reads it.</summary>
    public static string Clock(double hour)
    {
        var total = (int)Math.Round(Doubles.Mod(hour, 24) * 60, MidpointRounding.AwayFromZero);
        var hours = total / 60 % 24;
        var minutes = total % 60;
        return $"{hours:00}:{minutes:00}";
    }

    /// <summary>
    /// The numbers with number <paramref name="index"/> moved by <paramref name="change"/>, and not
    /// below <paramref name="minimum"/> when there is one.
    /// </summary>
    /// <remarks>
    /// A floor, so a scale cannot be dragged through zero into a matrix that cannot be inverted, nor a
    /// ball into one with no size.
    /// </remarks>
    public static double[] Dragged(IReadOnlyList<double> values, int index, double change, double? minimum)
    {
        var next = values.ToArray();
        var moved = next[index] + change;
        next[index] = minimum is { } floor && moved < floor ? floor : moved;
        return next;
    }

    /// <summary>
    /// What the prefab band says under the name, or null when there is nothing to add: the instance
    /// itself needs no explaining.
    /// </summary>
    public static string? PrefabNote(string source, bool readable, string? partOf)
    {
        var name = Path.GetFileName(source);
        if (!readable)
        {
            return $"{name} could not be read. Until it can, this is only a link.";
        }

        return partOf is null ? null : $"Part of {partOf}. Changes here are changes to that instance.";
    }

    /// <summary>
    /// The instance <paramref name="target"/> is, or the outermost one it is a part of, which is the one
    /// a scene links to, and so the one the prefab band acts on. Null for anything that is not in one.
    /// </summary>
    public static SceneObject? InstanceHolding(EditorScene scene, SceneObject target)
    {
        var head = EntityPath.InstanceOf(target.Id);
        var instance = head is null ? target : scene[head];
        return instance?.Prefab?.Asset is null ? null : instance;
    }

    /// <summary>
    /// How many layer name fields to show: as many as the names reach and one more to type the next
    /// into, because thirty-two fields down a side panel would bury the ones somebody uses.
    /// </summary>
    public static int LayerRows(IReadOnlyList<string> names, bool all)
    {
        if (all)
        {
            return SceneSettings.LayerCount;
        }

        var named = -1;
        for (var index = 0; index < names.Count; index++)
        {
            if (names[index].Length > 0)
            {
                named = index;
            }
        }

        return Math.Min(SceneSettings.LayerCount, Math.Max(LeastLayerRows, named + 2));
    }

    /// <summary>
    /// The names with layer <paramref name="index"/> called <paramref name="name"/>, and no empty names
    /// left hanging off the end, which is how a file that names nothing is written.
    /// </summary>
    public static List<string> RenamedLayers(IReadOnlyList<string> names, int index, string name)
    {
        var next = names.ToList();
        while (next.Count <= index)
        {
            next.Add("");
        }

        next[index] = name.Trim();
        while (next.Count > 0 && next[^1].Length == 0)
        {
            next.RemoveAt(next.Count - 1);
        }

        return next;
    }

    /// <summary>What each section that applies to <paramref name="target"/> builds, first to last.</summary>
    public static List<TView> Shown<TView>(IEnumerable<InspectorSection<TView>> sections, InspectorTarget target) =>
        [.. sections.Where(section => section.AppliesTo(target)).Select(section => section.Build(target))];
}

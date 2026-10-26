namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// What the inspector asks the rest of the editor to do for it. Each is null where nothing can do it,
/// such as a window with no project to write to, and the control that would ask is then disabled or
/// left out.
/// </summary>
internal sealed record InspectorLinks
{
    /// <summary>Shows one of the object's data objects in the project browser.</summary>
    public Action<string>? OnOpenData { get; init; }

    /// <summary>Takes a data object off an object, given the object's id and the path.</summary>
    public Action<string, string>? OnDetachData { get; init; }

    /// <summary>Opens the interface a canvas puts on screen.</summary>
    public Action<string>? OnOpenInterface { get; init; }

    /// <summary>How big a mesh is in its own space where the editor holds no geometry for it, for fitting a body to it.</summary>
    public Func<SceneObject, (Vector3 Min, Vector3 Max)?>? BoundsOf { get; init; }

    /// <summary>Saves what was changed in an instance into its prefab, given the selected object's id.</summary>
    public Action<string>? OnApplyPrefab { get; init; }

    public Action<string>? OnRevertPrefab { get; init; }

    public Action<string>? OnUnpackPrefab { get; init; }
}

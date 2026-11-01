namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;

/// <summary>
/// What a gizmo is being asked about: the object the handles sit on, and the view it is being
/// drawn in.
/// </summary>
/// <param name="Item">The one of the selection the handles belong to.</param>
/// <param name="Scene">The scene holding the object: the open one, or the set every scene shares.</param>
/// <param name="Selected">Everything selected, the object included.</param>
/// <param name="Projection">How the view's pixels map into the world.</param>
public sealed record GizmoTarget(
    SceneObject Item,
    EditorScene Scene,
    IReadOnlySet<string> Selected,
    ViewportProjection Projection);

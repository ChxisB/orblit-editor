namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;

// The view every gizmo test draws into, and a target for a selection of a scene.
internal static class GizmoView
{
    public static readonly ViewportProjection Projection = new(new OrbitCamera(), new Size(800, 600));

    public static GizmoTarget TargetOf(EditorScene scene, params string[] selected) =>
        new(scene[selected[0]]!, scene, selected.ToHashSet(), Projection);
}

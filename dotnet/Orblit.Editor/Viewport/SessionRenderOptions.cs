namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;

/// <summary>What every view of a scene tells the renderer about the project around it.</summary>
internal static class SessionRenderOptions
{
    /// <summary>
    /// The options for drawing a scene of <paramref name="workspace"/>. A playing scene is a copy with a
    /// workspace of its own, so the shared set it is drawn with is that copy's.
    /// </summary>
    public static SceneRenderOptions Of(EditorSession session, Workspace workspace, GeometryStore geometry) => new()
    {
        ProjectRoot = workspace.ProjectDirectory,
        Shared = workspace.Shared,
        GeometryOf = geometry.PathFor,
        SurfaceOf = session.Surfaces.SurfaceFor,
    };
}

namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Filament;

/// <summary>
/// The line the renderer draws round what is selected, worked out from the editor's selection.
///
/// Drawn by the renderer rather than painted over the picture because only the renderer knows
/// what is in front of what. A box projected over the texture cannot tell a selected crate behind
/// a wall from one in front of it; the renderer's outline follows the silhouette, and draws the
/// part the wall hides fainter and dashed, so the crate is findable without looking as if it were
/// in the room.
///
/// Only objects the renderer draws can have one: meshes and shapes. A light, a camera or a group
/// has no silhouette, and the handles already say where it is. The primary is the object the
/// inspector is showing and the handles sit on, and gets the brighter colour. That is the same
/// convention most 3D tools use, so it means something before anybody has read about it.
/// </summary>
public static class SelectionOutline
{
    /// <summary>
    /// The outline for <paramref name="selected"/>. Objects in the project's shared set are drawn
    /// alongside every scene, so they are looked up there as well as in the scene that is open.
    /// </summary>
    public static Outline Of(
        EditorScene? scene,
        IEnumerable<string> selected,
        EditorScene? shared = null,
        string? primary = null)
    {
        var keys = selected.Select(id => KeyOf(scene, shared, id)).OfType<long>().Distinct().ToList();
        var active = KeyOf(scene, shared, primary);
        return keys.Count == 0 && active is null
            ? Outline.None
            : new Outline { Keys = keys, Primary = active };
    }

    private static long? KeyOf(EditorScene? scene, EditorScene? shared, string? id)
    {
        if (id is null)
        {
            return null;
        }

        var found = scene?[id] ?? shared?[id];
        return found is { IsDrawable: true } ? found.RenderKey : null;
    }
}

namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;

/// <summary>Where a dragged row would land if it were dropped now.</summary>
public enum DropKind
{
    /// <summary>Above the row it is over, as a sibling.</summary>
    Before,

    /// <summary>Inside it, as a child.</summary>
    Inside,

    /// <summary>Below it, as a sibling.</summary>
    After,
}

/// <summary>One row of the flattened tree. A null <see cref="Item"/> is a scene's own row.</summary>
public sealed record OutlinerRow(SceneEntry Entry, SceneObject? Item, int Depth, bool HasChildren)
{
    /// <summary>What a fold is remembered under: the object's id, or the scene's.</summary>
    public string Key => Item?.Id ?? Entry.Id;
}

/// <summary>
/// What the hierarchy shows: every open scene as a root with its objects under it, so several can be
/// worked on at once and it is always clear which one a thing belongs to.
/// </summary>
public sealed class OutlinerModel(Workspace workspace)
{
    // Collapsed rather than expanded, so a fresh scene has nothing in it and a newly added object is
    // visible and not inside a closed parent.
    private readonly HashSet<string> _collapsed = [];

    /// <summary>
    /// While it holds something, every match is shown whatever was collapsed: a match inside a closed
    /// parent is a match nobody can see.
    /// </summary>
    public string Filter { get; set; } = "";

    public int SceneCount => workspace.Entries.Count;

    public bool IsCollapsed(string key) => _collapsed.Contains(key);

    public void Toggle(string key)
    {
        if (!_collapsed.Remove(key))
        {
            _collapsed.Add(key);
        }
    }

    /// <summary>Rows in draw order, skipping anything inside a collapsed parent.</summary>
    public IReadOnlyList<OutlinerRow> Rows()
    {
        var query = Filter.Trim();

        // Shared first, because what every scene has comes before whichever one is open.
        return [.. new[] { workspace.SharedEntry }.Concat(workspace.Entries).SelectMany(entry => RowsOf(entry, query))];
    }

    /// <summary>Turns a drop on a row into a place in the tree.</summary>
    public static Drop PlaceFor(OutlinerRow row, DropKind kind)
    {
        var scene = row.Entry.Scene!;

        // Onto a scene's own row: into that scene, at the top level.
        if (row.Item is not { } one)
        {
            return new Drop(row.Entry.Id, null, kind == DropKind.Before ? 0 : scene.Roots.Count);
        }

        if (kind == DropKind.Inside)
        {
            return new Drop(row.Entry.Id, one.Id, scene.ChildrenOf(one.Id).Count);
        }

        var at = scene.IndexOf(one.Id);
        return new Drop(row.Entry.Id, one.ParentId, kind == DropKind.Before ? at : at + 1);
    }

    /// <summary>
    /// Whether a row can be dropped on. An object cannot go into itself or into what it holds, and
    /// nothing goes into a scene that is not loaded.
    /// </summary>
    public bool Accepts(OutlinerRow row, string draggedId)
    {
        if (row.Entry.Scene is not { } scene)
        {
            return false;
        }

        if (row.Item is not { } onto)
        {
            return true;
        }

        if (draggedId == onto.Id || workspace.SceneHolding(draggedId) is not { } holder)
        {
            return false;
        }

        return holder.Id != row.Entry.Id || !scene.IsAncestorOf(draggedId, onto.Id);
    }

    /// <summary>
    /// Which part of a row a pointer is over, from how far down it is (0 at the top, 1 at the foot). A
    /// scene's row only takes things in, and the edges of an object's row are above and below it.
    /// </summary>
    public static DropKind BandAt(OutlinerRow row, double fraction) => row.Item switch
    {
        null => DropKind.Inside,
        _ when fraction < 0.25 => DropKind.Before,
        _ when fraction > 0.75 => DropKind.After,
        _ => DropKind.Inside,
    };

    // One scene's own row, then its objects unless it is closed.
    private List<OutlinerRow> RowsOf(SceneEntry entry, string query)
    {
        var scene = entry.Scene;
        var kept = scene is null || query.Length == 0 ? null : KeptBy(scene, query);
        if (!IsListed(entry, query, kept))
        {
            return [];
        }

        var rows = new List<OutlinerRow> { new(entry, null, 0, scene is not null && scene.SiblingsOf(null).Count > 0) };

        // An unloaded scene has no objects to show. Listing children it does not hold would suggest
        // they could be edited, and they cannot.
        if (scene is not null && (kept is not null || !_collapsed.Contains(entry.Id)))
        {
            var pass = new Pass(entry, scene, kept, rows);
            Walk(pass, ShownUnder(pass, null), 1);
        }

        return rows;
    }

    // A scene is listed when nothing is filtered, or its own name matches, or one of its objects does.
    private static bool IsListed(SceneEntry entry, string query, HashSet<string>? kept) =>
        query.Length == 0 || entry.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || kept is { Count: > 0 };

    private void Walk(Pass pass, List<SceneObject> objects, int depth)
    {
        foreach (var one in objects)
        {
            var children = ShownUnder(pass, one.Id);
            pass.Rows.Add(new OutlinerRow(pass.Entry, one, depth, children.Count > 0));
            if (pass.Kept is not null || !_collapsed.Contains(one.Id))
            {
                Walk(pass, children, depth + 1);
            }
        }
    }

    private static List<SceneObject> ShownUnder(Pass pass, string? parentId) =>
        [.. pass.Scene.SiblingsOf(parentId).Where(one => pass.Kept is null || pass.Kept.Contains(one.Id))];

    // The objects whose names hold the query, and every parent above them, so a match shows where it
    // sits in the tree and not loose.
    private static HashSet<string> KeptBy(EditorScene scene, string query)
    {
        var kept = new HashSet<string>();
        foreach (var one in scene.Objects.Where(one => scene.DisplayNameOf(one).Contains(query, StringComparison.OrdinalIgnoreCase)))
        {
            string? id = one.Id;
            while (id is not null && kept.Add(id))
            {
                id = scene[id]?.ParentId;
            }
        }

        return kept;
    }

    // What one scene's walk shares: where the rows go, who is whose child, and what a filter kept.
    private sealed record Pass(
        SceneEntry Entry,
        EditorScene Scene,
        HashSet<string>? Kept,
        List<OutlinerRow> Rows);
}

namespace Orblit.Editor.Core;

using System.Diagnostics;
using Orblit.Mesh;

/// <summary>
/// Which shelf an action sits on.
/// </summary>
/// <remarks>
/// Grouped because twenty-five buttons in a column is a list nobody reads, and the three groups are
/// not arbitrary: one changes what is selected, one changes the part of the shape that is selected
/// and one changes the whole object. Knowing which of those a button will do is most of knowing
/// whether to press it.
/// </remarks>
public enum ToolGroup
{
    /// <summary>Changes the selection without touching the geometry.</summary>
    Selection,

    /// <summary>Changes the selected part of the shape.</summary>
    Geometry,

    /// <summary>Changes the whole shape, selection or no selection.</summary>
    Whole,
}

public static class ToolGroups
{
    public static string Label(this ToolGroup group) => group switch
    {
        ToolGroup.Selection => "Selection",
        ToolGroup.Geometry => "Geometry",
        ToolGroup.Whole => "Whole shape",
        _ => throw new UnreachableException(),
    };
}

/// <summary>The number an action takes: a distance, a count.</summary>
public sealed record ToolAmount(string Label, double Value, double Min, double Max);

/// <summary>
/// One thing somebody can do to geometry.
/// </summary>
/// <remarks>
/// A description rather than a method call, so the same list draws the menu, answers whether it can
/// be pressed, and runs. Three copies of "which mode is Extrude available in" is two copies too many.
/// </remarks>
public sealed record MeshAction
{
    public required string Label { get; init; }

    /// <summary>The name of its icon.</summary>
    public required string Icon { get; init; }

    /// <summary>Which modes it appears in. Empty means the whole object.</summary>
    public required IReadOnlyList<ElementMode> Modes { get; init; }

    /// <summary>
    /// Does it, and says what should be selected afterwards. The mesh is changed in place and
    /// handed back, because a mesh is a few thousand numbers and copying one is nothing next to a
    /// frame, while describing an extrude as a diff is more code than the extrude.
    /// </summary>
    public required Func<Mesh, ElementSelection, double, ElementSelection> Run { get; init; }

    /// <summary>Which shelf it sits on.</summary>
    public ToolGroup Group { get; init; } = ToolGroup.Geometry;

    /// <summary>How many elements it needs. Bridging needs two edges, extruding needs one face.</summary>
    public int Needs { get; init; } = 1;

    /// <summary>What it says on the tooltip.</summary>
    public string Hint { get; init; } = "";

    public ToolAmount? Amount { get; init; }

    public bool AvailableIn(ElementMode? mode, ElementSelection selection)
    {
        if (Modes.Count == 0)
        {
            return true;
        }

        return mode is { } at && Modes.Contains(at) && selection.CountIn(at) >= Needs;
    }
}

/// <summary>
/// Everything the editor can do to a mesh.
/// </summary>
/// <remarks>
/// The set ProBuilder has, less the ones that need something this engine does not have yet: there
/// are no colliders to set, no submeshes to detach to, and no UV editor for the ones that are really
/// about texture coordinates. What is here is the geometry.
/// </remarks>
public static class MeshTools
{
    public static IReadOnlyList<MeshAction> All { get; } =
    [
        // Faces.
        new MeshAction
        {
            Label = "Extrude",
            Icon = "open_in_full",
            Modes = [ElementMode.Face],
            Hint = "Pulls the face out and puts a wall on every edge of it. The move everything from a doorway to a chimney is made of.",
            Amount = new("Distance", 0.5, -4, 4),
            Run = OnFaces((mesh, faces, amount) => mesh.Extrude(faces, amount)),
        },
        new MeshAction
        {
            Label = "Inset",
            Icon = "filter_frames",
            Modes = [ElementMode.Face],
            Hint = "Shrinks the face towards its middle, leaving a border. Inset then extrude inwards is a window.",
            Amount = new("Distance", 0.15, 0.01, 2),
            Run = OnFaces((mesh, faces, amount) => mesh.Inset(faces, amount)),
        },
        new MeshAction
        {
            Label = "Bevel",
            Icon = "rounded_corner",
            Modes = [ElementMode.Face, ElementMode.Edge],
            Hint = "Cuts the corner off, leaving a face where the edge was.",
            Amount = new("Distance", 0.1, 0.01, 2),
            Run = (mesh, selection, amount) =>
            {
                IEnumerable<(int, int)> edges = selection.Edges.Count > 0 ? selection.Edges : MeshEdits.EdgesOf(selection.FacesIn(mesh));
                return new ElementSelection(faces: PositionsOf(mesh, mesh.Bevel(edges, amount)));
            },
        },
        new MeshAction
        {
            Label = "Subdivide",
            Icon = "grid_4x4",
            Modes = [ElementMode.Face],
            Hint = "Cuts each face into four, so there is something to pull on.",
            Run = OnFaces((mesh, faces, _) => mesh.Subdivide(faces)),
        },
        new MeshAction
        {
            Label = "Delete",
            Icon = "delete_outline",
            Modes = [ElementMode.Face],
            Hint = "Takes the face out, leaving a hole.",
            Run = (mesh, selection, _) =>
            {
                mesh.DeleteFaces(selection.FacesIn(mesh));
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Flip normals",
            Icon = "flip",
            Modes = [ElementMode.Face],
            Hint = "Turns the face the other way, so it is visible from the side it was not.",
            Run = (mesh, selection, _) =>
            {
                var faces = selection.FacesIn(mesh);
                mesh.FlipFaces(faces);
                return new ElementSelection(faces: PositionsOf(mesh, faces));
            },
        },
        new MeshAction
        {
            Label = "Merge",
            Icon = "join_full",
            Modes = [ElementMode.Face],
            Needs = 2,
            Hint = "Joins the faces into one and drops the edges between them.",
            Run = (mesh, selection, _) => mesh.MergeFaces(selection.FacesIn(mesh)) is { } made
                ? new ElementSelection(faces: PositionsOf(mesh, [made]))
                : selection,
        },
        new MeshAction
        {
            Label = "Triangulate",
            Icon = "change_history",
            Modes = [ElementMode.Face],
            Hint = "Cuts the faces down to triangles.",
            Run = OnFaces((mesh, faces, _) => mesh.TriangulateFaces(faces)),
        },
        new MeshAction
        {
            Label = "Grow",
            Group = ToolGroup.Selection,
            Icon = "zoom_out_map",
            Modes = [ElementMode.Face],
            Hint = "Takes in the neighbours. The angle keeps it on the flat rather than round a corner.",
            Amount = new("Within", 180, 0, 180),
            Run = OnFaces((mesh, faces, amount) => mesh.Grow(faces, amount >= 179.5 ? null : amount)),
        },
        new MeshAction
        {
            Label = "Shrink",
            Group = ToolGroup.Selection,
            Icon = "zoom_in_map",
            Modes = [ElementMode.Face],
            Needs = 2,
            Hint = "Drops the faces round the edge of the selection.",
            Run = OnFaces((_, faces, _) => MeshActions.Shrink(faces)),
        },
        new MeshAction
        {
            Label = "Loop",
            Group = ToolGroup.Selection,
            Icon = "all_inclusive",
            Modes = [ElementMode.Face],
            Hint = "Selects the ring of faces this one runs round. Quads only.",
            Run = (mesh, selection, _) => selection.Faces.Count == 0
                ? selection
                : new ElementSelection(faces: PositionsOf(mesh, selection.FacesIn(mesh).SelectMany(face => mesh.FaceLoop(face)))),
        },

        // Edges.
        new MeshAction
        {
            Label = "Bridge",
            Icon = "compare_arrows",
            Modes = [ElementMode.Edge],
            Needs = 2,
            Hint = "Puts a face between two open edges, joining two pieces.",
            Run = (mesh, selection, _) =>
            {
                // Guarded here as well as by Needs. That decides what the menu offers. This is what
                // happens when something calls it anyway, such as a shortcut, a script or a test,
                // and a range error two frames from here is a worse answer than doing nothing.
                var edges = selection.Edges.Take(2).ToList();
                if (edges.Count < 2)
                {
                    return selection;
                }

                return mesh.Bridge(edges[0], edges[1]) is { } made
                    ? new ElementSelection(faces: PositionsOf(mesh, [made]))
                    : selection;
            },
        },
        new MeshAction
        {
            Label = "Subdivide edge",
            Icon = "linear_scale",
            Modes = [ElementMode.Edge],
            Hint = "Cuts the edge into pieces, and puts the new points into every face using it.",
            Amount = new("Cuts", 1, 1, 8),
            Run = (mesh, selection, amount) =>
                new ElementSelection(vertices: mesh.SubdivideEdges(selection.Edges, (int)Math.Round(amount, MidpointRounding.AwayFromZero))),
        },
        new MeshAction
        {
            Label = "Edge loop",
            Group = ToolGroup.Selection,
            Icon = "rotate_right",
            Modes = [ElementMode.Edge],
            Hint = "Selects the loop running through this edge. Quads only.",
            Run = (mesh, selection, _) =>
                new ElementSelection(edges: selection.Edges.SelectMany(edge => mesh.EdgeLoop(edge))),
        },
        new MeshAction
        {
            Label = "Edge ring",
            Group = ToolGroup.Selection,
            Icon = "swap_horiz",
            Modes = [ElementMode.Edge],
            Hint = "Selects the ring of edges parallel to this one. Quads only.",
            Run = (mesh, selection, _) =>
                new ElementSelection(edges: selection.Edges.SelectMany(edge => mesh.EdgeRing(edge))),
        },

        // Vertices.
        new MeshAction
        {
            Label = "Weld",
            Icon = "compress",
            Modes = [ElementMode.Vertex],
            Needs = 2,
            Hint = "Joins points that are already in the same place.",
            Amount = new("Within", 0.01, 0.0001, 1),
            Run = (mesh, _, amount) =>
            {
                mesh.Weld(amount);
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Collapse",
            Icon = "center_focus_strong",
            Modes = [ElementMode.Vertex],
            Needs = 2,
            Hint = "Pulls the points together into one, however far apart they are.",
            Run = (mesh, selection, _) =>
            {
                mesh.Collapse(selection.Vertices);
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Split",
            Icon = "call_split",
            Modes = [ElementMode.Vertex],
            Hint = "Gives each face its own copy of the point, so they come apart.",
            Run = (mesh, selection, _) =>
            {
                mesh.Split(selection.Vertices);
                return selection;
            },
        },
        new MeshAction
        {
            Label = "Fill hole",
            Icon = "format_color_fill",
            Modes = [ElementMode.Vertex, ElementMode.Edge],
            Hint = "Puts a face over the hole these are on the edge of.",
            Run = (mesh, selection, _) =>
            {
                IEnumerable<int> around = selection.Vertices.Count > 0
                    ? selection.Vertices
                    : selection.Edges.SelectMany(edge => new[] { edge.Item1, edge.Item2 });
                return new ElementSelection(faces: PositionsOf(mesh, mesh.FillHole(around)));
            },
        },

        // The whole object.
        new MeshAction
        {
            Label = "Subdivide all",
            Group = ToolGroup.Whole,
            Icon = "grid_on",
            Modes = [],
            Hint = "Cuts every face into four.",
            Run = (mesh, _, _) =>
            {
                mesh.Subdivide([.. mesh.Faces]);
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Conform normals",
            Group = ToolGroup.Whole,
            Icon = "compass_calibration",
            Modes = [],
            Hint = "Turns the odd face back the way the rest of them point. What fixes a shape that came in partly inside out.",
            Run = (mesh, selection, _) =>
            {
                mesh.ConformNormals();
                return selection;
            },
        },
        new MeshAction
        {
            Label = "Flip all normals",
            Group = ToolGroup.Whole,
            Icon = "swap_vert",
            Modes = [],
            Hint = "Turns the whole shape inside out, which is how a box becomes a room.",
            Run = (mesh, selection, _) =>
            {
                mesh.Flip();
                return selection;
            },
        },
        new MeshAction
        {
            Label = "Weld all",
            Group = ToolGroup.Whole,
            Icon = "merge_type",
            Modes = [],
            Hint = "Joins every pair of points in the same place, so pieces that touch become one solid.",
            Amount = new("Within", 0.001, 0.0001, 1),
            Run = (mesh, _, amount) =>
            {
                mesh.Weld(amount);
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Triangulate all",
            Group = ToolGroup.Whole,
            Icon = "details",
            Modes = [],
            Hint = "Cuts every face down to triangles.",
            Run = (mesh, _, _) =>
            {
                mesh.TriangulateFaces([.. mesh.Faces]);
                return new ElementSelection();
            },
        },
        new MeshAction
        {
            Label = "Centre pivot",
            Group = ToolGroup.Whole,
            Icon = "filter_center_focus",
            Modes = [],
            Hint = "Moves the middle of the object to where it turns and scales around.",
            Run = (mesh, selection, _) =>
            {
                mesh.CentrePivotOn([]);
                return selection;
            },
        },
    ];

    /// <summary>The ones that can be pressed right now.</summary>
    public static List<MeshAction> AvailableIn(ElementMode? mode, ElementSelection selection) =>
        [.. All.Where(action => action.AvailableIn(mode, selection))];

    // The same three steps for every action that works on the selected faces and selects what it
    // made: read the faces, do it, find where the results ended up.
    private static Func<Mesh, ElementSelection, double, ElementSelection> OnFaces(
        Func<Mesh, List<Face>, double, IEnumerable<Face>> make) =>
        (mesh, selection, amount) => new ElementSelection(faces: PositionsOf(mesh, make(mesh, selection.FacesIn(mesh), amount)));

    // Faces by position, for the selection. Looked up after the edit, because the edit moves them.
    private static List<int> PositionsOf(Mesh mesh, IEnumerable<Face> faces)
    {
        var wanted = faces.ToHashSet();
        return [.. Enumerable.Range(0, mesh.Faces.Count).Where(at => wanted.Contains(mesh.Faces[at]))];
    }
}

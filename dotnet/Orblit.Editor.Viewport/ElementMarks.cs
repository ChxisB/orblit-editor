namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Mesh;
using MeshEdge = (int, int);

/// <summary>
/// The mesh being edited as marks on the screen: its wire, then what is chosen and what is under the
/// pointer, in the kind of part the mode picks.
/// </summary>
public static class ElementMarks
{
    /// <summary>The marks for the mesh being edited, or none when nothing is.</summary>
    public static List<OverlayMark> Of(SceneViewportState state, ViewportProjection projection)
    {
        if (state.Editing is not { } editing)
        {
            return [];
        }

        var lens = new Lens(editing, projection);
        var chosen = state.ElementSelection;
        var hovered = state.HoveredElement;
        List<OverlayMark> marks = [.. Lines(OverlayStyle.Wire, lens.Lines(editing.Mesh.AllEdges))];
        marks.AddRange(state.ElementMode switch
        {
            ElementMode.Face => Faces(lens, chosen, hovered),
            ElementMode.Edge => Edges(lens, chosen, hovered),
            _ => Vertices(lens, chosen, hovered),
        });
        return marks;
    }

    private static List<OverlayMark> Faces(Lens lens, ElementSelection chosen, ElementSelection? hovered)
    {
        var marks = new List<OverlayMark>();
        var faces = lens.Editing.Mesh.Faces;
        for (var index = 0; index < faces.Count; index++)
        {
            var isChosen = chosen.Faces.Contains(index);
            if (!isChosen && hovered?.Faces.Contains(index) != true)
            {
                continue;
            }

            var corners = lens.Corners(faces[index]);
            if (corners.Count > 0)
            {
                marks.Add(new PolygonMark(isChosen ? OverlayStyle.FaceChosen : OverlayStyle.FaceHovered, corners));
            }
        }

        return marks;
    }

    private static OverlayMark[] Edges(Lens lens, ElementSelection chosen, ElementSelection? hovered)
    {
        var picked = new List<MeshEdge>();
        var under = new List<MeshEdge>();
        foreach (var edge in lens.Editing.Mesh.AllEdges)
        {
            if (chosen.Edges.Contains(edge))
            {
                picked.Add(edge);
            }
            else if (hovered?.Edges.Contains(edge) == true)
            {
                under.Add(edge);
            }
        }

        return [.. Lines(OverlayStyle.EdgeChosen, lens.Lines(picked)), .. Lines(OverlayStyle.EdgeHovered, lens.Lines(under))];
    }

    private static List<OverlayMark> Vertices(Lens lens, ElementSelection chosen, ElementSelection? hovered)
    {
        var marks = new List<OverlayMark>();
        for (var index = 0; index < lens.Editing.Mesh.Positions.Count; index++)
        {
            if (lens.At(index) is { } at)
            {
                marks.Add(new DotMark(VertexStyle(chosen.Vertices.Contains(index), hovered?.Vertices.Contains(index) == true), at));
            }
        }

        return marks;
    }

    private static OverlayStyle VertexStyle(bool chosen, bool under)
    {
        if (chosen)
        {
            return OverlayStyle.VertexChosen;
        }

        return under ? OverlayStyle.VertexHovered : OverlayStyle.Vertex;
    }

    // Nothing at all and not an empty mark, so the view has no path to build for it.
    private static OverlayMark[] Lines(OverlayStyle style, List<(Offset From, Offset To)> lines) =>
        lines.Count == 0 ? [] : [new LineMark(style, lines)];

    // Where each corner of the mesh lands on the screen. One behind the camera has no place, and is left out.
    private sealed record Lens(EditingMesh Editing, ViewportProjection Projection)
    {
        public Offset? At(int index)
        {
            var positions = Editing.Mesh.Positions;
            return index < 0 || index >= positions.Count
                ? null
                : Projection.Project(Editing.Transform.Transformed3(positions[index]));
        }

        public List<Offset> Corners(Face face) => [.. face.Vertices.Select(At).OfType<Offset>()];

        public List<(Offset From, Offset To)> Lines(IEnumerable<MeshEdge> edges)
        {
            var lines = new List<(Offset, Offset)>();
            foreach (var (a, b) in edges)
            {
                if (At(a) is { } from && At(b) is { } to)
                {
                    lines.Add((from, to));
                }
            }

            return lines;
        }
    }
}

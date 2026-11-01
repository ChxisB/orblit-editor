namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;

/// <summary>
/// The outline or path being drawn as marks on the screen, drawn by the view and not by the renderer
/// so a line projected with the same camera says honestly where the point is.
/// </summary>
public static class SketchMarks
{
    /// <summary>The marks for what has been put down so far, or none while no tool is drawing.</summary>
    public static List<OverlayMark> Of(Sketch sketch, ViewportProjection projection)
    {
        if (sketch.Tool == ViewportTool.None)
        {
            return [];
        }

        var placed = sketch.Points.Select(projection.Project).ToList();
        var reaching = sketch.Hovering is { } hovering ? projection.Project(hovering) : null;
        List<OverlayMark> marks = [];
        AddLines(marks, OverlayStyle.Placed, Between(placed));
        AddLines(marks, OverlayStyle.Ghost, Ghost(sketch.Tool, placed, reaching));
        marks.AddRange(Dots(sketch.Tool, placed, reaching));
        return marks;
    }

    private static void AddLines(List<OverlayMark> marks, OverlayStyle style, List<(Offset From, Offset To)> lines)
    {
        if (lines.Count > 0)
        {
            marks.Add(new LineMark(style, lines));
        }
    }

    // A point behind the camera has no place, so the pieces on either side of it are left out.
    private static List<(Offset From, Offset To)> Between(List<Offset?> placed)
    {
        var lines = new List<(Offset, Offset)>();
        for (var index = 0; index + 1 < placed.Count; index++)
        {
            if (placed[index] is { } from && placed[index + 1] is { } to)
            {
                lines.Add((from, to));
            }
        }

        return lines;
    }

    // And back to the start for a shape, so the shape somebody is about to close is the shape they can see.
    private static List<(Offset From, Offset To)> Ghost(ViewportTool tool, List<Offset?> placed, Offset? reaching)
    {
        if (reaching is not { } to || placed.Count == 0 || placed[^1] is not { } last)
        {
            return [];
        }

        List<(Offset From, Offset To)> lines = [(last, to)];
        if (tool == ViewportTool.PolyShape && placed.Count >= 2 && placed[0] is { } first)
        {
            lines.Add((to, first));
        }

        return lines;
    }

    private static List<OverlayMark> Dots(ViewportTool tool, List<Offset?> placed, Offset? reaching)
    {
        var marks = new List<OverlayMark>();
        for (var index = 0; index < placed.Count; index++)
        {
            if (placed[index] is not { } at)
            {
                continue;
            }

            marks.Add(new DotMark(OverlayStyle.Point, at));
            if (index == 0 && tool == ViewportTool.PolyShape)
            {
                marks.Add(new DotMark(OverlayStyle.Ring, at));
            }
        }

        if (reaching is { } pointer)
        {
            marks.Add(new DotMark(OverlayStyle.Reach, pointer));
        }

        return marks;
    }
}

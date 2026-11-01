namespace Orblit.Editor.Viewport;

/// <summary>What a mark is for. The view picks the colour, the width and the radius of each.</summary>
public enum OverlayStyle
{
    /// <summary>Every edge of the mesh being edited, faint, so its shape reads where nothing is chosen.</summary>
    Wire,
    FaceChosen,
    FaceHovered,
    EdgeChosen,
    EdgeHovered,
    Vertex,
    VertexChosen,
    VertexHovered,

    /// <summary>A piece of the outline being drawn that has both ends put down.</summary>
    Placed,

    /// <summary>The piece still following the pointer, lighter so it reads as not yet placed.</summary>
    Ghost,
    Point,

    /// <summary>Round the first point of a shape, because clicking it again finishes the shape.</summary>
    Ring,

    /// <summary>Where the pointer would put the next point.</summary>
    Reach,
}

/// <summary>One thing to draw over the render, already on the screen.</summary>
public abstract record OverlayMark(OverlayStyle Style);

public sealed record LineMark(OverlayStyle Style, IReadOnlyList<(Offset From, Offset To)> Lines) : OverlayMark(Style);

public sealed record PolygonMark(OverlayStyle Style, IReadOnlyList<Offset> Corners) : OverlayMark(Style);

public sealed record DotMark(OverlayStyle Style, Offset At) : OverlayMark(Style);

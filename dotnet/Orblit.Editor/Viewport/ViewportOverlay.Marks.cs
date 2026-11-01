using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Viewport;
using ViewOffset = Orblit.Editor.Viewport.Offset;
using ViewSize = Orblit.Editor.Viewport.Size;

// The mesh being edited, the outline being drawn and the box being dragged. The marks are made in the
// viewport project, where they are tested, and only given a colour here.
internal sealed partial class ViewportOverlay
{
    private static readonly IBrush MarqueeFill = Tone(0x22E5893F);
    private static readonly Pen MarqueeEdge = new(Tone(0xCCE5893F), 1);

    // What each kind of mark looks like. A radius is for a dot, and a pen with no fill is a ring.
    private static readonly Dictionary<OverlayStyle, Look> Looks = new()
    {
        [OverlayStyle.Wire] = new(null, Line(0x66FFFFFF, 1)),
        [OverlayStyle.FaceChosen] = new(Tone(0x55E5893F), Line(0xFFE5893F, 2)),
        [OverlayStyle.FaceHovered] = new(Tone(0x22E5893F), null),
        [OverlayStyle.EdgeChosen] = new(null, Line(0xFFE5893F, 3)),
        [OverlayStyle.EdgeHovered] = new(null, Line(0x99E5893F, 3)),
        [OverlayStyle.Vertex] = new(Tone(0xCCD8DEE8), null, 3),
        [OverlayStyle.VertexChosen] = new(Tone(0xFFE5893F), null, 4.5),
        [OverlayStyle.VertexHovered] = new(Tone(0xCCD8DEE8), null, 4.5),
        [OverlayStyle.Placed] = new(null, Line(0xFFE5893F, 2)),
        [OverlayStyle.Ghost] = new(null, Line(0x88E5893F, 1.5)),
        [OverlayStyle.Point] = new(Tone(0xFFE5893F), null, 3.5),
        [OverlayStyle.Ring] = new(null, Line(0xFFF3C08A, 2), 6.5),
        [OverlayStyle.Reach] = new(Tone(0x99F3C08A), null, 2.5),
    };

    private static ImmutableSolidColorBrush Tone(uint argb) => new(Color.FromUInt32(argb));

    private static Pen Line(uint argb, double width) => new(Tone(argb), width);

    // shortcut: the wire is rebuilt from the faces on every paint. Cache it on the mesh when a modelled mesh
    // with thousands of faces drags.
    private void DrawMarks(DrawingContext context)
    {
        var projection = new ViewportProjection(_state.Camera, new ViewSize(Bounds.Width, Bounds.Height));
        Draw(context, ElementMarks.Of(_state, projection));
        if (_state.Sketch is { } sketch)
        {
            Draw(context, SketchMarks.Of(sketch, projection));
        }

        if (_state.Marquee is { } box)
        {
            context.DrawRectangle(MarqueeFill, MarqueeEdge, new Avalonia.Rect(box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top));
        }
    }

    private static void Draw(DrawingContext context, IEnumerable<OverlayMark> marks)
    {
        foreach (var mark in marks)
        {
            var look = Looks[mark.Style];
            switch (mark)
            {
                case LineMark line:
                    context.DrawGeometry(null, look.Pen, Path(line.Lines));
                    break;
                case PolygonMark polygon:
                    context.DrawGeometry(look.Fill, look.Pen, Closed(polygon.Corners));
                    break;
                case DotMark dot:
                    context.DrawEllipse(look.Fill, look.Pen, At(dot.At), look.Radius, look.Radius);
                    break;
                default:
                    throw new UnreachableException();
            }
        }
    }

    private static StreamGeometry Closed(IReadOnlyList<ViewOffset> corners)
    {
        var path = new StreamGeometry();
        using var open = path.Open();
        open.BeginFigure(At(corners[0]), true);
        foreach (var corner in corners.Skip(1))
        {
            open.LineTo(At(corner));
        }

        open.EndFigure(true);
        return path;
    }

    private sealed record Look(IBrush? Fill, Pen? Pen, double Radius = 0);
}

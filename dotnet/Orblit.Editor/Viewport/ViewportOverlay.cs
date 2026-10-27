using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;
using ViewOffset = Orblit.Editor.Viewport.Offset;
using ViewSize = Orblit.Editor.Viewport.Size;

/// <summary>
/// What the view draws over the render: the handles, the gizmos of what is selected, the
/// boundary when the renderer's outline is off, and the orientation gizmo.
/// </summary>
/// <remarks>
/// Ordinary visuals over the frame rather than a pass in the renderer, for the same reason as the
/// Dart view: an overlay needs no depth buffer and no second draw of the scene, and it is always on
/// top where a handle has to be to be grabbed. It takes no pointer, so a drag that starts here
/// still reaches the view.
/// </remarks>
internal sealed class ViewportOverlay : Control
{
    /// <summary>The orientation gizmo's box.</summary>
    public const double OrientationSize = 56;

    // Past this many edges an outline is a smear rather than a shape, so the box is drawn instead.
    private const int TooManyEdges = 3000;
    private const double Arm = 15;
    private const double Disc = 6;

    private static readonly IBrush Shadow = new ImmutableSolidColorBrush(Color.FromUInt32(0x66000000));
    private static readonly IBrush Core = new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.45);
    private static readonly IBrush DiscFill = new ImmutableSolidColorBrush(Color.FromUInt32(0xB3161A21));
    private static readonly IBrush Pivot = new ImmutableSolidColorBrush(Color.FromUInt32(0xE6FFFFFF));
    private static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    private readonly SceneViewportState _state;
    private readonly EditorSession _session;

    public ViewportOverlay(SceneViewportState state, EditorSession session)
    {
        _state = state;
        _session = session;
        IsHitTestVisible = false;
    }

    /// <summary>Whether the renderer outlines the selection. When it does not, the boundary is drawn here.</summary>
    public bool OutlineSelection { get; set; } = true;

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        if (!OutlineSelection)
        {
            DrawBoundaries(context);
        }

        foreach (var (_, strokes) in _state.Overlays())
        {
            foreach (var stroke in strokes)
            {
                DrawLines(context, stroke.Lines.Strokes, ToneBrush(stroke.Tone), 1.25);
            }
        }

        DrawBrush(context);
        DrawHandles(context);
        DrawOrientation(context);
    }

    private static IBrush ToneBrush(GizmoTone tone) => tone switch
    {
        GizmoTone.Good => OrblitColors.Good,
        GizmoTone.Warn => OrblitColors.Warn,
        GizmoTone.InkMid => OrblitColors.InkMid,
        _ => OrblitColors.AxisZ,
    };

    private static Point At(ViewOffset offset) => new(offset.Dx, offset.Dy);

    // Over a dark pass, so the lines read against a pale floor as well as a dark sky.
    private static void DrawLines(
        DrawingContext context,
        IEnumerable<(ViewOffset From, ViewOffset To)> segments,
        IBrush tone,
        double width)
    {
        var path = Path(segments);
        context.DrawGeometry(null, new Pen(Shadow, 3), path);
        context.DrawGeometry(null, new Pen(tone, width), path);
    }

    private static StreamGeometry Path(IEnumerable<(ViewOffset From, ViewOffset To)> segments)
    {
        var path = new StreamGeometry();
        using var open = path.Open();
        foreach (var (from, to) in segments)
        {
            open.BeginFigure(At(from), false);
            open.LineTo(At(to));
            open.EndFigure(false);
        }

        return path;
    }

    // The ring is where the brush would land, so it follows the pointer over the ground. The inner ring
    // is where the strength starts to fall away, and is left out when it would sit on the edge or the centre.
    private void DrawBrush(DrawingContext context)
    {
        var bench = _session.Terrains;
        if (!_session.Mode.UsesBrush || bench.Hover is not { } centre || _session.TerrainTarget() is not { } open)
        {
            return;
        }

        var projection = new ViewportProjection(_state.Camera, new ViewSize(Bounds.Width, Bounds.Height));
        var brush = bench.Brush;
        DrawLines(context, BrushRing.Segments(open.Terrain, centre, brush.Radius, projection), OrblitColors.Ember, 1.5);
        if (BrushRing.CoreRadius(brush) is { } core)
        {
            context.DrawGeometry(null, new Pen(Core, 1), Path(BrushRing.Segments(open.Terrain, centre, core, projection)));
        }

        if (projection.Project(centre) is { } middle)
        {
            context.DrawEllipse(OrblitColors.Ember, null, At(middle), 2, 2);
        }
    }

    private void DrawBoundaries(DrawingContext context)
    {
        if (_state.Selected.Count == 0 || _session.Workspace.Loaded?.Scene is not { } scene)
        {
            return;
        }

        var projection = new ViewportProjection(_state.Camera, new ViewSize(Bounds.Width, Bounds.Height));
        var segments = new List<(ViewOffset From, ViewOffset To)>();
        foreach (var id in _state.Selected)
        {
            if (scene[id] is { IsDrawable: true } item && !item.Boundary.IsNothing)
            {
                segments.AddRange(BoundarySegments(item, scene.WorldOf(id), projection));
            }
        }

        if (segments.Count > 0)
        {
            DrawLines(context, segments, OrblitColors.Ember, 1.5);
        }
    }

    // The boundary as what it is: a box round a drawn room says nothing true about where its walls
    // are. A box only when asked for, or when the shape has more edges than anybody could read.
    private static List<(ViewOffset From, ViewOffset To)> BoundarySegments(
        SceneObject item,
        Matrix4 world,
        ViewportProjection projection)
    {
        var shell = item.BoundaryMesh;
        var edges = item.BoundaryEdges;
        if (shell is not null && edges.Count <= TooManyEdges)
        {
            return Joined(edges, index => projection.Project(world.Transformed3(shell.Positions[index])));
        }

        var corners = CornersOf(item.Boundary.BoxFrom(item.LocalBounds()));
        var points = corners.Select(corner => projection.Project(world.Transformed3(corner))).ToList();
        return points.Contains(null) ? [] : Joined(BoxEdges, index => points[index]);
    }

    // Pairs of corner indices making the twelve edges of a box.
    private static readonly (int, int)[] BoxEdges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0),
        (4, 5), (5, 7), (7, 6), (6, 4),
        (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    private static List<Vector3> CornersOf((Vector3 Min, Vector3 Max) box) =>
    [
        .. from x in new[] { box.Min.X, box.Max.X }
           from y in new[] { box.Min.Y, box.Max.Y }
           from z in new[] { box.Min.Z, box.Max.Z }
           select new Vector3(x, y, z),
    ];

    // An edge with an end behind the camera is skipped rather than drawn across the whole view.
    private static List<(ViewOffset From, ViewOffset To)> Joined(
        IEnumerable<(int, int)> edges,
        Func<int, ViewOffset?> where)
    {
        var joined = new List<(ViewOffset, ViewOffset)>();
        foreach (var (a, b) in edges)
        {
            if (where(a) is { } from && where(b) is { } to)
            {
                joined.Add((from, to));
            }
        }

        return joined;
    }

    private void DrawHandles(DrawingContext context)
    {
        if (_state.Handles is not { } gizmo || gizmo.Projection.Project(gizmo.Pivot) is not { } centre)
        {
            return;
        }

        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            DrawHandle(context, gizmo, axis, centre);
        }

        // So it is clear what the handles turn around even when one points straight at the camera.
        context.DrawEllipse(Pivot, null, At(centre), 3, 3);
    }

    private void DrawHandle(DrawingContext context, Gizmo gizmo, GizmoAxis axis, ViewOffset centre)
    {
        var active = _state.Dragging == axis || (_state.Dragging is null && _state.Hovered == axis);
        var brush = HandleBrush(axis, active);
        var pen = new Pen(brush, active ? 3.5 : 2.5, lineCap: PenLineCap.Round);

        if (gizmo.Mode == GizmoMode.Move)
        {
            if (gizmo.Projection.Project(gizmo.EndOf(axis)) is { } end)
            {
                context.DrawLine(pen, At(centre), At(end));
                DrawArrowhead(context, centre, end, brush);
            }

            return;
        }

        DrawRing(context, gizmo.RingOnScreen(axis), pen);
    }

    // Dimmed while another handle is dragged, so what is moving is never in doubt.
    private ImmutableSolidColorBrush HandleBrush(GizmoAxis axis, bool active)
    {
        var colour = Color.FromUInt32(axis.Colour().Argb);
        if (_state.Dragging is not null && _state.Dragging != axis)
        {
            return new ImmutableSolidColorBrush(colour, 0.25);
        }

        return new ImmutableSolidColorBrush(active ? Colors.White : colour);
    }

    private static void DrawRing(DrawingContext context, List<ViewOffset> ring, Pen pen)
    {
        if (ring.Count < 2)
        {
            return;
        }

        var path = new StreamGeometry();
        using (var open = path.Open())
        {
            open.BeginFigure(At(ring[0]), false);
            foreach (var point in ring.Skip(1))
            {
                open.LineTo(At(point));
            }

            open.EndFigure(false);
        }

        context.DrawGeometry(null, pen, path);
    }

    private static void DrawArrowhead(DrawingContext context, ViewOffset from, ViewOffset to, IBrush brush)
    {
        var line = to - from;
        if (line.Distance < 12)
        {
            return;
        }

        var direction = line / line.Distance;
        var across = new ViewOffset(-direction.Dy, direction.Dx);
        var baseAt = to - (direction * 9);
        var path = new StreamGeometry();
        using (var open = path.Open())
        {
            open.BeginFigure(At(to), true);
            open.LineTo(At(baseAt + (across * 4)));
            open.LineTo(At(baseAt - (across * 4)));
            open.EndFigure(true);
        }

        context.DrawGeometry(brush, null, path);
    }

    // The world's three axes as the camera sees them, so somebody who has orbited away from the
    // grid can find their way back. The handles use the same colours.
    private void DrawOrientation(DrawingContext context)
    {
        var centre = new Point(Bounds.Width - Space.Md - (OrientationSize / 2), Space.Md + (OrientationSize / 2));
        context.DrawEllipse(DiscFill, null, centre, (OrientationSize / 2) - 2, (OrientationSize / 2) - 2);

        var (right, up, forward) = _state.Camera.Basis;
        // Furthest first, so an axis pointing at the camera is drawn over the ones behind it.
        foreach (var axis in Enum.GetValues<GizmoAxis>().OrderByDescending(a => a.Direction().Dot(forward)))
        {
            var direction = axis.Direction();
            // Against the camera's up, because the screen's y runs downwards.
            var tip = centre + new Vector(direction.Dot(right), -direction.Dot(up)) * Arm;
            var brush = new ImmutableSolidColorBrush(Color.FromUInt32(axis.Colour().Argb));
            context.DrawLine(new Pen(brush, 2, lineCap: PenLineCap.Round), centre, tip);
            context.DrawEllipse(brush, null, tip, Disc, Disc);

            var letter = new FormattedText(
                axis.ToString(),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Bold,
                9,
                OrblitColors.Ground);
            context.DrawText(letter, tip - new Vector(letter.Width / 2, letter.Height / 2));
        }
    }
}

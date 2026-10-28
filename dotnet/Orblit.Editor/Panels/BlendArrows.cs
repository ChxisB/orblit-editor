using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Panels;

/// <summary>
/// The curves of the blend graph with a head on each, and a dot that follows a link being dragged.
/// Loops are only a label, and the curves take no presses, so what lies under one stays pressable.
/// </summary>
internal sealed class BlendArrows : Control
{
    private const double HeadLength = 8;
    private const double HeadSpread = 0.45;
    private const double DotRadius = 7;

    private readonly List<(IBrush Ink, StreamGeometry Line, StreamGeometry Head)> _drawn;
    private Spot? _dot;

    /// <param name="sketch">Where the curves run.</param>
    /// <param name="picked">The change chosen, whose arrow is drawn in the accent, or -1 for none.</param>
    public BlendArrows(BlendSketch sketch, int picked)
    {
        Width = sketch.Width;
        Height = sketch.Height;
        IsHitTestVisible = false;
        _drawn =
        [
            .. sketch.Arrows.Where(static arrow => !arrow.IsLoop).Select(arrow => (
                arrow.Changes.Contains(picked) ? OrblitColors.Ember : OrblitColors.InkDim,
                Line(arrow.Curve),
                Head(arrow.Curve))),
        ];
    }

    public void Follow(Spot? dot)
    {
        _dot = dot;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        foreach (var (ink, line, head) in _drawn)
        {
            context.DrawGeometry(null, new Pen(ink, 1.5), line);
            context.DrawGeometry(ink, null, head);
        }

        if (_dot is { } at)
        {
            context.DrawEllipse(OrblitColors.Ember, null, At(at), DotRadius, DotRadius);
        }
    }

    private static Point At(Spot spot) => new(spot.X, spot.Y);

    private static StreamGeometry Line(SketchCurve curve)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(At(curve.Start), false);
        context.CubicBezierTo(At(curve.PullStart), At(curve.PullEnd), At(curve.End));
        context.EndFigure(false);
        return geometry;
    }

    private static StreamGeometry Head(SketchCurve curve)
    {
        Point Wing(double turn) => new(
            curve.End.X - (HeadLength * Math.Cos(curve.Heading + turn)),
            curve.End.Y - (HeadLength * Math.Sin(curve.Heading + turn)));
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(At(curve.End), true);
        context.LineTo(Wing(-HeadSpread));
        context.LineTo(Wing(HeadSpread));
        context.EndFigure(true);
        return geometry;
    }
}

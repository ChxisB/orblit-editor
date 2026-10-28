using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// Where the texture of the selected faces sits, drawn on the texture's own square, and a drag in it
/// that moves, scales or turns what is selected.
/// </summary>
/// <remarks>
/// The view is framed once, when the panel builds, and not again while a drag runs. A frame that
/// followed the faces would move under the pointer and the drag would chase its own tail.
/// </remarks>
internal sealed class UvCanvas : Control
{
    // Wider than this and the square pushes the buttons below it out of sight.
    private const double MostAcross = 360;

    private static readonly IBrush SquareFill = Colour(0x14FFFFFF);
    private static readonly IPen SquareEdge = new Pen(Colour(0x66FFFFFF), 1);
    private static readonly IPen Quarter = new Pen(Colour(0x22FFFFFF), 0.5);
    private static readonly IBrush FaceFill = Colour(0x33E5893F);
    private static readonly IPen FaceEdge = new Pen(OrblitColors.Ember, 1.5);

    private readonly EditorSession _session;
    private readonly FieldHost _host;
    private readonly UvFrame _frame;
    private UvDrag? _drag;

    public UvCanvas(EditorSession session, FieldHost host)
    {
        _session = session;
        _host = host;
        _frame = session.UvMesh is { } mesh ? UvFrame.Around(mesh, session.UvSelection.FacesIn(mesh)) : UvFrame.Start;
        Name = "uv_canvas";
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        ClipToBounds = true;
        // A drag redraws this and nothing else, because a rebuild would drop the pointer.
        host.Live(InvalidateVisual);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(OrblitColors.Ground, new Pen(OrblitColors.LineSoft, 1), new Rect(Bounds.Size), 6, 6);
        if (_session.UvMesh is not { } mesh || Bounds.Width <= 0)
        {
            return;
        }

        Square(context);
        foreach (var face in _session.UvSelection.FacesIn(mesh))
        {
            Face(context, mesh.UvsOf(face));
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var across = Math.Min(double.IsInfinity(availableSize.Width) ? MostAcross : availableSize.Width, MostAcross);
        return new Size(across, across);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        _host.Hold();
        _drag = new UvDrag(_session.UvGesture, SelectionMiddle(), Under(e));
    }

    // Scaling and turning hold this point still. The middle of the view is where the square sits, not
    // where the faces are, so a face off to one side would swing away from under the pointer.
    private Vector2 SelectionMiddle() =>
        _session.UvMesh is { } mesh && mesh.UvBoundsOf(_session.UvSelection.FacesIn(mesh)) is { } box
            ? (box.Min + box.Max) * 0.5
            : _frame.Centre;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _drag?.Move(Under(e), _session);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        End();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        End();
    }

    private void End()
    {
        if (_drag is null)
        {
            return;
        }

        _drag = null;
        _session.EndUvRun();
        _host.Settle();
    }

    private Vector2 Under(PointerEventArgs e)
    {
        var at = e.GetPosition(this);
        return _frame.ToUv(at.X, at.Y, Bounds.Width, Bounds.Height);
    }

    private Point Place(Vector2 uv)
    {
        var (x, y) = _frame.At(uv, Bounds.Width, Bounds.Height);
        return new Point(x, y);
    }

    // The texture's own square, which is the one thing in here that is not the geometry: everything
    // is read as a fraction of it.
    private void Square(DrawingContext context)
    {
        var square = new Rect(Place(new Vector2(0, 1)), Place(new Vector2(1, 0)));
        context.DrawRectangle(SquareFill, SquareEdge, square);
        // Quarters, so a face can be seen to be half a texture across without anybody measuring.
        for (var part = 0.25; part < 1; part += 0.25)
        {
            context.DrawLine(Quarter, Place(new Vector2(part, 0)), Place(new Vector2(part, 1)));
            context.DrawLine(Quarter, Place(new Vector2(0, part)), Place(new Vector2(1, part)));
        }
    }

    private void Face(DrawingContext context, List<Vector2> corners)
    {
        if (corners.Count < 3)
        {
            return;
        }

        var outline = new StreamGeometry();
        using (var pen = outline.Open())
        {
            pen.BeginFigure(Place(corners[0]), true);
            foreach (var corner in corners.Skip(1))
            {
                pen.LineTo(Place(corner));
            }

            pen.EndFigure(true);
        }

        context.DrawGeometry(FaceFill, FaceEdge, outline);
        foreach (var corner in corners)
        {
            context.DrawEllipse(OrblitColors.Ember, null, Place(corner), 2.5, 2.5);
        }
    }

    private static ImmutableSolidColorBrush Colour(uint argb) => new ImmutableSolidColorBrush(Color.FromUInt32(argb));
}

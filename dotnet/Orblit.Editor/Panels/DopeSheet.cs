using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>
/// The sheet of keys beside the track names: a row for each line of the timeline, a diamond for each key.
/// Pressing picks keys, dragging from empty room draws a box, and dragging a key moves it.
/// </summary>
/// <remarks>
/// A press that does not travel is a tap and one that does is a drag. What either does is in
/// <see cref="SheetModel"/>, so this only draws and turns pointer events into the model's calls.
/// </remarks>
internal sealed class DopeSheet : Control
{
    // How far the pointer can wander from a press and still be a tap.
    private const double Slop = 4;
    private const double KeyHalf = 4.5;
    private const double SummaryHalf = 3.5;

    private static readonly IBrush PastEnd = new ImmutableSolidColorBrush(OrblitColors.GroundColor, 0.6);
    private static readonly IPen Outline = new Pen(OrblitColors.Ground, 1);
    private static readonly Geometry KeyShape = Diamond(KeyHalf);
    private static readonly Geometry SummaryShape = Diamond(SummaryHalf);

    private readonly ClipBench _bench;
    private readonly SheetModel _model;

    // A press waiting to turn into a tap or a drag.
    private Point? _press;
    private bool _shift;
    private bool _dragging;

    public DopeSheet(ClipBench bench)
    {
        _bench = bench;
        _model = new SheetModel(bench, TimelineTracks.RowHeight);
        Name = "timeline_sheet";
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        // Filled, so the room below the rows takes a press and not only the marks drawn on it.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (_bench.Clip is not { } clip)
        {
            return;
        }

        var rows = TimelineRow.Of(clip);
        var axis = new TimeAxis(clip, Bounds.Width);
        Rows(context, rows);
        var past = axis.XOf(axis.Duration);
        if (past < Bounds.Width)
        {
            context.DrawRectangle(PastEnd, null, new Rect(past, 0, Bounds.Width - past, Bounds.Height));
        }

        FrameLines(context, axis, clip.Rate);
        Keys(context, clip, rows, axis);
        var now = axis.XOf(_bench.At);
        context.DrawLine(new Pen(OrblitColors.Ember, 1.5), new Point(now, 0), new Point(now, Bounds.Height));
        Box(context);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        _press = e.GetPosition(this);
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_press is not { } press || AxisOf() is not { } axis)
        {
            return;
        }

        var at = e.GetPosition(this);
        if (!_dragging && Distance(at, press) > Slop)
        {
            _dragging = true;
            _model.Start(axis, press.X, press.Y, _shift);
        }

        if (_dragging)
        {
            _model.Update(axis, at.X, at.Y);
            InvalidateVisual();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_press is not { } press)
        {
            return;
        }

        if (_dragging)
        {
            _model.End();
        }
        else if (AxisOf() is { } axis)
        {
            _model.Tap(axis, press.X, press.Y, _shift);
        }

        _press = null;
        _dragging = false;
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _model.End();
        }

        _press = null;
        _dragging = false;
        InvalidateVisual();
    }

    private static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static StreamGeometry Diamond(double half)
    {
        var shape = new StreamGeometry();
        using var draw = shape.Open();
        draw.BeginFigure(new Point(0, -half), isFilled: true);
        draw.LineTo(new Point(half, 0));
        draw.LineTo(new Point(0, half));
        draw.LineTo(new Point(-half, 0));
        draw.EndFigure(isClosed: true);
        return shape;
    }

    private TimeAxis? AxisOf() => _bench.Clip is { } clip ? new TimeAxis(clip, Bounds.Width) : null;

    private void Rows(DrawingContext context, IReadOnlyList<TimelineRow> rows)
    {
        var line = new Pen(OrblitColors.LineSoft, 1);
        for (var index = 0; index < rows.Count; index++)
        {
            var top = index * TimelineTracks.RowHeight;
            if (rows[index].IsHeading)
            {
                context.DrawRectangle(OrblitColors.Raised, null, new Rect(0, top, Bounds.Width, TimelineTracks.RowHeight));
            }

            var bottom = top + TimelineTracks.RowHeight - 0.5;
            context.DrawLine(line, new Point(0, bottom), new Point(Bounds.Width, bottom));
        }
    }

    private void FrameLines(DrawingContext context, TimeAxis axis, double rate)
    {
        var step = TimeAxis.FrameStep(axis.PerSecond, rate);
        var second = new Pen(OrblitColors.Line, 1);
        var frame = new Pen(OrblitColors.LineSoft, 1);
        var frames = rate <= 0 ? 0 : (int)Math.Floor(axis.Span * rate);
        for (var at = 0; at <= frames; at += step)
        {
            var x = Math.Round(axis.XOf(at / rate)) + 0.5;
            var whole = at % (int)Math.Round(rate) == 0;
            context.DrawLine(whole ? second : frame, new Point(x, 0), new Point(x, Bounds.Height));
        }
    }

    private void Keys(DrawingContext context, ClipDocument clip, IReadOnlyList<TimelineRow> rows, TimeAxis axis)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            var middle = index * TimelineTracks.RowHeight + TimelineTracks.RowHeight / 2;
            if (rows[index].Channel is { } channel)
            {
                Channel(context, channel, axis, middle);
            }
            else
            {
                Summary(context, clip, rows[index].Target, axis, middle);
            }
        }
    }

    private void Channel(DrawingContext context, ClipChannel channel, TimeAxis axis, double middle)
    {
        var address = ClipEdits.AddressOf(channel);
        for (var place = 0; place < channel.BoxedKeys.Count; place++)
        {
            Stamp(context, KeyShape, axis.XOf(channel.BoxedKeys[place].At), middle, _bench.Selection.Contains(new KeyRef(address, place)));
        }
    }

    // A heading has one diamond for each moment its thing is keyed at, picked only when every key there is.
    private void Summary(DrawingContext context, ClipDocument clip, string target, TimeAxis axis, double middle)
    {
        var moments = new List<(double At, bool Picked)>();
        var keys = clip.Channels
            .Where(channel => channel.Target == target)
            .SelectMany(channel => channel.BoxedKeys.Select((key, place) => (key.At, Picked: _bench.Selection.Contains(new KeyRef(ClipEdits.AddressOf(channel), place)))))
            .OrderBy(key => key.At);
        foreach (var (at, picked) in keys)
        {
            if (moments.Count > 0 && Math.Abs(moments[^1].At - at) < ClipEdits.SameMoment)
            {
                moments[^1] = (moments[^1].At, moments[^1].Picked && picked);
            }
            else
            {
                moments.Add((at, picked));
            }
        }

        foreach (var (at, picked) in moments)
        {
            Stamp(context, SummaryShape, axis.XOf(at), middle, picked);
        }
    }

    private static void Stamp(DrawingContext context, Geometry shape, double x, double y, bool picked)
    {
        using var _ = context.PushTransform(Matrix.CreateTranslation(x, y));
        context.DrawGeometry(picked ? OrblitColors.Ember : OrblitColors.InkMid, Outline, shape);
    }

    private void Box(DrawingContext context)
    {
        if (_model.Box is { } box)
        {
            var rect = new Rect(box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
            context.DrawRectangle(OrblitColors.EmberWash, new Pen(OrblitColors.Ember, 1), rect);
        }
    }
}

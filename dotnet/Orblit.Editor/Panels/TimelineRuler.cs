using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// The ruler over the sheet, which is where the playhead is dragged.
/// </summary>
/// <remarks>
/// Scrubbing stops playback and puts the playhead on a frame: a key goes where the playhead is, and one
/// between frames would be a key nobody could land on again.
/// </remarks>
internal sealed class TimelineRuler : Control
{
    private static readonly Typeface Face = new(FontFamily.Default);

    private readonly ClipBench _bench;

    public TimelineRuler(ClipBench bench)
    {
        _bench = bench;
        Name = "timeline_ruler";
        Cursor = new Cursor(StandardCursorType.Hand);
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        // Filled, so the whole strip takes a press and not only the marks drawn on it.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (_bench.Clip is not { } clip)
        {
            return;
        }

        var axis = new TimeAxis(clip, Bounds.Width);
        var past = axis.XOf(axis.Duration);
        if (past < Bounds.Width)
        {
            context.DrawRectangle(OrblitColors.Ground, null, new Rect(past, 0, Bounds.Width - past, Bounds.Height));
        }

        Ticks(context, axis, clip.Rate);
        context.DrawLine(new Pen(OrblitColors.LineSoft, 1), new Point(0, Bounds.Height - 0.5), new Point(Bounds.Width, Bounds.Height - 0.5));
        Playhead(context, axis.XOf(_bench.At));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Pointer.Capture(this);
            Scrub(e.GetPosition(this).X);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            Scrub(e.GetPosition(this).X);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (ReferenceEquals(e.Pointer.Captured, this))
        {
            e.Pointer.Capture(null);
        }
    }

    private void Scrub(double x)
    {
        if (_bench.Clip is not { } clip)
        {
            return;
        }

        _bench.Playing = false;
        _bench.At = ClipEdits.Snapped(new TimeAxis(clip, Bounds.Width).AtOf(x), clip.Rate);
    }

    private void Ticks(DrawingContext context, TimeAxis axis, double rate)
    {
        var tick = TimeAxis.FrameStep(axis.PerSecond, rate, gap: 6);
        var label = TimeAxis.FrameStep(axis.PerSecond, rate, gap: 48);
        var line = new Pen(OrblitColors.Line, 1);
        var frames = rate <= 0 ? 0 : (int)Math.Floor(axis.Span * rate);
        for (var frame = 0; frame <= frames; frame += tick)
        {
            var x = Math.Round(axis.XOf(frame / rate)) + 0.5;
            var isLabelled = frame % label == 0;
            context.DrawLine(line, new Point(x, Bounds.Height - (isLabelled ? 9 : 4)), new Point(x, Bounds.Height));
            if (isLabelled)
            {
                var text = new FormattedText(TimelineWords.Seconds(frame / rate), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, 10, OrblitColors.InkDim);
                context.DrawText(text, new Point(x + 3, 2));
            }
        }
    }

    private void Playhead(DrawingContext context, double x)
    {
        var height = Bounds.Height;
        var head = new StreamGeometry();
        using (var draw = head.Open())
        {
            draw.BeginFigure(new Point(x - 5, height - 10), isFilled: true);
            draw.LineTo(new Point(x + 5, height - 10));
            draw.LineTo(new Point(x, height - 4));
            draw.EndFigure(isClosed: true);
        }

        context.DrawGeometry(OrblitColors.Ember, null, head);
        context.DrawLine(new Pen(OrblitColors.Ember, 1.5), new Point(x, height - 5), new Point(x, height));
    }
}

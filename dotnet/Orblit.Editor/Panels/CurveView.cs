using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// One channel drawn as the numbers it moves through. The sheet says when; this says how much, and how it
/// gets there.
/// </summary>
/// <remarks>
/// A key can be dragged in time and in value, and the picked keys grow handles that bend the curve through
/// them. What a press does is in <see cref="CurveModel"/>, so this only draws and turns pointer events into
/// the model's calls.
/// </remarks>
internal sealed class CurveView : Control
{
    // How far the pointer can wander from a press and still be a tap.
    private const double Slop = 4;

    private static readonly Typeface Face = new(FontFamily.Default);
    private static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);
    private static readonly IBrush PastEnd = new ImmutableSolidColorBrush(OrblitColors.GroundColor, 0.6);
    private static readonly IPen Outline = new Pen(OrblitColors.Ground, 1);

    // X, Y and Z, tinted the way the inspector tints them.
    private static readonly IBrush[] PartColours =
    [
        new ImmutableSolidColorBrush(Color.FromUInt32(0xFFD9634F)),
        new ImmutableSolidColorBrush(Color.FromUInt32(0xFF7FB069)),
        new ImmutableSolidColorBrush(Color.FromUInt32(0xFF5B8DD9)),
    ];

    private readonly ClipBench _bench;
    private readonly CurveModel _model;

    // A press waiting to turn into a tap or a drag.
    private Spot? _press;
    private bool _shift;
    private bool _dragging;

    public CurveView(ClipBench bench)
    {
        _bench = bench;
        _model = new CurveModel(bench);
        Name = "timeline_curves";
        ClipToBounds = true;
    }

    public override void Render(DrawingContext context)
    {
        // Filled, so the whole view takes a press and not only the marks drawn on it.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (_model.FrameOf(Bounds.Width, Bounds.Height) is not { } frame || _bench.Clip is not { } clip)
        {
            Say(context, _model.Note ?? "");
            return;
        }

        var axis = frame.Axis;
        var past = axis.XOf(axis.Duration);
        if (past < Bounds.Width)
        {
            context.DrawRectangle(PastEnd, null, new Rect(past, 0, Bounds.Width - past, Bounds.Height));
        }

        Backdrop(context, frame, clip.Rate);
        var parts = ClipEdits.CurvesOf(frame.Channel.Kind);
        Lines(context, frame, parts);
        Handles(context, frame);
        Keys(context, frame, parts);
        var now = Math.Round(axis.XOf(_bench.At)) + 0.5;
        context.DrawLine(new Pen(OrblitColors.Ember, 1.5), new Point(now, 0), new Point(now, Bounds.Height));
        if (parts > 1)
        {
            Legend(context, parts);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(this);
        _press = At(e);
        _shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        _dragging = false;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_press is not { } press || _model.FrameOf(Bounds.Width, Bounds.Height) is not { } frame)
        {
            return;
        }

        var at = At(e);
        if (!_dragging && press.Distance(at) > Slop)
        {
            _dragging = true;
            _model.Start(frame, press, _shift);
        }

        if (_dragging)
        {
            _model.Update(frame, at, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Alt));
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
        else if (_model.FrameOf(Bounds.Width, Bounds.Height) is { } frame)
        {
            _model.Tap(frame, press, _shift);
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

    private static FormattedText Text(string text, IBrush brush, double size, Typeface? face = null) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face ?? Face, size, brush);

    private Spot At(PointerEventArgs e)
    {
        var at = e.GetPosition(this);
        return new Spot(at.X, at.Y);
    }

    private static Point AsPoint(Spot spot) => new(spot.X, spot.Y);

    // What there is to say in place of a curve, in the middle.
    private void Say(DrawingContext context, string note)
    {
        var text = Text(note, OrblitColors.InkDim, OrblitText.Caption);
        text.MaxTextWidth = Math.Max(Bounds.Width - Space.Lg * 2, 1);
        text.TextAlignment = TextAlignment.Center;
        context.DrawText(text, new Point(Space.Lg, Math.Max((Bounds.Height - text.Height) / 2, 0)));
    }

    // A line every second and a line at every round value, with the values written against the left edge.
    private void Backdrop(DrawingContext context, CurveFrame frame, double rate)
    {
        var firm = new Pen(OrblitColors.Line, 1);
        var soft = new Pen(OrblitColors.LineSoft, 1);
        if (rate > 0)
        {
            for (var second = 0; second <= frame.Axis.Span; second++)
            {
                var x = Math.Round(frame.Axis.XOf(second)) + 0.5;
                context.DrawLine(firm, new Point(x, 0), new Point(x, Bounds.Height));
            }
        }

        var step = CurveGrid.NiceStep((frame.High - frame.Low) / (Bounds.Height / 36));
        if (step <= 0 || !double.IsFinite(step))
        {
            return;
        }

        foreach (var value in CurveGrid.Lines(frame.Low, frame.High, step))
        {
            var y = Math.Round(frame.YOf(value)) + 0.5;
            context.DrawLine(Math.Abs(value) < step / 2 ? firm : soft, new Point(0, y), new Point(Bounds.Width, y));
            var label = Text(CurveGrid.Label(value, step), OrblitColors.InkDim, 10);
            context.DrawText(label, new Point(3, y - label.Height - 1));
        }
    }

    // One line for each number the channel moves.
    private void Lines(DrawingContext context, CurveFrame frame, int parts)
    {
        var axis = frame.Axis;
        var start = axis.XOf(0);
        var end = Math.Min(Bounds.Width, axis.XOf(axis.Span));
        var xs = new List<double> { start };
        for (var x = start + 2; x <= end + 1; x += 2)
        {
            xs.Add(x);
        }

        var values = xs.Select(x => frame.Channel.ValueAt(axis.AtOf(x))).ToList();
        for (var part = 0; part < parts; part++)
        {
            var path = new StreamGeometry();
            using (var draw = path.Open())
            {
                draw.BeginFigure(new Point(xs[0], frame.YOf(ClipEdits.PartOf(values[0], part))), isFilled: false);
                for (var at = 1; at < xs.Count; at++)
                {
                    draw.LineTo(new Point(xs[at], frame.YOf(ClipEdits.PartOf(values[at], part))));
                }

                draw.EndFigure(isClosed: false);
            }

            context.DrawGeometry(null, new Pen(parts == 1 ? OrblitColors.Ember : PartColours[part], 1.5), path);
        }
    }

    private void Handles(DrawingContext context, CurveFrame frame)
    {
        var line = new Pen(OrblitColors.InkMid, 1);
        foreach (var (end, grip) in _model.PickedHandles(frame))
        {
            var key = frame.Channel.BoxedKeys[grip.Index];
            context.DrawLine(line, AsPoint(frame.At(key.At, ClipEdits.PartOf(key.Value, grip.Part))), AsPoint(end));
            context.DrawEllipse(OrblitColors.Ink, null, AsPoint(end), 3, 3);
        }
    }

    private void Keys(DrawingContext context, CurveFrame frame, int parts)
    {
        var picked = _model.Picked(frame);
        for (var index = 0; index < frame.Channel.BoxedKeys.Count; index++)
        {
            var chosen = picked.Contains(index);
            var key = frame.Channel.BoxedKeys[index];
            for (var part = 0; part < parts; part++)
            {
                var radius = chosen ? 4.5 : 3.5;
                context.DrawEllipse(chosen ? OrblitColors.Ember : OrblitColors.Ink, Outline, AsPoint(frame.At(key.At, ClipEdits.PartOf(key.Value, part))), radius, radius);
            }
        }
    }

    private void Legend(DrawingContext context, int parts)
    {
        var x = Bounds.Width - Space.Sm;
        for (var part = parts - 1; part >= 0; part--)
        {
            var letter = Text("XYZ"[part].ToString(), PartColours[part], 10, Bold);
            x -= letter.Width;
            context.DrawText(letter, new Point(x, Space.Xs));
            x -= Space.Xs;
        }
    }
}

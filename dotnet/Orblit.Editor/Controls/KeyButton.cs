using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// A diamond after a field a clip can move. Filled where there is a key on this frame, an outline where
/// the clip moves the field with no key here, dim where it does not move it yet. Pressing it keys the
/// field as it is now. Gone where the clip cannot key the field, so a row does not offer what it cannot do.
/// </summary>
internal sealed class KeyButton : Control
{
    private const double Reach = 6;

    private readonly IKeying _keying;
    private readonly SceneObject _target;
    private readonly string _field;

    /// <param name="field">The field as a clip names it, like <c>transform.position</c>.</param>
    public KeyButton(IKeying keying, SceneObject target, string field)
    {
        _keying = keying;
        _target = target;
        _field = field;
        Name = $"key_{field.Replace('.', '_')}";
        Width = 16;
        Height = 24;
        Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, "Key this frame");
        Refresh();
    }

    /// <summary>How the field looks from the clip, as last read.</summary>
    public KeyMark Mark { get; private set; }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Listening for itself: the playhead moving changes the mark and nothing else about the row.
        _keying.Changed += Refresh;
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _keying.Changed -= Refresh;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _keying.Key(_target, _field);
            e.Handled = true;
        }
    }

    public override void Render(DrawingContext context)
    {
        // Painted, so the whole 16 by 24 answers a press and not only the lines of the diamond.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        var ink = Mark == KeyMark.Unkeyed ? OrblitColors.InkDim : OrblitColors.Ember;
        var middle = new Point(Bounds.Width / 2, Bounds.Height / 2);
        var diamond = new StreamGeometry();
        using (var path = diamond.Open())
        {
            path.BeginFigure(new Point(middle.X, middle.Y - Reach), true);
            path.LineTo(new Point(middle.X + Reach, middle.Y));
            path.LineTo(new Point(middle.X, middle.Y + Reach));
            path.LineTo(new Point(middle.X - Reach, middle.Y));
            path.EndFigure(true);
        }

        context.DrawGeometry(Mark == KeyMark.Keyed ? ink : null, new Pen(ink, 1.4, lineJoin: PenLineJoin.Round), diamond);
    }

    private void Refresh()
    {
        var mark = _keying.MarkFor(_target, _field);
        IsVisible = mark != KeyMark.None;
        if (mark != Mark)
        {
            Mark = mark;
            InvalidateVisual();
        }
    }
}

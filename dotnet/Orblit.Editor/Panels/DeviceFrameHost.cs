using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Draws its child at the shape of a device, as large as the room allows without passing the device's
/// own size, and centred. The child fills the frame, so a renderer in it sizes to the frame and the
/// camera's view follows.
/// </summary>
internal sealed class DeviceFrameHost : Decorator
{
    private const double Margins = Space.Md;

    private DeviceFrame _frame = DeviceFrame.Desktop;

    /// <summary>Raised after the picture's size changes, for a window that reads <see cref="Scale"/>.</summary>
    public event Action? Fitted;

    /// <summary>How much of the device's own size the picture is drawn at, from 0 to 1.</summary>
    public double Scale { get; private set; }

    /// <summary>The picture's size in the window, or zero before it has been arranged.</summary>
    public Size ScreenSize { get; private set; }

    public void Show(DeviceFrame frame)
    {
        _frame = frame;
        InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        Child?.Measure(new Size(_frame.Width, _frame.Height));
        return new Size(
            double.IsInfinity(availableSize.Width) ? _frame.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? _frame.Height : availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var scale = _frame.FitInto(finalSize.Width - (2 * Margins), finalSize.Height - (2 * Margins));
        var size = new Size(_frame.Width * scale, _frame.Height * scale);
        var origin = new Point((finalSize.Width - size.Width) / 2, (finalSize.Height - size.Height) / 2);
        Child?.Arrange(new Rect(origin, size));
        if (scale != Scale || size != ScreenSize)
        {
            (Scale, ScreenSize) = (scale, size);
            Fitted?.Invoke();
        }

        return finalSize;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var origin = new Point((Bounds.Width - ScreenSize.Width) / 2, (Bounds.Height - ScreenSize.Height) / 2);
        context.DrawRectangle(null, new Pen(OrblitColors.Line, 1), new Rect(origin, ScreenSize).Inflate(1));
    }
}

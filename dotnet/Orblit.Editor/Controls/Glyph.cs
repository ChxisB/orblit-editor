using Avalonia.Controls;
using Avalonia.Media;
using GlyphPath = Avalonia.Controls.Shapes.Path;

namespace Orblit.Editor;

/// <summary>
/// A small outline picture drawn from path data on a 24 unit square. There is no icon font, so every
/// icon in the editor is path data in its view (decision 84).
/// </summary>
internal sealed class Glyph : Viewbox
{
    private readonly GlyphPath _path;

    public Glyph(string data, double size, IBrush? ink = null)
    {
        _path = new GlyphPath
        {
            Data = Geometry.Parse(data),
            Stroke = ink ?? OrblitColors.InkMid,
            StrokeThickness = 1.7,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        Width = size;
        Height = size;
        // A press on the picture is a press on whatever it sits in.
        IsHitTestVisible = false;
        Child = new Canvas { Width = 24, Height = 24, Children = { _path } };
    }

    public IBrush? Ink
    {
        get => _path.Stroke;
        set => _path.Stroke = value;
    }
}

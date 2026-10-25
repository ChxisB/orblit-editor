using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>The brand mark: a ring tilted around a dot. Drawn, so it is sharp at any size.</summary>
public sealed class OrblitMark : Control
{
    private static readonly Pen Ring = new(OrblitColors.Ink, 1.5);

    public override void Render(DrawingContext context)
    {
        // Drawn on a 24 unit square about its middle, then scaled to whatever the control is given.
        var scale = Math.Min(Bounds.Width, Bounds.Height) / 24;
        using (context.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(Bounds.Width / 2, Bounds.Height / 2)))
        {
            using (context.PushTransform(Matrix.CreateRotation(-24 * Math.PI / 180)))
            {
                context.DrawEllipse(null, Ring, default, 10.5, 4);
            }

            context.DrawEllipse(OrblitColors.Ember, null, default, 5, 5);
        }
    }
}

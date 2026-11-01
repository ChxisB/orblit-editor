using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Platform;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

// Added in C#: the marks are checked in OverlayMarksTests. These check that they reach the screen, which
// a missing colour in the overlay's table would break at paint time and nowhere else.
public sealed class ViewportOverlayMarksTests
{
    private static int Orange(ViewportRig rig)
    {
        Headless.Render();
        Headless.Render();
        var frame = rig.Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame.");
        var from = rig.View.TranslatePoint(new Point(0, 0), rig.Window)!.Value;
        using var locked = frame.Lock();
        var bytes = new byte[locked.RowBytes * locked.Size.Height];
        Marshal.Copy(locked.Address, bytes, 0, bytes.Length);
        var (red, blue) = locked.Format == PixelFormat.Bgra8888 ? (2, 0) : (0, 2);

        var count = 0;
        for (var y = (int)from.Y; y < (int)(from.Y + rig.View.Bounds.Height) && y < locked.Size.Height; y++)
        {
            for (var x = (int)from.X; x < (int)(from.X + rig.View.Bounds.Width) && x < locked.Size.Width; x++)
            {
                var at = (y * locked.RowBytes) + (x * 4);
                count += IsOrange(bytes[at + red], bytes[at + 1], bytes[at + blue]) ? 1 : 0;
            }
        }

        return count;
    }

    // The theme's ember, or the translucent wash of it over a dark view, and not a grey or an axis colour.
    private static bool IsOrange(byte red, byte green, byte blue) => red > 110 && red > green + 25 && green > blue + 15;

    [Fact]
    public Task AChosenFaceIsPaintedOverTheView() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Session.AddShape(ShapeKind.Cube);
        rig.Session.SetContext(EditContext.Element);
        var before = Orange(rig);

        rig.Session.PickElements(new ElementSelection(faces: [0, 1, 2, 3, 4, 5]), false);

        Assert.True(Orange(rig) > before + 200, "the chosen faces were not painted");
    });

    [Fact]
    public Task AnOutlineBeingDrawnIsPaintedOverTheView() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var before = Orange(rig);

        rig.Session.UseTool(ViewportTool.PolyShape);
        var up = new Vector3(0, 1, 0);
        rig.Session.DrawPoint(new DrawnPoint(new Vector3(-2, 0, -2), new Vector3(0, 0, 0), up, null));
        rig.Session.DrawPoint(new DrawnPoint(new Vector3(2, 0, -2), new Vector3(0, 0, 0), up, null));
        rig.Session.Sketch.Hovering = new Vector3(-2, 0, 2);
        rig.Session.DrawPoint(new DrawnPoint(new Vector3(2, 0, 2), new Vector3(0, 0, 0), up, null));

        Assert.True(Orange(rig) > before + 100, "the outline was not painted");
    });
}

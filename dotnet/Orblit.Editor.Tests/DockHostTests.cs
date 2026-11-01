using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// A layout that says what is already drawn must not rebuild its panels, because the window offers its layout
// on every change to the scene.
public sealed class DockHostTests
{
    [Fact]
    public Task ALayoutThatSaysTheSameThingIsNotDrawnAgain() => Headless.Run(() =>
    {
        var built = 0;
        var layout = DockLayout.Standard();
        var host = new DockHost(layout, _ =>
        {
            built++;
            return new TextBlock();
        });
        var first = built;

        host.Layout = DockLayout.Read(layout.ToText())!;

        Assert.True(first > 0);
        Assert.Equal(first, built);
    });

    [Fact]
    public Task ALayoutThatSaysSomethingElseIsDrawn() => Headless.Run(() =>
    {
        var built = 0;
        var layout = DockLayout.Standard();
        var host = new DockHost(layout, _ =>
        {
            built++;
            return new TextBlock();
        });
        var first = built;

        host.Layout = layout.CopyWith(locked: !layout.Locked);

        Assert.True(built > first);
    });
}

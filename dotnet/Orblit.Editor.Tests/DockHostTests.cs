using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;

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

    [Fact]
    public Task APanelThatIsKeptMovesIntoTheNewPane() => Headless.Run(() =>
    {
        var kept = new Dictionary<string, TextBlock>();
        var layout = DockLayout.Standard();
        var host = new DockHost(layout, panel => kept.TryGetValue(panel.Id, out var one) ? one : kept[panel.Id] = new TextBlock());
        var window = new Window { Content = host };
        window.Show();
        var before = kept["scene"].Parent;

        host.Layout = layout.CopyWith(locked: !layout.Locked);

        Assert.NotSame(before, kept["scene"].Parent);
        Assert.Equal("pane_centre", ((Border)kept["scene"].Parent!).Name);
    });

    private static Window Show(DockLayout layout)
    {
        var window = new Window { Width = 1200, Height = 800, Content = new DockHost(layout, _ => new TextBlock()) };
        window.Show();
        Headless.Render();
        return window;
    }

    [Fact]
    public Task APaneUnderAStripIsSquareAtTheTopAndRoundAtTheBottom() => Headless.Run(() =>
    {
        var window = Show(DockLayout.Standard());

        var pane = Headless.Find<Border>(window, "pane_centre");

        Assert.Equal(new CornerRadius(0, 0, DockStyle.Radius, DockStyle.Radius), pane.CornerRadius);
        Assert.Equal(new Thickness(DockStyle.BorderWidth), pane.BorderThickness);
        Assert.Equal(OrblitColors.Surface, pane.Background);
    });

    [Fact]
    public Task TheShowingTabJoinsItsPaneAndTheOthersAreBare() => Headless.Run(() =>
    {
        var window = Show(DockLayout.Standard());

        var showing = Headless.Find<Border>(window, "tabshape_scene");
        var other = Headless.Find<Border>(window, "tabshape_game");

        Assert.Equal(OrblitColors.Surface, showing.Background);
        Assert.Equal(new CornerRadius(DockStyle.Radius, DockStyle.Radius, 0, 0), showing.CornerRadius);
        Assert.Equal(Brushes.Transparent, other.Background);
        Assert.Equal(new Thickness(0), other.BorderThickness);
    });

    [Fact]
    public Task ATabIsPaddedByTheDockNumbers() => Headless.Run(() =>
    {
        var window = Show(DockLayout.Standard());

        var tab = Headless.Find<Button>(window, "tab_scene");

        Assert.Equal(new Thickness(DockStyle.TabPadX, DockStyle.TabPadY), tab.Padding);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task TwoPanesAreAGapApartWhetherOrNotTheLayoutIsLocked(bool locked) => Headless.Run(() =>
    {
        var layout = DockLayout.Standard().CopyWith(locked: locked);
        var window = Show(layout);

        var split = Headless.Find<Grid>(window, $"split_{layout.Root.Id}");
        var left = split.Children[0];
        var right = split.Children[2];

        Assert.Equal(DockStyle.Gap, right.Bounds.X - left.Bounds.Right, 1e-6);
    });

    [Fact]
    public Task AHandleIsWiderToGrabThanTheGapItDraws() => Headless.Run(() =>
    {
        var layout = DockLayout.Standard();
        var window = Show(layout);

        var handle = Headless.Find<GridSplitter>(window, $"handle_{layout.Root.Id}_0");

        Assert.Equal(DockStyle.GrabWidth, handle.Bounds.Width, 1e-6);
    });

    [Fact]
    public Task AHandleShowsNothingUntilItIsHovered() => Headless.Run(() =>
    {
        var layout = DockLayout.Standard();
        var window = Show(layout);
        var handle = Headless.Find<GridSplitter>(window, $"handle_{layout.Root.Id}_0");
        Assert.Equal(Brushes.Transparent, handle.Background);

        window.MouseMove(Headless.Centre(window, handle));
        Assert.NotEqual(Brushes.Transparent, handle.Background);

        window.MouseMove(new Point(2, 2));
        Assert.Equal(Brushes.Transparent, handle.Background);
    });
}

using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor.Tests;

// Dart: orblit-editor/test/controls_test.dart. C# buttons carry words and no icons, so the case for one
// button giving up its icon is not ported, and the row's case keeps only when it stacks. The widths come
// from the word "Draw shape" as this font draws it, two pixels either side of where it would be cut.
public sealed class ButtonRowTests
{
    private sealed record Shown(ButtonRow Row, Button Draw, Button Cut) : IDisposable
    {
        public void Dispose() => ((Window)Row.Parent!).Close();
    }

    // Measured in a window, because a button only has its padding and border once its template is applied.
    private static double Natural()
    {
        using var wide = Show(1000);
        return wide.Draw.DesiredSize.Width;
    }

    private static Shown Show(double width)
    {
        var draw = Fields.Button("Draw shape", "Draw.", () => { });
        var cut = Fields.Button("Cut", "Cut.", () => { });
        var row = new ButtonRow { VerticalAlignment = VerticalAlignment.Top, Children = { draw, cut } };
        new Window { Width = width, Height = 300, Content = row }.Show();
        Headless.Render();
        return new Shown(row, draw, cut);
    }

    [Fact]
    public Task SideBySideInEqualSharesWhileEveryWordFits() => Headless.Run(() =>
    {
        using var shown = Show((2 * Natural()) + Space.Xs + 2);

        Assert.Equal(shown.Draw.Bounds.Y, shown.Cut.Bounds.Y);
        Assert.Equal(shown.Draw.Bounds.Width, shown.Cut.Bounds.Width, 1);
        Assert.Equal(shown.Draw.Bounds.Right + Space.Xs, shown.Cut.Bounds.X, 1);
        Assert.Equal(shown.Row.Bounds.Width, shown.Cut.Bounds.Right, 1);
    });

    [Fact]
    public Task OneAboveAnotherWithTheWholeWidthWhenAWordWouldBeCut() => Headless.Run(() =>
    {
        using var shown = Show((2 * Natural()) + Space.Xs - 2);

        Assert.Equal(shown.Draw.Bounds.Bottom + Space.Xs, shown.Cut.Bounds.Y, 1);
        Assert.Equal(shown.Row.Bounds.Width, shown.Draw.Bounds.Width, 1);
        Assert.Equal(shown.Row.Bounds.Width, shown.Cut.Bounds.Width, 1);
        // Tall enough for both, so whatever comes under the row is not drawn over the lower one.
        Assert.Equal(shown.Cut.Bounds.Bottom, shown.Row.Bounds.Height, 1);
    });
}

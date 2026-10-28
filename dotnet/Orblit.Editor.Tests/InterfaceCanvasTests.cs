using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;
using Orblit.Ui.Avalonia;

// Dart: orblit-editor/test/ui_canvas_test.dart, the canvas group, and ui_responsive_test.dart, the
// responsive canvas and column grid groups. Here the canvas follows a bench, as the workspace has it,
// so "not designing" is the bench previewing. The last two are added in C#: a click that shakes does
// not move what it chooses, and pointing at an element marks it.
public sealed class InterfaceCanvasTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_interface_canvas");

    public void Dispose() => _root.Delete(recursive: true);

    private static UiNode Button(string text) => new() { Type = "button", Text = text };

    // The title, and Play and Quit side by side in a row placed in the stack.
    private static UiDocument Menu() => new()
    {
        Root = new UiNode
        {
            Type = "stack",
            Classes = "w-full h-full",
            Children =
            [
                new UiNode { Type = "text", Classes = "text-3xl", Css = "left: 80px; top: 60px", Text = "Orblit" },
                new UiNode { Type = "row", Classes = "gap-2", Css = "left: 80px; top: 160px", Children = [Button("Play"), Button("Quit")] },
            ],
        },
    };

    // The smallest interface that is two layouts: its buttons are a column until there is room for a row.
    private static UiDocument Responsive() => new()
    {
        Canvas = new UiCanvas { Fit = CanvasFit.Responsive },
        Root = new UiNode
        {
            Type = "stack",
            Classes = "w-full h-full",
            Children =
            [
                new UiNode { Type = "text", Classes = "text-3xl", Css = "left: 80px; top: 60px", Text = "Orblit" },
                new UiNode { Type = "box", Classes = "col md:row gap-2", Css = "left: 80px; top: 160px", Children = [Button("Play"), Button("Quit")] },
            ],
        },
    };

    private static void Show(Action change)
    {
        change();
        Headless.Render();
    }

    private sealed class Rig
    {
        public Rig(string folder, UiDocument document)
        {
            var path = Path.Combine(folder, "menu.oui");
            File.WriteAllText(path, document.ToText());
            Bench = new InterfaceBench(new History(new Workspace(folder)));
            Assert.Null(Bench.OpenFile(path));
            Window = new Window { Width = 1400, Height = 900, Content = new InterfaceCanvas(Bench, () => { }) };
            Window.Show();
            Headless.Render();
        }

        public InterfaceBench Bench { get; }

        public Window Window { get; }

        public IEnumerable<T> All<T>() => Window.GetVisualDescendants().OfType<T>();

        public TextBlock Words(string text) => All<TextBlock>().Single(one => one.Text == text);

        public InterfaceGuides Guides => All<InterfaceGuides>().Single();

        public UiSurface Surface => All<UiSurface>().Single();

        public double TitleLeft => Bench.Document!.Root.Children[0].Placed!.Value.Left;

        public Point Centre(string text) => Headless.Centre(Window, Words(text));

        public Point Where(string text) => Words(text).TranslatePoint(default, Surface)!.Value;

        public void Press(string text)
        {
            Window.MouseDown(Centre(text), MouseButton.Left);
            Window.MouseUp(Centre(text), MouseButton.Left);
            Headless.Render();
        }

        // Drags the title right in steps of 14 pixels and gives where it was after each.
        public List<double> DragTitle()
        {
            var at = Centre("Orblit");
            Window.MouseDown(at, MouseButton.Left);
            var lefts = new List<double>();
            for (var step = 0; step < 24; step++)
            {
                at += new Vector(14, 0);
                Window.MouseMove(at);
                Headless.Render();
                lefts.Add(TitleLeft);
            }

            Window.MouseUp(at, MouseButton.Left);
            Headless.Render();
            return lefts;
        }
    }

    [Fact]
    public Task ShowsTheInterfaceItself() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        Assert.True(rig.Words("Orblit").IsVisible);
        Assert.True(rig.Words("Play").IsVisible);
    });

    [Fact]
    public Task DrawsItsBoundsWhileDesigning() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        Assert.True(rig.Guides.Edges);
    });

    [Fact]
    public Task TheGameGetsTheInterfaceAndNoCanvas() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        Show(() => rig.Bench.Previewing = true);

        Assert.True(rig.Words("Orblit").IsVisible);
        Assert.False(rig.Guides.Edges || rig.Guides.Columns);
        Assert.Null(Headless.Find<Panel>(rig.Window, "interface_page").Background);
    });

    [Fact]
    public Task NothingIsWrappedWhenItIsNotBeingDesigned() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        Show(() => rig.Bench.Previewing = true);
        Assert.Empty(rig.All<InterfaceCanvas.Outlined>());

        Show(() => rig.Bench.Previewing = false);

        Assert.NotEmpty(rig.All<InterfaceCanvas.Outlined>());
    });

    [Fact]
    public Task ClickingAnElementSelectsTheOneUnderThePointer() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.Press("Quit");

        // The innermost element, not the row it sits in or the stack under it.
        Assert.Equal([1, 1], rig.Bench.Selected);
    });

    [Fact]
    public Task AClickInTheGameDoesNotSelectAnything() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        Show(() => rig.Bench.Previewing = true);

        rig.Press("Quit");

        // Still the root, which a bench chooses when it opens an interface.
        Assert.Equal([], rig.Bench.Selected);
    });

    [Fact]
    public Task TheInterfaceIsLaidOutAtTheSizeItWasAuthoredAt() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        // Scaled to fit, not reflowed. The window is 1400 wide and the canvas 1920.
        Assert.Equal(1920, rig.Surface.Bounds.Width);
    });

    [Fact]
    public Task ARespondingCanvasIsLaidOutAtTheDeviceBeingPreviewed() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive());

        Show(() => rig.Bench.Device = (390, 844));
        Assert.True(rig.Where("Quit").Y > rig.Where("Play").Y);

        Show(() => rig.Bench.Device = (1440, 900));
        Assert.True(rig.Where("Quit").X > rig.Where("Play").X);
        Assert.Equal(rig.Where("Play").Y, rig.Where("Quit").Y, 3);
    });

    [Fact]
    public Task TextGrowsWithTheScreenAndStopsGrowing() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive());
        double TitleAt((double, double) device)
        {
            Show(() => rig.Bench.Device = device);
            return rig.Words("Orblit").FontSize;
        }

        var onPhone = TitleAt((390, 844));
        var onDesktop = TitleAt((1920, 1080));
        var onAWall = TitleAt((7680, 4320));

        Assert.True(onPhone < onDesktop);
        // Clamped. A wall-sized screen shows more of the interface, not four times the type size.
        Assert.Equal(onDesktop * 1.5, onAWall, 6);
    });

    [Fact]
    public Task AFixedCanvasIsStillOneLayoutScaled() => Headless.Run(() =>
    {
        var fixedOne = new UiDocument
        {
            Canvas = new UiCanvas { Width = 1920, Height = 1080 },
            Root = new UiNode { Type = "stack", Children = [new UiNode { Type = "text", Classes = "md:text-3xl", Text = "Hi" }] },
        };
        var rig = new Rig(_root.FullName, fixedOne);

        Show(() => rig.Bench.Device = (390, 844));
        var onPhone = rig.Words("Hi").FontSize;
        Show(() => rig.Bench.Device = (1920, 1080));

        // Previewing a phone shows the bars, not a second design.
        Assert.Equal(onPhone, rig.Words("Hi").FontSize);
    });

    [Fact]
    public Task ADragNeverStopsInsideAColumnEdge() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive());
        Show(() => rig.Bench.Columns = true);

        var lefts = rig.DragTitle();

        // Anything close enough to an edge to be pulled onto it is on it.
        var canvas = new UiCanvas { Fit = CanvasFit.Responsive };
        Assert.All(lefts, left => Assert.True((canvas.SnapAcross(left, 1920) ?? left) == left, $"{left} is beside an edge"));
        // And it crossed some on the way, so the rule was tested.
        Assert.Contains(lefts, left => canvas.SnapAcross(left, 1920) == left);
    });

    [Fact]
    public Task AnElementInAStackAwayFromThePageEdgeSnapsToTheGridOnThePage() => Headless.Run(() =>
    {
        const double inset = 203;
        var nested = new UiDocument
        {
            Canvas = new UiCanvas { Fit = CanvasFit.Responsive },
            Root = new UiNode
            {
                Type = "stack",
                Classes = "w-full h-full",
                Children =
                [
                    new UiNode
                    {
                        Type = "stack",
                        Css = $"left: {inset}px; top: 60px; width: 1200px; height: 400px",
                        Children = [new UiNode { Type = "text", Classes = "text-3xl", Css = "left: 0px; top: 0px", Text = "Orblit" }],
                    },
                ],
            },
        };
        var rig = new Rig(_root.FullName, nested);
        Show(() => rig.Bench.Columns = true);
        Assert.Equal(inset, rig.Where("Orblit").X);

        var at = rig.Centre("Orblit");
        rig.Window.MouseDown(at, MouseButton.Left);
        var lefts = new List<double>();
        for (var step = 0; step < 24; step++)
        {
            at += new Vector(14, 0);
            rig.Window.MouseMove(at);
            Headless.Render();
            lefts.Add(inset + rig.Bench.Document!.Root.Children[0].Children[0].Placed!.Value.Left);
        }

        rig.Window.MouseUp(at, MouseButton.Left);
        Headless.Render();

        // On the page, where the grid is drawn, and not from the edge of the stack it sits in.
        var canvas = new UiCanvas { Fit = CanvasFit.Responsive };
        Assert.All(lefts, left => Assert.True((canvas.SnapAcross(left, 1920) ?? left) == left, $"{left} is beside an edge"));
        Assert.Contains(lefts, left => canvas.SnapAcross(left, 1920) == left);
    });

    [Fact]
    public Task NothingSnapsWhileTheGridIsDown() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive());

        var lefts = rig.DragTitle();

        var canvas = new UiCanvas { Fit = CanvasFit.Responsive };
        Assert.True(lefts.Count(left => canvas.SnapAcross(left, 1920) == left) < lefts.Count);
    });

    [Fact]
    public Task AClickThatShakesDoesNotMoveWhatItChooses() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        var at = rig.Centre("Orblit");

        rig.Window.MouseDown(at, MouseButton.Left);
        rig.Window.MouseMove(at + new Vector(2, 1));
        rig.Window.MouseUp(at + new Vector(2, 1), MouseButton.Left);
        Headless.Render();

        Assert.Equal([0], rig.Bench.Selected);
        Assert.Equal(80, rig.TitleLeft);
        Assert.False(rig.Bench.History.CanUndo);
    });

    [Fact]
    public Task PointingAtAnElementMarksIt() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.Window.MouseMove(rig.Centre("Play"));
        Headless.Render();

        Assert.Equal([1, 0], rig.Bench.Hovered);
    });
}

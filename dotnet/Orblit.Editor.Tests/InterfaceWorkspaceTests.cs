using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;
using Orblit.Ui.Avalonia;

// Dart: orblit-editor/test/ui_canvas_test.dart, the laying one out group, ui_responsive_test.dart, the
// editor group, and interface_strings_test.dart, the canvas speaks a language group. Dart finds a
// control by the words on it, and here it is found by name. The last is added in C#: each panel says
// what to do with nothing open.
public sealed class InterfaceWorkspaceTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_interface_workspace");

    public void Dispose() => _root.Delete(recursive: true);

    private static UiNode Button(string text) => new() { Type = "button", Text = text };

    private static UiNode Keyed(string type, string key) => new() { Type = type, Props = new Dictionary<string, object?> { ["textKey"] = key } };

    // The title, and Play and Quit side by side in a row placed in the stack.
    private static UiDocument Menu() => new()
    {
        Name = "Main menu",
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
        Name = "Main menu",
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

    // Words by key, and one button whose words are written into the file.
    private static UiDocument Spoken() => new()
    {
        Name = "Menu",
        Root = new UiNode
        {
            Type = "column",
            Children = [Keyed("text", "greet"), Keyed("button", "play"), Keyed("button", "quit"), Button("Fixed")],
        },
    };

    private static readonly Dictionary<string, string> Languages = new()
    {
        ["en"] = """{"greet": "Hello", "play": "Play", "quit": "Quit"}""",
        ["fr"] = """{"greet": "Bonjour", "play": "Jouer"}""",
    };

    // The workspace's panels where the dock puts them: the tree, the shelf over the canvas, then the
    // design side. Undo and save are the editor's, so a test calls them on the bench.
    private sealed class Rig
    {
        private readonly string _path;

        public Rig(string folder, UiDocument document, IReadOnlyDictionary<string, string>? languages = null, double height = 950)
        {
            _path = Path.Combine(folder, "menu.oui");
            File.WriteAllText(_path, document.ToText());
            var strings = Path.Combine(folder, "strings");
            foreach (var (locale, text) in languages ?? new Dictionary<string, string>())
            {
                Directory.CreateDirectory(strings);
                File.WriteAllText(Path.Combine(strings, $"{locale}.json"), text);
            }

            Bench = new InterfaceBench(new History(new Workspace(folder)), strings);
            Bench.LoadStrings();
            Assert.Null(Bench.OpenFile(_path));
            var elements = new InterfaceElements(Bench) { Width = 248 };
            var middle = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            var canvas = new InterfaceCanvas(Bench, () => { });
            canvas[Grid.RowProperty] = 1;
            middle.Children.Add(new InterfaceShelf(Bench, () => { }));
            middle.Children.Add(canvas);
            middle[Grid.ColumnProperty] = 1;
            var design = new InterfaceDesign(Bench) { Width = 296 };
            design[Grid.ColumnProperty] = 2;
            Window = new Window
            {
                Width = 1600,
                Height = height,
                Content = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { elements, middle, design } },
            };
            Window.Show();
            Headless.Render();
        }

        public InterfaceBench Bench { get; }

        public Window Window { get; }

        public UiSurface Surface => Window.GetVisualDescendants().OfType<UiSurface>().Single();

        public InterfaceGuides Guides => Window.GetVisualDescendants().OfType<InterfaceGuides>().Single();

        public int Outlined => Window.GetVisualDescendants().OfType<InterfaceCanvas.Outlined>().Count(one => one.Outline.BorderBrush is not null);

        public string Text(string name) => Headless.Find<TextBlock>(Window, name).Text ?? "";

        public string? Tip(string name) => ToolTip.GetTip(Headless.Find<Control>(Window, name)) as string;

        // Words on the canvas rather than in the tree, which names elements by their words as well.
        public IEnumerable<TextBlock> OnCanvas(string text) => Surface.GetVisualDescendants().OfType<TextBlock>().Where(one => one.Text == text);

        public IEnumerable<TextBlock> InTree(string text) =>
            Headless.Find<InterfaceElements>(Window, "interface_elements").GetVisualDescendants().OfType<TextBlock>().Where(one => one.Text == text);

        public bool Says(string text) => Window.GetVisualDescendants().OfType<TextBlock>().Any(one => one.Text == text);

        // The design side scrolls, so what is pressed is brought into view first.
        public void Press(string name)
        {
            var control = Headless.Find<Control>(Window, name);
            control.BringIntoView();
            Headless.Render();
            Headless.Click(Window, control);
        }

        public void Press(Control control) => Headless.Click(Window, control);

        public void Undo()
        {
            Bench.History.Undo();
            Headless.Render();
        }

        // Typed into a focused box and left, which ends the step the typing made.
        public void Type(string name, string text)
        {
            var box = Headless.Find<TextBox>(Window, name);
            Assert.True(box.Focus());
            box.Text = text;
            Headless.Render();
            Assert.True(Headless.Find<Button>(Window, "shelf_new_interface").Focus());
            Headless.Render();
        }

        // Moves by an exact amount in even steps, without the drag slop padding it.
        public void DragBy(TextBlock what, Vector by, int steps = 12)
        {
            var at = Headless.Centre(Window, what);
            Window.MouseDown(at, MouseButton.Left);
            for (var step = 0; step < steps; step++)
            {
                at += by / steps;
                Window.MouseMove(at);
                Headless.Render();
            }

            Window.MouseUp(at, MouseButton.Left);
            Headless.Render();
        }

        public UiDocument Saved()
        {
            Assert.Empty(Bench.SaveAll());
            var read = UiDocument.Read(File.ReadAllText(_path));
            Assert.NotNull(read);
            return read;
        }
    }

    [Fact]
    public Task TheElementsAreListedAsATree() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        // Named by their words where they have any, since "Play" says more than "button".
        Assert.Single(rig.InTree("Play"));
    });

    [Fact]
    public Task AddingPutsAnElementInsideTheSelection() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        // The root is selected to begin with.
        rig.Press("add_box");

        Assert.NotEmpty(rig.InTree("box"));
        Assert.Equal("box", rig.Bench.Document!.Root.Children[^1].Type);
    });

    [Fact]
    public Task AChangeCanBeUndone() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        rig.Press("add_box");
        Assert.NotEmpty(rig.InTree("box"));

        rig.Undo();

        Assert.Empty(rig.InTree("box"));
    });

    [Fact]
    public Task SavingWritesAFileThatOpensAgain() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.Press("add_box");

        Assert.Equal("box", rig.Saved().Root.Children[^1].Type);
    });

    [Fact]
    public Task PreviewingTakesTheChromeAway() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        Assert.True(rig.Guides.Edges);

        rig.Press("shelf_preview");

        Assert.False(rig.Guides.Edges || rig.Guides.Columns);
        // And the interface is still there, which is the point of the toggle.
        Assert.Single(rig.OnCanvas("Orblit"));
    });

    [Fact]
    public Task TurningElementOutlinesOffLeavesOnlyTheSelection() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        Assert.True(rig.Outlined > 1);

        rig.Press("shelf_element_outlines");

        // The selection keeps its mark. Taking away what answers "what am I editing" is a lost selection.
        Assert.Equal(1, rig.Outlined);
    });

    [Fact]
    public Task TheCanvasGuidesHaveAToggleOfTheirOwn() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        Assert.True(rig.Guides.Edges);

        rig.Press("shelf_canvas_guides");

        Assert.False(rig.Guides.Edges);
        // And the elements are still outlined: two controls, two things.
        Assert.True(rig.Outlined > 1);
    });

    [Fact]
    public Task AnElementOnAStackStaysUnderThePointer() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        var title = rig.OnCanvas("Orblit").Single();
        var at = Headless.Centre(rig.Window, title);
        rig.Window.MouseDown(at, MouseButton.Left);
        for (var step = 0; step < 6; step++)
        {
            at += new Vector(8, 4);
            rig.Window.MouseMove(at);
            Headless.Render();
        }

        var wasAt = rig.OnCanvas("Orblit").Single().TranslatePoint(default, rig.Window)!.Value;
        for (var step = 0; step < 10; step++)
        {
            at += new Vector(12, 6);
            rig.Window.MouseMove(at);
            Headless.Render();
        }

        var nowAt = rig.OnCanvas("Orblit").Single().TranslatePoint(default, rig.Window)!.Value;
        rig.Window.MouseUp(at, MouseButton.Left);
        Headless.Render();

        // On screen, not in canvas units. The canvas is zoomed to fit the panel, and an element that
        // moved by the drag in canvas units would lag the pointer by exactly the zoom.
        Assert.Equal(120, nowAt.X - wasAt.X, 1.0);
        Assert.Equal(60, nowAt.Y - wasAt.Y, 1.0);
    });

    [Fact]
    public Task WhereItWasDraggedToIsWhatGetsSaved() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.DragBy(rig.OnCanvas("Orblit").Single(), new Vector(120, 60));

        // Further than the drag, because the 1920-wide canvas is shown in less.
        var after = rig.Saved().Root.Children[0].Placed!.Value;
        Assert.True(after.Left > 80 + 120);
        Assert.True(after.Top > 60 + 60);
    });

    [Fact]
    public Task AWholeDragIsOneUndoStep() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.DragBy(rig.OnCanvas("Orblit").Single(), new Vector(80, 0));
        rig.Undo();

        // One press put it back, not eighty.
        Assert.False(rig.Bench.AnyUnsaved);
        Assert.Equal(80, rig.Saved().Root.Children[0].Placed!.Value.Left);
    });

    [Fact]
    public Task ADragTheCanvasLeavesHalfWayIsAStepOfItsOwn() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        var at = Headless.Centre(rig.Window, rig.OnCanvas("Orblit").Single());
        rig.Window.MouseDown(at, MouseButton.Left);
        for (var step = 0; step < 6; step++)
        {
            at += new Vector(10, 0);
            rig.Window.MouseMove(at);
            Headless.Render();
        }

        // A change of workspace takes the canvas away with the button still down.
        var content = rig.Window.Content;
        rig.Window.Content = null;
        Headless.Render();
        rig.Window.Content = content;
        Headless.Render();
        rig.Window.MouseUp(at, MouseButton.Left);
        rig.DragBy(rig.OnCanvas("Orblit").Single(), new Vector(40, 0));
        rig.Undo();

        // The second drag is undone and the first is still there.
        Assert.True(rig.Bench.AnyUnsaved);
        Assert.Equal(Math.Round(rig.Bench.Document!.Root.Children[0].Placed!.Value.Left), rig.Bench.Document.Root.Children[0].Placed!.Value.Left);
    });

    [Fact]
    public Task AnElementAColumnLaysOutIsNotDraggable() => Headless.Run(() =>
    {
        var flowed = new UiDocument
        {
            Root = new UiNode { Type = "column", Classes = "w-full h-full p-8", Children = [new UiNode { Type = "text", Text = "Row one" }] },
        };
        var rig = new Rig(_root.FullName, flowed);

        rig.DragBy(rig.OnCanvas("Row one").Single(), new Vector(100, 100));

        // A place its parent throws away on the next layout is not a move.
        Assert.False(rig.Bench.AnyUnsaved);
        Assert.Single(rig.OnCanvas("Row one"));
    });

    [Fact]
    public Task TheCanvasSizeCanBeChanged() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());

        rig.Press("size_390_844");

        var canvas = rig.Saved().Canvas;
        Assert.Equal(390, canvas.Width);
        Assert.Equal(844, canvas.Height);
    });

    [Fact]
    public Task EditingAnElementReachesTheCanvas() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Menu());
        rig.Press(rig.OnCanvas("Orblit").Single());

        rig.Type("entry_words", "Orblit Engine");

        Assert.Single(rig.OnCanvas("Orblit Engine"));
    });

    [Fact]
    public Task TheToolbarSaysWhatTheLayoutIsAnsweringTo() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        Assert.Equal("1920 × 1080", rig.Text("shelf_laid_out"));
        Assert.Equal("xl", rig.Text("shelf_breakpoint"));

        rig.Press("device_phone");

        Assert.Equal("390 × 844", rig.Text("shelf_laid_out"));
        Assert.Equal("base", rig.Text("shelf_breakpoint"));
    });

    [Fact]
    public Task AddingAColumnPutsTheGridUp() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        Assert.Equal("Turn on column grid.", rig.Tip("shelf_column_grid"));

        rig.Press("add_column");

        // A container with nothing to line it up against is where somebody goes looking for the setting.
        Assert.Equal("Turn off column grid.", rig.Tip("shelf_column_grid"));
    });

    [Fact]
    public Task AddingAButtonLeavesTheGridAlone() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);

        rig.Press("add_button");

        Assert.Equal("Turn on column grid.", rig.Tip("shelf_column_grid"));
    });

    [Fact]
    public Task SplittingMakesARowOfEqualColumns() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);

        // The root is selected to begin with, so this splits the canvas.
        rig.Press("split_3");

        var top = rig.Saved().Root;
        Assert.Equal("row", top.Type);
        Assert.Equal(3, top.Children.Count);
        Assert.All(top.Children, child => Assert.Equal("column", child.Type));
        Assert.All(top.Children, child => Assert.Contains("md:flex-1", child.Classes, StringComparison.Ordinal));
        // Stacked until there is room: equal widths only where they are widths.
        Assert.Contains("md:row", top.Classes, StringComparison.Ordinal);
    });

    [Fact]
    public Task WhatWasInItGoesIntoTheFirstColumnWithoutItsPlace() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);

        rig.Press("split_2");

        var top = rig.Saved().Root;
        Assert.Equal(2, top.Children[0].Children.Count);
        Assert.Empty(top.Children[^1].Children);
        // A place left on an element no longer in a stack means nothing.
        Assert.All(top.Children[0].Children, child => Assert.Null(child.Placed));
    });

    [Fact]
    public Task ASplitRowStillShowsWhatIsInItsColumns() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        rig.Press("add_row");
        rig.Press("split_4");

        rig.Press(rig.InTree("column").ElementAt(2));
        rig.Press("add_button");

        Assert.Single(rig.OnCanvas("Button"));
    });

    [Fact]
    public Task SplittingSomethingPlacedOnAStackGivesItAWidth() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);

        rig.Press("add_row");
        rig.Press("split_2");

        // Reaching the far edge is the honest reading of "split this into columns".
        Assert.Contains("right: 0", rig.Saved().Root.Children[^1].Css, StringComparison.Ordinal);
    });

    [Fact]
    public Task ColumnsStackOnAPhoneAndSitSideBySideOnALaptop() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        rig.Press("add_row");
        rig.Press("split_3");
        bool rows() => rig.Surface.GetVisualDescendants().OfType<FlexPanel>().Any(one => one.Orientation == Orientation.Horizontal);

        rig.Press("device_laptop");
        Assert.True(rows());

        rig.Press("device_phone");
        // Three columns across a phone are three columns nobody can read.
        Assert.False(rows());
    });

    [Fact]
    public Task ADeviceCanBeHeldSideways() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);

        rig.Press("device_phone");
        Assert.Equal("390 × 844", rig.Text("shelf_laid_out"));

        rig.Press("held_landscape");
        Assert.Equal("844 × 390", rig.Text("shelf_laid_out"));

        // The way it is held is kept when the device changes, so a tablet comes on its side.
        rig.Press("device_tablet");
        Assert.Equal("1112 × 834", rig.Text("shelf_laid_out"));
    });

    [Fact]
    public Task TheGridSaysWhenItIsDrawingFewerColumnsThanAuthored() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        Assert.Equal("12 across this screen", rig.Text("grid_count"));

        rig.Press("device_phone");

        Assert.Equal("4 across this screen. 12 is too fine here.", rig.Text("grid_count"));
    });

    [Fact]
    public Task TheSplitCanBeUndone() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Responsive(), height: 1100);
        rig.Press("split_2");
        Assert.NotEmpty(rig.InTree("row"));

        rig.Undo();

        Assert.NotEmpty(rig.InTree("stack"));
    });

    [Fact]
    public Task ShowsTheWordsOfTheLanguageChosen() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Spoken(), Languages);
        Assert.Single(rig.OnCanvas("Play"));

        rig.Press("locale_fr");

        Assert.Single(rig.OnCanvas("Jouer"));
        Assert.Empty(rig.OnCanvas("Play"));
        // Words written into the file are the same in every language.
        Assert.Single(rig.OnCanvas("Fixed"));
    });

    [Fact]
    public Task ListsWhatTheLanguageIsMissing() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Spoken(), Languages);
        Assert.False(rig.Says("MISSING IN EN"));

        rig.Press("locale_fr");

        Assert.True(rig.Says("MISSING IN FR"));
        Assert.True(rig.Says("quit"));
    });

    [Fact]
    public Task AKeyIsTypedIntoTheKeyFieldAndCanBeUndone() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Spoken(), Languages);
        rig.Press(rig.OnCanvas("Fixed").Single());

        rig.Type("entry_key", "play");

        Assert.Equal("play", rig.Bench.Element?.TextKey);
        Assert.Empty(rig.OnCanvas("Fixed"));

        rig.Undo();

        Assert.Null(rig.Bench.Element?.TextKey);
        Assert.Single(rig.OnCanvas("Fixed"));
    });

    [Fact]
    public Task TheSectionIsLeftOutOfAProjectInOneLanguage() => Headless.Run(() =>
    {
        var plain = new UiDocument { Name = "Plain", Root = new UiNode { Type = "column", Children = [new UiNode { Type = "text" }] } };
        var rig = new Rig(_root.FullName, plain);

        Assert.DoesNotContain(rig.Window.GetVisualDescendants().OfType<Control>(), one => one.Name == "section_languages");
    });

    [Fact]
    public Task AnElementIsNamedByItsKeyInTheTree() => Headless.Run(() =>
    {
        var rig = new Rig(_root.FullName, Spoken(), Languages);

        Assert.Single(rig.InTree("play"));
    });

    [Fact]
    public Task EachPanelSaysWhatToDoWithNothingOpen() => Headless.Run(() =>
    {
        var bench = new InterfaceBench(new History(new Workspace(_root.FullName)));
        var asked = 0;
        var window = new Window
        {
            Width = 1600,
            Height = 950,
            Content = new StackPanel
            {
                Children = { new InterfaceShelf(bench, () => { }), new InterfaceElements(bench), new InterfaceCanvas(bench, () => asked++), new InterfaceDesign(bench) },
            },
        };
        window.Show();
        Headless.Render();

        Assert.False(Headless.Find<Control>(window, "interface_shelf").GetVisualDescendants().OfType<StackPanel>().First().IsVisible);
        Assert.True(Headless.Find<Control>(window, "interface_elements_none").IsVisible);
        Assert.True(Headless.Find<Control>(window, "interface_design_none").IsVisible);
        Headless.Click(window, "button_new_interface");
        Assert.Equal(1, asked);
    });
}

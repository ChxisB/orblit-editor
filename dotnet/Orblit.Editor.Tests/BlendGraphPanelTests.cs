using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

// Dart: orblit-editor/test/editor_shell_blends_test.dart, the tests of the panel opened empty and of the
// tab marked until it is saved, here on the panel alone. The rest is added in C#: the tabs themselves,
// and the fields going under the graph when the panel is narrow.
public sealed class BlendGraphPanelTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_blend_panel");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private sealed class Rig
    {
        public Rig(double width = 900)
        {
            History = new History(new Workspace("/project"));
            Bench = new BlendBench(History);
            Window = new Window
            {
                Width = width,
                Height = 600,
                Content = new BlendGraphPanel(Bench, History, () => ["idle", "walk"], () => Made++),
            };
            Window.Show();
            Headless.Render();
        }

        public History History { get; }

        public BlendBench Bench { get; }

        public Window Window { get; }

        public int Made { get; private set; }

        public bool Seen(string name) => Headless.Find<Control>(Window, name).IsEffectivelyVisible;

        public bool Has(string name) => Headless.All<Control>(Window).Any(one => one.Name == name);

        public string TextOf(string name) => Headless.Find<TextBlock>(Window, name).Text ?? "";

        public Point TopLeft(string name) =>
            Headless.Find<Control>(Window, name).TranslatePoint(new Point(0, 0), Window)!.Value;

        public Rect Bounds(string name) => Headless.Find<Control>(Window, name).Bounds;

        public void Click(string name) => Headless.Click(Window, name);
    }

    private static BlendDocument Named(string name) => new(
        name,
        [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
        new Dictionary<string, double> { ["speed"] = 0 },
        [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]);

    private Rig Open(params string[] names)
    {
        var rig = new Rig();
        foreach (var name in names)
        {
            rig.Bench.OpenDocument(PathOf($"{name}.oblend"), Named(name));
        }

        Headless.Render();
        return rig;
    }

    [Fact]
    public Task WithNoBlendOpenItSaysWhatABlendIsAndOffersToMakeOne() => Headless.Run(() =>
    {
        var rig = new Rig();

        Assert.True(rig.Seen("blend_empty"));
        Assert.False(rig.Seen("blend_shown"));
        Assert.StartsWith("No blend is open.", rig.TextOf("blend_none"));

        rig.Click("button_new_blend");

        Assert.Equal(1, rig.Made);
    });

    [Fact]
    public Task AnOpenBlendShowsItsGraphWithTheFieldsBesideIt() => Headless.Run(() =>
    {
        var rig = Open("moves");

        Assert.False(rig.Seen("blend_empty"));
        Assert.True(rig.Seen("blend_shown"));
        Assert.True(rig.Has("state_idle"));
        Assert.True(rig.Has("state_move"));
        Assert.True(rig.Has("change_0"));
        var (graph, fields) = (rig.TopLeft("blend_canvas"), rig.TopLeft("blend_inspector"));
        Assert.Equal(graph.Y, fields.Y, 1);
        Assert.Equal(graph.X + rig.Bounds("blend_canvas").Width + 1, fields.X, 1);
        Assert.Equal(288, rig.Bounds("blend_inspector").Width, 1);
    });

    [Fact]
    public Task APanelNarrowerThanTheFieldsAndTheGraphTogetherPutsTheFieldsUnderTheGraph() => Headless.Run(() =>
    {
        var rig = Open("moves");

        rig.Window.Width = 559;
        Headless.Render();

        var (graph, fields) = (rig.TopLeft("blend_canvas"), rig.TopLeft("blend_inspector"));
        Assert.Equal(graph.X, fields.X, 1);
        Assert.Equal(graph.Y + rig.Bounds("blend_canvas").Height + 1, fields.Y, 1);
        // Three parts of the height to the graph and two to the fields, with a line between.
        Assert.Equal(rig.Bounds("blend_canvas").Height / 3, rig.Bounds("blend_inspector").Height / 2, 0);

        // At exactly the width they fit in, they go side by side.
        rig.Window.Width = 560;
        Headless.Render();

        Assert.Equal(rig.TopLeft("blend_canvas").Y, rig.TopLeft("blend_inspector").Y, 1);
    });

    [Fact]
    public Task EachOpenBlendIsATabNamedWithoutItsExtensionAndThePressedOneIsShown() => Headless.Run(() =>
    {
        var rig = Open("moves", "idles");

        Assert.Equal("moves", rig.TextOf("blend_tab_text_moves"));
        Assert.Equal("idles", rig.TextOf("blend_tab_text_idles"));
        Assert.Equal(OrblitColors.Ember, Headless.Find<Border>(rig.Window, "blend_tab_idles").BorderBrush);
        Assert.NotEqual(OrblitColors.Ember, Headless.Find<Border>(rig.Window, "blend_tab_moves").BorderBrush);

        rig.Click("blend_tab_text_moves");

        Assert.Equal(PathOf("moves.oblend"), rig.Bench.Shown!.Path);
        Assert.Equal(OrblitColors.Ember, Headless.Find<Border>(rig.Window, "blend_tab_moves").BorderBrush);
        Assert.NotEqual(OrblitColors.Ember, Headless.Find<Border>(rig.Window, "blend_tab_idles").BorderBrush);
    });

    [Fact]
    public Task ClosingATabShowsTheOneBeforeItAndClosingTheLastShowsTheEmptyPanel() => Headless.Run(() =>
    {
        var rig = Open("moves", "idles");

        rig.Click("blend_close_idles");

        Assert.Equal(PathOf("moves.oblend"), rig.Bench.Shown!.Path);
        Assert.False(rig.Has("blend_tab_idles"));

        rig.Click("blend_close_moves");

        Assert.Null(rig.Bench.Shown);
        Assert.True(rig.Seen("blend_empty"));
        Assert.False(rig.Seen("blend_shown"));
    });

    [Fact]
    public Task ATabWithChangesIsMarkedUntilItIsSaved() => Headless.Run(() =>
    {
        var rig = Open("moves");
        Assert.Equal("moves", rig.TextOf("blend_tab_text_moves"));

        rig.Bench.Edit("Set start state", one => one.WithStart("move"));
        Headless.Render();
        Assert.Equal("moves •", rig.TextOf("blend_tab_text_moves"));

        rig.History.Undo();
        Headless.Render();
        Assert.Equal("moves", rig.TextOf("blend_tab_text_moves"));

        rig.History.Redo();
        Assert.Null(rig.Bench.Save(rig.Bench.Shown!));
        Headless.Render();
        Assert.Equal("moves", rig.TextOf("blend_tab_text_moves"));
    });

    [Fact]
    public Task ThePlusBesideTheTabsMakesABlend() => Headless.Run(() =>
    {
        var rig = Open("moves");

        rig.Click("blend_new");

        Assert.Equal(1, rig.Made);
    });
}

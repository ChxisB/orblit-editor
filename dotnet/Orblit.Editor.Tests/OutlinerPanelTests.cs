using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The hierarchy as the window shows it, and the edit menu that its selection feeds.</summary>
public sealed class OutlinerPanelTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS) { Width = 1440, Height = 900 };
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static IEnumerable<T> All<T>(Control root) => root.GetLogicalDescendants().OfType<T>();

    private static T Find<T>(Control root, string name)
        where T : Control => All<T>(root).First(one => one.Name == name);

    private static bool Has(Control root, string name) => All<Control>(root).Any(one => one.Name == name);

    private static Point Centre(Window window, string name)
    {
        var at = Find<Control>(window, name);
        return at.TranslatePoint(new Point(at.Bounds.Width / 2, at.Bounds.Height / 2), window)!.Value;
    }

    private static void Click(Window window, string name, RawInputModifiers held = RawInputModifiers.None)
    {
        var at = Centre(window, name);
        window.MouseDown(at, MouseButton.Left, held);
        window.MouseUp(at, MouseButton.Left, held);
        Headless.Render();
    }

    private static string Header(Window window, string name) =>
        (string)Items(Find<Menu>(window, "menu")).First(one => one.Name == name).Header!;

    private static IEnumerable<MenuItem> Items(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => new[] { item }.Concat(Items(item)));

    // A palette label is drawn in runs, so the part that matched can be lit.
    private static string TextOf(TextBlock block) =>
        block.Inlines is { Count: > 0 } runs ? string.Concat(runs.OfType<Run>().Select(run => run.Text)) : block.Text ?? "";

    private static PaletteDialog Palette(Window window) => Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));

    [Fact]
    public Task ShowsTheSharedSetAndTheSceneWithItsObjectsAndHowManyScenesAreOpen() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.All(new[] { "row_shared", "row_scene0", "row_sun", "row_props", "row_crate", "row_weather" }, name => Assert.True(Has(window, name)));
        Assert.Equal("1 scene open", Find<TextBlock>(window, "outliner_count").Text);
        window.Close();
    });

    [Fact]
    public Task ClickingARowSelectsItAndTheSelectionIsDrawn() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Click(window, "row_crate");

        Assert.Equal(["crate"], session.Selected);
        Assert.NotEqual(Find<Border>(window, "row_sun").Background, Find<Border>(window, "row_crate").Background);
        window.Close();
    });

    [Fact]
    public Task ClickingASceneRowClearsTheSelection() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Click(window, "row_crate");

        Click(window, "row_scene0");

        Assert.Empty(session.Selected);
        window.Close();
    });

    [Fact]
    public Task TheEditMenuSaysWhatPasteWouldDo() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Assert.Equal("Paste", Header(window, "menu_paste"));

        Click(window, "row_crate");
        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Meta);

        Assert.Equal("Paste Crate", Header(window, "menu_paste"));
        window.Close();
    });

    [Fact]
    public Task CommandClickAddsToTheSelection() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Click(window, "row_sun");

        Click(window, "row_ground", RawInputModifiers.Meta);

        Assert.Equal(["sun", "ground"], session.Selected);
        Assert.Equal("Copy 2 objects", Header(window, "menu_copy"));
        window.Close();
    });

    [Fact]
    public Task ShiftClickTakesEverythingBetween() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Click(window, "row_sun");

        Click(window, "row_cube", RawInputModifiers.Shift);

        Assert.Equal(["sun", "ground", "props", "cube"], session.Selected);
        Assert.Equal("Copy 4 objects", Header(window, "menu_copy"));
        window.Close();
    });

    [Fact]
    public Task TheFilterBoxNarrowsTheRowsAndSaysSoWhenNothingHasThatName() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var nothing = Find<TextBlock>(window, "outliner_nothing");
        Assert.False(nothing.IsVisible);

        Find<TextBox>(window, "outliner_filter").Text = "cub";
        Headless.Render();
        Assert.True(Has(window, "row_cube"));
        Assert.False(Has(window, "row_ground"));

        Find<TextBox>(window, "outliner_filter").Text = "zzz";
        Headless.Render();
        Assert.True(nothing.IsVisible);
        Assert.False(Has(window, "row_cube"));
        window.Close();
    });

    [Fact]
    public Task AFoldHidesWhatIsInsideAndDoesNotSelectTheParent() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Click(window, "fold_props");

        Assert.False(Has(window, "row_crate"));
        Assert.Empty(session.Selected);
        Click(window, "fold_props");
        Assert.True(Has(window, "row_crate"));
        window.Close();
    });

    [Fact]
    public Task TheRemoveButtonOnARowDeletesThatObject() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var row = Centre(window, "row_sun");
        window.MouseMove(row);
        Headless.Render();

        Click(window, "delete_sun");

        Assert.Null(session.Workspace.Loaded!.Scene!["sun"]);
        Assert.False(Has(window, "row_sun"));
        window.Close();
    });

    [Fact]
    public Task DeleteAndTheEditKeysActOnTheSelectionButNotWhileATextBoxHasTheKeyboard() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Click(window, "row_sun");
        var filter = Find<TextBox>(window, "outliner_filter");
        filter.Focus();
        Headless.Render();

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.NotNull(session.Workspace.Loaded!.Scene!["sun"]);

        // A click on a row takes the keyboard back from the box.
        Click(window, "row_sun");
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Null(session.Workspace.Loaded!.Scene!["sun"]);
        window.Close();
    });

    [Fact]
    public Task ThePlusOpensThePaletteOnTheAddCommands() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(window, "outliner_add");

        var palette = Palette(window);
        Assert.Equal("Add ", Find<TextBox>(palette, "field").Text);
        var words = All<TextBlock>(palette).Select(TextOf).ToList();
        Assert.Contains(words, text => text.Contains("Add ", StringComparison.Ordinal));
        // Only what adds something: the rest of the palette is not offered.
        Assert.DoesNotContain(words, text => text.Contains("Go to", StringComparison.Ordinal));
        window.Close();
    });

    [Fact]
    public Task ThePlusAddsWhatIsChosen() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var before = session.Workspace.Loaded!.Scene!.Objects.Count;

        Click(window, "outliner_add");
        Palette(window).KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();
        Headless.Render();

        Assert.Equal(before + 1, session.Workspace.Loaded!.Scene!.Objects.Count);
        window.Close();
    });

    [Fact]
    public Task TheAddMenuAddsAnObjectOfThatKindAndSelectsIt() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var scene = session.Workspace.Loaded!.Scene!;

        Items(Find<Menu>(window, "menu")).First(one => one.Name == "add_Light").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        var added = Assert.Single(scene.Objects, one => one.Name == "Light");
        Assert.Equal(ObjectKind.Light, added.Kind);
        Assert.Equal([added.Id], session.Selected);
        Assert.True(Has(window, $"row_{added.Id}"));
        window.Close();
    });

    [Fact]
    public Task TheEditMenuEntriesWaitForASelection() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var names = new[] { "menu_cut", "menu_copy", "menu_duplicate", "menu_delete" };
        Assert.All(names, name => Assert.False(Items(Find<Menu>(window, "menu")).First(one => one.Name == name).IsEnabled));

        Click(window, "row_sun");

        Assert.All(names, name => Assert.True(Items(Find<Menu>(window, "menu")).First(one => one.Name == name).IsEnabled));
        window.Close();
    });

    [Fact]
    public Task DroppingOnARowMovesTheObjectThereAndARefusedDropMovesNothing() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var panel = All<OutlinerPanel>(window).Single();
        var scene = session.Workspace.Loaded!.Scene!;

        Assert.True(panel.Release("props", "sun", 0.5));
        Assert.Equal("props", scene["sun"]!.ParentId);

        Assert.False(panel.Release("props", "props", 0.5));
        Assert.False(panel.Release("cube", "props", 0.5));
        Assert.False(panel.Release("missing", "sun", 0.5));
        Assert.Null(scene["props"]!.ParentId);
        window.Close();
    });
}

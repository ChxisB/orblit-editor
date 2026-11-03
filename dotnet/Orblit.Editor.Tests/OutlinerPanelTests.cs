using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Controls.Documents;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The hierarchy as the window shows it, and the edit menu that its selection feeds.</summary>
public sealed class OutlinerPanelTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    // With props, a second scene is listed beside the loaded one and is not loaded. With many, the main
    // scene holds that many loose objects, o0 onwards, and nothing else.
    private (EditorSession Session, EditorWindow Window) Open(bool props = false, int many = 0)
    {
        if (many > 0)
        {
            Directory.CreateDirectory(Path.Combine(_temp.FullName, "scenes"));
            File.WriteAllText(
                Path.Combine(_temp.FullName, "scenes", "main" + Orblit.Scene.SceneDocument.SceneExtension),
                SceneDocument.Encode(new EditorScene([.. Enumerable.Range(0, many).Select(i => new SceneObject(id: $"o{i}", name: $"Object {i}", kind: ObjectKind.Mesh))])));
        }

        if (props)
        {
            Directory.CreateDirectory(Path.Combine(_temp.FullName, "scenes"));
            File.WriteAllText(
                Path.Combine(_temp.FullName, "scenes", "props" + Orblit.Scene.SceneDocument.SceneExtension),
                SceneDocument.Encode(new EditorScene([new SceneObject(id: "barrel", name: "Barrel", kind: ObjectKind.Mesh)], name: "Props")));
        }

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

    private static List<Border> RowControls(Control root) =>
        [.. All<Border>(root).Where(one => one.Name?.StartsWith("row_", StringComparison.Ordinal) == true)];

    private static ScrollViewer ListOf(Control root) => Find<Control>(root, "outliner_rows").FindAncestorOfType<ScrollViewer>()!;

    private static void ScrollTo(Window window, double offset)
    {
        ListOf(window).Offset = new Vector(0, offset);
        Headless.Render();
        Headless.Render();
    }

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

    private static void DoubleClick(Window window, string name)
    {
        var at = Centre(window, name);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Headless.Render();
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // Where a row's name is drawn, since the indent is inside the row.
    private static Point NameAt(Window window, string row, string name) =>
        All<TextBlock>(Find<Control>(window, row)).First(one => one.Text == name).TranslatePoint(default, window)!.Value;

    private static IEnumerable<string> Notes(Window window, string row) =>
        All<TextBlock>(Find<Control>(window, row)).Select(one => one.Text ?? "");

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

    [Fact]
    public Task AnUnloadedSceneRowSaysHowToLoadIt() => Headless.Run(() =>
    {
        var (_, window) = Open(props: true);

        Assert.Equal("Double-click to load props", ToolTip.GetTip(Find<Control>(window, "row_scene1")));
        Assert.DoesNotContain("not loaded", Notes(window, "row_scene0"));
        window.Close();
    });

    [Fact]
    public Task OnlyTheLoadedSceneHasObjectsInTheTree() => Headless.Run(() =>
    {
        var (_, window) = Open(props: true);

        Assert.True(Has(window, "row_scene1"));
        Assert.Single(All<TextBlock>(window), one => one.Text == "not loaded");
        Assert.False(Has(window, "row_barrel"));
        window.Close();
    });

    [Fact]
    public Task TheSceneIsTheRootWithItsObjectsUnderIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var sceneAt = NameAt(window, "row_scene0", "main");

        foreach (var (row, name) in new[] { ("row_sun", "Sun"), ("row_ground", "Ground"), ("row_props", "Props") })
        {
            var at = NameAt(window, row, name);
            Assert.True(at.Y > sceneAt.Y);
            Assert.True(at.X > sceneAt.X);
        }

        window.Close();
    });

    [Fact]
    public Task DraggingARowOntoAnotherMakesItAChild() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Assert.True(All<OutlinerPanel>(window).Single().Release("props", "ground", 0.5));

        Assert.Equal("Move Ground", session.History.UndoLabel);
        window.Close();
    });

    [Fact]
    public Task DraggingToTheEdgeOfARowReordersInstead() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Assert.True(All<OutlinerPanel>(window).Single().Release("sun", "ground", 0.05));

        Assert.Equal("Reorder Ground", session.History.UndoLabel);
        window.Close();
    });

    [Fact]
    public Task AThingCannotBeDroppedIntoItsOwnChild() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Assert.False(All<OutlinerPanel>(window).Single().Release("cube", "props", 0.5));

        Assert.Null(session.History.UndoLabel);
        Assert.True(Has(window, "row_props"));
        window.Close();
    });

    [Fact]
    public Task DoubleClickingASceneLoadsIt() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);
        session.Save();
        Headless.Render();

        DoubleClick(window, "row_scene1");
        Headless.Render();

        Assert.True(Has(window, "row_barrel"));
        Assert.False(Has(window, "row_props"));
        Assert.Equal("props", session.Workspace.Loaded!.Title);
        window.Close();
    });

    // The change is made after a save, so what the dialog is asked about is only the Group.
    private static (string Added, LeaveDialog Dialog) LeaveMainByDoubleClicking(EditorSession session, EditorWindow window)
    {
        session.Save();
        session.Add(ObjectKind.Group);
        var added = session.Selected.Single();
        Headless.Render();

        DoubleClick(window, "row_scene1");
        Headless.Render();
        return (added, Assert.IsType<LeaveDialog>(Assert.Single(window.OwnedWindows)));
    }

    private static void Answer(LeaveDialog dialog, string button)
    {
        Click(Find<Button>(dialog, button));
        Headless.Render();
        Headless.Render();
    }

    [Fact]
    public Task LeavingASceneWithChangesAsksBeforeDroppingThem() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);

        var (_, dialog) = LeaveMainByDoubleClicking(session, window);

        Assert.Equal("Save main first?", dialog.Title);
        Answer(dialog, "cancel");
        window.Close();
    });

    [Fact]
    public Task CancellingThePromptKeepsYouWhereYouWere() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);
        var (added, dialog) = LeaveMainByDoubleClicking(session, window);

        Answer(dialog, "cancel");

        Assert.True(Has(window, "row_props"));
        Assert.True(Has(window, $"row_{added}"));
        Assert.True(session.IsUnsaved(session.Workspace.Loaded!));
        window.Close();
    });

    [Fact]
    public Task ChoosingSaveWritesTheChangeAndThenMovesOn() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);
        var (_, dialog) = LeaveMainByDoubleClicking(session, window);

        Answer(dialog, "save");

        Assert.Contains("\"Group\"", File.ReadAllText(Path.Combine(_temp.FullName, "scenes", "main.oscene")));
        Assert.True(Has(window, "row_barrel"));
        window.Close();
    });

    [Fact]
    public Task DiscardingTheChangeMovesOnAndWritesNothing() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);
        var (_, dialog) = LeaveMainByDoubleClicking(session, window);

        Answer(dialog, "discard");

        Assert.DoesNotContain("\"Group\"", File.ReadAllText(Path.Combine(_temp.FullName, "scenes", "main.oscene")));
        Assert.True(Has(window, "row_barrel"));
        window.Close();
    });

    [Fact]
    public Task ALoadedSceneThatWasNeverWrittenIsMarkedAndTheSharedRowIsNamedForWhatItIs() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.Contains("•", Notes(window, "row_scene0"));
        Assert.Contains("In every scene", Notes(window, "row_shared"));
        window.Close();
    });

    [Fact]
    public Task ClickingASceneRowSelectsItAndLoadsNothing() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);

        Click(window, "row_scene1");

        Assert.Equal("scene1", session.SelectedScene);
        Assert.False(session.Workspace.Entries.Single(one => one.Id == "scene1").IsLoaded);
        window.Close();
    });

    [Fact]
    public Task TheCloseButtonShowsOnHoverAndTakesTheSceneOffTheList() => Headless.Run(() =>
    {
        var (session, window) = Open(props: true);
        var button = Find<Button>(window, "scene_close_scene1");
        Assert.False(button.IsVisible);

        window.MouseMove(Centre(window, "row_scene1"));
        Assert.True(button.IsVisible);
        Click(button);
        Headless.Render();

        Assert.False(Has(window, "row_scene1"));
        Assert.DoesNotContain(session.Workspace.Entries, one => one.Id == "scene1");
        window.Close();
    });

    [Fact]
    public Task TheSharedRowCannotBeClosed() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.False(Has(window, "scene_close_shared"));
        window.Close();
    });

    [Fact]
    public Task NewSceneFromTheFileMenuStartsAnUntitledSceneWithItsOwnRow() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);

        Items(Find<Menu>(window, "menu")).First(one => one.Name == "menu_new_scene").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Headless.Render();

        Assert.Equal("Untitled", session.Workspace.Loaded!.Title);
        Assert.True(Has(window, $"row_{session.Workspace.Loaded.Id}"));
        window.Close();
    });

    [Fact]
    public Task CommandNStartsANewScene() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.ConfirmLeaving = _ => Task.FromResult(Leaving.Discard);

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.Meta);
        Headless.Render();

        Assert.Equal("Untitled", session.Workspace.Loaded!.Title);
        window.Close();
    });

    [Fact]
    public Task ALongListBuildsTheRowsInViewAndStandsInForTheRest() => Headless.Run(() =>
    {
        var (_, window) = Open(many: 5000);

        var built = RowControls(window);

        Assert.InRange(built.Count, 10, 100);
        Assert.True(Has(window, "row_o0"));
        Assert.False(Has(window, "row_o4999"));

        // The shared set's row and the scene's own row come before the 5,000 objects.
        var standIn = Find<Border>(window, "outliner_before").Height + Find<Border>(window, "outliner_after").Height;
        Assert.Equal(5002 * built[0].Height, standIn + (built.Count * built[0].Height));
        window.Close();
    });

    [Fact]
    public Task AFarRowCanBeReachedByScrollingAndClicked() => Headless.Run(() =>
    {
        var (session, window) = Open(many: 5000);
        var list = ListOf(window);

        ScrollTo(window, list.Extent.Height - list.Viewport.Height);

        Assert.True(Has(window, "row_o4999"));
        Assert.False(Has(window, "row_o0"));
        Click(window, "row_o4999");
        Assert.Equal(["o4999"], session.Selected);
        window.Close();
    });

    [Fact]
    public Task AScrollThatLeavesTheSameRowsInViewBuildsNoNewRow() => Headless.Run(() =>
    {
        var (_, window) = Open(many: 5000);
        var before = Find<Border>(window, "row_o10");

        // Half of the part of the last row that is not yet showing. Any more and that row comes into
        // view and is a row the window has to build.
        var hidden = (Math.Ceiling(ListOf(window).Viewport.Height / 24) * 24) - ListOf(window).Viewport.Height;
        Assert.True(hidden > 0, "The view is a whole number of rows, so no scroll leaves the same rows in it.");
        ScrollTo(window, hidden / 2);

        Assert.Same(before, Find<Border>(window, "row_o10"));
        window.Close();
    });

    [Fact]
    public Task AListThatShrinksUnderAScrolledViewDrawsItsRowsAgain() => Headless.Run(() =>
    {
        var (session, window) = Open(many: 5000);
        var list = ListOf(window);
        ScrollTo(window, list.Extent.Height - list.Viewport.Height);

        // The offset left behind is far past the end of what is left.
        session.History.Run(new DeleteObjects(session.Workspace.Loaded!.Id, [.. Enumerable.Range(5, 4995).Select(i => $"o{i}")], "most"));
        Headless.Render();
        Headless.Render();

        Assert.Equal(5, RowControls(window).Count(row => row.Name!.StartsWith("row_o", StringComparison.Ordinal)));
        window.Close();
    });

    [Fact]
    public Task EveryRowIsTheSameHeightWhetherOrNotItsButtonShows() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Assert.All(RowControls(window), row => Assert.Equal(24, row.Bounds.Height));

        foreach (var button in All<Button>(Find<Control>(window, "outliner_rows")))
        {
            button.IsVisible = true;
        }

        Headless.Render();
        Assert.All(RowControls(window), row => Assert.Equal(24, row.Bounds.Height));
        window.Close();
    });

    [Fact]
    public Task SelectingBuildsNoRowControl() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var before = RowControls(window);

        Click(window, "row_crate");

        Assert.Equal(before, RowControls(window));
        Assert.All(before, row => Assert.Equal(row.Name == "row_crate" ? OrblitColors.EmberWash : Brushes.Transparent, row.Background));
        window.Close();
    });

    [Fact]
    public Task SelectingAnotherRowUnlightsTheLastAndASceneRowUnlightsThemAll() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(window, "row_crate");
        Click(window, "row_sun");

        Assert.Equal(OrblitColors.EmberWash, Find<Border>(window, "row_sun").Background);
        Assert.Equal(Brushes.Transparent, Find<Border>(window, "row_crate").Background);

        Click(window, "row_scene0");

        Assert.Equal(OrblitColors.EmberWash, Find<Border>(window, "row_scene0").Background);
        Assert.Equal(Brushes.Transparent, Find<Border>(window, "row_sun").Background);
        window.Close();
    });
}

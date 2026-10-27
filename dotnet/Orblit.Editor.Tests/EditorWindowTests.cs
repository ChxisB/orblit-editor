using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Doc = Orblit.Scene;
using Orblit.Editor.Core;
using Orblit.Motion;

public sealed class EditorWindowTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        session.Report(opened.Problems);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        return (session, window);
    }

    private static IEnumerable<T> All<T>(Control root) => root.GetLogicalDescendants().OfType<T>();

    private static T Find<T>(Control root, string name)
        where T : Control => All<T>(root).First(one => one.Name == name);

    private static bool Has(Control root, string name) => All<Control>(root).Any(one => one.Name == name);

    private static IEnumerable<MenuItem> Items(ItemsControl root) =>
        root.Items.OfType<MenuItem>().SelectMany(item => new[] { item }.Concat(Items(item)));

    private static MenuItem Menu(EditorWindow window, string name) =>
        Items(Find<Menu>(window, "menu")).First(one => one.Name == name);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void Click(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static AddObject Add() => new(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Group), "scene0");

    private static int Count(Control root, string prefix) => All<Control>(root).Count(one => one.Name?.StartsWith(prefix, StringComparison.Ordinal) == true);

    [Fact]
    public Task OpensWithTheModesAndThePanelsOfTheStandardLayout() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Assert.All(session.Modes.All, mode => Assert.True(Has(window, $"mode_{mode.Name}")));
        Assert.All(session.Layout.Panels, panel => Assert.True(Has(window, $"tab_{panel.Id}")));
        Assert.StartsWith(session.Workspace.Loaded!.Title, Find<TextBlock>(window, "title").Text);
        window.Close();
    });

    [Fact]
    public Task ChoosingAModeShowsItsPanels() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Click(Find<Button>(window, "mode_modelling"));

        Assert.Equal("modelling", session.Mode.Name);
        Assert.Equal(session.Layout.Panels.Count, Count(window, "tab_"));
        Assert.All(session.Layout.Panels, panel => Assert.True(Has(window, $"tab_{panel.Id}")));
        window.Close();
    });

    [Fact]
    public Task TheAnimationModeShowsTheTimeline() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(Find<Button>(window, "mode_animation"));

        Assert.Single(All<TimelinePanel>(window));
        Assert.True(Has(window, "timeline_none"));
        window.Close();
    });

    [Fact]
    public Task AClipOpenedFromTheProjectPanelShowsOnTheTimeline() => Headless.Run(() =>
    {
        File.WriteAllText(Path.Combine(_temp.FullName, "wave.oclip"), new ClipDocument("wave", 2).Encode());
        var (session, window) = Open();
        session.OpenPanel(PanelKind.Project);
        Headless.Render();

        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "wave.oclip"));

        Assert.Equal("animation", session.Mode.Name);
        Assert.Equal("wave", session.Clips.Clip!.Name);
        window.Close();
    });

    [Fact]
    public Task TheFileMenuShowsADotWhileAClipHasChangesNotOnDisk() => Headless.Run(() =>
    {
        File.WriteAllText(Path.Combine(_temp.FullName, "wave.oclip"), new ClipDocument("wave", 2).Encode());
        var (session, window) = Open();
        session.Save();
        var file = Menu(window, "menu_file");
        session.OpenClip(Path.Combine(_temp.FullName, "wave.oclip"));
        Assert.Equal("File", file.Header);

        session.Clips.Edit("Lengthen", _ => new ClipDocument("wave", 3));
        Assert.Equal("File •", file.Header);

        session.Save();
        Assert.Equal("File", file.Header);
        window.Close();
    });

    [Fact]
    public Task ATabThatIsClosedStaysClosedWhenTheProjectIsOpenedAgain() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var id = session.Layout.Panels.First(panel => panel.Id != "scene").Id;

        Click(Find<Button>(window, $"close_{id}"));
        window.Close();
        var (_, again) = Open();

        Assert.False(session.Layout.Holds(id));
        Assert.False(Has(again, $"tab_{id}"));
        Assert.True(Has(again, "tab_scene"));
        again.Close();
    });

    [Fact]
    public Task ACommandShowsInTheMenuAndRunsBackwardsFromIt() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var undo = Menu(window, "menu_undo");
        var redo = Menu(window, "menu_redo");
        Assert.False(undo.IsEnabled);

        session.History.Run(Add());

        Assert.True(undo.IsEnabled);
        Assert.StartsWith("Undo ", (string)undo.Header!, StringComparison.Ordinal);
        Click(undo);
        Assert.False(session.History.CanUndo);
        Assert.True(redo.IsEnabled);
        window.Close();
    });

    [Fact]
    public Task AMenuItemShowsTheKeyThatDoesTheSame() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.Equal(
            KeyMapping.GestureOf(HostPlatform.MacOS.CommandShortcut(EditorKey.KeyZ, shift: true)),
            Menu(window, "menu_redo").InputGesture);
        window.Close();
    });

    [Fact]
    public Task TheKeysUndoRedoAndSave() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.History.Run(Add());

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Meta);
        Assert.False(session.History.CanUndo);

        window.KeyPressQwerty(PhysicalKey.Z, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Assert.True(session.History.CanUndo);

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Meta);
        Assert.True(File.Exists(Path.Combine(_temp.FullName, "scenes", "main.oscene")));
        Assert.Equal("Saved scenes/main.oscene", Find<TextBlock>(window, "status").Text);
        Assert.DoesNotContain("•", Find<TextBlock>(window, "title").Text);
        window.Close();
    });

    [Fact]
    public Task AKeyTheWindowCannotActOnYetIsLeftAlone() => Headless.Run(() =>
    {
        var (_, window) = Open();
        bool? handled = null;
        window.AddHandler(InputElement.KeyDownEvent, (_, press) => handled = press.Handled, RoutingStrategies.Bubble, handledEventsToo: true);

        window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.None);

        Assert.False(handled);
        window.Close();
    });

    [Fact]
    public Task AnUnsavedSceneIsMarkedInTheTitle() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.Save();
        Assert.DoesNotContain("•", window.Title);

        session.History.Run(Add());

        Assert.EndsWith(" •", window.Title, StringComparison.Ordinal);
        window.Close();
    });

    [Fact]
    public Task StatsAndFocusAreInTheViewMenu() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Click(Menu(window, "menu_stats"));
        Assert.True(session.ShowStats);
        Assert.True(Menu(window, "menu_stats").IsChecked);

        Click(Menu(window, "menu_focus"));
        Assert.True(session.IsFocused);
        Assert.Equal("Show panels", Menu(window, "menu_focus").Header);
        Assert.Equal(1, Count(window, "tab_"));

        Click(Menu(window, "menu_focus"));
        Assert.Equal(session.Layout.Panels.Count, Count(window, "tab_"));
        window.Close();
    });

    [Fact]
    public Task ALayoutIsSavedByNameAndLoadedFromTheMenu() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Assert.False(Menu(window, "menu_load_layout").IsEnabled);
        var id = session.Layout.Panels.First(panel => panel.Id != "scene").Id;

        session.SaveLayout("mine");
        Click(Find<Button>(window, $"close_{id}"));
        Assert.True(Menu(window, "menu_load_layout").IsEnabled);
        Click(Menu(window, "load_layout_mine"));

        Assert.True(session.Layout.Holds(id));
        Assert.True(Has(window, $"tab_{id}"));

        Click(Menu(window, "delete_layout_mine"));
        Assert.False(Menu(window, "menu_load_layout").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task ALockedLayoutHasNoHandlesAndNoCloseButtons() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Assert.NotEmpty(All<GridSplitter>(window));

        Click(Menu(window, "menu_lock"));

        Assert.True(session.Layout.Locked);
        Assert.Equal("Unlock layout", Menu(window, "menu_lock").Header);
        Assert.Empty(All<GridSplitter>(window));
        Assert.Equal(0, Count(window, "close_"));
        window.Close();
    });

    [Fact]
    public Task ResetPanelsPutsBackTheModesOwnArrangement() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var id = session.Layout.Panels.First(panel => panel.Id != "scene").Id;
        Click(Find<Button>(window, $"close_{id}"));

        Click(Menu(window, "menu_reset"));

        Assert.True(session.Layout.Holds(id));
        window.Close();
    });

    [Fact]
    public Task ThePanelsMenuOpensAPanelThatWasClosed() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Click(Find<Button>(window, "close_console"));
        Assert.False(Has(window, "tab_console"));

        Click(Menu(window, "panel_console"));

        Assert.True(Has(window, "tab_console"));
        Assert.True(session.Layout.Holds("console"));
        window.Close();
    });

    [Fact]
    public Task ADragThatEndsSavesTheProportionsItEndedWith() => Headless.Run(() =>
    {
        var (session, window) = Open();
        window.UpdateLayout();
        var before = session.Layout;

        All<GridSplitter>(window).First().RaiseEvent(
            new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = new Avalonia.Vector(0, 0) });

        Assert.NotSame(before, session.Layout);
        window.Close();
    });

    [Fact]
    public Task WhatTheSessionSaidBeforeTheWindowOpenedIsShown() => Headless.Run(() =>
    {
        File.WriteAllText(Path.Combine(Directory.CreateDirectory(Path.Combine(_temp.FullName, "scenes")).FullName, "main.oscene"), "{nope");

        var (_, window) = Open();

        var status = Find<TextBlock>(window, "status");
        Assert.False(string.IsNullOrEmpty(status.Text));
        Assert.Same(OrblitColors.Bad, status.Foreground);
        window.Close();
    });

    [Fact]
    public Task APromptGivesBackWhatWasTypedOrNothingWhenCancelled() => Headless.Run(async () =>
    {
        var (_, window) = Open();
        var prompt = new NamePrompt("Save layout", "Name this layout");
        var typed = prompt.ShowDialog<string?>(window);
        Find<TextBox>(prompt, "name").Text = "mine";
        Click(Find<Button>(prompt, "ok"));
        Assert.Equal("mine", await typed);

        var cancelled = new NamePrompt("Save layout", "Name this layout");
        var nothing = cancelled.ShowDialog<string?>(window);
        Click(Find<Button>(cancelled, "cancel"));
        Assert.Null(await nothing);
        window.Close();
    });

    [Fact]
    public Task APromptStartsWithTheTextItWasGivenAndDropsTheSpacesAroundWhatIsTyped() => Headless.Run(async () =>
    {
        var (_, window) = Open();
        var prompt = new NamePrompt("Save scene as", "Goes in scenes/", "main");
        var typed = prompt.ShowDialog<string?>(window);
        var field = Find<TextBox>(prompt, "name");
        Assert.Equal("main", field.Text);

        field.Text = "  other ";
        Click(Find<Button>(prompt, "ok"));

        Assert.Equal("other", await typed);
        window.Close();
    });

    private static NamePrompt Prompt(Window window) => Assert.IsType<NamePrompt>(Assert.Single(window.OwnedWindows));

    [Theory]
    [InlineData(false, RawInputModifiers.Meta | RawInputModifiers.Shift)]
    [InlineData(true, RawInputModifiers.Meta)]
    public Task SaveAsAndSavingASceneWithNoFileBothAskForAName(bool noFile, RawInputModifiers held) => Headless.Run(() =>
    {
        var (session, window) = Open();
        if (noFile)
        {
            session.Workspace.Loaded!.Path = null;
        }

        window.KeyPressQwerty(PhysicalKey.S, held);
        Headless.Render();

        Assert.Equal(session.Workspace.Loaded!.Title, Find<TextBox>(Prompt(window), "name").Text);
        Assert.Empty(Directory.GetFiles(_temp.FullName, "*.oscene", SearchOption.AllDirectories));
        window.OwnedWindows[0].Close();
        window.Close();
    });

    [Fact]
    public Task TheNameChosenInThePromptIsWhereTheSceneIsSaved() => Headless.Run(() =>
    {
        var (session, window) = Open();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Headless.Render();
        var prompt = Prompt(window);
        Find<TextBox>(prompt, "name").Text = "copy";
        Click(Find<Button>(prompt, "ok"));
        Headless.Render();
        Headless.Render();

        Assert.True(File.Exists(Path.Combine(_temp.FullName, "scenes", "copy.oscene")));
        Assert.Equal("Saved scenes/copy.oscene", Find<TextBlock>(window, "status").Text);
        Assert.Equal("copy", session.Workspace.Loaded!.Title);
        window.Close();
    });

    [Fact]
    public Task CancellingThePromptWritesNothing() => Headless.Run(() =>
    {
        var (_, window) = Open();

        window.KeyPressQwerty(PhysicalKey.S, RawInputModifiers.Meta | RawInputModifiers.Shift);
        Headless.Render();
        Click(Find<Button>(Prompt(window), "cancel"));
        Headless.Render();
        Headless.Render();

        Assert.Empty(window.OwnedWindows);
        Assert.False(File.Exists(Path.Combine(_temp.FullName, "scenes", "copy.oscene")));
        window.Close();
    });

    private static PaletteDialog Palette(Window window) => Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));

    // What is chosen runs once the dialog has gone, on a later turn of the dispatcher.
    private static void TypeAndRun(PaletteDialog palette, string text)
    {
        Find<TextBox>(palette, "field").Text = text;
        Headless.Render();
        palette.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Headless.Render();
        Headless.Render();
    }

    [Fact]
    public Task TheCommandKeyAndKOpenThePaletteAndEscapeClosesIt() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Assert.Empty(window.OwnedWindows);

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        var palette = Palette(window);

        palette.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Headless.Render();
        Assert.Empty(window.OwnedWindows);
        window.Close();
    });

    [Fact]
    public Task TheSearchButtonInTheTopBarOpensThePalette() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Click(Find<Button>(window, "search_button"));
        Headless.Render();

        Assert.NotNull(Find<TextBox>(Palette(window), "field"));
        window.OwnedWindows[0].Close();
        window.Close();
    });

    [Fact]
    public Task AWorkspaceCanBeReachedByNameFromThePalette() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Assert.NotEqual("modelling", session.Mode.Name);

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        TypeAndRun(Palette(window), "go to modelling");

        Assert.Empty(window.OwnedWindows);
        Assert.Equal("modelling", session.Mode.Name);
        window.Close();
    });

    [Fact]
    public Task AnObjectInTheSceneCanBeFoundAndSelectedFromThePalette() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.History.Run(Add());
        Assert.Empty(session.Selected);

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();
        TypeAndRun(Palette(window), "extra");

        Assert.Equal(["extra"], session.Selected);
        window.Close();
    });

    [Fact]
    public Task TheInspectorSitsInTheWindowAndShowsWhatIsSelected() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Assert.Single(All<InspectorPanel>(window));
        Assert.False(Has(window, "section_transform"));

        session.Select(["cube"]);
        Headless.Render();

        Assert.True(Has(window, "section_transform"));
        window.Close();
    });

    [Fact]
    public Task TheProjectPanelSitsInTheWindowAndListsTheProjectsFiles() => Headless.Run(() =>
    {
        File.WriteAllText(Path.Join(_temp.FullName, "notes.md"), "read me");
        var (_, window) = Open();

        Assert.Single(All<AssetBrowserPanel>(window));
        Assert.Contains(All<AssetTile>(window), tile => tile.Asset.Name == "notes.md");
        window.Close();
    });

    [Fact]
    public Task OpeningASceneInTheProjectPanelLoadsIt() => Headless.Run(() =>
    {
        var scenes = Directory.CreateDirectory(Path.Join(_temp.FullName, "scenes")).FullName;
        File.WriteAllText(Path.Join(scenes, "main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        File.WriteAllText(Path.Join(scenes, "props.oscene"), SceneDocument.Encode(new EditorScene([], name: "Props")));
        var (session, window) = Open();
        Assert.Equal("main", session.Workspace.Loaded!.Title);
        Headless.Render();

        Headless.DoubleClick(window, All<AssetTile>(window).First(tile => tile.Asset.Name == "scenes"));
        Headless.DoubleClick(window, All<AssetTile>(window).First(tile => tile.Asset.Name == "props.oscene"));

        Assert.True(Headless.Until(() => session.Workspace.Loaded?.Title == "props"));
        window.Close();
    });
}

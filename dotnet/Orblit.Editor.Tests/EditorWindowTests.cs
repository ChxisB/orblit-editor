using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Doc = Orblit.Scene;
using Orblit.Editor.Core;

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
}

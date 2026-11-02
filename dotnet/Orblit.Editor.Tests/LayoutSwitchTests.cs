using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;
using SceneDocument = Orblit.Editor.Core.SceneDocument;

/// <summary>Choosing Base, Portrait or Landscape from the top bar, and what says which one is being edited.</summary>
public sealed class LayoutSwitchTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_layout_switch");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(Path.Combine(_project.FullName, "scenes", "main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static ToolbarChip Chip(EditorWindow window, string name) => Headless.Find<ToolbarChip>(window, name);

    private static bool Has(EditorWindow window, string name) => Headless.All<Control>(window).Any(one => one.Name == name);

    private static void MoveCrate(EditorSession session, double height)
    {
        var crate = session.Workspace.Loaded!.Scene!["crate"]!;
        session.History.Run(new SetTransform(
            session.Workspace.Loaded.Id, "crate", TransformField.Position, crate.Name, crate.Position, new Vector3(crate.Position.X, height, crate.Position.Z)));
    }

    private static string TipOf(Control control) => (string)ToolTip.GetTip(control)!;

    [Fact]
    public Task TheTopBarOffersTheBaseAndBothLayouts() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.True(Chip(window, "layout_switch_base").On);
        Assert.False(Chip(window, "layout_switch_portrait").On);
        Assert.False(Chip(window, "layout_switch_landscape").On);
        window.Close();
    });

    [Fact]
    public Task ChoosingALayoutShowsItAndLightsItsButton() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Headless.Click(window, "layout_switch_portrait");
        Headless.Render();

        Assert.Equal(LayoutShape.Portrait, session.ActiveLayout);
        Assert.True(Chip(window, "layout_switch_portrait").On);
        Assert.False(Chip(window, "layout_switch_base").On);
        window.Close();
    });

    [Fact]
    public Task ChoosingTheBaseLeavesTheLayout() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, "layout_switch_landscape");
        Headless.Render();

        Headless.Click(window, "layout_switch_base");
        Headless.Render();

        Assert.Null(session.ActiveLayout);
        Assert.True(Chip(window, "layout_switch_base").On);
        window.Close();
    });

    [Fact]
    public Task UndoMovesTheSwitchBackWithTheSession() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, "layout_switch_portrait");
        Headless.Render();

        session.Undo();
        Headless.Render();

        Assert.True(Chip(window, "layout_switch_base").On);
        Assert.False(Chip(window, "layout_switch_portrait").On);
        window.Close();
    });

    [Fact]
    public Task ABarOverTheViewNamesTheLayoutWhileOneIsEdited() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Assert.False(Headless.Find<Border>(window, "layout_bar").IsVisible);

        Headless.Click(window, "layout_switch_landscape");
        Headless.Render();

        var bar = Headless.Find<Border>(window, "layout_bar");
        Assert.True(bar.IsVisible);
        Assert.Contains("Landscape", ((TextBlock)bar.Child!).Text);
        window.Close();
    });

    [Fact]
    public Task TheBaseButtonSaysWhereObjectsAreAddedOnceALayoutIsShown() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var plain = TipOf(Chip(window, "layout_switch_base"));

        Headless.Click(window, "layout_switch_portrait");
        Headless.Render();

        Assert.NotEqual(plain, TipOf(Chip(window, "layout_switch_base")));
        Assert.Equal("Switch to Base to add or remove objects.", TipOf(Chip(window, "layout_switch_base")));
        window.Close();
    });

    [Fact]
    public Task WithNoSceneThereIsNothingToSwitch() => Headless.Run(() =>
    {
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        session.Workspace.Unload(session.Workspace.Loaded!);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();

        Assert.Null(session.Workspace.Loaded?.Scene);
        Assert.False(Chip(window, "layout_switch_portrait").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task TheInspectorNamesWhatTheLayoutSetsOnTheSelectedObject() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.SelectObject("crate");
        Headless.Click(window, "layout_switch_portrait");
        MoveCrate(session, 1);
        Headless.Render();

        var band = Headless.Find<Border>(window, "layout_band");

        Assert.Contains("Portrait", Headless.Find<TextBlock>(window, "layout_overrides").Text);
        Assert.Contains("Position", Headless.Find<TextBlock>(window, "layout_overrides").Text);
        Assert.True(band.IsVisible);
        window.Close();
    });

    [Fact]
    public Task TheInspectorSaysNothingOfLayoutsInTheBase() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.SelectObject("crate");
        Headless.Render();

        Assert.False(Has(window, "layout_band"));
        window.Close();
    });

    [Fact]
    public Task TheInspectorSaysNothingForAnObjectTheLayoutLeavesAlone() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Headless.Click(window, "layout_switch_portrait");
        MoveCrate(session, 1);
        session.SelectObject("camera");
        Headless.Render();

        Assert.False(Has(window, "layout_band"));
        window.Close();
    });

    [Fact]
    public Task ResetToBaseOnTheBandPutsTheObjectBackAndClearsTheBand() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var home = session.Workspace.Loaded!.Scene!["crate"]!.Position.Y;
        session.SelectObject("crate");
        Headless.Click(window, "layout_switch_portrait");
        MoveCrate(session, 1);
        Headless.Render();

        Headless.Click(window, "layout_reset");
        Headless.Render();

        Assert.Equal(home, session.Workspace.Loaded!.Scene!["crate"]!.Position.Y);
        Assert.False(Has(window, "layout_band"));
        window.Close();
    });
}

using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>Every panel the View menu lists, opened in the window on the Third person scene with the character selected.</summary>
public sealed class EditorWindowPanelsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_panels");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) OpenWithTheCharacterSelected()
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(
            Path.Combine(_project.FullName, "scenes", "main.oscene"),
            SceneDocument.Encode(ProjectTemplate.ThirdPerson.SceneFor()));
        var opened = ProjectWorkspace.Open(_project.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        Headless.Click(window, "row_character");
        Headless.Render();
        return (session, window);
    }

    [Theory]
    [InlineData("viewport", typeof(SceneViewport))]
    [InlineData("modelling", typeof(ModellingPanel))]
    [InlineData("outliner", typeof(OutlinerPanel))]
    [InlineData("project", typeof(AssetBrowserPanel))]
    [InlineData("console", typeof(ConsolePanel))]
    [InlineData("profile", typeof(ProfilePanel))]
    [InlineData("timeline", typeof(TimelinePanel))]
    [InlineData("cutsceneTimeline", typeof(TimelinePanel))]
    [InlineData("shotPreview", typeof(ShotPreviewPanel))]
    [InlineData("shots", typeof(ShotListPanel))]
    [InlineData("blendGraph", typeof(BlendGraphPanel))]
    [InlineData("terrainBrush", typeof(TerrainBrushPanel))]
    [InlineData("uvs", typeof(UvPanel))]
    [InlineData("inspector", typeof(InspectorPanel))]
    [InlineData("interfaceCanvas", typeof(InterfaceCanvas))]
    [InlineData("interfaceElements", typeof(InterfaceElements))]
    [InlineData("interfaceDesign", typeof(InterfaceDesign))]
    public Task APanelOpensAsItsOwnControl(string kind, Type control) => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCharacterSelected();

        session.OpenPanel(PanelKind.Named(kind)!);
        Headless.Render();

        Assert.Contains(Headless.All<Control>(window), one => one.GetType() == control && one.IsEffectivelyVisible);
        Assert.False(session.Log.HasErrors);
        window.Close();
    });

    // Decision 90 has not said what the game view is, so it shows its name.
    [Fact]
    public Task TheGameViewShowsItsName() => Headless.Run(() =>
    {
        var (session, window) = OpenWithTheCharacterSelected();

        session.OpenPanel(PanelKind.Game);
        Headless.Render();

        Assert.Contains(Headless.All<TextBlock>(window), one => one.Text == PanelKind.Game.Label && one.IsEffectivelyVisible);
        Assert.False(session.Log.HasErrors);
        window.Close();
    });
}

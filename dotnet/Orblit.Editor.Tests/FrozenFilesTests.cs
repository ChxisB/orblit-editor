namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Json;

/// <summary>
/// Reads the frozen files and writes them again. A file the editor wrote once has to open and save back
/// the same, and byte equality is the cheapest proof of both directions at once.
/// </summary>
/// <remarks>
/// The fixtures are never regenerated. Change one only when the file format changes.
/// </remarks>
public sealed class FrozenFilesTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "frozen", name);

    private static string Text(string name) => File.ReadAllText(Fixture(name));

    [Fact]
    public void TheFrozenProjectFileReadsBackAsItWas()
    {
        var project = ProjectStore.Open(Path.GetDirectoryName(Fixture(Path.Combine("project", Project.FileName))) ?? "");

        Assert.NotNull(project);
        Assert.Equal("My Game", project.Name);
        Assert.Equal("0.3.0", project.EngineVersion);
        Assert.Equal(new DateTime(2026, 10, 24, 10, 20, 30, 123), project.LastOpened);
        Assert.Equal(Text(Path.Combine("project", Project.FileName)), JsonText.Encode(project.ToJson(), indent: "  "));
    }

    [Theory]
    [InlineData(ProjectTemplate.Empty, "empty")]
    [InlineData(ProjectTemplate.Scene, "scene")]
    [InlineData(ProjectTemplate.ThirdPerson, "thirdPerson")]
    public void TheFrozenScenesOpenWithNoProblemsAndWriteBackTheSame(ProjectTemplate template, string name)
    {
        var written = Text($"scene.{name}.oscene");

        var load = SceneDocument.Decode(written);

        Assert.False(load.HasProblems);
        Assert.Equal(written, SceneDocument.Encode(load.Scene));
        Assert.Equal(template.SceneFor().Objects.Count, load.Scene.Objects.Count);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("modelling")]
    [InlineData("animation")]
    [InlineData("fourViews")]
    public void TheFrozenLayoutsReadAndWriteBackTheSame(string name)
    {
        var written = Text($"layout.{name}.json");

        var layout = DockLayout.Read(written, PanelKind.BuiltIn);

        Assert.NotNull(layout);
        Assert.Equal(written, layout.ToText());
    }

    [Fact]
    public void TheArrangementsCodeBuildsMatchTheFixtures()
    {
        Assert.Equal(Text("layout.scene.json"), DockLayout.Standard().ToText());
        Assert.Equal(Text("layout.modelling.json"), DockLayout.Modelling().ToText());
        Assert.Equal(Text("layout.animation.json"), DockLayout.Animation().ToText());
        Assert.Equal(Text("layout.fourViews.json"), DockLayout.FourViews().ToText());
    }

    [Fact]
    public void TheFrozenLibraryIsFoundByModeAndName()
    {
        var library = new LayoutLibrary(Fixture("layouts.json"), PanelKind.BuiltIn);

        Assert.Equal(["mine"], library.Names("scene"));
        Assert.Equal(["wide"], library.Names("modelling"));
        Assert.Equal(Text("layout.scene.json"), library.Find("scene", "mine")?.ToText());
        Assert.Equal(Text("layout.modelling.json"), library.Find("modelling", "wide")?.ToText());
    }
}

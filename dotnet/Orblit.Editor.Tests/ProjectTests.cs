namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Json;

public sealed class ProjectTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");
    private static readonly DateTime Day = new(2026, 10, 25, 9, 0, 0);
    private readonly FixedClock _clock = new(Day);
    private readonly ProjectStore _store;

    public ProjectTests() => _store = new ProjectStore(Path.Combine(_temp.FullName, "support", "recent.json"), _clock);

    public void Dispose() => _temp.Delete(recursive: true);

    private string Folder(string name) => Path.Combine(_temp.FullName, name);

    private static Project Made(string name, string directory) => new(name, directory, DateTime.Now);

    [Theory]
    [InlineData(ProjectTemplate.Empty)]
    [InlineData(ProjectTemplate.Scene)]
    [InlineData(ProjectTemplate.ThirdPerson)]
    public void WhatANewProjectStartsWithTheTemplateOpensInTheEditor(ProjectTemplate template)
    {
        // The seam where a format drifts without anyone noticing: the launcher writes the file and the
        // editor reads it, and nothing else joins them up.
        var written = SceneDocument.Encode(template.SceneFor());
        var load = SceneDocument.Decode(written);

        Assert.False(load.HasProblems, string.Join("; ", load.Problems));
        Assert.Equal(template.SceneFor().Length, load.Scene.Length);
    }

    [Fact]
    public void WhatANewProjectStartsWithTheEmptyTemplateReallyIsEmpty() =>
        Assert.Equal(0, ProjectTemplate.Empty.SceneFor().Length);

    [Fact]
    public void WhatANewProjectStartsWithALitSceneHasSomethingToLightAndSomethingToLightItWith()
    {
        var scene = ProjectTemplate.Scene.SceneFor();

        Assert.Contains(scene.Objects, one => one.Kind == ObjectKind.Light);
        Assert.Contains(scene.Objects, one => one.IsDrawable);
    }

    [Fact]
    public void AProjectOnDiskIsAFolderWithAProjectFileScenesAndAssets()
    {
        // Written directly rather than through Create, which also touches the recents list.
        var directory = Directory.CreateDirectory(Folder("my-game"));
        var created = Made("My Game", directory.FullName);
        File.WriteAllText(Path.Combine(directory.FullName, Project.FileName), JsonText.Encode(created.ToJson()));

        var reopened = ProjectStore.Open(directory.FullName);

        Assert.NotNull(reopened);
        Assert.Equal("My Game", reopened.Name);
        Assert.True(reopened.Exists);
    }

    [Fact]
    public void AProjectOnDiskAFolderWithoutAProjectFileOpensAsNothing() =>
        Assert.Null(ProjectStore.Open(_temp.FullName));

    [Fact]
    public void AProjectOnDiskACorruptProjectFileIsRefusedNotHalfRead()
    {
        File.WriteAllText(Path.Combine(_temp.FullName, Project.FileName), "{not json");

        Assert.Null(ProjectStore.Open(_temp.FullName));
    }

    [Fact]
    public void AProjectOnDiskAPathUnderHomeIsShortenedForDisplay() =>
        Assert.Equal("~/dev/game", Made("X", "/Users/robin/dev/game").DisplayPath("/Users/robin"));

    [Fact]
    public void AProjectOnDiskAPathOutsideHomeIsShownAsItIs()
    {
        Assert.Equal("/srv/game", Made("X", "/srv/game").DisplayPath("/Users/robin"));
        Assert.Equal("/srv/game", Made("X", "/srv/game").DisplayPath(null));
    }

    [Fact]
    public void CreatingWritesTheProjectFileScenesAndAssets()
    {
        var project = _store.Create("My Game", _temp.FullName, ProjectTemplate.Scene);

        Assert.Equal(Folder("my-game"), project.Directory);
        Assert.True(Directory.Exists(Path.Combine(project.Directory, "assets")));
        Assert.Equal("build/\n.orblit/\n", File.ReadAllText(Path.Combine(project.Directory, ".gitignore")));
        var scene = SceneDocument.Decode(File.ReadAllText(Path.Combine(project.Directory, "scenes", "main.oscene")));
        Assert.Equal(3, scene.Scene.Length);
        Assert.Equal("My Game", ProjectStore.Open(project.Directory)!.Name);
    }

    [Fact]
    public void CreatingRefusesAFolderThatAlreadyHoldsAProject()
    {
        _store.Create("My Game", _temp.FullName, ProjectTemplate.Empty);

        Assert.Throws<InvalidOperationException>(() => _store.Create("my game", _temp.FullName, ProjectTemplate.Scene));
    }

    [Theory]
    [InlineData("My Game!", "my-game")]
    [InlineData("  --A  b--  ", "a-b")]
    [InlineData("***", "orblit-project")]
    public void CreatingNamesTheFolderFromTheProjectName(string name, string folder) =>
        Assert.Equal(Folder(folder), _store.Create(name, _temp.FullName, ProjectTemplate.Empty).Directory);

    [Fact]
    public void RememberingPutsTheNewestFirstAndKeepsEachFolderOnce()
    {
        _clock.At = Day.AddSeconds(1);
        _store.Remember(Made("A", "/a"));
        _clock.At = Day.AddSeconds(2);
        _store.Remember(Made("B", "/b"));
        _clock.At = Day.AddSeconds(3);
        _store.Remember(Made("A", "/a"));

        Assert.Equal(new[] { "/a", "/b" }, _store.Recents().Select(one => one.Directory));
    }

    [Fact]
    public void RememberingKeepsTwentyAndDropsTheOldest()
    {
        for (var at = 0; at < 22; at++)
        {
            _clock.At = Day.AddSeconds(at);
            _store.Remember(Made($"P{at}", $"/p{at}"));
        }

        var kept = _store.Recents();

        Assert.Equal(20, kept.Count);
        Assert.Equal("/p21", kept[0].Directory);
        Assert.Equal("/p2", kept[^1].Directory);
    }

    [Fact]
    public void ForgettingDropsOneFolderAndWorksBeforeAnythingWasRemembered()
    {
        _store.Forget(Made("A", "/a"));
        _store.Remember(Made("A", "/a"));
        _store.Remember(Made("B", "/b"));

        _store.Forget(Made("A", "/a"));

        Assert.Equal(new[] { "/b" }, _store.Recents().Select(one => one.Directory));
    }

    [Fact]
    public void AFolderThatMovedStaysInTheListAsMissing()
    {
        _store.Remember(Made("Gone", Folder("gone")));

        var gone = Assert.Single(_store.Recents());

        Assert.False(gone.Exists);
    }

    [Fact]
    public void ACorruptRecentsListReadsAsEmpty()
    {
        var file = Path.Combine(_temp.FullName, "support", "recent.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        foreach (var text in new[] { "{not json", "{}", "[1, {\"name\": \"no directory\"}]" })
        {
            File.WriteAllText(file, text);
            Assert.Empty(_store.Recents());
        }
    }

    [Fact]
    public void ATimeIsWrittenToTheMillisecondOrTheMicrosecond()
    {
        var plain = new Project("A", "/a", new DateTime(2026, 10, 24, 10, 20, 30, 123));
        var fine = new Project("A", "/a", new DateTime(2026, 10, 24, 10, 20, 30, 123).AddTicks(450));
        var utc = new Project("A", "/a", new DateTime(2026, 10, 24, 10, 20, 30, 0, DateTimeKind.Utc));

        Assert.Equal("2026-10-24T10:20:30.123", plain.ToJson()["lastOpened"]);
        Assert.Equal("2026-10-24T10:20:30.123045", fine.ToJson()["lastOpened"]);
        Assert.Equal("2026-10-24T10:20:30.000Z", utc.ToJson()["lastOpened"]);
    }

    [Fact]
    public void AProjectFileWithAFractionalSecondOpens()
    {
        File.WriteAllText(
            Path.Combine(_temp.FullName, Project.FileName),
            "{\"formatVersion\":1,\"name\":\"My Game\",\"engine\":\"0.3.0\",\"lastOpened\":\"2026-10-24T10:20:30.123456\"}");

        var project = ProjectStore.Open(_temp.FullName)!;

        Assert.Equal("0.3.0", project.EngineVersion);
        Assert.Equal(new DateTime(2026, 10, 24, 10, 20, 30, 123).AddTicks(4560), project.LastOpened);
    }

    [Fact]
    public void AProjectFileWithGapsIsReadWithDefaults()
    {
        File.WriteAllText(Path.Combine(_temp.FullName, Project.FileName), "{}");

        var project = ProjectStore.Open(_temp.FullName)!;

        Assert.Equal(Path.GetFileName(_temp.FullName), project.Name);
        Assert.Equal("unknown", project.EngineVersion);
        Assert.Equal(DateTime.UnixEpoch, project.LastOpened);
    }

    private Project OpenedWith(object? game)
    {
        var json = new OrderedDictionary<string, object?> { ["name"] = "My Game" };
        if (game is not null)
        {
            json["game"] = game;
        }

        File.WriteAllText(Path.Combine(_temp.FullName, Project.FileName), JsonText.Encode(json));
        return ProjectStore.Open(_temp.FullName)!;
    }

    private static OrderedDictionary<string, object?> GameJson(object? project, object? type) =>
        new() { ["project"] = project, ["type"] = type };

    [Fact]
    public void AProjectFileNamingAGameReadsToTheEntry()
    {
        var project = OpenedWith(GameJson("game/Game.csproj", "Scape.Game.ScapeGame"));

        Assert.Equal(new GameEntry("game/Game.csproj", "Scape.Game.ScapeGame"), project.Game);
        Assert.Null(project.GameProblem);
    }

    [Fact]
    public void AProjectFileWithoutAGameHasNoGameAndNoProblem()
    {
        var project = OpenedWith(null);

        Assert.Null(project.Game);
        Assert.Null(project.GameProblem);
    }

    [Theory]
    [InlineData("/etc/x.csproj")]
    [InlineData("../x.csproj")]
    [InlineData("game/../../x.csproj")]
    [InlineData("game\\..\\x.csproj")]
    [InlineData("\\game\\x.csproj")]
    [InlineData("C:/game/x.csproj")]
    [InlineData("x.cs")]
    [InlineData("game/")]
    [InlineData("")]
    public void AGameProjectThatLeavesTheFolderOrIsNotAProjectIsAProblemAndNotAGame(string path)
    {
        var project = OpenedWith(GameJson(path, "Scape.Game.ScapeGame"));

        Assert.Null(project.Game);
        Assert.Contains("project", project.GameProblem);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AGameWithoutATypeNameIsAProblemAndNotAGame(string type)
    {
        var project = OpenedWith(GameJson("game/Game.csproj", type));

        Assert.Null(project.Game);
        Assert.Contains("type", project.GameProblem);
    }

    [Fact]
    public void AGameOfTheWrongShapeIsAProblemAndNotAGame()
    {
        var shapes = new object?[]
        {
            "game/Game.csproj",
            new OrderedDictionary<string, object?> { ["type"] = "Scape.Game.ScapeGame" },
            new OrderedDictionary<string, object?> { ["project"] = "game/Game.csproj" },
            GameJson(7L, "Scape.Game.ScapeGame"),
            GameJson("game/Game.csproj", true),
        };

        foreach (var shape in shapes)
        {
            var project = OpenedWith(shape);

            Assert.Null(project.Game);
            Assert.NotNull(project.GameProblem);
        }
    }

    [Fact]
    public void AProjectWithABadGameStillOpensWithItsName()
    {
        // Play can say why it cannot run, which it cannot do for a project that did not open.
        var project = OpenedWith(GameJson("../x.csproj", "Scape.Game.ScapeGame"));

        Assert.Equal("My Game", project.Name);
        Assert.True(project.Exists);
    }

    [Fact]
    public void AGameEntryRefusesWhatItCouldNotRun()
    {
        Assert.Throws<ArgumentException>(() => new GameEntry("../x.csproj", "Scape.Game.ScapeGame"));
        Assert.Throws<ArgumentException>(() => new GameEntry("game/Game.csproj", ""));
    }

    [Fact]
    public void AGameSurvivesWritingAndReadingTheProjectFile()
    {
        var entry = new GameEntry("game/Game.csproj", "Scape.Game.ScapeGame");
        var written = new Project("My Game", "/a", Day) { Game = entry };

        var decoded = (IReadOnlyDictionary<string, object?>)JsonText.Decode(JsonText.Encode(written.ToJson()))!;

        Assert.Equal(entry, Project.FromJson(decoded, "/a").Game);
    }

    [Fact]
    public void AProjectWithoutAGameWritesNoGameField() =>
        Assert.False(new Project("My Game", "/a", Day).ToJson().ContainsKey("game"));

    [Fact]
    public void AProjectWithAGameIsStillListedInTheRecents()
    {
        var entry = new GameEntry("game/Game.csproj", "Scape.Game.ScapeGame");

        _store.Remember(new Project("My Game", Folder("my-game"), Day) { Game = entry });

        Assert.Equal(entry, Assert.Single(_store.Recents()).Game);
    }

    [Fact]
    public void TheDestinationIsTheFolderTheNameBecomes()
    {
        Assert.Equal(Folder("my-game"), ProjectStore.DestinationFor("My Game!", _temp.FullName));
        Assert.Equal(Folder("orblit-project"), ProjectStore.DestinationFor("***", _temp.FullName));
    }
}

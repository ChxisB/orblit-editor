namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class LauncherSessionTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 10, 25, 9, 0, 0);

    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");
    private readonly ProjectStore _store;
    private readonly Clock _clock = new(Now);
    private readonly List<Project> _opened = [];
    private readonly List<(string Title, string Detail)> _complaints = [];

    public LauncherSessionTests() => _store = new ProjectStore(Path.Combine(_temp.FullName, "support", "recent.json"), _clock);

    public void Dispose() => _temp.Delete(recursive: true);

    private LauncherSession Launcher(string? home = null)
    {
        var session = new LauncherSession(_store, home, _clock);
        session.Opened += _opened.Add;
        session.Complained += (title, detail) => _complaints.Add((title, detail));
        return session;
    }

    // A project with a folder and a project file on disk, as one the editor has opened before.
    private Project Existing(string name, TimeSpan? ago = null)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_temp.FullName, name)).FullName;
        var project = new Project(name, directory, Now - (ago ?? TimeSpan.Zero));
        File.WriteAllText(Path.Combine(directory, Project.FileName), "{}");
        return project;
    }

    [Fact]
    public void StartsOnTheProjectsPageWithTheLitSceneChosen()
    {
        var launcher = Launcher();

        Assert.Equal(LauncherView.Projects, launcher.View);
        Assert.Equal(ProjectTemplate.Scene, launcher.Template);
    }

    [Fact]
    public void StartingFromATemplateChoosesItAndShowsTheCreatePage()
    {
        var launcher = Launcher();
        var changes = 0;
        launcher.Changed += () => changes++;

        launcher.StartFrom(ProjectTemplate.ThirdPerson);

        Assert.Equal(ProjectTemplate.ThirdPerson, launcher.Template);
        Assert.Equal(LauncherView.Create, launcher.View);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ListsWhatWasOpenedMostRecentFirst()
    {
        _clock.At = Now.AddMinutes(-5);
        _store.Remember(Existing("Runner"));
        _clock.At = Now;
        _store.Remember(Existing("Sunset Valley"));

        Assert.Equal(new[] { "Sunset Valley", "Runner" }, Launcher().Recents.Select(one => one.Name));
    }

    [Fact]
    public void NarrowsToTheProjectsWhoseNameHasWhatWasTyped()
    {
        _store.Remember(Existing("Sunset Valley"));
        _store.Remember(Existing("Runner"));
        var launcher = Launcher();

        launcher.Search("  SUN ");
        Assert.Equal(new[] { "Sunset Valley" }, launcher.Matching.Select(one => one.Name));

        launcher.Search("zzz");
        Assert.Empty(launcher.Matching);
        Assert.Equal(2, launcher.Recents.Count);

        launcher.Search("");
        Assert.Equal(2, launcher.Matching.Count);
    }

    [Fact]
    public void ShowsAProjectsPathWithHomeAsATildeAndWhenItWasOpened()
    {
        var launcher = Launcher(home: _temp.FullName);
        var project = Existing("Runner", TimeSpan.FromHours(2));

        Assert.Equal("~/Runner".Replace('/', Path.DirectorySeparatorChar), launcher.PathOf(project));
        Assert.Equal("2h ago", launcher.OpenedAgo(project));
    }

    [Fact]
    public void OpeningAProjectRemembersItAndHandsItOn()
    {
        var launcher = Launcher();
        var project = Existing("Runner");

        launcher.Open(project);

        Assert.Equal(new[] { project }, _opened);
        Assert.Equal(new[] { project.Directory }, _store.Recents().Select(one => one.Directory));
    }

    [Fact]
    public void OpeningAProjectWhoseFolderHasGoneSaysSoAndOpensNothing()
    {
        var launcher = Launcher(home: _temp.FullName);

        launcher.Open(new Project("Gone", Path.Combine(_temp.FullName, "gone"), Now));

        Assert.Empty(_opened);
        var complaint = Assert.Single(_complaints);
        Assert.Equal("That folder has moved", complaint.Title);
        Assert.StartsWith("~", complaint.Detail, StringComparison.Ordinal);
        Assert.EndsWith(" no longer holds a project.", complaint.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OpeningAFolderThatHoldsAProjectOpensItAndOneThatDoesNotSaysSo()
    {
        var launcher = Launcher();
        var project = Existing("Runner");

        launcher.OpenFolder(_temp.FullName);
        launcher.OpenFolder(project.Directory);

        Assert.Equal("No Orblit project there", Assert.Single(_complaints).Title);
        Assert.Equal(project.Directory, Assert.Single(_opened).Directory);
        Assert.Single(_store.Recents());
    }

    [Fact]
    public void ForgettingTakesAProjectOffTheListAndNotOffTheDisk()
    {
        var project = Existing("Runner");
        _store.Remember(project);
        var launcher = Launcher();
        var changes = 0;
        launcher.Changed += () => changes++;

        launcher.Forget(project);

        Assert.Empty(launcher.Recents);
        Assert.Equal(1, changes);
        Assert.True(project.Exists);
        Assert.Empty(_opened);
    }

    [Fact]
    public void CreatingMakesTheProjectFromTheChosenTemplateAndHandsItOn()
    {
        var launcher = Launcher();
        launcher.StartFrom(ProjectTemplate.Empty);

        launcher.Create("  My Game ", _temp.FullName);

        var project = Assert.Single(_opened);
        Assert.Equal("My Game", project.Name);
        Assert.Equal(ProjectStore.DestinationFor("My Game", _temp.FullName), project.Directory);
        Assert.True(project.Exists);
        Assert.Empty(_complaints);
    }

    [Fact]
    public void CreatingRefusesANamelessProjectRatherThanMakingOne()
    {
        Launcher().Create("   ", _temp.FullName);

        Assert.Equal("A project needs a name", Assert.Single(_complaints).Title);
        Assert.Empty(_opened);
        Assert.Empty(_temp.GetDirectories());
    }

    [Fact]
    public void CreatingWhereAProjectAlreadyIsSaysSo()
    {
        var launcher = Launcher();
        launcher.Create("My Game", _temp.FullName);

        launcher.Create("my game", _temp.FullName);

        Assert.Equal("Could not create it", Assert.Single(_complaints).Title);
        Assert.Single(_opened);
    }

    [Fact]
    public void CreatingWhereNothingCanBeWrittenSaysSo()
    {
        var blocker = Path.Combine(_temp.FullName, "file");
        File.WriteAllText(blocker, "");

        Launcher().Create("My Game", blocker);

        Assert.Equal("Could not write there", Assert.Single(_complaints).Title);
        Assert.Empty(_opened);
    }

    [Fact]
    public void ANewProjectDefaultsToOrblitInDocumentsUnderHome()
    {
        Assert.Equal(Path.Combine("home", "Documents", "Orblit"), Launcher(home: "home").DefaultLocation);
        Assert.Equal(
            Path.Combine(Directory.GetCurrentDirectory(), "Documents", "Orblit"),
            Launcher(home: null).DefaultLocation);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(59, "just now")]
    [InlineData(60, "1m ago")]
    [InlineData(59 * 60, "59m ago")]
    [InlineData(60 * 60, "1h ago")]
    [InlineData(23 * 3600, "23h ago")]
    [InlineData(24 * 3600, "1d ago")]
    [InlineData(29 * 86400, "29d ago")]
    [InlineData(30 * 86400, "1mo ago")]
    [InlineData(75 * 86400, "2mo ago")]
    [InlineData(-3600, "just now")]
    public void AgeIsTheShortestFormThatIsStillTrue(int seconds, string text) =>
        Assert.Equal(text, Elapsed.Ago(Now.AddSeconds(-seconds), Now));

    private sealed class Clock(DateTime at) : TimeProvider
    {
        public DateTime At { get; set; } = at;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(At, DateTimeKind.Utc));
    }
}

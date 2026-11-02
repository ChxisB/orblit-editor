namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class GameBuilderTests : IDisposable
{
    private static readonly GameEntry Entry = new("game/Game.csproj", "Crate.CrateGame");
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private string Output => Path.Combine(_temp.FullName, ".orblit", "game");

    [Fact]
    public async Task TheBuildIsAskedForTheProjectFileAndTheGameFolder()
    {
        var command = new FakeBuild(new BuildOutput(false, "no"));

        await new GameBuilder(command).BuildAsync(_temp.FullName, Entry, TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath(Path.Combine(_temp.FullName, "game", "Game.csproj")), command.ProjectFile);
        Assert.Equal(Path.GetFullPath(Output), command.OutputFolder);
    }

    [Fact]
    public async Task AProjectPathWrittenWithBackslashesIsFoundOnAnySystem()
    {
        var command = new FakeBuild(new BuildOutput(false, "no"));

        await new GameBuilder(command).BuildAsync(_temp.FullName, new GameEntry("game\\Game.csproj", "Crate.CrateGame"), TestContext.Current.CancellationToken);

        Assert.Equal(Path.GetFullPath(Path.Combine(_temp.FullName, "game", "Game.csproj")), command.ProjectFile);
    }

    [Fact]
    public async Task AFailedBuildHasNoAssemblyAndKeepsTheOutput()
    {
        var build = await new GameBuilder(new FakeBuild(new BuildOutput(false, "Game.cs(3): error CS1002"))).BuildAsync(_temp.FullName, Entry, TestContext.Current.CancellationToken);

        Assert.False(build.Succeeded);
        Assert.Null(build.AssemblyPath);
        Assert.Equal("Game.cs(3): error CS1002", build.Output);
    }

    [Fact]
    public async Task ABuildThatWorkedGivesTheAssemblyNamedForTheProjectFile()
    {
        var command = new FakeBuild(new BuildOutput(true, "Build succeeded."), folder => File.WriteAllBytes(Path.Combine(folder, "Game.dll"), []));

        var build = await new GameBuilder(command).BuildAsync(_temp.FullName, Entry, TestContext.Current.CancellationToken);

        Assert.True(build.Succeeded);
        Assert.Equal(Path.Combine(Path.GetFullPath(Output), "Game.dll"), build.AssemblyPath);
        Assert.Equal("Build succeeded.", build.Output);
    }

    [Fact]
    public async Task ABuildThatMadeNoSuchAssemblyFailsAndSaysWhatItLookedFor()
    {
        var build = await new GameBuilder(new FakeBuild(new BuildOutput(true, "Build succeeded."))).BuildAsync(_temp.FullName, Entry, TestContext.Current.CancellationToken);

        Assert.False(build.Succeeded);
        Assert.Contains("Game.dll", build.Output);
        Assert.Contains("Build succeeded.", build.Output);
    }

    // A project that names its assembly differently would otherwise be passed by the one an earlier build left.
    [Fact]
    public async Task AnAssemblyLeftByAnEarlierBuildIsNotMistakenForThisBuildsOwn()
    {
        Directory.CreateDirectory(Output);
        File.WriteAllBytes(Path.Combine(Output, "Game.dll"), []);

        var build = await new GameBuilder(new FakeBuild(new BuildOutput(true, "Build succeeded."))).BuildAsync(_temp.FullName, Entry, TestContext.Current.CancellationToken);

        Assert.False(build.Succeeded);
        Assert.Null(build.AssemblyPath);
    }

    [Fact]
    public async Task TheCallersCancellationReachesTheBuild()
    {
        var command = new FakeBuild(new BuildOutput(false, "no"));
        using var cancel = new CancellationTokenSource();

        await new GameBuilder(command).BuildAsync(_temp.FullName, Entry, cancel.Token);

        Assert.Equal(cancel.Token, command.Cancel);
    }

    private sealed class FakeBuild(BuildOutput result, Action<string>? onRun = null) : IBuildCommand
    {
        public string? ProjectFile { get; private set; }

        public string? OutputFolder { get; private set; }

        public CancellationToken Cancel { get; private set; }

        public Task<BuildOutput> RunAsync(string projectFile, string outputFolder, CancellationToken cancel)
        {
            ProjectFile = projectFile;
            OutputFolder = outputFolder;
            Cancel = cancel;
            Directory.CreateDirectory(outputFolder);
            onRun?.Invoke(outputFolder);
            return Task.FromResult(result);
        }
    }
}

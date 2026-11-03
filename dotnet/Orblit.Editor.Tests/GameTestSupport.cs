namespace Orblit.Editor.Tests;

using System.Runtime.Loader;
using Orblit.Editor.Core;

internal sealed class AllowAll : IProjectTrust
{
    public bool IsTrusted(string projectFolder) => true;

    public void Grant(string projectFolder)
    {
    }

    public void Revoke(string projectFolder)
    {
    }
}

/// <summary>A build that runs the given step in place of the compiler, and counts how often it was asked.</summary>
internal sealed class FakeBuild(Func<string, CancellationToken, Task<BuildOutput>> step) : IBuildCommand
{
    public int Runs { get; private set; }

    public Task<BuildOutput> RunAsync(string projectFile, string outputFolder, CancellationToken cancel)
    {
        Runs++;
        return step(outputFolder, cancel);
    }
}

/// <summary>What the tests that play a project's game share: a project with a game field, and builds that need no compiler.</summary>
internal static class GameTestSupport
{
    public static string Entry(string type) => $$"""{ "project": "TinyGame/TinyGame.csproj", "type": "{{type}}" }""";

    // The fixture's game is built once for a class. A build that copies it is quick and offline.
    public static FakeBuild Copying(GameBuild built) => new((output, _) =>
    {
        Directory.CreateDirectory(output);
        foreach (var file in Directory.EnumerateFiles(Path.GetDirectoryName(built.AssemblyPath)!))
        {
            File.Copy(file, Path.Combine(output, Path.GetFileName(file)), overwrite: true);
        }

        return Task.FromResult(new BuildOutput(true, "built"));
    });

    public static FakeBuild Failing(string text) => new((_, _) => Task.FromResult(new BuildOutput(false, text)));

    // A build that says it has started and then waits to be cancelled.
    public static FakeBuild Blocked(TaskCompletionSource started) => new(async (_, cancel) =>
    {
        started.SetResult();
        await Task.Delay(Timeout.Infinite, cancel);
        return new BuildOutput(true, "never");
    });

    /// <summary>A project with the starter's camera and the given <c>game</c> field, opened as a session.</summary>
    public static EditorSession Open(DirectoryInfo project, string game, IBuildCommand build, IProjectTrust? trust = null)
    {
        WriteProject(project, game, new EditorScene([EditorScene.Starter()["camera"]!]));
        return OpenWritten(project, build, trust);
    }

    /// <summary>Writes a project that names <paramref name="game"/> and holds <paramref name="scene"/> as its main scene.</summary>
    public static void WriteProject(DirectoryInfo project, string game, EditorScene scene)
    {
        File.WriteAllText(
            Path.Combine(project.FullName, Project.FileName),
            $$"""{ "formatVersion": 1, "name": "Crates", "game": {{game}} }""");
        Directory.CreateDirectory(Path.Combine(project.FullName, "scenes"));
        File.WriteAllText(Path.Combine(project.FullName, "scenes", "main.oscene"), SceneDocument.Encode(scene));
    }

    /// <summary>A project already written to disk, opened as a session.</summary>
    public static EditorSession OpenWritten(DirectoryInfo project, IBuildCommand build, IProjectTrust? trust = null)
    {
        var opened = ProjectWorkspace.Open(project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Trust = trust ?? new AllowAll(),
            Builder = new GameBuilder(build),
        };
    }

    public static bool AnyGameContext() => GameContextCount() > 0;

    public static int GameContextCount() =>
        AssemblyLoadContext.All.Count(one => one.Name?.StartsWith("game:", StringComparison.Ordinal) == true);
}

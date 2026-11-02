namespace Orblit.Editor.Tests;

using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Orblit.Editor.Core;
using Orblit.Game;
using Doc = Orblit.Scene;

/// <summary>Builds the game under Fixtures/TinyGame once for every test in the class.</summary>
public sealed class TinyGameBuild : IAsyncLifetime
{
    public const string Collection = "game loading";

    public GameBuild Build { get; private set; } = new(null, "not built");

    public async ValueTask InitializeAsync() => Build = await new GameBuilder(new DotnetBuild()).BuildAsync(
        Path.Combine(AppContext.BaseDirectory, "Fixtures"),
        new GameEntry("TinyGame/TinyGame.csproj", "TinyGame.Tiny"),
        TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// The tests that load a game share a collection, so one test's contexts are never counted in another's.
[Collection(TinyGameBuild.Collection)]
public sealed class GameLoaderTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>
{
    private string Dll => fixture.Build.AssemblyPath ?? throw new InvalidOperationException(fixture.Build.Output);

    private LoadedGame Load(string type) => new GameLoader().Load(Dll, type);

    [Fact]
    public void TheFixtureGameBuilds() => Assert.True(fixture.Build.Succeeded, fixture.Build.Output);

    [Fact]
    public void TheBuiltFolderHoldsItsOwnCopiesOfTheEnginesAssemblies()
    {
        var folder = Path.GetDirectoryName(Dll)!;

        Assert.True(File.Exists(Path.Combine(folder, "Orblit.Game.dll")));
        Assert.True(File.Exists(Path.Combine(folder, "Orblit.Scene.dll")));
    }

    [Fact]
    public void TheLoadedGameIsTheHostsIGameAndChangesTheHostsScene()
    {
        // The seam where a plug-in goes wrong: two copies of Orblit.Game make two IGame types, and the
        // game's SceneDiff would not be the host's SceneDiff.
        using var loaded = Load("TinyGame.Tiny");
        var host = new GameHost(loaded.Game, new Doc.SceneDocument(), "/project", new SilentLog());

        host.Start();

        Assert.Null(host.Fault);
        Assert.True(host.Scene.Contains("crate"));
    }

    [Fact]
    public void TheGamesContextHoldsTheGameAndItsOwnLibraryAndNoneOfTheEnginesAssemblies()
    {
        using var loaded = Load("TinyGame.Tiny");
        new GameHost(loaded.Game, new Doc.SceneDocument(), "/project", new SilentLog()).Start();

        var context = AssemblyLoadContext.GetLoadContext(loaded.Game.GetType().Assembly)!;
        var names = context.Assemblies.Select(one => one.GetName().Name).ToList();

        Assert.NotSame(AssemblyLoadContext.Default, context);
        Assert.Contains("TinyGame", names);
        Assert.Contains("TinyPart", names);
        Assert.DoesNotContain("Orblit.Game", names);
        Assert.DoesNotContain("Orblit.Scene", names);
    }

    [Fact]
    public void AGameIsGoneFromTheLoadedGameOnceItIsDisposed()
    {
        var loaded = Load("TinyGame.Tiny");

        loaded.Dispose();

        Assert.Throws<ObjectDisposedException>(() => loaded.Game);
    }

    [Fact]
    public void DisposingTheGameStartsItsContextUnloading()
    {
        var loaded = Load("TinyGame.Tiny");
        var context = AssemblyLoadContext.GetLoadContext(loaded.Game.GetType().Assembly)!;
        using var bytes = new MemoryStream([1, 2, 3]);

        loaded.Dispose();

        Assert.Throws<InvalidOperationException>(() => context.LoadFromStream(bytes));
    }

    [Fact]
    public void DisposingTheGameLetsItsContextBeCollected()
    {
        var context = LoadRunAndDispose();

        Assert.True(IsCollected(context), "the game's load context is still alive after Dispose");
    }

    [Theory]
    [InlineData("TinyGame.Missing", "no type named TinyGame.Missing")]
    [InlineData("TinyGame.NotAGame", "is not an IGame")]
    [InlineData("TinyGame.Idle", "abstract")]
    [InlineData("TinyGame.NeedsAnArgument", "public constructor with no parameters")]
    [InlineData("TinyGame.Breaks", "the crate is missing")]
    public void ATypeThatCannotBeAGameIsRefusedInWords(string type, string said)
    {
        var refusal = Assert.Throws<GameLoadException>(() => Load(type));

        Assert.Contains(said, refusal.Message);
    }

    [Fact]
    public void AFileThatIsNotAnAssemblyIsRefusedInWords()
    {
        var folder = Directory.CreateTempSubdirectory("orblit_test");
        try
        {
            var notAssembly = Path.Combine(folder.FullName, "Game.dll");
            File.WriteAllBytes(notAssembly, [1, 2, 3]);

            Assert.Throws<GameLoadException>(() => new GameLoader().Load(notAssembly, "TinyGame.Tiny"));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void AnAssemblyThatIsNotThereIsRefusedInWords() =>
        Assert.Throws<GameLoadException>(() => new GameLoader().Load(Path.Combine(Path.GetTempPath(), "orblit_none", "Game.dll"), "TinyGame.Tiny"));

    [Fact]
    public void ARefusedLoadLeavesNoContextBehind()
    {
        Assert.Throws<GameLoadException>(() => Load("TinyGame.Missing"));
        AssertEveryGameContextIsUnloading();

        Assert.True(Until(() => !AssemblyLoadContext.All.Any(one => one.Name?.StartsWith("game:", StringComparison.Ordinal) == true)));
    }

    [Fact]
    public void AGameBuiltForAnotherVersionOfTheContractIsRefusedBeforeAnyTypeIsMade()
    {
        var refusal = Assert.Throws<GameLoadException>(() => new GameLoader(new Version(0, 9, 0)).Load(Dll, "TinyGame.Tiny"));

        // The fixture is built against the contract this test project references.
        Assert.Contains((typeof(IGame).Assembly.GetName().Version ?? new Version()).ToString(3), refusal.Message);
        Assert.Contains("0.9.0", refusal.Message);
    }

    [Fact]
    public void TheSameVersionAndAnotherPatchAreAccepted()
    {
        Assert.Null(GameLoader.VersionProblem(new Version(0, 1, 0), new Version(0, 1, 0)));
        Assert.Null(GameLoader.VersionProblem(new Version(0, 1, 3), new Version(0, 1, 0)));
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 1, 0)]
    public void AnotherMinorOrMajorIsRefusedNamingBothVersions(int major, int minor, int patch)
    {
        var problem = GameLoader.VersionProblem(new Version(0, 1, 0), new Version(major, minor, patch));

        Assert.Contains($"{major}.{minor}.{patch}", problem);
        Assert.Contains("0.1.0", problem);
    }

    [Fact]
    public void AnAssemblyThatDoesNotUseTheContractIsRefused() =>
        Assert.Contains("Orblit.Game", GameLoader.VersionProblem(new Version(0, 1, 0), null));

    // Kept out of line so nothing in the test's own frame keeps the game alive.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private WeakReference LoadRunAndDispose()
    {
        var loaded = Load("TinyGame.Tiny");
        var host = new GameHost(loaded.Game, new Doc.SceneDocument(), "/project", new SilentLog());
        host.Start();
        host.Update(new GameInput(new Orblit.Input.FrameControls()), 0.016);
        host.Stop();
        var context = new WeakReference(AssemblyLoadContext.GetLoadContext(loaded.Game.GetType().Assembly));
        loaded.Dispose();
        return context;
    }

    // A collectible context that was never unloaded is collected all the same once nothing holds it, so only
    // an unloading context's refusal of a new assembly shows that Unload ran. Out of line so no local holds one.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertEveryGameContextIsUnloading()
    {
        using var bytes = new MemoryStream([1, 2, 3]);
        foreach (var context in AssemblyLoadContext.All.Where(one => one.Name?.StartsWith("game:", StringComparison.Ordinal) == true))
        {
            Assert.Throws<InvalidOperationException>(() => context.LoadFromStream(bytes));
        }
    }

    private static bool IsCollected(WeakReference reference) => Until(() => !reference.IsAlive);

    // Unloading finishes on a later collection than the one that finds nothing holding the context.
    internal static bool Until(Func<bool> done)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (done())
            {
                return true;
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return done();
    }

    private sealed class SilentLog : IGameLog
    {
        public void Say(string message)
        {
        }

        public void Error(string message, Exception error)
        {
        }
    }
}

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

// What the browser's cook marks say, asked of the real cook over a real folder.
public sealed class CookStatusTests : IDisposable
{
    // The smallest thing GltfImporter will take. Managed code with no encoder behind it, so a test
    // can cook for real on a machine with no tools built.
    private const string Gltf = """{"asset": {"version": "2.0"}}""";

    private const string GltfWithScenes = """{"asset": {"version": "2.0"}, "scenes": []}""";

    private readonly string _project = Directory.CreateTempSubdirectory("orblit_cook_status").FullName;

    public void Dispose() => Directory.Delete(_project, recursive: true);

    private string At(params string[] parts) => Path.Join([_project, .. parts]);

    private void WriteAsset(string name, string contents)
    {
        var file = At(["assets", .. name.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, contents);
    }

    // Never the machine's own cache: a test that cooked into it would leave its rubbish in somebody's
    // cache and read somebody's rubbish back.
    private CookStatusIndex IndexFor(CookTarget? target = null) => new(_project, At("cache"), target);

    private Task<CookReport> Cook() =>
        new CookProject(At("assets"), At("build", "assets"), CookProject.CurrentCookTarget, At("cache")).RunAsync();

    [Fact]
    public async Task AProjectWithNoAssetsFolderSaysNothingAndIsFine()
    {
        var index = IndexFor();
        await index.RefreshAsync();

        Assert.Empty(index.Counts);
        Assert.Null(index.Problem);
    }

    [Fact]
    public async Task AnAssetNobodyHasCookedNeedsCooking()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();
        await index.RefreshAsync();

        Assert.Equal(CookState.Stale, index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task AfterACookItIsCooked()
    {
        WriteAsset("ship.gltf", Gltf);
        await Cook();

        var index = IndexFor();
        await index.RefreshAsync();
        Assert.Equal(CookState.Cooked, index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task ChangingItNeedsCookingAgain()
    {
        WriteAsset("ship.gltf", Gltf);
        await Cook();
        WriteAsset("ship.gltf", GltfWithScenes);

        var index = IndexFor();
        await index.RefreshAsync();
        Assert.Equal(CookState.Stale, index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task AFileNoImporterClaimsHasNothingToCook()
    {
        WriteAsset("notes.md", "read me");
        var index = IndexFor();
        await index.RefreshAsync();

        Assert.Equal(CookState.Ignored, index[At("assets", "notes.md")]);
    }

    [Fact]
    public async Task AFileOutsideTheAssetsFolderIsNotAskedAbout()
    {
        Directory.CreateDirectory(At("scenes"));
        File.WriteAllText(At("scenes", "main.oscene"), "");
        WriteAsset("ship.gltf", Gltf);

        var index = IndexFor();
        await index.RefreshAsync();

        // The cook reads assets/, so nothing else gets a mark.
        Assert.Null(index[At("scenes", "main.oscene")]);
    }

    [Fact]
    public async Task AFolderDeepInTheProjectIsFoundAndNamed()
    {
        WriteAsset("ships/small/scout.gltf", Gltf);
        var index = IndexFor();
        await index.RefreshAsync();

        // Ids are separated by '/' whatever this machine spells a path with.
        Assert.Equal(CookState.Stale, index[At("assets", "ships", "small", "scout.gltf")]);
    }

    [Fact]
    public async Task ItCountsWhatIsOutstanding()
    {
        WriteAsset("one.gltf", Gltf);
        WriteAsset("two.gltf", GltfWithScenes);
        WriteAsset("notes.md", "read me");

        var index = IndexFor();
        await index.RefreshAsync();

        Assert.Equal(2, index.Counts[CookState.Stale]);
        Assert.Equal(1, index.Counts[CookState.Ignored]);
    }

    [Fact]
    public async Task ATargetItDoesNotKnowIsSaidNotThrown()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor(new CookTarget("nintendo-64"));
        await index.RefreshAsync();

        Assert.Contains("nintendo-64", index.Problem);
        // No answer is better than an answer about the wrong machine.
        Assert.Empty(index.Counts);
    }

    [Fact]
    public async Task AskingTwiceAtOnceAsksAgainRatherThanTwice()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();

        var first = index.RefreshAsync();
        Assert.True(index.Asking);
        var second = index.RefreshAsync();
        await Task.WhenAll(first, second);

        Assert.False(index.Asking);
        Assert.Equal(CookState.Stale, index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task AListenerThatThrowsDoesNotLeaveItAskingForEver()
    {
        var index = IndexFor();
        void Throws() => throw new InvalidOperationException("The window is gone.");
        index.Changed += Throws;

        await Assert.ThrowsAsync<InvalidOperationException>(index.RefreshAsync);
        index.Changed -= Throws;

        // A flag left up would turn every later ask into a request that nobody answers.
        Assert.False(index.Asking);
        WriteAsset("ship.gltf", Gltf);
        await index.RefreshAsync();
        Assert.Equal(CookState.Stale, index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task ItSaysWhenItIsWorkingAndWhenItHasStopped()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();
        var seen = new List<bool>();
        index.Changed += () => seen.Add(index.Asking);

        await index.RefreshAsync();

        Assert.Equal([true, false], seen);
    }

    [Fact]
    public async Task ClearingForgetsWhatItKnew()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();
        await index.RefreshAsync();
        Assert.NotEmpty(index.Counts);

        index.Clear();

        Assert.Empty(index.Counts);
        Assert.Null(index[At("assets", "ship.gltf")]);
    }

    [Fact]
    public async Task AskingDoesNotCookAnything()
    {
        WriteAsset("ship.gltf", Gltf);
        await IndexFor().RefreshAsync();

        // The browser describes a build, it does not run one.
        Assert.False(Directory.Exists(At("build")));
    }

    [Fact]
    public void EveryStateHasWordsAndAReason()
    {
        foreach (var state in Enum.GetValues<CookState>())
        {
            Assert.NotEmpty(state.Label());
            Assert.NotEmpty(state.Explanation());
        }
    }

    [Fact]
    public void TheThreeWorthADotHaveOneAndTheFourthDoesNot()
    {
        Assert.NotNull(CookState.Cooked.Mark());
        Assert.NotNull(CookState.Stale.Mark());
        Assert.NotNull(CookState.Failed.Mark());

        // A README is not a problem and never will be.
        Assert.Null(CookState.Ignored.Mark());
    }

    [Fact]
    public void TheyDoNotAllLookTheSame()
    {
        HashSet<CookMark?> marks = [CookState.Cooked.Mark(), CookState.Stale.Mark(), CookState.Failed.Mark()];

        Assert.Equal(3, marks.Count);
    }

    [Fact]
    public async Task NothingIsReadyUntilItHasAnAnswerOrStoppedAsking()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();

        // Not asked yet, so nothing is on its way and the empty answer is the whole answer.
        Assert.True(index.Ready);

        var asking = index.RefreshAsync();
        Assert.False(index.Ready);
        await asking;

        Assert.True(index.Ready);
    }

    [Fact]
    public async Task APathWithDotsInItReadsAsTheFileItNames()
    {
        WriteAsset("ship.gltf", Gltf);
        var index = IndexFor();
        await index.RefreshAsync();

        Assert.Equal(CookState.Stale, index[At("assets", "meshes", "..", "ship.gltf")]);
    }
}

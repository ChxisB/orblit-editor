namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

// Dart: orblit-editor/test/pack_export_test.dart. Each case cooks and packs a real folder.
public sealed class PackExportTests : IDisposable
{
    private readonly string _project = Directory.CreateTempSubdirectory("orblit_export").FullName;

    public PackExportTests()
    {
        WriteAsset("ship.gltf", Gltf());
        WriteAsset("rock.gltf", Gltf(40));
    }

    public void Dispose() => Directory.Delete(_project, recursive: true);

    // A glTF of a size the test picks. Managed code cooks it, so a test cooks for real on a machine with
    // no native tools built.
    private static string Gltf(int padding = 0) => $$$"""{"asset": {"version": "2.0"}, "extras": {"pad": "{{{new string('x', padding)}}}"}}""";

    private string At(params string[] parts) => Path.Join([_project, .. parts]);

    private void WriteAsset(string name, string contents)
    {
        var file = At(["assets", .. name.Split('/')]);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, contents);
    }

    // Never the machine's own cache, as in the cook status tests.
    private ExportRequest Request(IReadOnlyList<string> targets, string? patchOf = null) =>
        new() { ProjectDirectory = _project, Targets = targets, PatchOf = patchOf, CachePath = At("cache") };

    private Task<TargetExport> Export(string target, string? patchOf = null) =>
        ExportRun.ExportTargetAsync(Request([target], patchOf), target);

    private string[] Exported() => [.. Directory.GetFiles(At("exports")).Select(Path.GetFileName).Order(StringComparer.Ordinal)!];

    [Fact]
    public async Task AWholeExportWritesOnePackPerTargetUnderExports()
    {
        var done = await ExportRun.RunAsync(Request(["macos", "android"]));

        Assert.All(done, one => Assert.Equal(ExportOutcome.Packed, one.Outcome));
        Assert.Equal(["game.macos.opack", "game.android.opack"], done.Select(one => Path.GetFileName(one.Path)));
        var listing = await OpenedExports.ListAsync(_project);
        Assert.Equal(["android", "macos"], listing.Packs.Select(pack => pack.Target).Order(StringComparer.Ordinal));
        Assert.All(listing.Packs, pack => Assert.False(pack.IsPatch));
    }

    [Fact]
    public async Task AWholeExportReportsTheSizeTheAssetsAndTheSharing()
    {
        var done = await Export("macos");

        var summary = done.Summary!;
        Assert.Equal(2, summary.Assets);
        Assert.Equal(2, summary.Added);
        Assert.Equal(0, summary.Replaced);
        Assert.Equal(new FileInfo(done.Path!).Length, summary.Bytes);
        Assert.NotEmpty(done.Cooked);
    }

    [Fact]
    public async Task AWholeExportGoesOnToTheNextTargetAfterOneCannotBeDone()
    {
        var done = await ExportRun.RunAsync(Request(["nowhere", "macos"]));

        Assert.Equal(ExportOutcome.Failed, done[0].Outcome);
        Assert.Contains("nowhere", done[0].Message, StringComparison.Ordinal);
        Assert.Equal(ExportOutcome.Packed, done[1].Outcome);
    }

    [Fact]
    public async Task ACookThatDiesIsReportedAndTheNextTargetRuns()
    {
        List<string> seen = [];
        var done = await ExportRun.RunAsync(
            Request(["macos", "android"]),
            landed => seen.Add(landed.Target),
            (request, target) => target == "macos" ? throw new InvalidOperationException("out of memory") : ExportRun.ExportTargetAsync(request, target));

        Assert.Equal([ExportOutcome.Failed, ExportOutcome.Packed], done.Select(one => one.Outcome));
        Assert.Contains("stopped before it finished", done[0].Message, StringComparison.Ordinal);
        Assert.Contains("out of memory", done[0].Message, StringComparison.Ordinal);
        Assert.Equal(["macos", "android"], seen);
    }

    [Fact]
    public async Task AWholeExportSaysWhatDidNotCookAndWritesNoPackForTheTarget()
    {
        WriteAsset("broken.gltf", "{ not json");

        var done = await Export("macos");

        Assert.Equal(ExportOutcome.CookFailed, done.Outcome);
        Assert.Equal(["broken.gltf"], done.Failures.Select(failure => failure.Id));
        Assert.NotEmpty(done.Failures[0].Reason);
        Assert.Null(done.Summary);
        Assert.False(File.Exists(At("exports", "game.macos.opack")));
    }

    [Fact]
    public async Task AWholeExportKeepsTheLastGoodPackWhenTheNextCookFails()
    {
        await Export("macos");
        var before = File.ReadAllBytes(At("exports", "game.macos.opack"));
        WriteAsset("broken.gltf", "{ not json");

        var done = await Export("macos");

        Assert.Equal(ExportOutcome.CookFailed, done.Outcome);
        Assert.Equal(before, File.ReadAllBytes(At("exports", "game.macos.opack")));
    }

    [Fact]
    public async Task AProjectWithNoAssetsFolderSaysSo()
    {
        Directory.Delete(At("assets"), recursive: true);

        var done = await Export("macos");

        Assert.Equal(ExportOutcome.Failed, done.Outcome);
        Assert.Contains("assets", done.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSameProjectExportedAgainIsUnchanged()
    {
        await Export("macos");

        var again = await Export("macos");

        Assert.Equal(ExportOutcome.Unchanged, again.Outcome);
        Assert.Single((await OpenedExports.ListAsync(_project)).Packs);
        Assert.Equal(["game.macos.opack"], Exported());
    }

    [Fact]
    public async Task AWholeExportSetsTheOldPackAsideUnderItsHashWhenItChanges()
    {
        var first = await Export("macos");
        var firstHash = (await OpenedExports.ListAsync(_project)).Packs.Single().Hash;
        WriteAsset("ship.gltf", Gltf(10));

        var second = await Export("macos");

        Assert.Equal(ExportOutcome.Packed, second.Outcome);
        Assert.Equal(first.Path, second.Path);
        Assert.Equal(2, (await OpenedExports.ListAsync(_project)).Packs.Count);
        Assert.True(File.Exists(At("exports", $"game.macos.{firstHash[..12]}.opack")));
    }

    // Added in C#. File.Move refuses a name that is taken, where Dart's rename replaced it. A pack that
    // comes back to an earlier state is set aside under a hash that was set aside before.
    [Fact]
    public async Task APackSetAsideASecondTimeReplacesTheFirst()
    {
        await Export("macos");
        WriteAsset("ship.gltf", Gltf(10));
        await Export("macos");
        WriteAsset("ship.gltf", Gltf());
        await Export("macos");
        WriteAsset("ship.gltf", Gltf(10));

        var done = await Export("macos");

        Assert.Equal(ExportOutcome.Packed, done.Outcome);
        Assert.Equal(3, Exported().Length);
    }

    // Added in C#. A whole pack that comes back to an earlier state leaves two files with one hash, the
    // pack set aside and the current one. Dart's map kept one of them, where ToDictionary threw.
    [Fact]
    public async Task APatchStandsOnAPackThatTwoFilesHold()
    {
        await Export("macos");
        WriteAsset("ship.gltf", Gltf(10));
        await Export("macos");
        WriteAsset("ship.gltf", Gltf());
        var whole = (await Export("macos")).Path!;
        WriteAsset("rock.gltf", Gltf(60));

        var done = await Export("macos", whole);

        Assert.Equal(ExportOutcome.Packed, done.Outcome);
    }

    private async Task<string> Whole() => (await Export("macos")).Path!;

    [Fact]
    public async Task APatchHoldsTheOneAssetThatChanged()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));

        var done = await Export("macos", whole);

        Assert.Equal(ExportOutcome.Packed, done.Outcome);
        Assert.Equal("game.macos.opack", done.PatchOf);
        var summary = done.Summary!;
        Assert.Equal(1, summary.Assets);
        Assert.Equal(1, summary.Replaced);
        Assert.Equal(0, summary.Added);
        Assert.Equal(0, summary.Removed);
        Assert.NotEqual("game.macos.opack", Path.GetFileName(done.Path));
        Assert.True(File.Exists(whole));
    }

    [Fact]
    public async Task APatchSaysWhatWasAddedAndWhatWent()
    {
        var whole = await Whole();
        WriteAsset("tree.gltf", Gltf(20));
        File.Delete(At("assets", "rock.gltf"));

        var done = await Export("macos", whole);

        var summary = done.Summary!;
        Assert.Equal(1, summary.Added);
        Assert.Equal(0, summary.Replaced);
        Assert.Equal(1, summary.Removed);
    }

    [Fact]
    public async Task APatchNamesThePackItStandsOn()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var done = await Export("macos", whole);

        var listing = await OpenedExports.ListAsync(_project);
        var patch = listing.Packs.Single(pack => pack.Path == done.Path);
        var under = listing.Packs.Single(pack => pack.Path == whole);
        Assert.True(patch.IsPatch);
        Assert.Equal(under.Hash, patch.Base);
    }

    [Fact]
    public async Task APatchOfNothingThatChangedWritesNothing()
    {
        var whole = await Whole();

        var done = await Export("macos", whole);

        Assert.Equal(ExportOutcome.Unchanged, done.Outcome);
        Assert.Contains("game.macos.opack", done.Message, StringComparison.Ordinal);
        Assert.Single((await OpenedExports.ListAsync(_project)).Packs);
        Assert.Equal(["game.macos.opack"], Exported());
    }

    [Fact]
    public async Task APatchOfAPackThatIsNotThereIsAFailureThatNamesIt()
    {
        await Whole();

        var done = await Export("macos", At("exports", "gone.opack"));

        Assert.Equal(ExportOutcome.Failed, done.Outcome);
        Assert.Contains("gone.opack", done.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APatchForAnotherTargetThanItsBaseIsRefused()
    {
        var whole = await Whole();

        var done = await Export("android", whole);

        Assert.Equal(ExportOutcome.Failed, done.Outcome);
        Assert.Contains("macos", done.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APatchCanStandOnAnotherPatch()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var one = (await Export("macos", whole)).Path!;
        WriteAsset("rock.gltf", Gltf(60));

        var two = await Export("macos", one);

        Assert.Equal(ExportOutcome.Packed, two.Outcome);
        Assert.Equal(1, two.Summary!.Assets);
        Assert.Equal(Path.GetFileName(one), two.PatchOf);
        Assert.Equal(3, (await PackContents.ReadAsync(_project, two.Path!)).Packs.Count);
    }

    [Fact]
    public async Task APatchStillFindsItsBaseAfterANewerWholePackReplacesIt()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var patch = (await Export("macos", whole)).Path!;
        WriteAsset("rock.gltf", Gltf(60));
        await Export("macos");

        var contents = await PackContents.ReadAsync(_project, patch);

        Assert.Equal(2, contents.Packs.Count);
        Assert.NotEqual("game.macos.opack", contents.Packs[0]);
    }

    [Fact]
    public async Task TheFolderListsNewestFirstAndSetsAsideWhatIsNotAPack()
    {
        await Export("macos");
        File.WriteAllText(At("exports", "notes.opack"), "not a pack");
        File.WriteAllText(At("exports", "rock.obj"), "v 0 0 0");

        var listing = await OpenedExports.ListAsync(_project);

        Assert.Equal(["game.macos.opack"], listing.Packs.Select(pack => Path.GetFileName(pack.Path)));
        Assert.StartsWith("notes.opack", Assert.Single(listing.Unreadable), StringComparison.Ordinal);
    }

    // Added in C#. Dart's case listed one pack, which says nothing of the order.
    [Fact]
    public async Task TheFolderListsTheNewestPackFirst()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var patch = (await Export("macos", whole)).Path!;
        File.SetLastWriteTimeUtc(whole, DateTime.UtcNow.AddHours(-1));

        var listing = await OpenedExports.ListAsync(_project);

        Assert.Equal([patch, whole], listing.Packs.Select(pack => pack.Path));
    }

    [Fact]
    public async Task AnEmptyProjectHasNoExports()
    {
        var listing = await OpenedExports.ListAsync(_project);

        Assert.Empty(listing.Packs);
        Assert.Empty(listing.Unreadable);
    }

    [Fact]
    public async Task TheFolderSaysWhichPackIsMissingWhenAChainIsBroken()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var patch = (await Export("macos", whole)).Path!;
        File.Delete(whole);

        var error = await Assert.ThrowsAsync<ExportChainBrokenException>(() => PackContents.ReadAsync(_project, patch));

        Assert.Contains("was built on", error.Message, StringComparison.Ordinal);
        Assert.Contains("exports folder", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheContentsListTheLargestAssetFirst()
    {
        var whole = await Whole();

        var contents = await PackContents.ReadAsync(_project, whole);

        Assert.Equal(["rock.gltf.gltf", "ship.gltf.gltf"], contents.Rows.Select(row => row.Id));
        Assert.True(contents.Rows[0].Bytes > contents.Rows[1].Bytes);
        Assert.Equal(contents.Rows.Sum(row => row.Bytes), contents.Bytes);
        Assert.False(contents.Layered);
    }

    [Fact]
    public async Task TheContentsNameWhichPackEachAssetComesFrom()
    {
        var whole = await Whole();
        WriteAsset("ship.gltf", Gltf(10));
        var patch = await Export("macos", whole);

        var contents = await PackContents.ReadAsync(_project, patch.Path!);

        var owners = contents.Rows.ToDictionary(row => row.Id, row => row.Pack);
        Assert.Equal(Path.GetFileName(patch.Path), owners["ship.gltf.gltf"]);
        Assert.Equal("game.macos.opack", owners["rock.gltf.gltf"]);
        Assert.True(contents.Layered);
    }

    [Fact]
    public async Task TheContentsNameTheIdsAPatchTakesAway()
    {
        var whole = await Whole();
        File.Delete(At("assets", "rock.gltf"));
        var patch = await Export("macos", whole);

        var contents = await PackContents.ReadAsync(_project, patch.Path!);

        Assert.Equal(["rock.gltf.gltf"], contents.Removed);
        Assert.Equal(["ship.gltf.gltf"], contents.Rows.Select(row => row.Id));
    }

    [Fact]
    public async Task TheContentsReadTheDirectoryAndNotTheBlobs()
    {
        WriteAsset("big.gltf", Gltf(300000));
        var whole = await Whole();
        var bytes = new Counting(new MemoryPackBytes(File.ReadAllBytes(whole)));

        var pack = await AssetPack.OpenAsync(bytes);
        var contents = PackContents.Of([("game.macos.opack", pack)]);

        Assert.True(contents.Rows[0].Bytes > 300000);
        Assert.True(bytes.Read < 20000, $"{bytes.Read} bytes read");
    }

    // Counts the bytes asked of the pack, to show that listing reads a directory and not the blobs.
    private sealed class Counting(IPackBytes inner) : IPackBytes
    {
        public long Read { get; private set; }

        public Task<long> LengthAsync() => inner.LengthAsync();

        public Task<byte[]> ReadAsync(long at, long bytes)
        {
            Read += bytes;
            return inner.ReadAsync(at, bytes);
        }
    }
}

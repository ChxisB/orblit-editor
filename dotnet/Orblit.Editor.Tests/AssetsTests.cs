namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;

// The project folder as the browser reads it: kinds, sizes, folders, watching, and the things it makes.
public sealed class AssetsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_assets").FullName;

    public AssetsTests()
    {
        Directory.CreateDirectory(At("assets", "meshes"));
        Directory.CreateDirectory(At("scenes"));
        File.WriteAllText(At("scenes", "main.orblitscene"), "{}");
        File.WriteAllBytes(At("assets", "meshes", "crate.glb"), new byte[2048]);
        File.WriteAllBytes(At("assets", "rock.png"), [1, 2, 3]);
        File.WriteAllText(At(".hidden"), "x");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string At(params string[] parts) => Path.Join([_root, .. parts]);

    private static string[] Names(IEnumerable<Asset> assets) => [.. assets.Select(asset => asset.Name)];

    [Fact]
    public void PutsFoldersFirstAndThenSortsByName()
    {
        Assert.Equal(["assets", "scenes"], Names(AssetTree.Read(_root)));
    }

    [Fact]
    public void LeavesDotfilesOutSinceTheyAreTheToolsBusiness()
    {
        Assert.DoesNotContain(".hidden", Names(AssetTree.Read(_root)));
    }

    [Fact]
    public void LabelsAFileByItsExtension()
    {
        Assert.Same(AssetKind.Mesh, AssetTree.Read(At("assets", "meshes")).Single().Kind);
    }

    [Fact]
    public void AnUnknownExtensionIsAFileNotACrash()
    {
        File.WriteAllText(At("notes.zzz"), "x");

        Assert.Same(AssetKind.Other, AssetTree.Read(_root).First(asset => asset.Name == "notes.zzz").Kind);
    }

    [Fact]
    public void AFolderThatHasGoneReadsAsEmptyRatherThanThrowing()
    {
        // What happens when somebody deletes a folder in Finder while the browser is open on it.
        Assert.Empty(AssetTree.Read(At("nowhere")));
    }

    [Fact]
    public void ReportsASizeSomebodyCanRead()
    {
        Assert.Equal("2 KB", AssetTree.Read(At("assets", "meshes")).Single().Size);
    }

    [Fact]
    public void AFolderHasNoSizeBecauseTheNumberWouldBeALie()
    {
        var first = AssetTree.Read(_root)[0];

        Assert.Null(first.Bytes);
        Assert.Equal("", first.Size);
    }

    [Fact]
    public void FindsNestedFoldersWithTheirDepth()
    {
        var byName = new AssetTree(_root).Folders().ToDictionary(folder => Path.GetFileName(folder.Path), folder => folder.Depth);

        Assert.Equal(new Dictionary<string, int> { ["assets"] = 0, ["meshes"] = 1, ["scenes"] = 0 }, byName);
    }

    [Fact]
    public void StopsAtTheDepthItIsGivenRatherThanWalkingForever()
    {
        var deep = _root;
        for (var i = 0; i < 6; i++)
        {
            deep = Path.Join(deep, $"d{i}");
        }

        Directory.CreateDirectory(deep);

        Assert.All(new AssetTree(_root).Folders(maximumDepth: 2), folder => Assert.True(folder.Depth <= 2));
    }

    [Fact]
    public void TheBreadcrumbIsRelativeToTheProject()
    {
        Assert.Equal("assets/meshes", new AssetTree(_root).Relative(At("assets", "meshes")));
    }

    [Fact]
    public void TheBreadcrumbIsEmptyAtTheRootRatherThanADot()
    {
        Assert.Equal("", new AssetTree(_root).Relative(_root));
    }

    [Fact]
    public void TheBreadcrumbShowsTheWholePathForSomewhereOutsideTheProject()
    {
        // Rather than a string of ".." segments, which would be both ugly and a sign that something has gone wrong.
        var outside = At("assets");

        Assert.Equal(outside, new AssetTree(At("scenes")).Relative(outside));
    }

    [Fact]
    public void TheBreadcrumbAlwaysUsesASlashNotWhateverThisHostDoes()
    {
        // A scene file is meant to open on whichever platform somebody has the project on, and a
        // backslash only means something on one of them. The expected text is written with the slash
        // joined literally, so the test cannot pass by using the host's own separator.
        Assert.Equal("assets/meshes", new AssetTree(_root).Relative(Path.Join(_root, "assets", "meshes")));
    }

    [Fact]
    public async Task SaysWhenAFileAppears()
    {
        using var tree = new AssetTree(_root);
        var seen = new TaskCompletionSource();
        tree.Changed += () => seen.TrySetResult();

        // A beat for the watch to be in place before anything changes.
        await Task.Delay(120, TestContext.Current.CancellationToken);
        File.WriteAllBytes(At("new.png"), [1]);

        await seen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CoalescesABurstIntoOne()
    {
        using var tree = new AssetTree(_root);
        var fired = 0;
        tree.Changed += () => Interlocked.Increment(ref fired);
        await Task.Delay(120, TestContext.Current.CancellationToken);

        // What copying a folder of assets in looks like.
        for (var i = 0; i < 30; i++)
        {
            File.WriteAllBytes(At($"burst{i}.png"), [1]);
        }

        await Task.Delay(700, TestContext.Current.CancellationToken);

        // Thirty files should not mean thirty rebuilds.
        Assert.InRange(Volatile.Read(ref fired), 1, 2);
    }

    [Fact]
    public async Task DisposingStopsItSoAClosedBrowserIsNotStillListening()
    {
        var tree = new AssetTree(_root);
        var fired = 0;
        tree.Changed += () => Interlocked.Increment(ref fired);
        await Task.Delay(120, TestContext.Current.CancellationToken);
        tree.Dispose();
        var before = Volatile.Read(ref fired);

        File.WriteAllBytes(At("after.png"), [1]);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(before, Volatile.Read(ref fired));
    }

    [Fact]
    public async Task LeavingTheLastListenerStopsTheWatch()
    {
        using var tree = new AssetTree(_root);
        var fired = 0;
        void Count() => Interlocked.Increment(ref fired);
        tree.Changed += Count;
        await Task.Delay(120, TestContext.Current.CancellationToken);
        tree.Changed -= Count;
        var before = Volatile.Read(ref fired);

        File.WriteAllBytes(At("after.png"), [1]);
        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(before, Volatile.Read(ref fired));
    }

    [Fact]
    public async Task AWriteUnderADotFolderIsNotNewsToTheBrowser()
    {
        // .git and .orblit hold the tool's own writes, and Read does not list them.
        Directory.CreateDirectory(At(".orblit", "shaders"));
        using var tree = new AssetTree(_root);
        var fired = 0;
        var seen = new TaskCompletionSource();
        tree.Changed += () =>
        {
            Interlocked.Increment(ref fired);
            seen.TrySetResult();
        };
        await Task.Delay(120, TestContext.Current.CancellationToken);

        File.WriteAllBytes(At(".orblit", "shaders", "pulse.filamat"), [1]);
        await Task.Delay(500, TestContext.Current.CancellationToken);
        Assert.Equal(0, Volatile.Read(ref fired));

        // The watch itself is alive, so the silence above means something.
        File.WriteAllBytes(At("visible.png"), [1]);
        await seen.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void DeletingRemovesAFile()
    {
        var target = At("assets", "rock.png");

        Assert.Null(AssetTree.Delete(target));
        Assert.False(File.Exists(target));
    }

    [Fact]
    public void DeletingRemovesAFolderAndWhatIsInIt()
    {
        Assert.Null(AssetTree.Delete(At("assets")));
        Assert.False(Directory.Exists(At("assets")));
    }

    [Fact]
    public void DeletingSaysSoWhenThereIsNothingThereRatherThanPretending()
    {
        // Silently succeeding is how somebody comes to believe a file is gone when it is not.
        Assert.NotNull(AssetTree.Delete(At("ghost")));
    }

    [Fact]
    public void RenamingMovesItWithinItsFolder()
    {
        var from = At("assets", "rock.png");

        Assert.Null(AssetTree.Rename(from, "stone.png"));
        Assert.True(File.Exists(At("assets", "stone.png")));
        Assert.False(File.Exists(from));
    }

    [Fact]
    public void RenamingRefusesToOverwriteSomethingAlreadyThere()
    {
        File.WriteAllBytes(At("assets", "stone.png"), [9]);

        var problem = AssetTree.Rename(At("assets", "rock.png"), "stone.png");

        Assert.NotNull(problem);
        // And the other file is untouched, rather than half replaced.
        Assert.Equal([9], File.ReadAllBytes(At("assets", "stone.png")));
    }

    [Fact]
    public void RenamingRefusesANameWithAPathInIt()
    {
        var problem = AssetTree.Rename(At("assets", "rock.png"), "../escaped.png");

        Assert.NotNull(problem);
        Assert.False(File.Exists(At("..", "escaped.png")));
    }

    [Fact]
    public void RenamingRefusesAnEmptyName()
    {
        Assert.NotNull(AssetTree.Rename(At("assets", "rock.png"), "  "));
    }

    [Fact]
    public void RenamingToTheSameNameIsNotAnError()
    {
        Assert.Null(AssetTree.Rename(At("assets", "rock.png"), "rock.png"));
    }

    [Fact]
    public void AFolderRenamesToo()
    {
        Assert.Null(AssetTree.Rename(At("scenes"), "levels"));
        Assert.True(Directory.Exists(At("levels")));
    }

    [Fact]
    public void ANameWithAForwardSlashIsRefusedOnEveryHost()
    {
        Assert.NotNull(AssetTree.Rename(At("assets", "rock.png"), "meshes/rock.png"));
    }

    [Fact]
    public void ARenameThatOnlyChangesTheCaseGoesThrough()
    {
        Assert.Null(AssetTree.Rename(At("assets", "rock.png"), "Rock.png"));
        Assert.Contains("Rock.png", Names(AssetTree.Read(At("assets"))));
        Assert.DoesNotContain("rock.png", Names(AssetTree.Read(At("assets"))));
    }

    [Fact]
    public void AFolderRenamesToItsOwnNameInAnotherCase()
    {
        Assert.Null(AssetTree.Rename(At("scenes"), "Scenes"));
        Assert.Contains("Scenes", Names(AssetTree.Read(_root)));
    }

    [Fact]
    public void AFreeNameIsTheOneAskedForWhenNothingIsUsingIt()
    {
        Assert.Equal("new.oscene", Path.GetFileName(AssetTree.Available(_root, "new.oscene")));
    }

    [Fact]
    public void AFreeNameIsNumberedWhenItIsTakenKeepingTheExtension()
    {
        File.WriteAllText(At("new.oscene"), "x");

        Assert.Equal("new 2.oscene", Path.GetFileName(AssetTree.Available(_root, "new.oscene")));
    }

    [Fact]
    public void AFolderAppearsWhereItWasAskedFor()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Folder, "props");

        Assert.Null(made.Problem);
        Assert.True(Directory.Exists(At("props")));
    }

    [Fact]
    public void AScriptGetsItsExtensionAndSomethingToRun()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Script, "walker");

        Assert.EndsWith("walker.ts", made.Path);
        Assert.Contains("onFrame", File.ReadAllText(made.Path!));
    }

    [Fact]
    public void EveryKindWritesAStarterThatIsNotEmpty()
    {
        var tree = new AssetTree(_root);
        foreach (var what in NewAsset.Values.Where(what => !what.IsFolder))
        {
            var made = tree.Create(_root, what, what.Suggested);

            Assert.True(made.Problem is null, what.Label);
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(made.Path!)), what.Label);
        }
    }

    [Fact]
    public void TheSecondOneOfANameGetsANameOfItsOwn()
    {
        var tree = new AssetTree(_root);
        tree.Create(_root, NewAsset.Folder, "props");

        var second = tree.Create(_root, NewAsset.Folder, "props");

        Assert.Null(second.Problem);
        Assert.NotEqual("props", Path.GetFileName(second.Path));
        Assert.True(Directory.Exists(second.Path));
    }

    [Fact]
    public void ANameThatIsAPathIsRefusedRatherThanFollowed()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Folder, "../escaped");

        Assert.NotNull(made.Problem);
        Assert.False(Directory.Exists(Path.Join(Directory.GetParent(_root)!.FullName, "escaped")));
    }

    [Fact]
    public void AnEmptyNameIsRefused()
    {
        Assert.NotNull(new AssetTree(_root).Create(_root, NewAsset.Folder, "   ").Problem);
    }

    [Fact]
    public void NothingIsMadeOutsideTheProject()
    {
        var outside = Directory.GetParent(_root)!.FullName;

        Assert.NotNull(new AssetTree(_root).Create(outside, NewAsset.Folder, "nope").Problem);
    }

    [Fact]
    public void NothingIsWrittenOutsideTheProject()
    {
        var outside = Directory.GetParent(_root)!.FullName;

        Assert.NotNull(new AssetTree(_root).Write(outside, "nope.oprefab", "{}").Problem);
    }

    [Theory]
    [InlineData("../escape.oprefab")]
    [InlineData("deeper/escape.oprefab")]
    [InlineData("..")]
    public void AWrittenNameThatIsAPathIsRefusedRatherThanFollowed(string name)
    {
        // A project one level down, so that where a bad name would land is a folder this test owns.
        var project = Directory.CreateDirectory(At("project")).FullName;

        var made = new AssetTree(project).Write(project, name, "{}");

        Assert.NotNull(made.Problem);
        Assert.Null(made.Path);
        Assert.False(File.Exists(At("escape.oprefab")));
    }

    [Fact]
    public void WhatAScriptIsCalledItIsKindedAs()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Interface, "panel");

        Assert.Same(AssetKind.Script, AssetTree.Describe(made.Path!).Kind);
    }

    [Fact]
    public void AWrittenFileComesBackDescribed()
    {
        var made = new AssetTree(_root).Write(_root, "crate.oprefab", """{"version":1}""");

        Assert.Null(made.Problem);
        var asset = AssetTree.Describe(made.Path!);
        Assert.Same(AssetKind.Prefab, asset.Kind);
        Assert.Equal(13, asset.Bytes);
    }

    [Fact]
    public void ASourceAndAHeaderComeNamedAfterEachOther()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Native, "movement");

        Assert.EndsWith("movement.cpp", made.Path);
        Assert.True(File.Exists(At("movement.h")));
    }

    [Fact]
    public void TheSourceIncludesTheHeaderItWasGiven()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Native, "movement");

        var source = File.ReadAllText(made.Path!);
        Assert.Contains("#include \"movement.h\"", source);
        Assert.DoesNotContain("NAME", source);
    }

    [Fact]
    public void TheHeaderIsNamedAfterThePairToo()
    {
        new AssetTree(_root).Create(_root, NewAsset.Native, "movement");

        var header = File.ReadAllText(At("movement.h"));
        Assert.Contains("movement", header);
        Assert.DoesNotContain("NAME", header);
    }

    [Fact]
    public void ASecondPairOfANameKeepsTheTwoHalvesTogether()
    {
        var tree = new AssetTree(_root);
        tree.Create(_root, NewAsset.Native, "movement");
        var second = tree.Create(_root, NewAsset.Native, "movement");

        var stem = Path.GetFileNameWithoutExtension(second.Path)!;
        Assert.NotEqual("movement", stem);
        Assert.True(File.Exists(At($"{stem}.h")));
        Assert.Contains($"#include \"{stem}.h\"", File.ReadAllText(second.Path!));
    }

    [Fact]
    public void NothingElseBringsASecondFileWithIt()
    {
        foreach (var what in NewAsset.Values)
        {
            Assert.Equal(what == NewAsset.Native, what.Companion is not null);
        }
    }

    [Fact]
    public void AHeaderThatIsAlreadyThereIsNotWrittenOver()
    {
        File.WriteAllText(At("movement.h"), "mine");

        new AssetTree(_root).Create(_root, NewAsset.Native, "movement");

        Assert.Equal("mine", File.ReadAllText(At("movement.h")));
    }

    [Fact]
    public void ABlendIsKindedByItsExtensionAndFoundByKind()
    {
        var tree = new AssetTree(_root);
        var made = tree.Create(_root, NewAsset.Blend, "moves");

        Assert.EndsWith("moves.oblend", made.Path);
        Assert.Same(AssetKind.Blend, AssetTree.Describe(made.Path!).Kind);
        Assert.Equal(["moves.oblend"], tree.Find(asset => asset.Kind == AssetKind.Blend));
    }

    [Fact]
    public void ANewBlendReadsBackWithAStateToStartIn()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.Blend, "moves");

        var load = BlendDocument.Decode(File.ReadAllText(made.Path!));

        Assert.Empty(load.Problems);
        Assert.Equal("moves", load.Blend.Name);
        Assert.Equal(["idle"], load.Blend.WrittenStates.Select(state => state.Name));
    }

    [Fact]
    public void ANewDataObjectIsTheTextTheDartEditorWrites()
    {
        var made = new AssetTree(_root).Create(_root, NewAsset.DataObject, "stats");

        Assert.Equal(
            "{\n  \"kind\": \"orblit.data\",\n  \"formatVersion\": 1,\n  \"name\": \"stats\",\n  \"fields\": [\n    {\n      \"key\": \"value\",\n      \"type\": \"number\",\n      \"value\": 0.0\n    }\n  ]\n}\n",
            File.ReadAllText(made.Path!));
    }

    [Fact]
    public void ALinkIsNotListed()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "A link needs a privilege on Windows.");
        Directory.CreateSymbolicLink(At("linked"), At("assets"));

        Assert.DoesNotContain("linked", Names(AssetTree.Read(_root)));
    }

    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "2 KB")]
    [InlineData(1048576, "1.0 MB")]
    [InlineData(1572864, "1.5 MB")]
    public void ASizeReadsAtAGlance(long bytes, string label)
    {
        Assert.Equal(label, SizeLabel.Of(bytes));
    }

    [Theory]
    [InlineData("crate.GLB", "Mesh")]
    [InlineData("theme.css", "Stylesheet")]
    [InlineData("a.tar.gz", "File")]
    [InlineData(".json", "File")]
    [InlineData("noextension", "File")]
    public void AKindComesFromTheLastExtensionInAnyCase(string path, string label)
    {
        Assert.Equal(label, AssetKind.Of(path).Label);
    }

    [Theory]
    [InlineData("a.cpp", true)]
    [InlineData("A.CC", true)]
    [InlineData("a.h", false)]
    [InlineData("a.ts", false)]
    public void OnlyASourceFileOffersToBuild(string name, bool builds)
    {
        Assert.Equal(builds, new Asset(name, "/p/" + name, AssetKind.Of(name)).CanBuild);
    }
}

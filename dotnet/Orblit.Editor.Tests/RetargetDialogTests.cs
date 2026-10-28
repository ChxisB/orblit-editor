using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// Dart: orblit-editor/test/retarget_dialog_test.dart. The dialog that makes a clip for another model, with
// models and maps from a fake project. A menu is opened and clicked as a hand would, and the dialog is read
// from its words and the names of its buttons.
public sealed class RetargetDialogTests
{
    private static ClipChannel<Quaternion> Turn(string bone) => new(
        "",
        "rotation",
        ChannelKind.Rotation,
        [
            new Key<Quaternion>(0, Quaternion.Identity(), Hold.Linear),
            new Key<Quaternion>(1, Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5), Hold.Linear),
        ],
        bone: bone);

    private static readonly ClipDocument Nod = new("nod", 1, channels: [Turn("spine"), Turn("head")]);

    private sealed class FakeFiles : IRetargetFiles
    {
        public Dictionary<string, RestSkeleton?> Skeletons { get; } = new()
        {
            ["hero.glb"] = RetargetModelTests.Chain("hips", "spine", "head"),
            ["dwarf.glb"] = RetargetModelTests.Chain("hips", "spine", "neck", "tail"),
            ["plain.glb"] = null,
        };

        public Dictionary<string, BoneMapLoad> Stored { get; } = [];

        public HashSet<string> Broken { get; } = [];

        public List<(string Name, BoneMap Map)> Written { get; } = [];

        public List<string> Asked { get; } = [];

        public string? Fails { get; set; }

        public IReadOnlyList<string> Models => [.. Skeletons.Keys.Order()];

        public IReadOnlyList<string> Maps => [.. Stored.Keys.Concat(Broken).Order()];

        public RestSkeleton? SkeletonOf(string model)
        {
            Asked.Add(model);
            return Skeletons.GetValueOrDefault(model);
        }

        public BoneMapLoad ReadMap(string path) =>
            Broken.Contains(path) ? throw new BoneMapFormatException("It is not a map.") : Stored[path];

        public AssetMade WriteMap(string name, BoneMap map)
        {
            if (Fails is { } problem)
            {
                return new AssetMade(problem, null);
            }

            Written.Add((name, map));
            return new AssetMade(null, $"retargets/{name}.obonemap");
        }
    }

    private static string Text(TextBlock block) => block.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
        : block.Text ?? "";

    private static string[] Words(Control root) => [.. Headless.All<TextBlock>(root).Where(block => block.IsEffectivelyVisible).Select(Text)];

    private static Task<RetargetMade?> Open(FakeFiles files, string? from, out RetargetDialog dialog)
    {
        var owner = new Window { Width = 900, Height = 700 };
        owner.Show();
        var made = RetargetDialog.Choose(owner, Nod, files, from);
        Headless.Render();
        dialog = Assert.IsType<RetargetDialog>(Assert.Single(owner.OwnedWindows));
        return made;
    }

    private static void Click(Window dialog, string name)
    {
        Headless.Click(dialog, name);
        Headless.Render();
    }

    private static void Pick(Window dialog, string button, string text)
    {
        var pick = Headless.Find<Button>(dialog, button);
        var menu = Assert.IsType<MenuFlyout>(pick.Flyout);
        menu.ShowAt(pick);
        Headless.Render();
        menu.Items.OfType<MenuItem>().First(item => (string?)item.Header == text)
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Hide();
        Headless.Render();
    }

    // The row of a bone of the dwarf, which is how its button is named.
    private static string Pair(string bone) => $"pair_{Array.IndexOf(["hips", "spine", "neck", "tail"], bone)}";

    private static string ShownBy(Window dialog, string button) => Text(Assert.IsType<TextBlock>(Headless.Find<Button>(dialog, button).Content));

    private static BoneMapLoad Stored(BoneMap map, params string[] problems) => new(map, problems);

    [Fact]
    public Task AsksForTwoModelsBeforeAnythingElse() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), null, out var dialog);

        Assert.Contains("Choose the model the clip was made for and the one it should move.", Words(dialog));
        Assert.False(Headless.Find<Button>(dialog, "make").IsEnabled);
        Assert.False(Headless.Find<Button>(dialog, "save_map").IsEnabled);
    });

    [Fact]
    public Task StartsOnTheModelTheClipWasMadeFor() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);

        Assert.Equal("hero.glb", ShownBy(dialog, "models_from"));
        Assert.Equal("Choose a model", ShownBy(dialog, "models_to"));
        Assert.Contains("Choose the model the clip was made for and the one it should move.", Words(dialog));
        Assert.DoesNotContain("", Words(dialog));
    });

    [Fact]
    public Task ListsAllTheModelsOfTheProjectInTheMenu() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), null, out var dialog);
        var pick = Headless.Find<Button>(dialog, "models_to");
        var menu = Assert.IsType<MenuFlyout>(pick.Flyout);

        menu.ShowAt(pick);
        Headless.Render();

        Assert.Equal(["dwarf.glb", "hero.glb", "plain.glb"], menu.Items.OfType<MenuItem>().Select(item => (string?)item.Header));
        menu.Hide();
    });

    [Fact]
    public Task ListsAPairForEveryBoneOfTheTarget() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);

        Pick(dialog, "models_to", "dwarf.glb");

        Assert.Contains("2 of 4 bones are moved by something.", Words(dialog));
        Assert.Equal("Automatic: spine", ShownBy(dialog, Pair("spine")));
        Assert.Equal("Automatic: no match", ShownBy(dialog, Pair("neck")));
        Assert.Equal("Automatic: no match", ShownBy(dialog, Pair("tail")));
        Assert.DoesNotContain("Choose the model the clip was made for and the one it should move.", Words(dialog));
        Assert.DoesNotContain("", Words(dialog));
        Assert.True(Headless.Find<Button>(dialog, "make").IsEnabled);
        Assert.True(Headless.Find<Button>(dialog, "save_map").IsEnabled);
    });

    [Fact]
    public Task PairsABoneByHand() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Pick(dialog, Pair("neck"), "head");

        Assert.Contains("3 of 4 bones are moved by something.", Words(dialog));
        Assert.Equal("head", ShownBy(dialog, Pair("neck")));
    });

    [Fact]
    public Task LeavesABoneWithNothingToMoveIt() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Pick(dialog, Pair("spine"), "Nothing");

        Assert.Contains("1 of 4 bones are moved by something.", Words(dialog));
        Assert.Equal("Nothing", ShownBy(dialog, Pair("spine")));
    });

    [Fact]
    public Task GivesABoneBackToItsNameWhenAutomaticIsChosen() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");
        Pick(dialog, Pair("spine"), "Nothing");

        Pick(dialog, Pair("spine"), "Automatic");

        Assert.Contains("2 of 4 bones are moved by something.", Words(dialog));
        Assert.Equal("Automatic: spine", ShownBy(dialog, Pair("spine")));
    });

    [Fact]
    public Task SaysWhenAModelHasNoSkeleton() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);

        Pick(dialog, "models_to", "plain.glb");

        Assert.Contains("plain.glb has no skeleton.", Words(dialog));
        Assert.False(Headless.Find<Button>(dialog, "make").IsEnabled);
    });

    [Fact]
    public Task MakesTheClipForTheTargetAndNamesItForIt() => Headless.Run(() =>
    {
        var made = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");
        Pick(dialog, Pair("neck"), "head");

        Click(dialog, "make");

        Assert.True(made.IsCompleted);
        var result = made.Result!;
        Assert.Equal("dwarf.glb", result.Model);
        Assert.Equal("nod (dwarf)", result.Clip.Name);
        Assert.Equal(["neck", "spine"], result.Clip.Channels.Select(channel => channel.Bone).Distinct().Order());
    });

    [Fact]
    public Task MakesNoChannelForABoneLeftWithNothing() => Headless.Run(() =>
    {
        var made = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");
        Pick(dialog, Pair("spine"), "Nothing");

        Click(dialog, "make");

        Assert.Empty(made.Result!.Clip.Channels);
    });

    [Fact]
    public Task CancelAnswersNull() => Headless.Run(() =>
    {
        var made = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Click(dialog, "cancel");

        Assert.True(made.IsCompleted);
        Assert.Null(made.Result);
    });

    [Fact]
    public Task SavesTheMapUnderTheNamesOfBothModels() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        _ = Open(files, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");
        Pick(dialog, Pair("neck"), "head");
        Pick(dialog, Pair("tail"), "Nothing");

        Click(dialog, "save_map");

        var (name, map) = Assert.Single(files.Written);
        Assert.Equal("hero-to-dwarf", name);
        Assert.Equal(("hero.glb", "dwarf.glb"), (map.FromAsset, map.ToAsset));
        Assert.Equal("head", Assert.Single(map.Bones, bone => bone.Key == "neck").Value);
        Assert.Equal(["tail"], map.Unpaired);
        Assert.Contains("Saved retargets/hero-to-dwarf.obonemap.", Words(dialog));
    });

    [Fact]
    public Task SaysWhyAMapCouldNotBeSaved() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles { Fails = "The disk is full." }, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Click(dialog, "save_map");

        Assert.Contains("Could not save the map: The disk is full.", Words(dialog));
    });

    [Fact]
    public Task LoadsASavedMapWithItsModelsAndItsPairs() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Stored["a.obonemap"] = Stored(new BoneMap("hero.glb", "dwarf.glb", new Dictionary<string, string> { ["neck"] = "head" }, humanoid: true));
        _ = Open(files, null, out var dialog);

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Contains("Loaded a.obonemap.", Words(dialog));
        Assert.Equal("hero.glb", ShownBy(dialog, "models_from"));
        Assert.Equal("dwarf.glb", ShownBy(dialog, "models_to"));
        Assert.Contains("3 of 4 bones are moved by something.", Words(dialog));
    });

    [Fact]
    public Task NamesWhatAMapLacksAndStillMakesTheClip() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Stored["a.obonemap"] = Stored(new BoneMap("hero.glb", "dwarf.glb", new Dictionary<string, string> { ["wing"] = "head" }));
        _ = Open(files, null, out var dialog);

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Contains(Words(dialog), line => line.Contains("names 1 bones these models lack", StringComparison.Ordinal));
        Assert.True(Headless.Find<Button>(dialog, "make").IsEnabled);
    });

    [Fact]
    public Task KeepsTheCurrentModelWhenAMapNamesOneTheProjectLacks() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Stored["a.obonemap"] = Stored(new BoneMap("gone.glb", "dwarf.glb", new Dictionary<string, string> { ["neck"] = "head" }));
        _ = Open(files, "hero.glb", out var dialog);

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Equal("hero.glb", ShownBy(dialog, "models_from"));
        Assert.Equal("dwarf.glb", ShownBy(dialog, "models_to"));
        Assert.DoesNotContain(Words(dialog), line => line.Contains("gone.glb", StringComparison.Ordinal));
        Assert.Contains("3 of 4 bones are moved by something.", Words(dialog));
    });

    [Fact]
    public Task SaysAnUnreadableMapIsNotOneAndChangesNothing() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Broken.Add("junk.obonemap");
        _ = Open(files, "hero.glb", out var dialog);

        Pick(dialog, "bone_map", "junk.obonemap");

        Assert.Contains("junk.obonemap: It is not a map.", Words(dialog));
        Assert.Equal("hero.glb", ShownBy(dialog, "models_from"));
    });

    [Fact]
    public Task SaysSoWhenThereAreNoSavedMaps() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), null, out var dialog);

        Assert.Equal("None saved yet", ShownBy(dialog, "bone_map"));
    });

    [Fact]
    public Task CountsWhatAMapCouldNotReadAndShowsTheFirst() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Stored["a.obonemap"] = Stored(new BoneMap("hero.glb", "dwarf.glb"), "Bone 3 has no name.", "Bone 7 is empty.");
        _ = Open(files, null, out var dialog);

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Contains("2 things in a.obonemap could not be read. First: Bone 3 has no name.", Words(dialog));
    });

    [Fact]
    public Task TurnsPairingByBodyOn() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        _ = Open(files, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Headless.Click(dialog, "switch_humanoid");
        Headless.Render();
        Click(dialog, "save_map");

        Assert.True(Assert.Single(files.Written).Map.Humanoid);
        Assert.False(files.Written[0].Map.FixRest);
    });

    [Fact]
    public Task TitlesItForTheClip() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), null, out var dialog);

        Assert.Equal("Retarget nod", dialog.Title);
    });

    [Fact]
    public Task TurnsRestPoseFixingOnWithoutTouchingTheRest() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        _ = Open(files, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Headless.Click(dialog, "switch_fix_rest");
        Headless.Render();
        Click(dialog, "save_map");

        Assert.True(Assert.Single(files.Written).Map.FixRest);
        Assert.False(files.Written[0].Map.Humanoid);
    });

    [Fact]
    public Task NamesFourOfTheBonesAMapLacksAndSaysThereAreMore() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        var wings = Enumerable.Range(1, 5).ToDictionary(index => $"w{index}", _ => "head");
        files.Stored["a.obonemap"] = Stored(new BoneMap("hero.glb", "dwarf.glb", wings));
        _ = Open(files, null, out var dialog);

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Contains(
            "The map names 5 bones these models lack, which are left out: w1, w2, w3, w4 and more.",
            Words(dialog));
    });

    [Fact]
    public Task AsksForASkeletonOncePerModel() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        _ = Open(files, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Pick(dialog, Pair("neck"), "head");
        Pick(dialog, Pair("tail"), "Nothing");

        Assert.Single(files.Asked, "hero.glb");
        Assert.Single(files.Asked, "dwarf.glb");
    });

    [Fact]
    public Task ShowsWhatPairingByBodyCouldNotDo() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Headless.Click(dialog, "switch_humanoid");
        Headless.Render();

        Assert.Contains(Words(dialog), line => line.StartsWith("In the source skeleton: ", StringComparison.Ordinal));
    });

    [Fact]
    public Task KeepsTheCurrentTargetWhenAMapNamesOneTheProjectLacks() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Stored["a.obonemap"] = Stored(new BoneMap("hero.glb", "gone.glb"));
        _ = Open(files, "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Pick(dialog, "bone_map", "a.obonemap");

        Assert.Equal("dwarf.glb", ShownBy(dialog, "models_to"));
    });

    [Fact]
    public Task NamesTheModelItWasMadeForWhenNeitherHasASkeleton() => Headless.Run(() =>
    {
        var files = new FakeFiles();
        files.Skeletons["bare.glb"] = null;
        _ = Open(files, "plain.glb", out var dialog);

        Pick(dialog, "models_to", "bare.glb");

        Assert.Contains("plain.glb has no skeleton.", Words(dialog));
        Assert.DoesNotContain("bare.glb has no skeleton.", Words(dialog));
    });

    [Fact]
    public Task HidesThePairsAgainWhenTheTargetStopsHavingASkeleton() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Pick(dialog, "models_to", "plain.glb");

        Assert.DoesNotContain(Words(dialog), line => line.Contains("bones are moved", StringComparison.Ordinal));
        Assert.DoesNotContain("Automatic: spine", Words(dialog));
    });

    private static string?[] Headers(Window dialog, string button)
    {
        var pick = Headless.Find<Button>(dialog, button);
        var menu = Assert.IsType<MenuFlyout>(pick.Flyout);
        menu.ShowAt(pick);
        Headless.Render();
        var headers = menu.Items.OfType<MenuItem>().Select(item => (string?)item.Header).ToArray();
        menu.Hide();
        Headless.Render();
        return headers;
    }

    [Fact]
    public Task OffersAutomaticNothingAndEveryBoneOfTheSourceForABone() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        Pick(dialog, "models_to", "dwarf.glb");

        Assert.Equal(["Automatic: spine", "Nothing", "hips", "spine", "head"], Headers(dialog, Pair("spine")));
        Pick(dialog, Pair("spine"), "Nothing");
        Assert.Equal(["Automatic", "Nothing", "hips", "spine", "head"], Headers(dialog, Pair("spine")));
    });

    [Fact]
    public Task OpeningAMenuAgainDoesNotListItsChoicesTwice() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), null, out var dialog);

        _ = Headers(dialog, "models_to");

        Assert.Equal(["dwarf.glb", "hero.glb", "plain.glb"], Headers(dialog, "models_to"));
    });

    [Fact]
    public Task AChoiceIsMadeAfterTheClickHasReturned() => Headless.Run(() =>
    {
        _ = Open(new FakeFiles(), "hero.glb", out var dialog);
        var pick = Headless.Find<Button>(dialog, "models_to");
        var menu = Assert.IsType<MenuFlyout>(pick.Flyout);
        menu.ShowAt(pick);
        Headless.Render();

        menu.Items.OfType<MenuItem>().First(item => (string?)item.Header == "dwarf.glb")
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        // The menu belongs to a button that the choice replaces, so it must have finished before then.
        Assert.Equal("Choose a model", ShownBy(dialog, "models_to"));
        menu.Hide();
        Headless.Render();
        Assert.Equal("dwarf.glb", ShownBy(dialog, "models_to"));
    });
}

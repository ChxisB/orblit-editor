namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Editor.Panels;

// Where the browser is and what it says, apart from the tiles that show it.
public sealed class AssetBrowserModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_browser_model").FullName;
    private readonly AssetTree _tree;

    public AssetBrowserModelTests() => _tree = new AssetTree(_root);

    public void Dispose()
    {
        _tree.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private string At(params string[] parts) => Path.Join([_root, .. parts]);

    private void Write(params string[] parts)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(parts))!);
        File.WriteAllText(At(parts), "x");
    }

    private CookStatusIndex Cook(params (string Name, CookState State)[] states)
    {
        var index = new CookStatusIndex(_root, At("cache"));
        index.Hold(states.ToDictionary(one => At(one.Name), one => one.State));
        return index;
    }

    private static Asset Entry(string name, AssetKind kind, long? bytes = null) => new(name, name, kind, bytes);

    [Fact]
    public void ItStartsAtTheTopOfTheProjectWithNothingPicked()
    {
        var model = new AssetBrowserModel(_tree);

        Assert.Equal(_root, model.Directory);
        Assert.Equal("", model.Crumb);
        Assert.False(model.CanGoUp);
        Assert.Null(model.Selected);
    }

    [Fact]
    public void OpeningAFolderNamesItAndForgetsWhatWasPicked()
    {
        Write("models", "rock.ts");
        var model = new AssetBrowserModel(_tree);
        model.Select(At("a.ts"));

        model.Open(At("models"));

        Assert.Equal("models", model.Crumb);
        Assert.True(model.CanGoUp);
        Assert.Null(model.Selected);
    }

    [Fact]
    public void UpGoesToTheFolderAboveAndStopsAtTheTop()
    {
        Write("models", "rocks", "a.ts");
        var model = new AssetBrowserModel(_tree);
        model.Open(At("models", "rocks"));

        model.Up();
        Assert.Equal(At("models"), model.Directory);
        model.Up();
        Assert.Equal(_root, model.Directory);
        model.Up();

        Assert.Equal(_root, model.Directory);
    }

    [Fact]
    public void TheFilterSurvivesOpeningAFolder()
    {
        Write("models", "rock.ts");
        var model = new AssetBrowserModel(_tree) { Filter = "rock" };

        model.Open(At("models"));

        Assert.Equal("rock", model.Filter);
        Assert.Equal(["rock.ts"], model.Entries().Select(one => one.Name));
    }

    [Fact]
    public void TheFilterIgnoresCaseAndTheSpacesAroundIt()
    {
        Write("Rock.ts");
        Write("tree.ts");
        var model = new AssetBrowserModel(_tree) { Filter = "  rOCK " };

        Assert.True(model.IsFiltering);
        Assert.Equal(["Rock.ts"], model.Entries().Select(one => one.Name));
    }

    [Fact]
    public void SpacesAloneAreNotAFilter()
    {
        Write("a.ts");
        var model = new AssetBrowserModel(_tree) { Filter = "   " };

        Assert.False(model.IsFiltering);
        Assert.Single(model.Entries());
    }

    [Theory]
    [InlineData("a.png", true)]
    [InlineData("a.PNG", true)]
    [InlineData("a.jpg", true)]
    [InlineData("a.webp", true)]
    [InlineData("a.ktx2", false)]
    [InlineData("a.hdr", false)]
    [InlineData("a.exr", false)]
    public void OnlyWhatTheDecoderReadsIsShownAsAPicture(string name, bool shown) =>
        Assert.Equal(shown, AssetBrowserModel.ShowsPicture(Entry(name, AssetKind.Texture)));

    [Fact]
    public void AFolderOrAScriptIsNeverShownAsAPicture()
    {
        Assert.False(AssetBrowserModel.ShowsPicture(Entry("a.png", AssetKind.Folder)));
        Assert.False(AssetBrowserModel.ShowsPicture(Entry("a.png", AssetKind.Native)));
    }

    [Fact]
    public void TheCountAgreesWithTheNumber()
    {
        Assert.Equal("0 items", AssetBrowserModel.CountOf(0));
        Assert.Equal("1 item", AssetBrowserModel.CountOf(1));
        Assert.Equal("2 items", AssetBrowserModel.CountOf(2));
    }

    [Fact]
    public void TheTipNamesTheFileItsKindAndSizeAndTheCook()
    {
        var tip = AssetBrowserModel.TipOf(Entry("rock.png", AssetKind.Texture, 2048), CookState.Stale);

        Assert.StartsWith("rock.png\nTexture · ", tip, StringComparison.Ordinal);
        Assert.Contains("Needs cooking", tip, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTipHasNoSizeForAFolderAndNoCookWithoutOne()
    {
        var tip = AssetBrowserModel.TipOf(Entry("models", AssetKind.Folder), null);

        Assert.Equal("models\nFolder", tip);
    }

    [Fact]
    public void ThereIsNothingToSayWhenNothingNeedsCooking()
    {
        Write("a.gltf");

        Assert.Null(CookSummary.Of(Cook(("a.gltf", CookState.Cooked), ("notes.md", CookState.Ignored))));
    }

    [Fact]
    public void WhatWouldNotCookComesFirstAndMakesTheLineRed()
    {
        var summary = CookSummary.Of(Cook(("a.gltf", CookState.Stale), ("b.gltf", CookState.Failed), ("c.gltf", CookState.Failed), ("d.gltf", CookState.Cooked)));

        Assert.NotNull(summary);
        Assert.Equal("2 would not cook · 1 to cook", summary.Text);
        Assert.Equal(CookMark.Bad, summary.Tone);
        Assert.Contains("1 already cooked.", summary.Tip, StringComparison.Ordinal);
    }

    [Fact]
    public void WhatNeedsCookingAloneMakesTheLineAmber()
    {
        var summary = CookSummary.Of(Cook(("a.gltf", CookState.Stale)));

        Assert.NotNull(summary);
        Assert.Equal("1 to cook", summary.Text);
        Assert.Equal(CookMark.Warn, summary.Tone);
    }

    [Fact]
    public async Task ACookThatCannotBeAskedIsSaidInRedWithItsReason()
    {
        Write("assets", "ship.gltf");
        var index = new CookStatusIndex(_root, At("cache"), new CookTarget("nintendo-64"));
        await index.RefreshAsync();

        var summary = CookSummary.Of(index);

        Assert.NotNull(summary);
        Assert.Equal("Cook unknown", summary.Text);
        Assert.Equal(CookMark.Bad, summary.Tone);
        Assert.Contains("nintendo-64", summary.Tip, StringComparison.Ordinal);
    }
}

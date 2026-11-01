namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

public sealed class StoreSettingsTests : IDisposable
{
    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("orblit_store_settings_");
    private readonly string _file;

    public StoreSettingsTests() => _file = Path.Combine(_scratch.FullName, "support", "store.json");

    public void Dispose() => _scratch.Delete(recursive: true);

    private static WebUrl Url(string text) => WebUrl.Parse(text);

    [Fact]
    public void HasTheDefaultStoreAndNothingElseBeforeAnythingIsSaved()
    {
        var settings = new StoreSettings(_file);

        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress }, settings.Repositories);
        Assert.False(settings.ShowIncompatible);
    }

    [Fact]
    public void KeepsAnAddedStoreAndTheChoiceForTheNextRun()
    {
        var first = new StoreSettings(_file);
        first.AddRepository("https://kits.example/shop");
        first.SetShowIncompatible(true);

        var again = new StoreSettings(_file);

        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress, Url("https://kits.example/shop/") }, again.Repositories);
        Assert.True(again.ShowIncompatible);
    }

    [Fact]
    public void ForgetsARemovedStoreAndNeverTheDefault()
    {
        var settings = new StoreSettings(_file);
        settings.AddRepository("https://kits.example/shop/");
        settings.RemoveRepository(Url("https://kits.example/shop/"));
        settings.RemoveRepository(StoreSettings.DefaultStoreAddress);

        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress }, settings.Repositories);
        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress }, new StoreSettings(_file).Repositories);
    }

    [Fact]
    public void SaysWhyAnAddressCannotBeAdded()
    {
        var settings = new StoreSettings(_file);
        settings.AddRepository("https://kits.example/shop/");

        string[] refused =
        [
            "http://kits.example/shop/",
            "kits.example/shop",
            "https://kits.example/shop/?a=1",
            "https://kits.example/shop",
            "https://orblitengine.com/store",
            "",
        ];
        foreach (var text in refused)
        {
            Assert.NotNull(settings.RepositoryProblem(text));
            Assert.Throws<ArgumentException>(() => settings.AddRepository(text));
        }

        Assert.Null(settings.RepositoryProblem(" https://other.example/ "));
    }

    [Fact]
    public void OpensOnADamagedFileAndOnOneWithOtherThingsInIt()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        foreach (var text in new[] { "{not json", "[]", "{\"repositories\":\"x\"}" })
        {
            File.WriteAllText(_file, text);
            Assert.Equal(new[] { StoreSettings.DefaultStoreAddress }, new StoreSettings(_file).Repositories);
        }

        File.WriteAllText(_file, "{\"repositories\":[\"http://bad.example/\",3,\"https://ok.example/\"]}");

        Assert.Equal(
            new[] { StoreSettings.DefaultStoreAddress, Url("https://ok.example/") },
            new StoreSettings(_file).Repositories);
    }

    [Fact]
    public void WorksForThisRunWhenTheFileCannotBeWritten()
    {
        // A folder where the file belongs.
        Directory.CreateDirectory(_file);
        var settings = new StoreSettings(_file);

        settings.AddRepository("https://kits.example/shop/");

        Assert.Equal(2, settings.Repositories.Count);
    }

    [Fact]
    public void LeavesNoTemporaryFileBehind()
    {
        new StoreSettings(_file).AddRepository("https://kits.example/shop/");

        Assert.False(File.Exists(_file + ".tmp"));
        Assert.True(File.Exists(_file));
    }

    [Fact]
    public void ReadsAFileInTheStoredShape()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        File.WriteAllText(_file, "{\"repositories\":[\"https://kits.example/shop/\"],\"showIncompatible\":true}");

        var settings = new StoreSettings(_file);

        Assert.Equal(new[] { StoreSettings.DefaultStoreAddress, Url("https://kits.example/shop/") }, settings.Repositories);
        Assert.True(settings.ShowIncompatible);
    }

    [Fact]
    public void WritesTheStoredShape()
    {
        var settings = new StoreSettings(_file);
        settings.AddRepository("https://kits.example/shop");
        settings.SetShowIncompatible(true);

        Assert.Equal(
            "{\"repositories\":[\"https://kits.example/shop/\"],\"showIncompatible\":true}",
            File.ReadAllText(_file));
    }
}

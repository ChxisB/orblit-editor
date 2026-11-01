namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Packing a kit of a project on the disk. The clock is the test's own.
public sealed class PublishSessionTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_publish_");

    public PublishSessionTests() => KitFolder.Write(InProject("assets/kits/crates"));

    public void Dispose() => _project.Delete(recursive: true);

    [Fact]
    public async Task PacksTheKitIntoExportsKitsAndDatesTheRelease()
    {
        var made = await RunAsync("assets/kits/crates", notes: " First release. ");

        Assert.Null(made.Problem);
        Assert.Equal(PublishStage.Done, made.Stage);
        Assert.Equal(InProject("exports/kits/crates-1.0.0.opack"), made.KitFile);
        Assert.Equal(made.Published!.Summary.Bytes, new FileInfo(made.KitFile!).Length);
        var release = made.Published.Release;
        Assert.Equal(KitFolder.HostedAt, release.Url);
        Assert.Equal("2026-10-19", release.Released);
        Assert.Equal("First release.", release.Notes);
        Assert.Contains("\"version\": \"1.0.0\"", made.EntryText, StringComparison.Ordinal);
        Assert.Contains(made.Published.Summary.Hash.Hex, made.EntryText, StringComparison.Ordinal);
        // The project's own folders are left as they were.
        Assert.True(File.Exists(InProject("assets/kits/crates/kit.json")));
    }

    [Fact]
    public async Task RefusesAnAddressWithNoFileNameBeforeAnythingIsMade()
    {
        var made = await RunAsync("assets/kits/crates", url: "https://example.com/");

        Assert.Equal(PublishStage.Editing, made.Stage);
        Assert.Contains("name of the kit file", made.Problem, StringComparison.Ordinal);
        Assert.False(Directory.Exists(InProject("exports")));
    }

    [Fact]
    public async Task RefusesAFolderOutsideTheProject()
    {
        // A whole path is refused even when it leads into the project.
        foreach (var folder in new[] { "", ".", "..", "../elsewhere", "/tmp", "a/../..", InProject("assets/kits/crates") })
        {
            var made = await RunAsync(folder);
            Assert.True(made.Problem?.Contains("inside the project", StringComparison.Ordinal), folder);
        }

        var missing = await RunAsync("assets/nothing");

        Assert.Contains("no folder assets/nothing", missing.Problem, StringComparison.Ordinal);
        Assert.False(Directory.Exists(InProject("exports")));
    }

    [Fact]
    public async Task NamesTheRuleAKitBreaksAndASecondTryCanWork()
    {
        File.WriteAllText(InProject("assets/kits/crates/.DS_Store"), "x");
        using var made = Session();

        await made.StartAsync("assets/kits/crates", KitFolder.HostedAt);

        Assert.Equal(PublishStage.Editing, made.Stage);
        Assert.Contains(".DS_Store", made.Problem, StringComparison.Ordinal);

        File.Delete(InProject("assets/kits/crates/.DS_Store"));
        await made.StartAsync("assets/kits/crates", KitFolder.HostedAt);

        Assert.Null(made.Problem);
        Assert.Equal(PublishStage.Done, made.Stage);
    }

    [Fact]
    public async Task RefusesALongNoteAndSaysSo()
    {
        var made = await RunAsync("assets/kits/crates", notes: new string('x', 400));

        Assert.Equal(PublishStage.Editing, made.Stage);
        Assert.NotNull(made.Problem);
    }

    [Fact]
    public void HasNoEntryBeforeARunIsDone()
    {
        using var made = Session();

        Assert.Throws<InvalidOperationException>(() => made.EntryText);
    }

    private string InProject(string name) => Path.Combine(_project.FullName, name);

    private PublishSession Session() => new(_project.FullName, new FixedClock(new DateTime(2026, 10, 19, 23, 30, 0)));

    private async Task<PublishSession> RunAsync(string folder, string url = KitFolder.HostedAt, string notes = "")
    {
        var made = Session();
        await made.StartAsync(folder, url, notes);
        return made;
    }
}

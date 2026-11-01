using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

// The dialog on a fake cook and a fake folder, so what is checked is what it draws and what it asks for, and
// no cook starts.
public sealed class ExportDialogTests
{
    private readonly FakeExports _exports = new();
    private readonly List<string> _revealed = [];

    private static ExportedPack Pack(string name, string target = "macos", string? @base = null, int assets = 3, long bytes = 2048) => new()
    {
        Path = $"/project/exports/{name}",
        Target = target,
        Hash = $"hash-of-{name}",
        Base = @base,
        Assets = assets,
        Removed = 0,
        Bytes = bytes,
        Modified = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
    };

    private static PackSummary Summary(int assets = 3, long bytes = 4096, long shared = 0) => new()
    {
        Hash = ContentHash.Of([1, 2, 3]),
        Bytes = bytes,
        Assets = assets,
        Added = assets,
        Replaced = 0,
        Blobs = assets,
        SharedBytes = shared,
        Removed = 0,
    };

    private ExportDialog Open(int width = 1440, int height = 900)
    {
        var owner = new Window { Width = width, Height = height };
        owner.Show();
        _ = ExportDialog.Open(owner, new ExportSession("/project", _exports), _revealed.Add);
        Headless.Render();
        return Assert.IsType<ExportDialog>(Assert.Single(owner.OwnedWindows));
    }

    private static string[] Words(Control root) =>
        [.. Headless.All<TextBlock>(root).Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? "")];

    private static void Click(Window dialog, string name)
    {
        Headless.Click(dialog, name);
        Headless.Render();
    }

    private static ToggleButton Chip(Window dialog, string target) => Headless.Find<ToggleButton>(dialog, $"target_{target}");

    // Whether the control is drawn inside the dialog and not pushed out of it by what is above.
    private static bool Inside(Window dialog, string name)
    {
        var control = Headless.Find<Control>(dialog, name);
        var bottom = control.TranslatePoint(new Point(0, control.Bounds.Height), dialog);
        return bottom is { } point && point.Y <= dialog.ClientSize.Height;
    }

    [Fact]
    public Task AnEmptyFolderSaysAWholePackGoesFirst() => Headless.Run(() =>
    {
        var dialog = Open();

        Assert.Single(Words(dialog), word => word.StartsWith("Nothing exported yet", StringComparison.Ordinal));
        Assert.Equal("Export", Headless.Find<Button>(dialog, "export").Content);
    });

    [Fact]
    public Task ListsWhatTheExportsFolderHolds() => Headless.Run(() =>
    {
        _exports.Held = [Pack("game.macos.opack", bytes: 3 * 1024 * 1024), Pack("game.macos.abc123.opack", @base: "x", assets: 1)];
        var dialog = Open();

        var words = Words(dialog);
        Assert.Single(words, word => word == "game.macos.opack");
        Assert.Single(words, word => word == "game.macos.abc123.opack");
        Assert.Single(words, word => word.Contains("whole · 3 assets", StringComparison.Ordinal));
        Assert.Single(words, word => word.Contains("patch · 1 asset ·", StringComparison.Ordinal));
    });

    [Fact]
    public Task SaysAFileThatDidNotOpenAndStillListsTheRest() => Headless.Run(() =>
    {
        _exports.Held = [Pack("game.macos.opack")];
        _exports.Unreadable = ["bad.opack: not a pack"];
        var dialog = Open();

        Assert.Contains("game.macos.opack", Words(dialog));
        Assert.Contains("bad.opack: not a pack", Words(dialog));
    });

    [Fact]
    public Task ExportsTheTargetsThatAreChosen() => Headless.Run(() =>
    {
        _exports.Answer =
        [
            new TargetExport
            {
                Target = "macos",
                Outcome = ExportOutcome.Packed,
                Path = "/project/exports/game.macos.opack",
                Summary = Summary(assets: 2, bytes: 2 * 1024 * 1024, shared: 1024),
                Cooked = "2 cooked",
            },
        ];
        var dialog = Open();
        foreach (var name in CookTargets.Names)
        {
            if (Chip(dialog, name).IsChecked != (name == "macos"))
            {
                Click(dialog, $"target_{name}");
            }
        }

        Click(dialog, "export");

        var asked = Assert.Single(_exports.Requests);
        Assert.Equal(["macos"], asked.Targets);
        Assert.Null(asked.PatchOf);
        Assert.Contains("game.macos.opack · 2 assets · 2.0 MB", Words(dialog));
        Assert.Single(Words(dialog), word => word.Contains("Cook: 2 cooked.", StringComparison.Ordinal));
    });

    [Fact]
    public Task APatchLocksTheTargetAndSaysWhatItChanged() => Headless.Run(() =>
    {
        _exports.Held = [Pack("game.android.opack", target: "android")];
        _exports.Answer =
        [
            new TargetExport
            {
                Target = "android",
                Outcome = ExportOutcome.Packed,
                Path = "/project/exports/game.android.abc.opack",
                PatchOf = "game.android.opack",
                Summary = new PackSummary
                {
                    Hash = ContentHash.Of([4]),
                    Bytes = 900,
                    Assets = 3,
                    Added = 1,
                    Replaced = 2,
                    Blobs = 3,
                    SharedBytes = 0,
                    Removed = 1,
                },
                Cooked = "3 cooked",
            },
        ];
        var dialog = Open();

        RetargetDialogTests.Pick(dialog, "patch_of", "A patch of game.android.opack");

        Assert.Equal("Export patch", Headless.Find<Button>(dialog, "export").Content);
        Assert.False(Chip(dialog, "macos").IsEnabled);
        Assert.True(Chip(dialog, "android").IsChecked);

        Click(dialog, "export");

        var asked = Assert.Single(_exports.Requests);
        Assert.Equal("/project/exports/game.android.opack", asked.PatchOf);
        Assert.Equal(["android"], asked.Targets);
        Assert.Single(Words(dialog), word => word.Contains("Patch of game.android.opack: 1 new, 2 replaced, 1 removed.", StringComparison.Ordinal));
    });

    [Fact]
    public Task CannotBeClosedWhileItRuns() => Headless.Run(() =>
    {
        _exports.Hold = new TaskCompletionSource();
        _exports.Answer = [new TargetExport { Target = "macos", Outcome = ExportOutcome.Packed }];
        var dialog = Open();

        Click(dialog, "export");

        Assert.False(Headless.Find<Button>(dialog, "close").IsEnabled);
        Assert.False(Headless.Find<Button>(dialog, "export").IsEnabled);
        Assert.Single(Words(dialog), word => word.StartsWith("Cooking and packing", StringComparison.Ordinal));
        // The window's own close is refused too.
        dialog.Close();
        Headless.Render();
        Assert.True(dialog.IsVisible);

        _exports.Hold.SetResult();
        Assert.True(Headless.Until(() => Headless.Find<Button>(dialog, "close").IsEnabled));

        Click(dialog, "close");
        Assert.False(dialog.IsVisible);
    });

    [Fact]
    public Task NamesTheAssetsThatDidNotCookAndTheReason() => Headless.Run(() =>
    {
        _exports.Answer =
        [
            new TargetExport
            {
                Target = "macos",
                Outcome = ExportOutcome.CookFailed,
                Message = "Nothing was packed for macos.",
                Cooked = "1 failed",
                Failures = [new ExportFailure("props/rock.gltf", "not valid JSON")],
            },
        ];
        var dialog = Open();

        Click(dialog, "export");

        Assert.Contains("Nothing was packed for macos.", Words(dialog));
        Assert.Contains("props/rock.gltf: not valid JSON", Words(dialog));
        Assert.DoesNotContain(Headless.All<Button>(dialog), button => button.Name == "contents_0");
    });

    [Fact]
    public Task ShowsWhatAPackServesWithThePackEachAssetIsIn() => Headless.Run(() =>
    {
        _exports.Held = [Pack("game.macos.abc.opack", @base: "x")];
        _exports.Reader = (_, _) => Task.FromResult(new PackContents(
            ["game.macos.opack", "game.macos.abc.opack"],
            [
                new ContentsRow("props/rock.gltf", 2048, "aaaaaaaaaaaa", "game.macos.abc.opack"),
                new ContentsRow("props/ship.gltf", 100, "bbbbbbbbbbbb", "game.macos.opack"),
            ],
            ["old.gltf"]));
        var dialog = Open();

        Click(dialog, "pack_0");

        var words = Words(dialog);
        Assert.Single(words, word => word == "props/rock.gltf");
        Assert.Single(words, word => word == "props/ship.gltf");
        Assert.Single(words, word => word == "game.macos.opack");
        Assert.Single(words, word => word.Contains("2 assets · 2 KB · 2 packs · 1 taken away", StringComparison.Ordinal));
    });

    [Fact]
    public Task SaysWhyABrokenChainCannotBeRead() => Headless.Run(() =>
    {
        _exports.Held = [Pack("game.macos.abc.opack", @base: "x")];
        _exports.Reader = (_, _) => Task.FromException<PackContents>(new ExportChainBrokenException("no pack is that one"));
        var dialog = Open();

        Click(dialog, "pack_0");

        Assert.Contains("no pack is that one", Words(dialog));
    });

    [Fact]
    public Task ShowsTheExportsFolderOnRequest() => Headless.Run(() =>
    {
        var dialog = Open();

        Click(dialog, "reveal");

        Assert.Equal([Path.Join("/project", "exports")], _revealed);
    });

    // The dialog is a window of its own, so the check is that a long folder and three failures scroll and
    // leave its buttons in view.
    [Theory]
    [InlineData(960, 620)]
    [InlineData(800, 600)]
    public Task KeepsItsButtonsInViewOverAFullFolder(int width, int height) => Headless.Run(() =>
    {
        _exports.Held = [.. Enumerable.Range(0, 8).Select(i => Pack($"game.macos.{i}.opack"))];
        _exports.Answer =
        [
            .. new[] { "macos", "windows", "linux" }.Select(target => new TargetExport
            {
                Target = target,
                Outcome = ExportOutcome.CookFailed,
                Message = $"Nothing was packed for {target}.",
                Cooked = "1 failed",
                Failures = [new ExportFailure("a.gltf", "bad")],
            }),
        ];
        var dialog = Open(width, height);

        Click(dialog, "export");
        Assert.True(Inside(dialog, "close"));
        Click(dialog, "view_contents");

        Assert.True(Inside(dialog, "close"));
        Assert.True(Inside(dialog, "reveal"));
    });

    private sealed class FakeExports : IExportWork
    {
        public List<ExportRequest> Requests { get; } = [];

        public IReadOnlyList<ExportedPack> Held { get; set; } = [];

        public IReadOnlyList<string> Unreadable { get; set; } = [];

        public IReadOnlyList<TargetExport> Answer { get; set; } = [];

        public TaskCompletionSource? Hold { get; set; }

        public Func<string, string, Task<PackContents>> Reader { get; set; } = (_, _) => Task.FromResult(new PackContents([], [], []));

        public async Task<IReadOnlyList<TargetExport>> RunAsync(ExportRequest request, Action<TargetExport> onTarget)
        {
            Requests.Add(request);
            if (Hold is { } hold)
            {
                await hold.Task;
            }

            foreach (var done in Answer)
            {
                onTarget(done);
            }

            return Answer;
        }

        public Task<ExportListing> ListAsync(string projectDirectory) => Task.FromResult(new ExportListing(Held, Unreadable));

        public Task<PackContents> ReadAsync(string projectDirectory, string path) => Reader(projectDirectory, path);
    }
}

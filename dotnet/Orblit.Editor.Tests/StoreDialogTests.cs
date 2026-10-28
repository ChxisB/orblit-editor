using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Asset;
using Orblit.Editor.Core;

// Dart: orblit-editor/test/store_dialog_test.dart. The store and install dialogs on a fake network and a
// real temporary project.
public sealed class StoreDialogTests : IAsyncLifetime
{
    private const string Into = "assets/kits/crates";

    private readonly DirectoryInfo _scratch = Directory.CreateTempSubdirectory("orblit_store_dialog_");
    private readonly List<MapTransport> _made = [];
    private StoreFixture _fixture = null!; // set in InitializeAsync, which runs before every test
    private (KitRow Row, KitRelease Release) _listing;

    private string Project => Path.Combine(_scratch.FullName, "project");

    // A project with a file of the kit's already in it, and a store with the kit. The listing is read here
    // because a test on the UI thread cannot await work that comes back to it.
    public async ValueTask InitializeAsync()
    {
        _fixture = await StoreFixture.BuildAsync(_scratch.FullName);
        _listing = await _fixture.ListingAsync();
        Directory.CreateDirectory(InProject($"{Into}/models"));
        File.WriteAllText(InProject($"{Into}/models/crate.obj"), "my own crate");
    }

    public ValueTask DisposeAsync()
    {
        _scratch.Delete(recursive: true);
        return ValueTask.CompletedTask;
    }

    private string InProject(string name) => Path.Combine(Project, name);

    private InstallSession InstallFor(KitRow row, KitRelease release) => new(
        row: row,
        release: release,
        projectDirectory: Project,
        downloads: Path.Combine(_scratch.FullName, "downloads"),
        transport: _fixture.Transport(),
        backoff: TimeSpan.Zero);

    private StoreDialog Open(string engine = "0.35.0", int width = 1440, int height = 900)
    {
        var owner = new Window { Width = width, Height = height };
        owner.Show();
        var session = new StoreSession(
            new StoreSettings(Path.Combine(_scratch.FullName, "store.json")),
            engine,
            () =>
            {
                var transport = _fixture.Transport();
                _made.Add(transport);
                return transport;
            });
        _ = StoreDialog.Open(owner, session, InstallFor);
        Headless.Render();
        return Assert.IsType<StoreDialog>(Assert.Single(owner.OwnedWindows));
    }

    private static StoreDialog Online(StoreDialog dialog)
    {
        Headless.Click(dialog, "go_online");
        Assert.True(Headless.Until(() => Words(dialog).Contains("Crates") || Words(dialog).Any(word => word.StartsWith("No kit", StringComparison.Ordinal))));
        return dialog;
    }

    // An install dialog over a kit that is downloaded and checked, with its conflicts read.
    private InstallDialog OpenInstall()
    {
        var owner = new Window { Width = 1440, Height = 900 };
        owner.Show();
        _ = InstallDialog.Open(owner, InstallFor(_listing.Row, _listing.Release));
        var dialog = Assert.IsType<InstallDialog>(Assert.Single(owner.OwnedWindows));
        Assert.True(Headless.Until(() => Shows(dialog, "replaces your file")));
        return dialog;
    }

    private static bool?[] Ticks(Control root) => [.. Headless.All<CheckBox>(root).Select(box => box.IsChecked)];

    private static bool Installable(Window dialog) => Headless.Find<Button>(dialog, "install").IsEnabled;

    private static string[] Words(Control root) =>
        [.. Headless.All<TextBlock>(root).Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? "")];

    private static bool Shows(Control root, string part) => Words(root).Any(word => word.Contains(part, StringComparison.Ordinal));

    private static bool Inside(Window dialog, string name)
    {
        var control = Headless.Find<Control>(dialog, name);
        var bottom = control.TranslatePoint(new Point(0, control.Bounds.Height), dialog);
        return bottom is { } point && point.Y <= dialog.ClientSize.Height;
    }

    [Fact]
    public Task SendsNothingUntilThePersonGoesOnline() => Headless.Run(() =>
    {
        var dialog = Open();

        Assert.True(Shows(dialog, "Nothing has been sent"));
        Assert.True(Shows(dialog, "https://orblitengine.com/store/"));
        Assert.Empty(_made);

        Online(dialog);

        Assert.Single(_made);
        Assert.Single(Words(dialog), word => word == "Crates");
        Assert.Single(Words(dialog), word => word == "Scripts");
    });

    [Fact]
    public Task SaysThatNoKitMatchesTheWordsTyped() => Headless.Run(() =>
    {
        var dialog = Online(Open());

        Headless.Find<TextBox>(dialog, "search").Text = "nothing like it";
        Headless.Render();

        Assert.DoesNotContain("Crates", Words(dialog));
        Assert.True(Shows(dialog, "No kit matches"));
    });

    [Fact]
    public Task HidesAKitForAnotherEngineUntilAsked() => Headless.Run(() =>
    {
        var dialog = Online(Open(engine: "0.50.0"));

        Assert.DoesNotContain("Crates", Words(dialog));
        Assert.True(Shows(dialog, "other engine versions are hidden"));

        Headless.Click(dialog, "show_incompatible");

        Assert.Single(Words(dialog), word => word == "Crates");
        Assert.Single(Words(dialog), word => word == "Scripts · Other engine");
    });

    [Fact]
    public Task ShowsTheNotesTheFilesAndTheScriptsFlag() => Headless.Run(() =>
    {
        var dialog = Online(Open());

        Headless.Click(dialog, "kit_0");
        Assert.True(Headless.Until(() => Shows(dialog, "The first release.")));

        Assert.True(Shows(dialog, "Contains scripts"));
        // The kit file is not touched until the person asks for its files.
        Assert.DoesNotContain("models/crate.obj", Words(dialog));
        Assert.DoesNotContain(Assert.Single(_made).Sent, sent => sent.Url.Equals(StoreFixture.CrateUrl));

        Headless.Click(dialog, "show_files");
        Assert.True(Headless.Until(() => Words(dialog).Contains("FILES (3)")));

        Assert.Contains(_made[0].Sent, sent => sent.Url.Equals(StoreFixture.CrateUrl));
        Assert.Contains("models/crate.obj", Words(dialog));
        Headless.Find<ScrollViewer>(dialog, "detail_scroll").ScrollToEnd();
        Headless.Render();
        Assert.Contains("scripts/spin.js", Words(dialog));

        Headless.Click(dialog, "all_kits");
        // The detail shows the name too, so the check is that the release notes went away.
        Assert.True(Headless.Until(() => !Shows(dialog, "The first release.")));
        Assert.Contains("Crates", Words(dialog));
    });

    // Dart checked the dialog fit an editor window this small. Here the dialog is a window of its own, so the
    // check is that its buttons stay in view over a kit.
    [Theory]
    [InlineData(960, 620)]
    [InlineData(800, 600)]
    public Task KeepsItsButtonsInViewOverAKit(int width, int height) => Headless.Run(() =>
    {
        var dialog = Online(Open(width: width, height: height));

        Headless.Click(dialog, "kit_0");
        Assert.True(Headless.Until(() => Headless.Find<Button>(dialog, "install").IsEnabled));

        Assert.True(Inside(dialog, "install"));
        Assert.True(Inside(dialog, "close"));
    });

    [Fact]
    public Task SaysWhyAStoreCannotBeAddedAndAddsOneThatCan() => Headless.Run(() =>
    {
        var dialog = Open();
        Headless.Click(dialog, "add_store");
        var prompt = Assert.IsType<AddStorePrompt>(Assert.Single(dialog.OwnedWindows));
        var address = Headless.Find<TextBox>(prompt, "address");
        var add = Headless.Find<Button>(prompt, "add");

        address.Text = "https://orblitengine.com/store/";
        Headless.Render();

        Assert.Equal("That store is already in the list.", Headless.Find<TextBlock>(prompt, "note").Text);
        Assert.False(add.IsEnabled);

        address.Text = "https://kits.example.com/";
        Headless.Render();
        Headless.Click(prompt, "add");

        Assert.True(Headless.Until(() => Words(dialog).Contains("kits.example.com/")));
        Headless.Click(dialog, "forget_store");
        Assert.True(Headless.Until(() => Words(dialog).Contains("orblitengine.com/store/")));
        Assert.Empty(_made);
    });

    [Fact]
    public Task OpensTheInstallDialogFromAKitAndListsItsScripts() => Headless.Run(() =>
    {
        var dialog = Online(Open());
        Headless.Click(dialog, "kit_0");
        Assert.True(Headless.Until(() => Headless.Find<Button>(dialog, "install").IsEnabled));

        Headless.Click(dialog, "install");
        Assert.True(Headless.Until(() => dialog.OwnedWindows.Count == 1));
        var install = Assert.IsType<InstallDialog>(dialog.OwnedWindows[0]);
        Assert.True(Headless.Until(() => Shows(install, "This kit holds 1 script.")));

        Assert.True(Shows(install, "never runs them"));
        Assert.Equal(2, Words(install).Count(word => word == "scripts/spin.js"));
        // Nothing is written by looking.
        Assert.False(Directory.Exists(InProject($"{Into}/scripts")));
    });

    [Fact]
    public Task LeavesTheFileOfTheProjectAloneUnlessItIsTicked() => Headless.Run(() =>
    {
        var dialog = OpenInstall();
        Assert.Equal([false, true, true], Ticks(dialog));

        Headless.Click(dialog, "install");
        Assert.True(Headless.Until(() => Shows(dialog, "Installed 2 files")));

        Assert.Equal("my own crate", File.ReadAllText(InProject($"{Into}/models/crate.obj")));
        Assert.True(File.Exists(InProject($"{Into}/scripts/spin.js")));
        Assert.True(File.Exists(InProject(KitRules.LockName)));
    });

    [Fact]
    public Task ReplacesTheFileOfTheProjectWhenItIsTicked() => Headless.Run(() =>
    {
        var dialog = OpenInstall();
        Headless.Click(dialog, "tick_0");
        Headless.Render();
        Assert.Equal([true, true, true], Ticks(dialog));

        Headless.Click(dialog, "install");
        Assert.True(Headless.Until(() => Shows(dialog, "Installed 3 files")));

        Assert.Equal("a crate", File.ReadAllText(InProject($"{Into}/models/crate.obj")));
    });

    [Fact]
    public Task RefusesAFolderAKitMayNotGoInto() => Headless.Run(() =>
    {
        var dialog = OpenInstall();
        Headless.Find<TextBox>(dialog, "folder").Text = ".git/hooks";
        Headless.Render();

        Assert.True(Shows(dialog, "starts with a dot"));
        Assert.False(Installable(dialog));
        Assert.False(Directory.Exists(InProject(".git")));
    });

    [Fact]
    public Task KeepsTheChoicesWhenTheWriteFails() => Headless.Run(() =>
    {
        File.WriteAllText(InProject(KitRules.LockName), "{not json");
        var dialog = OpenInstall();
        Headless.Click(dialog, "tick_1");
        Headless.Render();
        Assert.Equal([false, false, true], Ticks(dialog));

        Headless.Click(dialog, "install");
        Assert.True(Headless.Until(() => Shows(dialog, KitRules.LockName) && Installable(dialog)));

        Assert.Equal([false, false, true], Ticks(dialog));
        Assert.False(Directory.Exists(InProject($"{Into}/scripts")));
    });

    [Fact]
    public Task ReadsANewFolderOnlyAfterAPauseInTheTyping() => Headless.Run(() =>
    {
        var dialog = OpenInstall();
        Headless.Find<TextBox>(dialog, "folder").Text = "assets/other";
        // A third of the pause, so a slow machine does not reach its end. A read that started at once
        // would have finished.
        Headless.Wait(100);
        Assert.False(Installable(dialog));

        Assert.True(Headless.Until(() => Installable(dialog)));
        Assert.False(Shows(dialog, "replaces your file"));
    });

    [Fact]
    public Task DoesNotInstallWhenEveryFileIsUnticked() => Headless.Run(() =>
    {
        var dialog = OpenInstall();
        Headless.Click(dialog, "tick_1");
        Headless.Click(dialog, "tick_2");
        Headless.Render();

        Assert.Equal([false, false, false], Ticks(dialog));
        Assert.False(Installable(dialog));
    });
}

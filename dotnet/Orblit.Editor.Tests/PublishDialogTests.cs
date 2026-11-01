using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The publish dialog over a real temporary project. The clock is the test's own.
public sealed class PublishDialogTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_publish_dialog_");
    private readonly InMemoryTextClipboard _clipboard = new();

    public PublishDialogTests() => KitFolder.Write(InProject("assets/kits/crates"));

    public void Dispose() => _project.Delete(recursive: true);

    private string InProject(string name) => Path.Combine(_project.FullName, name);

    private PublishDialog Open()
    {
        var owner = new Window { Width = 960, Height = 620 };
        owner.Show();
        var session = new PublishSession(_project.FullName, new FixedClock(new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc)));
        _ = PublishDialog.Open(owner, session, _clipboard);
        Headless.Render();
        return Assert.IsType<PublishDialog>(Assert.Single(owner.OwnedWindows));
    }

    private static void FillIn(Window dialog, string url = KitFolder.HostedAt)
    {
        Headless.Find<TextBox>(dialog, "folder").Text = "assets/kits/crates";
        Headless.Find<TextBox>(dialog, "url").Text = url;
        Headless.Find<TextBox>(dialog, "notes").Text = "First release.";
    }

    private static void Publish(Window dialog, string waitFor)
    {
        Headless.Click(dialog, "publish");
        Assert.True(Headless.Until(() => Shows(dialog, waitFor)));
    }

    private static bool Shows(Control root, string part) =>
        Headless.All<TextBlock>(root).Any(block => block.IsEffectivelyVisible && block.Text?.Contains(part, StringComparison.Ordinal) == true);

    private static bool Inside(Window dialog, string name)
    {
        var control = Headless.Find<Control>(dialog, name);
        var bottom = control.TranslatePoint(new Point(0, control.Bounds.Height), dialog);
        return bottom is { } point && point.Y <= dialog.ClientSize.Height;
    }

    [Fact]
    public Task PacksAFolderAndShowsTheEntryToCopy() => Headless.Run(() =>
    {
        var dialog = Open();
        FillIn(dialog);

        Publish(dialog, "Packed crates-1.0.0.opack");

        Assert.True(Shows(dialog, "\"version\": \"1.0.0\""));
        Assert.True(Shows(dialog, "\"released\": \"2026-10-19\""));
        Assert.True(Shows(dialog, "ENTRY FOR BRASS-LAMP/CRATES"));
        Assert.True(Shows(dialog, "Upload the file"));
        Assert.True(File.Exists(InProject("exports/kits/crates-1.0.0.opack")));
        Assert.Equal("Close", Headless.Find<Button>(dialog, "close").Content);
    });

    [Fact]
    public Task CopiesTheEntryToTheClipboard() => Headless.Run(() =>
    {
        var dialog = Open();
        FillIn(dialog);
        Publish(dialog, "Packed crates-1.0.0.opack");

        Headless.Click(dialog, "copy");

        Assert.Contains($"\"url\": \"{KitFolder.HostedAt}\"", _clipboard.Text, StringComparison.Ordinal);
        Assert.Equal("Copied", Headless.Find<Button>(dialog, "copy").Content);
    });

    [Fact]
    public Task SaysWhatIsWrongAndKeepsWhatWasTyped() => Headless.Run(() =>
    {
        var dialog = Open();
        FillIn(dialog, url: "https://example.com/");

        Publish(dialog, "name of the kit file");

        Assert.Equal("assets/kits/crates", Headless.Find<TextBox>(dialog, "folder").Text);
        Assert.Equal("First release.", Headless.Find<TextBox>(dialog, "notes").Text);
        Assert.True(Headless.Find<Button>(dialog, "publish").IsEnabled);
        Assert.False(Directory.Exists(InProject("exports")));
    });

    // The dialog is a window of its own, so the check is that its buttons stay in view through a problem and
    // then a pack.
    [Fact]
    public Task KeepsItsButtonsInViewThroughAProblemAndAPack() => Headless.Run(() =>
    {
        var dialog = Open();
        FillIn(dialog, url: "https://example.com/");
        Publish(dialog, "name of the kit file");
        Assert.True(Inside(dialog, "publish"));
        Assert.True(Inside(dialog, "close"));

        Headless.Find<TextBox>(dialog, "url").Text = KitFolder.HostedAt;
        Publish(dialog, "Packed crates-1.0.0.opack");

        Assert.True(Inside(dialog, "close"));
    });

    // The dialog has room for a good many problems, so it takes forty, a line each, to be taller than the
    // dialog.
    [Fact]
    public Task KeepsTheButtonInViewWhenAKitHasManyProblems() => Headless.Run(() =>
    {
        for (var i = 0; i < 40; i++)
        {
            Directory.CreateDirectory(InProject($"assets/kits/crates/d{i}"));
            File.WriteAllText(InProject($"assets/kits/crates/d{i}/.DS_Store"), "x");
        }

        var dialog = Open();
        FillIn(dialog);

        Publish(dialog, ".DS_Store");

        Assert.True(Inside(dialog, "publish"));
    });
}

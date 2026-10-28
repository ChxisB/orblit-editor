using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Added in C#. The Dart console panel has no tests of its own beyond the shell's console group, which
// is EditorWindowConsoleTests.
public sealed class ConsolePanelTests
{
    private readonly EditorLog _log = new(clock: new FixedClock(new DateTime(2026, 10, 28, 14, 5, 9)));
    private readonly InMemoryTextClipboard _clipboard = new();

    private Window Show()
    {
        var window = new Window { Width = 800, Height = 400, Content = new ConsolePanel(_log, _clipboard) };
        window.Show();
        Headless.Render();
        return window;
    }

    private static void Press(Window window, string name)
    {
        Headless.Click(window, name);
        Headless.Render();
    }

    private static List<string> Words(Control root) => Headless.All<TextBlock>(root).Select(block => block.Text ?? "").ToList();

    [Fact]
    public Task EachFilterCountsItsLevel() => Headless.Run(() =>
    {
        _log.Say("One", SayLevel.Info);
        _log.Say("Two", SayLevel.Error);
        _log.Say("Three", SayLevel.Error);

        var window = Show();

        var words = Words(window);
        Assert.Contains("Info 1", words);
        Assert.Contains("Warnings 0", words);
        Assert.Contains("Errors 2", words);
    });

    [Fact]
    public Task TheNewestIsAtTheTop() => Headless.Run(() =>
    {
        _log.Say("First", SayLevel.Info);
        _log.Say("Second", SayLevel.Info);

        var window = Show();

        Assert.Contains("Second", Words(Headless.Find<Control>(window, "console_row_0")));
    });

    [Fact]
    public Task ARepeatShowsHowManyTimesAndWhen() => Headless.Run(() =>
    {
        _log.Say("A mesh is missing", SayLevel.Warning);
        _log.Say("A mesh is missing", SayLevel.Warning);

        var words = Words(Show());

        Assert.Contains("×2", words);
        Assert.Contains("14:05:09", words);
    });

    [Fact]
    public Task ClickingAnEntryWithDetailOpensIt() => Headless.Run(() =>
    {
        _log.Say("It did not build", SayLevel.Error, "line 4: expected ;");
        var window = Show();
        Assert.DoesNotContain("line 4: expected ;", Words(window));

        Press(window, "console_row_0");
        Assert.Contains("line 4: expected ;", Words(window));

        Press(window, "console_row_0");
        Assert.DoesNotContain("line 4: expected ;", Words(window));
    });

    [Fact]
    public Task AnOpenEntryStaysOpenWhenANewOneArrives() => Headless.Run(() =>
    {
        _log.Say("It did not build", SayLevel.Error, "line 4: expected ;");
        var window = Show();
        Press(window, "console_row_0");

        _log.Say("Saved", SayLevel.Info, "scenes/main.oscene");
        Headless.Render();

        // The entry is what is open, not the top row it was on.
        var words = Words(window);
        Assert.Contains("line 4: expected ;", words);
        Assert.DoesNotContain("scenes/main.oscene", words);
    });

    [Fact]
    public Task CopyTakesWhatIsShownOldestFirst() => Headless.Run(() =>
    {
        _log.Say("Saved", SayLevel.Info);
        _log.Say("A mesh is missing", SayLevel.Warning);
        _log.Say("It did not build", SayLevel.Error, "line 4: expected ;");
        var window = Show();
        Press(window, "console_filter_warning");

        Press(window, "console_copy");

        Assert.Equal("[info] Saved\n[error] It did not build\nline 4: expected ;", _clipboard.Text);
    });

    [Fact]
    public Task ItLetsTheLogGoWhenItCloses() => Headless.Run(() =>
    {
        var window = Show();
        window.Close();

        _log.Say("After", SayLevel.Info);

        Assert.DoesNotContain("After", Words(window));
    });
}

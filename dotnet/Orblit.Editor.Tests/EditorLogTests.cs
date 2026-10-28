using Avalonia.Controls;
using Avalonia.Threading;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Dart: orblit-editor/test/console_test.dart. The level is the session's SayLevel, and an entry has
// no source, because nothing in the Dart editor reads one. "What Flutter throws" is what the UI thread
// throws here. A closed log dropping what it is told is not ported: the Dart guard stops a disposed
// ChangeNotifier throwing, and a C# event has nothing to throw.
public sealed class EditorLogTests
{
    private readonly EditorLog _log = new(limit: 5);

    [Fact]
    public void AnEntryKeepsItsLevelAndItsDetail()
    {
        _log.Say("It did not build", SayLevel.Error, "line 4: expected ;");

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Equal("It did not build", entry.Message);
        Assert.Contains("line 4", entry.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSameThingSaidTwiceIsOneEntryThatSaysTwice()
    {
        _log.Say("A mesh is missing", SayLevel.Warning);
        _log.Say("A mesh is missing", SayLevel.Warning);
        _log.Say("A mesh is missing", SayLevel.Warning);

        Assert.Equal(3, Assert.Single(_log.Entries).Repeats);
    }

    [Fact]
    public void SomethingElseInBetweenBreaksTheRun()
    {
        _log.Say("A mesh is missing", SayLevel.Warning);
        _log.Say("Saved", SayLevel.Info);
        _log.Say("A mesh is missing", SayLevel.Warning);

        Assert.Equal(3, _log.Entries.Count);
    }

    [Fact]
    public void AMessageThatArrivesEveryFrameDoesNotFillIt()
    {
        for (var i = 0; i < 400; i++)
        {
            _log.Say("crate.glb is not there", SayLevel.Warning);
        }

        Assert.Equal(400, Assert.Single(_log.Entries).Repeats);
    }

    [Fact]
    public void OldEntriesFallOffRatherThanGrowingWithoutBound()
    {
        for (var i = 0; i < 12; i++)
        {
            _log.Say($"Step {i}", SayLevel.Info);
        }

        Assert.Equal(5, _log.Entries.Count);
        Assert.Equal("Step 7", _log.Entries[0].Message);
        Assert.Equal("Step 11", _log.Entries[^1].Message);
    }

    [Fact]
    public void ItCountsEachLevelForTheBadgeOnTheTab()
    {
        _log.Say("One", SayLevel.Info);
        _log.Say("Two", SayLevel.Warning);
        _log.Say("Three", SayLevel.Error);
        _log.Say("Four", SayLevel.Error);

        Assert.Equal(1, _log.CountOf(SayLevel.Info));
        Assert.Equal(1, _log.CountOf(SayLevel.Warning));
        Assert.Equal(2, _log.CountOf(SayLevel.Error));
        Assert.True(_log.HasErrors);
    }

    [Fact]
    public void ClearingEmptiesIt()
    {
        _log.Say("Something", SayLevel.Error);

        _log.Clear();

        Assert.Empty(_log.Entries);
        Assert.False(_log.HasErrors);
    }

    [Fact]
    public void AnythingListeningHearsAboutANewEntry()
    {
        var told = 0;
        _log.Changed += () => told++;

        _log.Say("One", SayLevel.Info);
        _log.Say("One", SayLevel.Info);

        // Twice: the second folded into the first, and something showing the count still has to redraw.
        Assert.Equal(2, told);
    }

    [Fact]
    public void AnEntryIsStampedByTheClock()
    {
        var clock = new FixedClock(new DateTime(2026, 10, 28, 14, 5, 9));
        var log = new EditorLog(clock: clock);

        log.Say("Saved", SayLevel.Info);

        Assert.Equal(new DateTime(2026, 10, 28, 14, 5, 9), Assert.Single(log.Entries).At.DateTime);
    }

    [Fact]
    public void WhatTheSessionSaysIsKept()
    {
        var project = Directory.CreateTempSubdirectory("orblit_log");
        try
        {
            var workspace = new Workspace(project.FullName);
            var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());

            session.Report(["crate.glb is not there"]);

            var entry = Assert.Single(session.Log.Entries);
            Assert.Equal(SayLevel.Error, entry.Level);
            Assert.Equal("crate.glb is not there", entry.Message);
        }
        finally
        {
            project.Delete(recursive: true);
        }
    }

    private static void Throw(string what)
    {
        Dispatcher.UIThread.Post(() => throw new InvalidOperationException(what));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task WhatTheUiThreadThrowsLandsInTheConsole() => Headless.Run(() =>
    {
        var window = new Window();
        window.Show();
        UiErrors.ToLog(_log, window);

        Throw("a control went wrong");
        window.Close();

        var entry = Assert.Single(_log.Entries);
        Assert.Equal(SayLevel.Error, entry.Level);
        Assert.Contains("a control went wrong", entry.Detail, StringComparison.Ordinal);
    });

    [Fact]
    public Task ClosingTheWindowGivesTheHandlerBack() => Headless.Run(() =>
    {
        // What happened in Dart: the handler outlived the log it wrote into, and a closed editor kept
        // reporting. Here the window it was caught for lets it go.
        var window = new Window();
        window.Show();
        UiErrors.ToLog(_log, window);
        window.Close();

        static void Quiet(object? sender, DispatcherUnhandledExceptionEventArgs thrown) => thrown.Handled = true;
        Dispatcher.UIThread.UnhandledException += Quiet;
        try
        {
            Throw("after");
        }
        finally
        {
            Dispatcher.UIThread.UnhandledException -= Quiet;
        }

        Assert.Empty(_log.Entries);
    });
}

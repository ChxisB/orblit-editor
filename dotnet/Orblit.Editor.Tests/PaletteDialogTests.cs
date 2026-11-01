using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class PaletteDialogTests
{
    private static PaletteEntry Command(string label) => new()
    {
        Group = PaletteGroup.Commands,
        Label = label,
        Icon = "bolt",
        OnRun = () => { },
    };

    private static PaletteEntry Object(string label) => new()
    {
        Group = PaletteGroup.Objects,
        Label = label,
        Icon = "circle",
        OnRun = () => { },
    };

    private static IEnumerable<T> All<T>(Control root) => root.GetLogicalDescendants().OfType<T>();

    private static T Find<T>(Control root, string name)
        where T : Control => All<T>(root).First(one => one.Name == name);

    private static string Text(TextBlock block) => block.Inlines is { Count: > 0 } inlines
        ? string.Concat(inlines.OfType<Run>().Select(run => run.Text))
        : block.Text ?? "";

    private static string[] Words(Control root) => [.. All<TextBlock>(root).Select(Text)];

    private static (Task<PaletteEntry?> Chosen, PaletteDialog Dialog, Window Owner) Open(IReadOnlyList<PaletteEntry> entries, string query = "")
    {
        var owner = new Window { Width = 900, Height = 700 };
        owner.Show();
        var chosen = PaletteDialog.Choose(owner, entries, query);
        Headless.Render();
        return (chosen, Assert.IsType<PaletteDialog>(Assert.Single(owner.OwnedWindows)), owner);
    }

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Headless.Render();
    }

    private static void Type(PaletteDialog dialog, string text)
    {
        Find<TextBox>(dialog, "field").Text = text;
        Headless.Render();
    }

    [Fact]
    public Task OpensWithItsFieldReadyAndTheFirstThingChosen() => Headless.Run(() =>
    {
        var (chosen, dialog, owner) = Open([Command("Save scene"), Object("Cube")]);

        Assert.Contains("Save scene", Words(dialog));
        Assert.Contains("Cube", Words(dialog));
        Assert.Contains("2 results", Words(dialog));
        Assert.True(Find<TextBox>(dialog, "field").IsFocused);

        Press(dialog, PhysicalKey.Enter);

        Assert.True(chosen.IsCompleted);
        Assert.Equal("Save scene", chosen.Result?.Label);
        owner.Close();
    });

    [Fact]
    public Task TypingNarrowsTheList() => Headless.Run(() =>
    {
        var (_, dialog, owner) = Open([Command("Save scene"), Object("Cube")]);

        Type(dialog, "cub");

        Assert.DoesNotContain("Save scene", Words(dialog));
        Assert.Contains("1 result", Words(dialog));
        owner.Close();
    });

    [Fact]
    public Task SaysSoWhenNothingMatchesAndEnterDoesNothing() => Headless.Run(() =>
    {
        var (chosen, dialog, owner) = Open([Command("Save scene")]);

        Type(dialog, "zzz");
        Press(dialog, PhysicalKey.Enter);

        Assert.Contains("Nothing matches.", Words(dialog));
        Assert.False(chosen.IsCompleted);
        dialog.Close(null);
        owner.Close();
    });

    [Fact]
    public Task TheArrowsMoveTheChoiceAndWrapRound() => Headless.Run(() =>
    {
        var (chosen, dialog, owner) = Open([Command("One"), Command("Two"), Command("Three")]);

        Press(dialog, PhysicalKey.ArrowDown);
        Press(dialog, PhysicalKey.ArrowDown);
        Press(dialog, PhysicalKey.ArrowDown);
        Press(dialog, PhysicalKey.ArrowUp);
        Press(dialog, PhysicalKey.Enter);

        // Down three times is back at the top, and up from there is the end.
        Assert.Equal("Three", chosen.Result?.Label);
        owner.Close();
    });

    [Fact]
    public Task EscapeClosesItWithoutChoosing() => Headless.Run(() =>
    {
        var (chosen, dialog, owner) = Open([Command("Save scene")]);

        Press(dialog, PhysicalKey.Escape);

        Assert.True(chosen.IsCompleted);
        Assert.Null(chosen.Result);
        Assert.Empty(owner.OwnedWindows);
        owner.Close();
    });

    [Fact]
    public Task ATapOnARowChoosesIt() => Headless.Run(() =>
    {
        var (chosen, dialog, owner) = Open([Command("Save scene"), Command("Add cube")]);
        var row = Find<Border>(dialog, "row_1");
        var at = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), dialog)!.Value;

        dialog.MouseDown(at, MouseButton.Left);
        dialog.MouseUp(at, MouseButton.Left);

        Assert.Equal("Add cube", chosen.Result?.Label);
        owner.Close();
    });

    [Fact]
    public Task StartsOnTheQueryItWasGiven() => Headless.Run(() =>
    {
        var (_, dialog, owner) = Open([Command("Save scene"), Command("Add cube")], query: "add");

        Assert.DoesNotContain("Save scene", Words(dialog));
        Assert.Contains("Add cube", Words(dialog));
        dialog.Close(null);
        owner.Close();
    });

    [Fact]
    public Task TheMatchedPartOfALabelIsLitAndTheRestIsNot() => Headless.Run(() =>
    {
        var (_, dialog, owner) = Open([Command("Add cube")], query: "cube");

        var label = Assert.IsType<TextBlock>(Find<Border>(dialog, "row_0").GetLogicalDescendants().OfType<TextBlock>().First());
        var runs = label.Inlines!.OfType<Run>().ToArray();

        Assert.Equal(["Add ", "cube", ""], runs.Select(run => run.Text));
        Assert.NotEqual(runs[0].Foreground, runs[1].Foreground);
        dialog.Close(null);
        owner.Close();
    });

    [Fact]
    public Task AGroupGetsOneHeadingAboveItsRows() => Headless.Run(() =>
    {
        var (_, dialog, owner) = Open([Command("One"), Command("Two"), Object("Cube")]);

        var words = Words(dialog);

        Assert.Single(words, "Commands");
        Assert.Single(words, "Objects");
        dialog.Close(null);
        owner.Close();
    });

    [Fact]
    public Task TheKeysAndTheMetaOfAnEntryAreShownBesideItsLabel() => Headless.Run(() =>
    {
        var (_, dialog, owner) = Open([Command("Undo") with { Keys = "⌘Z", Meta = "Add Cube" }]);

        var words = Words(dialog);

        Assert.Contains("⌘Z", words);
        Assert.Contains("Add Cube", words);
        dialog.Close(null);
        owner.Close();
    });

    [Fact]
    public Task TheChosenRowIsScrolledIntoViewWhenTheArrowsRunPastTheList() => Headless.Run(() =>
    {
        // A group shows at most twelve rows once something is typed, so two groups give 24 rows to run down.
        var entries = Enumerable.Range(0, 12).SelectMany(i => new[] { Command($"Command {i}"), Object($"Object {i}") });
        var (_, dialog, owner) = Open([.. entries], "o");
        var scroll = All<ScrollViewer>(dialog).First();

        for (var i = 0; i < 20; i++)
        {
            Press(dialog, PhysicalKey.ArrowDown);
        }

        Headless.Render();
        var row = Find<Border>(dialog, "row_20");
        var top = row.TranslatePoint(new Point(0, 0), scroll)!.Value.Y;
        Assert.InRange(top, 0, scroll.Viewport.Height - row.Bounds.Height);
        dialog.Close(null);
        owner.Close();
    });
}

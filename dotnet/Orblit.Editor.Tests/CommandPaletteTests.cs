namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class CommandPaletteTests
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

    private static string[] Labels(IEnumerable<PaletteMatch> found) => found.Select(match => match.Entry.Label).ToArray();

    private static readonly PaletteEntry[] Entries =
    [
        Command("Save scene"),
        Command("Add cube"),
        Command("Add light"),
        Command("Select all"),
        Object("Cube"),
        Object("Ground"),
    ];

    [Fact]
    public void WithNothingTypedListsEverythingAGroupAtATime()
    {
        var found = CommandPalette.Search(Entries, "");

        Assert.Equal(new[] { "Save scene", "Add cube", "Add light", "Select all", "Cube", "Ground" }, Labels(found));

        // There is nothing to light up.
        Assert.Equal(found[0].HitStart, found[0].HitEnd);
    }

    [Fact]
    public void KeepsOnlyLabelsHoldingEveryWordInAnyOrder()
    {
        Assert.Equal(new[] { "Add cube" }, Labels(CommandPalette.Search(Entries, "cube add")));
        Assert.Empty(CommandPalette.Search(Entries, "zzz"));
    }

    [Fact]
    public void IgnoresCaseAndTheSpaceAroundTheWords() =>
        Assert.Equal(new[] { "Save scene" }, Labels(CommandPalette.Search(Entries, "  SAVE   scene ")));

    [Fact]
    public void PutsTheEarliestHitFirstAndKeepsTheOrderOfATie()
    {
        // "Cube" the object hits at 0 but is in its own group, which follows the commands. Within the
        // commands, nothing else begins with it.
        Assert.Equal(new[] { "Add cube", "Cube" }, Labels(CommandPalette.Search(Entries, "cube")));

        PaletteEntry[] tied = [Command("Edit all"), Command("Select all"), Command("All")];
        Assert.Equal(new[] { "All", "Edit all", "Select all" }, Labels(CommandPalette.Search(tied, "all")));
    }

    [Fact]
    public void SaysWhichPartOfTheLabelMatched()
    {
        var match = Assert.Single(CommandPalette.Search(Entries, "light"));

        Assert.Equal("light", match.Entry.Label[match.HitStart..match.HitEnd]);
    }

    [Fact]
    public void ShowsFewerOfAGroupBeforeAnythingIsTyped()
    {
        var many = Enumerable.Range(0, 20).Select(at => Command($"Command {at}"))
            .Concat(Enumerable.Range(0, 20).Select(at => Object($"Object {at}")))
            .ToList();

        Assert.Equal(12, CommandPalette.Search(many, "").Count);
        Assert.Equal(12, CommandPalette.Search(many, "command").Count);
        Assert.Equal(11, CommandPalette.Search(many, "object 1").Count);
    }

    [Fact]
    public void KeepsAGroupsTitleAndOrderForTheShell()
    {
        Assert.Equal(new[] { "Commands", "Objects" }, Enum.GetValues<PaletteGroup>().Select(group => group.Title()));
    }

    [Fact]
    public void HandsBackTheEntryItselfSoTheCallerRunsItOnceThePaletteIsGone()
    {
        var ran = 0;
        var entry = Command("Save scene") with { OnRun = () => ran++ };

        var chosen = Assert.Single(CommandPalette.Search([entry], "save")).Entry;

        Assert.Equal(0, ran);
        chosen.OnRun();
        Assert.Equal(1, ran);
    }
}

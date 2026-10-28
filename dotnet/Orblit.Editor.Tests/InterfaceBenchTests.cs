namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Ui;

// The Interface workspace's model: an interface opened, laid out through the undo stack, and written
// back, with the language files the canvas speaks.
public sealed class InterfaceBenchTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_ui_words");

    private string StringsPath => Path.Combine(_root.FullName, "strings");

    public void Dispose() => _root.Delete(recursive: true);

    private static UiNode Keyed(string type, string key) =>
        new() { Type = type, Props = new Dictionary<string, object?> { ["textKey"] = key } };

    private static UiDocument Menu() => new()
    {
        Name = "Menu",
        Root = new UiNode
        {
            Type = "column",
            Children = [Keyed("text", "greet"), Keyed("button", "play"), Keyed("button", "quit"), new UiNode { Type = "button", Text = "Fixed" }],
        },
    };

    private InterfaceBench Bench() => new(new History(new Workspace(_root.FullName)), StringsPath);

    private void Write(string name, string text)
    {
        Directory.CreateDirectory(StringsPath);
        File.WriteAllText(Path.Combine(StringsPath, name), text);
    }

    private (InterfaceBench Bench, string Path) Opened(UiDocument document)
    {
        var path = Path.Combine(_root.FullName, "menu.oui");
        File.WriteAllText(path, document.ToText());
        var bench = Bench();
        Assert.Null(bench.OpenFile(path));
        return (bench, path);
    }

    private static UiNode Root(InterfaceBench bench)
    {
        Assert.NotNull(bench.Document);
        return bench.Document.Root;
    }

    [Fact]
    public void OneThatCannotBeReadIsReportedAndTheRestStillLoad()
    {
        Write("fr.json", """{"play": "Jouer"}""");
        Write("de.json", "[]");
        Write("notes.txt", "not a language");
        var loaded = Bench();
        loaded.LoadStrings();

        Assert.Equal(["fr"], loaded.Strings.Locales);
        Assert.StartsWith("de.json", Assert.Single(loaded.StringProblems), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadingAgainForgetsAKeyThatWasTakenOut()
    {
        Write("fr.json", """{"play": "Jouer", "quit": "Quitter"}""");
        var loaded = Bench();
        loaded.LoadStrings();
        loaded.Strings.Use("fr");
        Assert.Equal("Quitter", loaded.Strings.Text("quit"));

        Write("fr.json", """{"play": "Jouer"}""");
        loaded.LoadStrings();

        Assert.Equal("quit", loaded.Strings.Text("quit"));
        Assert.Equal("fr", loaded.Strings.Locale);
    }

    [Fact]
    public void TheOneTableIsKeptSoAViewThatHoldsItStaysLive()
    {
        Write("fr.json", """{"play": "Jouer"}""");
        var loaded = Bench();
        loaded.LoadStrings();
        var held = loaded.Strings;
        var heard = 0;
        held.Changed += () => heard++;

        loaded.LoadStrings();
        loaded.Strings.Use("fr");

        Assert.Same(held, loaded.Strings);
        Assert.True(heard > 0);
    }

    [Fact]
    public void OpensInALanguageTheProjectHasWhenItHasNoEn()
    {
        Write("fr.json", """{"play": "Jouer"}""");
        Write("de.json", """{"play": "Spielen"}""");
        var loaded = Bench();
        loaded.LoadStrings();

        Assert.Equal("de", loaded.Strings.Locale);
        Assert.Equal("Spielen", loaded.Strings.Text("play"));
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public void AFolderThatCannotBeListedIsReportedNotThrown()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows() || Environment.UserName == "root", "A folder cannot be locked from this user.");
        Write("fr.json", """{"play": "Jouer"}""");
        File.SetUnixFileMode(StringsPath, UnixFileMode.None);
        try
        {
            var loaded = Bench();
            loaded.LoadStrings();

            Assert.Empty(loaded.Strings.Locales);
            Assert.StartsWith("strings/", Assert.Single(loaded.StringProblems), StringComparison.Ordinal);
        }
        finally
        {
            File.SetUnixFileMode(StringsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public void AProjectWithNoStringsFolderHasNoLanguages()
    {
        var loaded = Bench();
        loaded.LoadStrings();

        Assert.Empty(loaded.Strings.Locales);
        Assert.Empty(loaded.StringProblems);
    }

    [Fact]
    public void MissingCountsTheKeysTheShownInterfaceAsksFor()
    {
        Write("en.json", """{"greet": "Hello", "play": "Play", "quit": "Quit"}""");
        Write("fr.json", """{"greet": "Bonjour", "play": "Jouer"}""");
        var (bench, _) = Opened(Menu());

        Assert.Equal(["quit"], bench.Missing["fr"]);
        Assert.False(bench.Missing.ContainsKey("en"));
    }

    [Fact]
    public void AddingPutsAnElementInsideTheSelectionAndSelectsIt()
    {
        var (bench, _) = Opened(Menu());
        bench.Selected = [];

        bench.Add(UiElement.Text);

        Assert.Equal(5, Root(bench).Children.Count);
        Assert.Equal([4], bench.Selected);
        Assert.Equal("Text", Assert.IsType<UiNode>(bench.Element).Text);
        Assert.Equal(["Add Text"], bench.History.Labels);
    }

    [Fact]
    public void AddingWithAButtonSelectedPutsTheElementAfterIt()
    {
        var (bench, _) = Opened(Menu());
        bench.Selected = [];
        bench.Add(UiElement.Button);

        bench.Add(UiElement.Text);

        // Nothing goes inside a button, and an add selects what it made, so a second add has to go beside it.
        Assert.Equal(6, Root(bench).Children.Count);
        Assert.Empty(Root(bench).Children[4].Children);
        Assert.Equal([5], bench.Selected);
        Assert.Equal("Text", Assert.IsType<UiNode>(bench.Element).Text);
    }

    [Fact]
    public void UndoTakesTheAddBackAndKeepsTheSelectionOnSomethingThere()
    {
        var (bench, _) = Opened(Menu());
        bench.Add(UiElement.Box);

        bench.History.Undo();

        Assert.Equal(4, Root(bench).Children.Count);
        Assert.Equal<int>([], bench.Selected);
    }

    [Fact]
    public void AnElementAddedToAStackIsPlacedAwayFromTheLastOne()
    {
        var (bench, _) = Opened(new UiDocument { Root = new UiNode { Type = "stack" } });

        bench.Add(UiElement.Box);
        bench.Selected = [];
        bench.Add(UiElement.Box);

        Assert.Equal((48.0, 48.0), Root(bench).Children[0].Placed);
        Assert.Equal((72.0, 72.0), Root(bench).Children[1].Placed);
    }

    [Fact]
    public void AddingAColumnPutsTheGridUpAndAButtonLeavesItAlone()
    {
        var (bench, _) = Opened(Menu());

        bench.Add(UiElement.Button);
        Assert.False(bench.Columns);

        bench.Selected = [];
        bench.Add(UiElement.Row);
        Assert.True(bench.Columns);
    }

    [Fact]
    public void SplitMakesEqualColumnsWithTheContentInTheFirstWithoutItsPlace()
    {
        var placed = new UiNode { Type = "box" }.PlaceAt(10, 20);
        var (bench, _) = Opened(new UiDocument
        {
            Root = new UiNode { Type = "stack", Classes = "stack md:row p-4", Children = [placed] },
        });

        bench.Split(3);

        var root = Root(bench);
        Assert.Equal("row", root.Type);
        Assert.Equal("p-4 gap-4 col md:row", root.Classes);
        Assert.Equal(3, root.Children.Count);
        Assert.All(root.Children, column => Assert.Equal("md:flex-1 gap-2", column.Classes));
        Assert.Null(Assert.Single(root.Children[0].Children).Placed);
        Assert.Empty(root.Children[2].Children);
        Assert.True(bench.Columns);
        Assert.Equal(["Split stack into 3 columns"], bench.History.Labels);

        bench.History.Undo();
        Assert.Equal("stack", Root(bench).Type);
    }

    [Fact]
    public void SplittingAPlacedElementGivesItAWidthAndLeavesOneThatHasOne()
    {
        var (bench, _) = Opened(new UiDocument
        {
            Root = new UiNode
            {
                Type = "stack",
                Children = [new UiNode { Type = "box" }.PlaceAt(10, 20), new UiNode { Type = "box", Classes = "w-32" }.PlaceAt(0, 0)],
            },
        });

        bench.Selected = [0];
        bench.Split(2);
        bench.Selected = [1];
        bench.Split(2);

        Assert.EndsWith("right: 0", Root(bench).Children[0].Css, StringComparison.Ordinal);
        Assert.DoesNotContain("right", Root(bench).Children[1].Css, StringComparison.Ordinal);
    }

    [Fact]
    public void AWholeDragIsOneStepAndLandsOnAWholePixel()
    {
        var (bench, _) = Opened(new UiDocument
        {
            Root = new UiNode { Type = "stack", Children = [new UiNode { Type = "box" }.PlaceAt(10, 10)] },
        });

        bench.Move([0], (12.4, 10.2));
        bench.Move([0], (30.6, 41.3));
        Assert.Equal((30.6, 41.3), Root(bench).Children[0].Placed);
        Assert.True(bench.History.LastOnlyMoved);

        bench.MoveDone();
        Assert.Equal((31.0, 41.0), Root(bench).Children[0].Placed);
        Assert.Equal(["Move box"], bench.History.Labels);

        bench.Move([0], (50, 50));
        bench.MoveDone();
        Assert.Equal(["Move box", "Move box"], bench.History.Labels);

        bench.History.Undo();
        bench.History.Undo();
        Assert.Equal((10.0, 10.0), Root(bench).Children[0].Placed);
    }

    [Fact]
    public void RemovingSelectsTheParentAndTheRootCannotBeRemoved()
    {
        var (bench, _) = Opened(Menu());

        bench.Remove([]);
        Assert.False(bench.History.CanUndo);

        bench.Selected = [1];
        bench.RemoveSelected();
        Assert.Equal(3, Root(bench).Children.Count);
        Assert.Equal<int>([], bench.Selected);
        Assert.Equal(["Remove play"], bench.History.Labels);
    }

    [Fact]
    public void TypingIntoOneElementIsOneStepAndAnotherElementIsAnother()
    {
        var (bench, _) = Opened(Menu());
        var typing = new object();

        bench.Selected = [3];
        bench.EditElement("Words", node => node with { Text = "S" }, typing);
        bench.EditElement("Words", node => node with { Text = "St" }, typing);
        bench.Selected = [2];
        bench.EditElement("Words", node => node with { Text = "Q" }, typing);

        Assert.Equal(["Words on Fixed", "Words on quit"], bench.History.Labels);
        Assert.Equal("St", Root(bench).Children[3].Text);
    }

    [Fact]
    public void SavingWritesAFileThatOpensAgain()
    {
        var (bench, path) = Opened(Menu());
        bench.Add(UiElement.Image);
        Assert.True(bench.AnyUnsaved);

        Assert.Empty(bench.SaveAll());

        Assert.False(bench.AnyUnsaved);
        var again = UiDocument.Read(File.ReadAllText(path));
        Assert.NotNull(again);
        Assert.Equal("image", again.Root.Children[^1].Type);
    }

    [Fact]
    public void AFileThatIsGoneOrIsNotAnInterfaceIsSaidNotThrown()
    {
        var bench = Bench();
        var notOne = Path.Combine(_root.FullName, "notes.oui");
        File.WriteAllText(notOne, "{}");

        Assert.Equal("gone.oui is not in the project any more.", bench.OpenFile(Path.Combine(_root.FullName, "gone.oui")));
        Assert.Equal("notes.oui is not a readable interface.", bench.OpenFile(notOne));
        Assert.Null(bench.Shown);
    }

    [Fact]
    public void ThePreviewIsTheDeviceTheWayItIsHeld()
    {
        var (bench, _) = Opened(Menu());
        Assert.Null(bench.Preview);

        bench.Landscape = true;
        Assert.Equal((1080.0, 1920.0), bench.Preview);

        bench.Device = (390, 844);
        Assert.Equal((844.0, 390.0), bench.Preview);
    }

    [Fact]
    public void AnElementIsNamedByItsKeyThenItsWordsThenItsType()
    {
        Assert.Equal("play", UiElements.NameOf(Keyed("button", "play")));
        Assert.Equal("Start", UiElements.NameOf(new UiNode { Type = "button", Text = "  Start " }));
        Assert.Equal("field", UiElements.NameOf(UiElement.Field.Make()));
    }

    [Fact]
    public void FlowingTakesOutEveryDirectionAndKeepsAGap()
    {
        Assert.Equal("p-2 gap-4", UiElements.Flowing("stack  p-2 lg:col"));
        Assert.Equal("gap-1", UiElements.Flowing("row gap-1"));
    }
}

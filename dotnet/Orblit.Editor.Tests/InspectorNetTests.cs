using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The network section, as a person meets it in the inspector: tagging a thing as shared, and what travels.</summary>
public sealed class InspectorNetTests
{
    private static EditorScene World() => new(
    [
        new SceneObject("crate", "Crate", ObjectKind.Mesh),
        new SceneObject("spot", "Spot", ObjectKind.Group),
        new SceneObject("sky", "Sky", ObjectKind.Weather),
        new SceneObject(
            "player",
            "Player",
            ObjectKind.Group,
            components: new() { [Doc.SceneComponents.Net] = new Doc.NetComponent(Doc.NetAuthority.Owner, ["transform", "score"]) }),
    ]);

    private static Doc.NetComponent? TagOf(EditorSession session, string id) =>
        PhysicsComponents.Of<Doc.NetComponent>(session.Workspace.Loaded!.Scene![id]!, Doc.SceneComponents.Net);

    // A rebuilt box announces its starting text, so the text is typed into a focused box and committed by leaving it.
    private static void Type(Window window, string name, string text)
    {
        var box = Headless.Find<TextBox>(window, name);
        Assert.True(box.Focus());
        box.Text = text;
        Headless.Render();
        Assert.True(Headless.Find<Button>(window, "elsewhere").Focus());
        Headless.Render();
    }

    [Fact]
    public Task EveryThingInTheWorldCanBeTaggedAndTheWeatherAndTheSceneCannot() => Headless.Run(() =>
    {
        Assert.True(Has(Selecting("crate", World()).Window, "section_network"));
        Assert.True(Has(Selecting("spot", World()).Window, "section_network"));
        Assert.False(Has(Selecting("sky", World()).Window, "section_network"));
        Assert.False(Has(Open(World()).Window, "section_network"));
    });

    [Fact]
    public Task AddingATagSharesThePlaceFromTheServerAndUndoTakesItOff() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());

        Press(window, "button_add_network_tag");

        Assert.Equal(Doc.NetAuthority.Server, TagOf(session, "crate")!.Authority);
        Assert.Equal(["transform"], TagOf(session, "crate")!.Sync);
        Assert.True(Has(window, "button_remove_network_tag"));
        Assert.False(Has(window, "button_add_network_tag"));
        Assert.Equal(["Add network tag to Crate"], session.History.Labels);

        session.History.Undo();
        Assert.Null(TagOf(session, "crate"));
        Assert.True(Has(window, "button_add_network_tag"));
    });

    [Fact]
    public Task ATaggedThingShowsItsAuthorityAndWhatItShares() => Headless.Run(() =>
    {
        var (_, window) = Selecting("player", World());

        Assert.Equal("transform, score", Headless.Find<TextBox>(window, "entry_sync").Text);
        Assert.True(Has(window, "choice_authority_owner"));
    });

    [Fact]
    public Task ChoosingOwnerIsOneStepThatKeepsWhatIsShared() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_network_tag");
        Type(window, "entry_sync", "transform, score");

        Press(window, "choice_authority_owner");

        Assert.Equal(Doc.NetAuthority.Owner, TagOf(session, "crate")!.Authority);
        Assert.Equal(["transform", "score"], TagOf(session, "crate")!.Sync);
        session.History.Undo();
        Assert.Equal(Doc.NetAuthority.Server, TagOf(session, "crate")!.Authority);
        Assert.Equal(["transform", "score"], TagOf(session, "crate")!.Sync);
    });

    [Fact]
    public Task TypingNamesChangesWhatTravelsAndEmptyTextSharesNothing() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_network_tag");

        Type(window, "entry_sync", "transform, health, health");
        Assert.Equal(["transform", "health"], TagOf(session, "crate")!.Sync);

        Type(window, "entry_sync", "");
        Assert.Empty(TagOf(session, "crate")!.Sync);
        Assert.NotNull(TagOf(session, "crate"));
    });

    [Fact]
    public Task ChoosingWhatIsAlreadyChosenLeavesNothingToUndo() => Headless.Run(() =>
    {
        var (session, window) = Selecting("crate", World());
        Press(window, "button_add_network_tag");
        var steps = session.History.Labels.Count;

        Press(window, "choice_authority_server");

        Assert.Equal(steps, session.History.Labels.Count);
    });

    [Fact]
    public Task RemovingTheTagTakesItOffAndUndoPutsBackWhatWasSet() => Headless.Run(() =>
    {
        var (session, window) = Selecting("player", World());

        Press(window, "button_remove_network_tag");

        Assert.Null(TagOf(session, "player"));
        Assert.True(Has(window, "button_add_network_tag"));
        Assert.Equal(["Remove network tag from Player"], session.History.Labels);
        session.History.Undo();
        Assert.Equal(Doc.NetAuthority.Owner, TagOf(session, "player")!.Authority);
        Assert.Equal(["transform", "score"], TagOf(session, "player")!.Sync);
    });

    [Fact]
    public void ATagSurvivesSavingAndReopeningTheScene()
    {
        var back = SceneDocument.Decode(SceneDocument.Encode(World())).Scene;

        var tag = PhysicsComponents.Of<Doc.NetComponent>(back["player"]!, Doc.SceneComponents.Net);
        Assert.Equal(Doc.NetAuthority.Owner, tag!.Authority);
        Assert.Equal(["transform", "score"], tag.Sync);
        Assert.Null(PhysicsComponents.Of<Doc.NetComponent>(back["crate"]!, Doc.SceneComponents.Net));
    }
}

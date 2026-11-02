namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Doc = Orblit.Scene;

/// <summary>Who owns what when a game is played by more than one person, with no game running.</summary>
public sealed class NetPlayOwnersTests
{
    private static Doc.SceneEntity Owned(string id) =>
        new Doc.SceneEntity(id, id).WithComponent(Doc.SceneComponents.Net, new Doc.NetComponent(Doc.NetAuthority.Owner));

    private static Doc.SceneEntity Served(string id) =>
        new Doc.SceneEntity(id, id).WithComponent(Doc.SceneComponents.Net, new Doc.NetComponent());

    private static Doc.SceneDocument Scene(params Doc.SceneEntity[] entities) => new("Scene", null, entities);

    [Fact]
    public void TheFirstOwnerEntityStaysWithTheHostAndTheNextGoToTheGuestsInJoinOrder()
    {
        var scene = Scene(Owned("p1"), Owned("p2"), Owned("p3"));

        var owners = NetPlay.OwnersFor(scene, ["player2", "player3"]);

        Assert.Equal([("p2", "player2"), ("p3", "player3")], owners);
    }

    [Fact]
    public void TheOrderIsTheOrderOfTheSceneAndNotTheOrderOfTheIds()
    {
        var scene = Scene(Owned("zed"), Owned("amy"));

        Assert.Equal([("amy", "player2")], NetPlay.OwnersFor(scene, ["player2"]));
    }

    [Fact]
    public void AnEntityTheServerMovesOrThatIsNotSharedIsNeverGiven()
    {
        var scene = Scene(new Doc.SceneEntity("sky", "sky"), Owned("p1"), Served("crate"), Owned("p2"));

        Assert.Equal([("p2", "player2")], NetPlay.OwnersFor(scene, ["player2"]));
    }

    [Fact]
    public void OwnerEntitiesBeyondTheGuestsStayWithTheHost()
    {
        var scene = Scene(Owned("p1"), Owned("p2"), Owned("npc"));

        Assert.Equal([("p2", "player2")], NetPlay.OwnersFor(scene, ["player2"]));
    }

    [Fact]
    public void AGuestWithNoEntityLeftOwnsNothing()
    {
        var scene = Scene(Owned("p1"), Owned("p2"));

        Assert.Equal([("p2", "player2")], NetPlay.OwnersFor(scene, ["player2", "player3"]));
    }

    [Fact]
    public void WithNoGuestsNothingIsGiven()
    {
        Assert.Empty(NetPlay.OwnersFor(Scene(Owned("p1"), Owned("p2")), []));
        Assert.Empty(NetPlay.OwnersFor(Scene(), ["player2"]));
    }
}

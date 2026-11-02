namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>What the network section decides about a tag, with no control in sight.</summary>
public sealed class NetModelTests
{
    [Fact]
    public void TheSyncListIsReadFromTextTheWayItIsTyped() =>
        Assert.Equal(["transform", "score", "health"], NetModel.ParseSync(" transform ,score,, score ,health "));

    [Fact]
    public void TextWithNoNamesSharesNoState()
    {
        Assert.Empty(NetModel.ParseSync(""));
        Assert.Empty(NetModel.ParseSync(" , ,"));
        Assert.Empty(NetModel.WithSync(new NetComponent(), "").Sync);
    }

    [Fact]
    public void TheSyncListIsShownAsNamesSeparatedByCommas() =>
        Assert.Equal("transform, score", NetModel.SyncText(new NetComponent(sync: ["transform", "score"])));

    [Fact]
    public void ANewTagIsServerOwnedAndSharesThePlace()
    {
        var tag = NetModel.Tag();

        Assert.Equal(NetAuthority.Server, tag.Authority);
        Assert.Equal(["transform"], tag.Sync);
    }

    [Fact]
    public void ChangingTheAuthorityKeepsTheSyncList()
    {
        var tag = new NetComponent(NetAuthority.Server, ["transform", "score"]);

        var owned = NetModel.WithAuthority(tag, "Owner");

        Assert.Equal(NetAuthority.Owner, owned.Authority);
        Assert.Equal(["transform", "score"], owned.Sync);
    }

    [Fact]
    public void ChangingTheSyncListKeepsTheAuthority()
    {
        var tag = new NetComponent(NetAuthority.Owner, ["transform"]);

        var changed = NetModel.WithSync(tag, "score");

        Assert.Equal(NetAuthority.Owner, changed.Authority);
        Assert.Equal(["score"], changed.Sync);
    }

    [Fact]
    public void AnAuthorityTheEditorDoesNotKnowReadsAsTheServers()
    {
        Assert.Equal(NetAuthority.Server, NetModel.WithAuthority(new NetComponent(NetAuthority.Owner), "Anyone").Authority);
        Assert.Equal("Owner", NetModel.AuthorityName(new NetComponent(NetAuthority.Owner)));
        Assert.Equal("Server", NetModel.AuthorityName(new NetComponent()));
    }
}

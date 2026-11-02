using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The section for the network tag: whether the thing is shared between the players of a game, who
/// moves it, and what travels.
/// </summary>
internal static class NetSection
{
    public static Control Build(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var rows = new ComponentRows<NetComponent>(fields, target, SceneComponents.Net);
        if (rows.Current is not { } tag)
        {
            return fields.Section(
                "Network",
                rows.Add("Add network tag", "Share this with every player. The server copies it to each client.", NetModel.Tag(), $"Add network tag to {name}"));
        }

        return fields.Section(
            "Network",
            Fields.Column(
            [
                rows.Choice("Authority", NetModel.Authorities, NetModel.AuthorityName, NetModel.WithAuthority),
                rows.Text("Sync", NetModel.SyncText, NetModel.WithSync, "transform, score"),
                Fields.Note(AuthorityNote(tag)),
                rows.Remove("Remove network tag", "Stop sharing this. Every player keeps its own copy.", $"Remove network tag from {name}"),
            ]));
    }

    private static string AuthorityNote(NetComponent tag) => tag.Authority == NetAuthority.Owner
        ? "The player who owns it moves it. The server passes it on."
        : "Only the server moves it. Every client follows.";
}

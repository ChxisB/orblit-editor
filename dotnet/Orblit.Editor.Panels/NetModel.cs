namespace Orblit.Editor.Panels;

using Orblit.Scene;

/// <summary>What the network section decides about a tag, apart from drawing its fields.</summary>
public static class NetModel
{
    /// <summary>The words the authority choice shows, in the order they sit.</summary>
    public static IReadOnlyList<string> Authorities { get; } = ["Server", "Owner"];

    /// <summary>A tag as a new one starts: the server moves the entity and its place is shared.</summary>
    public static NetComponent Tag() => new();

    public static string AuthorityName(NetComponent tag) => tag.Authority == NetAuthority.Owner ? "Owner" : "Server";

    public static string SyncText(NetComponent tag) => string.Join(", ", tag.Sync);

    /// <summary>
    /// The tag with the authority <paramref name="name"/> says. A word that is not Owner is the
    /// server's, so a slip of the hand never gives a client the entity.
    /// </summary>
    public static NetComponent WithAuthority(NetComponent tag, string name) =>
        new(name == "Owner" ? NetAuthority.Owner : NetAuthority.Server, tag.Sync);

    public static NetComponent WithSync(NetComponent tag, string text) => new(tag.Authority, ParseSync(text));

    /// <summary>
    /// The names in text typed as a list. The box applies each edit as it is made, so a blank between
    /// two commas, or a name that is already listed, is dropped and not refused.
    /// </summary>
    public static IReadOnlyList<string> ParseSync(string text) => text
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Distinct(StringComparer.Ordinal)
        .ToArray();
}

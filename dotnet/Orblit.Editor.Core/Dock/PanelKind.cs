namespace Orblit.Editor.Core;

/// <summary>
/// What a panel shows.
/// </summary>
/// <remarks>
/// A kind rather than a control, because the layout is data: it is saved to a file, sent through an
/// undo, and compared in a test, and a control is none of those things. What builds one is whoever
/// registered it.
/// <para>
/// A class rather than an enum, so something the editor core has never heard of, such as a terrain
/// brush panel or a timeline, can be a kind too. A kind is its name: that is what the layout file
/// stores, so two kinds with the same name are the same kind.
/// </para>
/// </remarks>
public sealed class PanelKind : IEquatable<PanelKind>
{
    public static readonly PanelKind Outliner = new("outliner", "Hierarchy", "account_tree_outlined");

    public static readonly PanelKind Inspector = new("inspector", "Inspector", "tune");

    public static readonly PanelKind Viewport = new("viewport", "Scene", "videocam_outlined", repeatable: true);

    public static readonly PanelKind Game = new("game", "Game", "sports_esports_outlined");

    public static readonly PanelKind Project = new("project", "Project", "folder_outlined");

    public static readonly PanelKind Console = new("console", "Console", "terminal");

    public static readonly PanelKind Uvs = new("uvs", "UVs", "grid_on_outlined");

    public static readonly PanelKind Modelling = new("modelling", "Modelling", "handyman_outlined");

    public static readonly PanelKind Timeline = new("timeline", "Timeline", "animation");

    public static readonly PanelKind Profile = new("profile", "Profile", "speed");

    /// <summary>The brush's settings, beside the inspector while the ground is being shaped.</summary>
    public static readonly PanelKind TerrainBrush = new("terrainBrush", "Brush", "brush_outlined");

    /// <summary>The graph of a blend: its states as boxes, its changes as arrows.</summary>
    public static readonly PanelKind BlendGraph = new("blendGraph", "Blend", "hub_outlined");

    /// <summary>
    /// The kinds the editor has always had, which a layout can be read against before anything else
    /// has registered.
    /// </summary>
    public static readonly IReadOnlyList<PanelKind> BuiltIn =
        [Outliner, Inspector, Viewport, Game, Project, Console, Uvs, Modelling, Timeline, Profile, TerrainBrush, BlendGraph];

    public PanelKind(string name, string label, string icon, bool repeatable = false)
    {
        Name = name;
        Label = label;
        Icon = icon;
        Repeatable = repeatable;
    }

    /// <summary>What the layout file calls it. Never changed once a kind has shipped, or every saved layout that has one loses it.</summary>
    public string Name { get; }

    public string Label { get; }

    /// <summary>
    /// The name of a Material icon, which the shell draws. A name and not a glyph, so that this
    /// project has no UI type in it.
    /// </summary>
    public string Icon { get; }

    /// <summary>
    /// Whether more than one of these makes sense.
    /// <para>
    /// Four scene views onto one world is the reason this whole thing exists. Four inspectors is four
    /// copies of the same fields.
    /// </para>
    /// </summary>
    public bool Repeatable { get; }

    /// <summary>The one of <paramref name="kinds"/> called <paramref name="name"/>, or null.</summary>
    public static PanelKind? Named(object? name, IEnumerable<PanelKind>? kinds = null) =>
        (kinds ?? BuiltIn).FirstOrDefault(kind => name is string text && kind.Name == text);

    public bool Equals(PanelKind? other) => other is not null && other.Name == Name;

    public override bool Equals(object? obj) => Equals(obj as PanelKind);

    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);

    public override string ToString() => $"PanelKind({Name})";
}

/// <summary>
/// One panel in the layout.
/// </summary>
/// <remarks>
/// The id is what a drag carries and what per-panel state is filed under. Two scene views are two
/// cameras, and without an id of its own the second would be looking wherever the first was.
/// </remarks>
/// <param name="Id">What the panel is filed under.</param>
/// <param name="Kind">What it shows.</param>
/// <param name="Title">What the tab says, when it is not just the kind's name. For telling four scene views apart.</param>
public sealed record DockPanel(string Id, PanelKind Kind, string? Title = null)
{
    public string Label => Title ?? Kind.Label;

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["id"] = Id, ["kind"] = Kind.Name };
        if (Title is not null)
        {
            json["title"] = Title;
        }

        return json;
    }

    /// <summary>
    /// Reads one, or null when its kind is not one of <paramref name="kinds"/>.
    /// <para>
    /// Left out rather than guessed: a panel from a build that had something this one does not is not
    /// worth showing as whatever it most resembles.
    /// </para>
    /// </summary>
    public static DockPanel? FromJson(object? value, IEnumerable<PanelKind> kinds)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return null;
        }

        var kind = PanelKind.Named(map.GetValueOrDefault("kind"), kinds);
        if (kind is null || map.GetValueOrDefault("id") is not string { Length: > 0 } id)
        {
            return null;
        }

        return new DockPanel(id, kind, map.GetValueOrDefault("title") as string);
    }
}

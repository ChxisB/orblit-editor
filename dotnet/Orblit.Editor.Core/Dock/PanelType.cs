namespace Orblit.Editor.Core;

/// <summary>
/// A kind of panel the editor can show.
/// </summary>
/// <remarks>
/// The layout says where a panel goes and this says which panels exist. The shell looks a panel's
/// kind up here, so a panel registered from outside the core opens, docks, saves and comes back
/// exactly as the built-in ones do. What draws one is the shell's, because the core has no control
/// type.
/// </remarks>
/// <param name="Kind">What the panel is.</param>
/// <param name="OpensAs">
/// The id the View menu opens one under, when it is not the kind's name. The first scene view is
/// <c>scene</c>, because that is what every layout written before this called it.
/// </param>
public sealed record PanelType(PanelKind Kind, string? OpensAs = null) : IRegistered
{
    /// <summary>The one the View menu opens.</summary>
    public DockPanel Panel => new(OpensAs ?? Kind.Name, Kind);

    public string Name => Kind.Name;
}

/// <summary>What a registry of panel types is asked about the panels.</summary>
public static class PanelKinds
{
    /// <summary>What a saved layout is read against.</summary>
    public static IReadOnlyList<PanelKind> Kinds(this Registry<PanelType> registry) =>
        [.. registry.All.Select(type => type.Kind)];

    public static PanelType? TypeOf(this Registry<PanelType> registry, PanelKind kind) => registry[kind.Name];
}

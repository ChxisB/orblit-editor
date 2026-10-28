namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>
/// What is chosen in a blend's graph, which is what the inspector shows.
/// </summary>
public abstract record BlendPick
{
    // Closed to this assembly, so a switch over the three below is complete.
    internal BlendPick()
    {
    }

    /// <summary>Nothing is chosen, and the inspector shows the blend itself.</summary>
    public static BlendPick Nothing { get; } = new NothingPicked();
}

/// <summary>Nothing is chosen.</summary>
public sealed record NothingPicked : BlendPick;

/// <summary>A state, by the name a file writes it under.</summary>
/// <param name="Name">The state's name.</param>
public sealed record StatePicked(string Name) : BlendPick;

/// <summary>A change, by where it stands in <see cref="BlendDocument.WrittenChanges"/>.</summary>
/// <param name="Index">Where the change stands.</param>
public sealed record ChangePicked(int Index) : BlendPick;

/// <summary>
/// A blend open in the graph.
/// </summary>
/// <param name="path">The file it was read from and is written to.</param>
/// <param name="blend">The blend as it was read.</param>
public sealed class OpenBlend(string path, BlendDocument blend)
{
    public string Path { get; } = path;

    /// <summary>As it is now, edits and all. Only ever replaced whole, by an edit through the bench.</summary>
    public BlendDocument Blend { get; internal set; } = blend;

    /// <summary>
    /// What the undo stack files this blend's changes under.
    /// <para>
    /// Prefixed so no scene or clip can ever be given the same one.
    /// </para>
    /// </summary>
    public string Id => IdFor(Path);

    /// <summary>The undo stack's stamp for this blend when it was last written.</summary>
    internal int SavedStamp { get; set; }

    internal static string IdFor(string path) => $"blend:{path}";
}

namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>
/// What a bench's documents are played on, which decides how a key names the thing it moves.
/// </summary>
public enum PlayedOn
{
    /// <summary>
    /// A clip, played on whichever object is chosen. Targets are named from that object, so one clip
    /// plays on every copy of a prefab.
    /// </summary>
    Owner,

    /// <summary>A cutscene, played on the whole scene. Targets are the scene's own ids.</summary>
    Scene,
}

/// <summary>
/// What ties an edit to the ones around it.
/// </summary>
/// <param name="Keys">The selection afterwards. The one before when not given.</param>
/// <param name="Gesture">
/// Ties a drag's worth of edits into one undo step. Null for an edit that stands alone.
/// </param>
/// <param name="OnlyMoves">
/// Says all the edit does is move keys or bend a curve. Never true when a key comes or goes.
/// </param>
public readonly record struct EditOptions(IReadOnlySet<KeyRef>? Keys = null, object? Gesture = null, bool OnlyMoves = false);

/// <summary>
/// A clip open on the timeline.
/// </summary>
public sealed class OpenClip(
    string path,
    ClipDocument clip,
    IReadOnlyList<CutsceneShot>? shots = null,
    IReadOnlyList<CutsceneSound>? sounds = null)
{
    public string Path { get; } = path;

    /// <summary>As it is now, edits and all. Only ever replaced whole, by an edit through the bench.</summary>
    public ClipDocument Clip { get; internal set; } = clip;

    /// <summary>
    /// A cutscene's shots in the order they start, or null for a clip. Only ever replaced whole, by an
    /// edit through the bench.
    /// </summary>
    public IReadOnlyList<CutsceneShot>? Shots { get; internal set; } = shots;

    /// <summary>
    /// A cutscene's sounds. Nothing here edits them yet, but saving must not drop what another tool
    /// wrote.
    /// </summary>
    public IReadOnlyList<CutsceneSound> Sounds { get; } = sounds ?? [];

    /// <summary>
    /// What the undo stack files this clip's changes under.
    /// <para>
    /// A clip is not a scene, but it is a document that changes and is saved, and the stack already
    /// answers "has this changed since it was written" for anything with an id. Prefixed so no scene
    /// can ever be given the same one.
    /// </para>
    /// </summary>
    public string Id => IdFor(Path);

    /// <summary>The undo stack's stamp for this clip when it was last written.</summary>
    internal int SavedStamp { get; set; }

    /// <summary>The cutscene as it is now, or null for a clip.</summary>
    public CutsceneDocument? Cutscene => Shots is { } held ? new CutsceneDocument(Clip, held, Sounds) : null;

    internal static string IdFor(string path) => $"clip:{path}";
}

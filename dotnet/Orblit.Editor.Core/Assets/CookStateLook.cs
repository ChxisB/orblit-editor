namespace Orblit.Editor.Core;

using Orblit.Asset;

/// <summary>A colour for a cook state, by its place in the theme. The window picks the brush.</summary>
public enum CookMark
{
    Good,
    Warn,
    Bad,
}

/// <summary>
/// How a cook state reads to somebody looking at a folder of files. Here rather than in the browser
/// so that the panel, the preview and the tooltip all call the same state the same thing. Two places
/// calling <c>Stale</c> two things is how a person ends up believing they are two states.
/// </summary>
public static class CookStateLook
{
    /// <summary>
    /// What to call it. Plain words: somebody reading a tooltip wants to know what to do about the
    /// file, not what the pipeline calls the condition.
    /// </summary>
    public static string Label(this CookState state) => state switch
    {
        CookState.Cooked => "Cooked",
        CookState.Stale => "Needs cooking",
        CookState.Failed => "Would not cook",
        CookState.Ignored => "Nothing to cook",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>A longer line, for where there is room to say why.</summary>
    public static string Explanation(this CookState state) => state switch
    {
        CookState.Cooked => "The cache already has this for this machine.",
        CookState.Stale => "It has changed, or has never been cooked.",
        CookState.Failed => "A cook tried this and the importer could not do it.",
        CookState.Ignored => "No importer claims this kind of file.",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    /// <summary>
    /// The dot's colour, or null for the states not worth a dot. <see cref="CookState.Ignored"/> gets
    /// none. A README is not a problem and never will be, and a mark on every file that is fine is a
    /// panel of marks that nobody reads the important one in.
    /// </summary>
    public static CookMark? Mark(this CookState state) => state switch
    {
        CookState.Cooked => CookMark.Good,
        CookState.Stale => CookMark.Warn,
        CookState.Failed => CookMark.Bad,
        CookState.Ignored => null,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };
}

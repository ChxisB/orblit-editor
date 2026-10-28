namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Path data for the timeline's pictures, each on a 24 unit square and drawn as an outline. Play, pause
/// and the clip and object pictures are <see cref="AssetIcons"/>.
/// </summary>
internal static class TimelineIcons
{
    public const string Start = "M6 5v14M18 6l-9 6 9 6z";
    public const string End = "M18 5v14M6 6l9 6-9 6z";
    public const string Auto = "M4 17c3 0 4-10 8-10s5 10 8 10";
    public const string Delete = "M5 7h14M10 7V4h4v3M7 7l1 13h8l1-13";
    public const string Close = "M6 6l12 12M18 6L6 18";
    public const string Caret = "M7 10l5 5 5-5";
    public const string Owner = "M12 21s-6-5.5-6-10a6 6 0 0 1 12 0c0 4.5-6 10-6 10zM10 11a2 2 0 1 0 4 0a2 2 0 1 0-4 0";
    public const string Link = "M5 12h14M13 6l6 6-6 6";
    public const string Flag = "M6 21V4M6 5h12l-3 4 3 4H6";
    public const string Unlinked = "M9 15l-2 2a3 3 0 0 1-4-4l3-3a3 3 0 0 1 4 0M15 9l2-2a3 3 0 0 1 4 4l-3 3a3 3 0 0 1-4 0M4 4l16 16";

    /// <summary>The picture for what a bench edits: a clip, or a cutscene.</summary>
    public static string Of(PlayedOn playedOn) => AssetIcons.Of(playedOn == PlayedOn.Owner ? AssetKind.Clip : AssetKind.Cutscene);
}

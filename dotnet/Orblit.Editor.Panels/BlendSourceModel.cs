namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>What a state plays, as the editor words it and changes it.</summary>
public static class BlendSourceModel
{
    /// <summary>What a state plays as a phrase.</summary>
    public static string Describe(BlendSource source) => source switch
    {
        BlendClip clip => clip.Clip,
        BlendLine line => $"{line.Points.Count} points along {line.Input}",
        BlendPlane plane => $"{plane.Points.Count} points across {plane.X} and {plane.Y}",
        BlendGraph graph => $"a graph of {graph.Graph.States.Count} states",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A source the editor does not know."),
    };

    /// <summary>
    /// The line <paramref name="source"/> is, when every point of it plays one clip. A line with a mix
    /// inside it is left to the file.
    /// </summary>
    public static BlendLine? LineOfClips(BlendSource source) =>
        source is BlendLine line && line.Points.All(static point => point.Plays is BlendClip) ? line : null;

    /// <summary>
    /// Whether the editor changes what <paramref name="source"/> plays. A plane, a graph and a line with
    /// a mix inside it are read and not changed.
    /// </summary>
    public static bool CanEdit(BlendSource source) => source is BlendClip || LineOfClips(source) is not null;

    /// <summary>The first clip <paramref name="source"/> names, or <c>clip</c> for one that names none.</summary>
    public static string FirstClipOf(BlendSource source) => source.Clips.FirstOrDefault() ?? "clip";

    /// <summary>
    /// A line along <paramref name="input"/> that plays <paramref name="clip"/> at nought and at one, which
    /// is somewhere to start from when a state that played one clip is to mix.
    /// </summary>
    public static BlendLine LineFrom(string clip, string input) =>
        new(input, [new LinePoint(0, new BlendClip(clip)), new LinePoint(1, new BlendClip(clip))]);

    /// <summary><paramref name="line"/> along <paramref name="input"/> instead.</summary>
    public static BlendLine LineAlong(BlendLine line, string input) => new(input, line.Points);

    /// <summary>
    /// <paramref name="line"/> with point <paramref name="index"/> moved to <paramref name="at"/> and playing
    /// <paramref name="clip"/>, each when given. The points are put in order again, so the point may not
    /// stay at <paramref name="index"/>.
    /// </summary>
    public static BlendLine PointChanged(BlendLine line, int index, double? at = null, string? clip = null) => new(
        line.Input,
        [
            .. line.Points.Select((point, i) => i != index
                ? point
                : new LinePoint(
                    at is { } moved && double.IsFinite(moved) ? moved : point.At,
                    string.IsNullOrEmpty(clip) ? point.Plays : new BlendClip(clip))),
        ]);

    /// <summary>
    /// <paramref name="line"/> with a point after the last, one further along and playing what the last
    /// does.
    /// </summary>
    public static BlendLine PointAdded(BlendLine line)
    {
        var last = line.Points[^1];
        return new BlendLine(line.Input, [.. line.Points, new LinePoint(last.At + 1, last.Plays)]);
    }

    /// <summary><paramref name="line"/> without point <paramref name="index"/>, or the same line when it has one point.</summary>
    public static BlendLine PointRemoved(BlendLine line, int index) =>
        line.Points.Count < 2 ? line : new BlendLine(line.Input, [.. line.Points.Where((_, i) => i != index)]);
}

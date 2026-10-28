namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>Cutting a cutscene between its cameras.</summary>
public static class Cinematics
{
    /// <summary>
    /// <paramref name="shots"/> with a cut to <paramref name="camera"/> at <paramref name="at"/>, in a
    /// cutscene <paramref name="length"/> long.
    /// <para>
    /// The shot running at <paramref name="at"/> ends there and one starting there gives way, so the
    /// change is a cut and not a blend. The new shot holds until the next one starts, or to the end.
    /// </para>
    /// </summary>
    public static IReadOnlyList<CutsceneShot> CutTo(IReadOnlyList<CutsceneShot> shots, string camera, double at, double length)
    {
        var next = shots.Select(shot => shot.Start).Where(start => start > at).Append(length).Min();

        // At the very end there is nothing left to hold for, and a shot has to last some time, so it
        // gets a couple of seconds past the end.
        var end = next > at ? next : at + 2;
        return
        [
            .. shots
                .Where(shot => shot.Start != at)
                .Select(shot => shot.Start < at && shot.End > at ? shot.CopyWith(duration: at - shot.Start) : shot),
            new CutsceneShot(camera, at, end - at),
        ];
    }
}

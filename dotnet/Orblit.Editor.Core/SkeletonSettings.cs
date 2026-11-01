namespace Orblit.Editor.Core;

using Orblit.Motion;
using Orblit.Sequence;

/// <summary>What the view shows of a model's skeleton.</summary>
/// <param name="Bones">Draws the bones of a selected model, at rest or in the clip's pose.</param>
/// <param name="Onion">
/// Draws the pose at moments either side of the playhead as well. Only shown with bones, since there is
/// nothing to ghost without them.
/// </param>
/// <param name="Before">How many ghosts come from before the playhead.</param>
/// <param name="After">How many come from after it.</param>
/// <param name="Stride">Frames between one ghost and the next.</param>
public sealed record SkeletonSettings(bool Bones = false, bool Onion = false, int Before = 2, int After = 2, int Stride = 2)
{
    /// <summary>Bones on or off. Taking the bones away takes the ghosts with them.</summary>
    public SkeletonSettings WithBonesToggled() => this with { Bones = !Bones, Onion = Bones ? false : Onion };

    /// <summary>Onion skin on or off. Turning it on brings the bones, since there is nothing to ghost without them.</summary>
    public SkeletonSettings WithOnionToggled() => this with { Bones = !Onion || Bones, Onion = !Onion };

    /// <summary>
    /// The moments of <paramref name="clip"/> ghosted round <paramref name="at"/>, the earlier ones first
    /// and the nearest first on each side.
    /// </summary>
    /// <remarks>
    /// A looping clip wraps, so the frame before its first is its last. Any other clip has nothing before
    /// its start or after its end, so a ghost that falls off either end is left out rather than piled on
    /// the first or last frame.
    /// </remarks>
    public IReadOnlyList<OnionMoment> OnionMoments(ClipDocument clip, double at)
    {
        if (!Onion || clip.Rate <= 0 || clip.Duration <= 0)
        {
            return [];
        }

        var step = Math.Clamp(Stride, 1, 1000) / clip.Rate;
        var sides = Enumerable.Range(1, Math.Max(Before, 0)).Select(i => (At: at - (i * step), Earlier: true))
            .Concat(Enumerable.Range(1, Math.Max(After, 0)).Select(i => (At: at + (i * step), Earlier: false)));
        var moments = new List<OnionMoment>();
        foreach (var (moment, earlier) in sides)
        {
            // A short loop comes back to the playhead itself, and to the same moment from both sides.
            if (Placed(clip, moment) is { } placed && !Near(placed, at) && !moments.Exists(one => Near(one.At, placed)))
            {
                moments.Add(new OnionMoment(placed, earlier));
            }
        }

        return moments;
    }

    // Where a moment lands in the clip, or null when it falls off an end that does not wrap.
    private static double? Placed(ClipDocument clip, double moment)
    {
        if (clip.WhenDone == WhenDone.Loop)
        {
            var wrapped = moment % clip.Duration;
            return wrapped < 0 ? wrapped + clip.Duration : wrapped;
        }

        return moment < 0 || moment > clip.Duration ? null : moment;
    }

    private static bool Near(double one, double other) => Math.Abs(one - other) < 1e-9;
}

/// <summary>One ghost: when it is in the clip, and which side of the playhead.</summary>
public readonly record struct OnionMoment(double At, bool Earlier);

/// <summary>
/// The skeleton settings the timeline's chips write and the view reads, kept in one place so neither
/// has to know the other.
/// </summary>
public sealed class SkeletonLook
{
    private SkeletonSettings _value = new();

    public event Action? Changed;

    public SkeletonSettings Value
    {
        get => _value;
        set
        {
            if (_value == value)
            {
                return;
            }

            _value = value;
            Changed?.Invoke();
        }
    }
}

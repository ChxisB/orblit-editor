namespace Orblit.Editor.Panels;

using System.Collections.Frozen;
using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>
/// What the timeline's controls do to the bench, without a window.
/// </summary>
/// <remarks>
/// Playing is sampling: each frame asks how far the clock moved and where that puts the playhead, so
/// scrubbing backwards shows exactly what playing forwards did. Every other control is an edit through the
/// undo stack.
/// </remarks>
public sealed class TimelineModel(ClipBench bench)
{
    private const double LongestLength = 600;

    // Which way a bouncing clip is going.
    private double _direction = 1;
    private TimeSpan? _last;

    // What ties typing a length into one step.
    private object? _typing;

    /// <summary>
    /// Moves the playhead for a frame that began at <paramref name="now"/> on any clock that only goes
    /// forward. The first frame after playing starts only reads the clock.
    /// </summary>
    public void Tick(TimeSpan now)
    {
        if (!bench.Playing)
        {
            _last = null;
            return;
        }

        var last = _last;
        _last = now;
        if (last is not { } before || bench.Clip is not { } clip)
        {
            return;
        }

        var at = bench.At + (now - before).TotalSeconds * _direction;
        if (clip.WhenDone is WhenDone.Hold or WhenDone.Release && at >= clip.Duration)
        {
            bench.At = clip.Duration;
            bench.Playing = false;
            return;
        }

        Turn(clip, at);
        bench.At = Wrapped(clip, at);
    }

    /// <summary>Plays, or pauses if it is playing. A clip that plays once and has run out starts over.</summary>
    public void Play()
    {
        if (bench.Clip is not { } clip)
        {
            return;
        }

        if (bench.Playing)
        {
            bench.Playing = false;
            return;
        }

        var once = clip.WhenDone is WhenDone.Hold or WhenDone.Release;
        if (once && bench.At >= clip.Duration - ClipEdits.SameMoment)
        {
            bench.At = 0;
        }

        _direction = 1;
        bench.Playing = true;
    }

    /// <summary>Stops, and moves the playhead <paramref name="frames"/> from the frame it is on.</summary>
    public void Step(int frames)
    {
        if (bench.Clip is not { Rate: > 0 } clip)
        {
            return;
        }

        bench.Playing = false;
        bench.At = ClipEdits.Snapped(bench.Frame + frames / clip.Rate, clip.Rate);
    }

    public void Home()
    {
        bench.Playing = false;
        bench.At = 0;
    }

    public void End()
    {
        bench.Playing = false;
        bench.At = bench.Clip?.Duration ?? 0;
    }

    public void DeleteKeys()
    {
        var picked = bench.Selection;
        if (picked.Count == 0)
        {
            return;
        }

        bench.Edit(
            picked.Count == 1 ? "Delete key" : "Delete keys",
            clip => ClipEdits.WithoutKeys(clip, picked),
            new EditOptions(Keys: FrozenSet<KeyRef>.Empty));
    }

    /// <summary>
    /// Sets the length from what was typed. Anything that is not a sensible number of seconds is left
    /// alone, so a half-typed length does not change the clip.
    /// </summary>
    public void SetLength(string text)
    {
        var parsed = double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);

        // Written to be false for NaN, which compares false either way round.
        if (!parsed || !(seconds > 0 && seconds <= LongestLength))
        {
            return;
        }

        _typing ??= new object();
        bench.Edit(
            TimelineWords.For(bench.PlayedOn).Length,
            clip => clip.CopyWith(duration: seconds),
            new EditOptions(Gesture: _typing));
    }

    /// <summary>Ends the typing of a length, so the next one is a step of its own.</summary>
    public void FinishLength()
    {
        _typing = null;
        bench.Seal();
    }

    public void SetRate(double rate) => bench.Edit("Frame rate", clip => clip.CopyWith(rate: rate));

    public void SetWhenDone(WhenDone done) => bench.Edit("When done", clip => clip.CopyWith(whenDone: done));

    /// <summary>How the picked keys travel to the next. Nothing happens with none picked.</summary>
    public void SetHold(Hold hold, Easing? shape = null)
    {
        if (bench.Selection.Count > 0)
        {
            bench.Edit(
                $"Hold {TimelineWords.Holding(hold).ToLowerInvariant()}",
                clip => ClipEdits.WithHold(clip, bench.Selection, hold, shape));
        }
    }

    /// <summary>Works the picked keys' slopes out again. Nothing happens with none picked.</summary>
    public void AutoSlopes()
    {
        if (bench.Selection.Count > 0)
        {
            bench.Edit("Auto slopes", clip => ClipEdits.WithAutoSlopes(clip, bench.Selection));
        }
    }

    /// <summary>Removes a channel and all its keys.</summary>
    public void RemoveChannel(ChannelAddress address)
    {
        if (bench.Clip is not { } clip || ClipEdits.ChannelAt(clip, address) is not { } channel)
        {
            return;
        }

        var keys = Enumerable.Range(0, channel.BoxedKeys.Count).Select(index => new KeyRef(address, index)).ToHashSet();
        bench.Edit(
            $"Remove {TimelineWords.Channel(address)}",
            edited => ClipEdits.WithoutKeys(edited, keys),
            new EditOptions(Keys: FrozenSet<KeyRef>.Empty));
    }

    // A bouncing clip that has gone past an end comes back the other way.
    private void Turn(ClipDocument clip, double at)
    {
        if (clip.WhenDone != WhenDone.Bounce)
        {
            return;
        }

        if (at >= clip.Duration)
        {
            _direction = -1;
        }
        else if (at <= 0)
        {
            _direction = 1;
        }
    }

    // Where a playhead that has gone past an end of a clip that plays on lands.
    private static double Wrapped(ClipDocument clip, double at) => clip.WhenDone switch
    {
        WhenDone.Loop when at >= clip.Duration => clip.Duration <= 0 ? 0 : at % clip.Duration,
        WhenDone.Bounce when at >= clip.Duration => Math.Max(2 * clip.Duration - at, 0),
        WhenDone.Bounce when at <= 0 => Math.Min(-at, clip.Duration),
        _ => at,
    };
}

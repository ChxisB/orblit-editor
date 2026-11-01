using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>Lets time pass in the playing scene, once for each frame the window draws.</summary>
internal sealed class PlaybackClock
{
    // A stall, such as a dragged window or a machine that slept, would otherwise throw a cutscene past its shots.
    private const double LongestStep = 0.1;

    private readonly EditorSession _session;
    private readonly TopLevel _window;
    private TimeSpan? _last;
    private bool _requested;
    private bool _detached;

    public PlaybackClock(EditorSession session, TopLevel window)
    {
        _session = session;
        _window = window;
        _session.PlaybackChanged += AskForFrame;
    }

    /// <summary>Stops listening, for a window that is closing. A frame already asked for does nothing.</summary>
    public void Detach()
    {
        _detached = true;
        _session.PlaybackChanged -= AskForFrame;
    }

    // Each step the scene takes asks for the next, so a scene that is paused or stopped asks for nothing.
    private void AskForFrame()
    {
        if (!_session.Playing)
        {
            // A frame already asked for may run after the next Play, which must not measure from before the pause.
            _last = null;
            return;
        }

        if (_requested)
        {
            return;
        }

        _requested = true;
        _window.RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        _requested = false;
        if (_detached || !_session.Playing)
        {
            return;
        }

        var step = StepSince(_last, now);
        _last = now;
        _session.AdvancePlayback(step);
    }

    /// <summary>Seconds from the last frame to this one. The first frame after a start has no last and takes none.</summary>
    internal static double StepSince(TimeSpan? last, TimeSpan now) =>
        last is { } before ? Math.Min((now - before).TotalSeconds, LongestStep) : 0;
}

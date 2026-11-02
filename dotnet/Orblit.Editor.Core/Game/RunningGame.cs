namespace Orblit.Editor.Core;

using Orblit.Game;
using Orblit.Input;

/// <summary>A game started in the scene that plays, and the loaded assembly it came from.</summary>
/// <remarks>
/// An edit made to the scene being edited reaches the game, and a change the game makes reaches the
/// playing scene. Disposing stops the game and lets its assembly unload, so nothing here may be kept
/// after it.
/// </remarks>
internal sealed class RunningGame : IDisposable
{
    // A game with no view to read from sees nothing held and no pointer.
    private static readonly GameInput NoInput = new(new FrameControls());

    private readonly LoadedGame _loaded;
    private readonly ScenePlayback _playback;
    private readonly GameHost _host;

    public RunningGame(LoadedGame loaded, ScenePlayback playback, string projectFolder, IGameLog log)
    {
        var scene = playback.Workspace.Loaded?.Scene ?? throw new ArgumentException("The playback has no scene to play in.", nameof(playback));
        _loaded = loaded;
        _playback = playback;
        _host = new GameHost(loaded.Game, SceneDocument.DocumentOf(scene), projectFolder, log);
    }

    /// <summary>The exception that ended the game, or null if it did not throw.</summary>
    public Exception? Fault => _host.Fault;

    public void Start()
    {
        // Both are joined before Start, because the game may add its first entities there.
        _host.SceneChanged += _playback.Apply;
        _playback.Edited += _host.ApplyEdit;
        _host.Start();
    }

    /// <summary>The game itself, for a host that asks whether it has a control to show.</summary>
    public IGame Game => _loaded.Game;

    /// <summary>
    /// Runs the game for one frame, with what <paramref name="input"/> reads. The frame ends after the
    /// update, and the host catches a throw, so the frame ends either way.
    /// </summary>
    public void Update(double seconds, IGameInputFeed? input)
    {
        _host.Update(input?.Read() ?? NoInput, seconds);
        input?.EndFrame();
    }

    /// <summary>Runs code of the game's that is not an update, and ends the game if it throws.</summary>
    public void Guard(string name, Action call) => _host.Guard(name, call);

    public void Dispose()
    {
        _playback.Edited -= _host.ApplyEdit;
        _host.SceneChanged -= _playback.Apply;
        _host.Stop();
        _loaded.Dispose();
    }
}

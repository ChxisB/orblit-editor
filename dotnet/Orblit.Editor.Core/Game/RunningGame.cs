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
    // The viewport forwards the keys and the pointer in a later step. Until then the game sees none.
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

    public void Update(double seconds) => _host.Update(NoInput, seconds);

    public void Dispose()
    {
        _playback.Edited -= _host.ApplyEdit;
        _host.SceneChanged -= _playback.Apply;
        _host.Stop();
        _loaded.Dispose();
    }
}

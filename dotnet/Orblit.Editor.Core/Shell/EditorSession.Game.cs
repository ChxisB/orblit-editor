namespace Orblit.Editor.Core;

using Orblit.Game;

// Playing a project's game: build it, load it, and run it in the playing copy of the scene.
public sealed partial class EditorSession
{
    private CancellationTokenSource? _building;
    private RunningGame? _game;

    /// <summary>Whether the project may build and run its game. Nothing is trusted until the editor is given an answer.</summary>
    public IProjectTrust Trust { get; init; } = new NoProjectTrust();

    /// <summary>
    /// Asked with the project folder when Play is pressed on a game that is not trusted yet. A window sets
    /// it to a dialog. Without one the answer is no, so a session with nobody to ask never runs a
    /// project's code.
    /// </summary>
    public Func<string, Task<bool>> AskToTrust { get; set; } = _ => Task.FromResult(false);

    /// <summary>
    /// Where the playing game reads the keys and the pointer. The game view sets it while it is open. Without
    /// one the game sees nothing held.
    /// </summary>
    public IGameInputFeed? InputFeed { get; set; }

    /// <summary>The game that is running, or null when none is. A window asks it whether it has a control to show.</summary>
    public IGame? PlayingGame => _game?.Game;

    public GameBuilder Builder { get; init; } = new(new DotnetBuild());

    public GameLoader Loader { get; init; } = new();

    /// <summary>Whether the game is being built. Play waits for the build, and Stop cancels it.</summary>
    public bool Building => _building is not null;

    /// <summary>
    /// Takes back the trust given to this project. Its game stops if it is running and a build under way
    /// is cancelled, because code that is no longer trusted should not start or go on running. The next
    /// Play asks again.
    /// </summary>
    public void StopTrusting()
    {
        _building?.Cancel();
        if (_game is not null)
        {
            StopPlayback();
        }

        try
        {
            Trust.Revoke(ProjectFolder);
            Say("This project is no longer trusted to run its code.", SayLevel.Info);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say("The trust in this project could not be taken back.", SayLevel.Error, error.Message);
        }
    }

    /// <summary>
    /// Runs code of the playing game's that is not an update, such as asking an interface game for its
    /// control. A throw ends Play, as one from an update does. Does nothing when no game is running.
    /// </summary>
    public void GuardGame(string name, Action call)
    {
        _game?.Guard(name, call);
        if (_game?.Fault is not null)
        {
            StopPlayback();
        }
    }

    // The project's game, or the reason it cannot be played. Nothing of the game's runs before the
    // person has trusted the project.
    private async Task PlayGame(Project project)
    {
        if (project.Game is not { } entry)
        {
            Say($"The game is not set up. {project.GameProblem}", SayLevel.Error);
            return;
        }

        // The question is inside the try, because a dialog that throws should end Play with a message and
        // not escape from the press that started it.
        try
        {
            if (await ObtainTrust() && await BuildGame(entry) is { AssemblyPath: { } assembly })
            {
                StartGame(assembly, entry.Type);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Say("Play stopped before the game started.", SayLevel.Error, error.ToString());
        }
    }

    // True when the project is trusted already or the person says yes. A no is said in the log.
    private async Task<bool> ObtainTrust()
    {
        var folder = ProjectFolder;
        if (Trust.IsTrusted(folder))
        {
            return true;
        }

        if (!await AskToTrust(folder))
        {
            Say(
                "The game did not start. This project has not been trusted to run its code.",
                SayLevel.Warning,
                "Opening a project never builds or runs it. Trust the project to play its game.");
            return false;
        }

        GrantTrust(folder);
        return true;
    }

    // The person said yes, so this run goes ahead even when the answer cannot be written down. Only the
    // next run asks again.
    private void GrantTrust(string folder)
    {
        try
        {
            Trust.Grant(folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say("The choice to trust this project could not be kept, so Play will ask again.", SayLevel.Warning, error.Message);
        }
    }

    // The build runs off the UI thread, because starting a process is slow on some machines. Stop
    // cancels it, which also covers a window that closes while it runs.
    private async Task<GameBuild?> BuildGame(GameEntry entry)
    {
        using var cancel = new CancellationTokenSource();
        _building = cancel;
        PlaybackChanged?.Invoke();
        try
        {
            var folder = ProjectFolder;
            var built = await Task.Run(() => Builder.BuildAsync(folder, entry, cancel.Token), cancel.Token);
            cancel.Token.ThrowIfCancellationRequested();
            if (built.Succeeded)
            {
                return built;
            }

            Say("The game did not build.", SayLevel.Error, built.Output);
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        finally
        {
            _building = null;
            PlaybackChanged?.Invoke();
        }
    }

    private void StartGame(string assembly, string type)
    {
        PauseTimelines();
        var playback = NewPlayback();
        LoadedGame loaded;
        try
        {
            loaded = Loader.Load(assembly, type);
        }
        catch (GameLoadException error)
        {
            Say($"The game could not be loaded. {error.Message}", SayLevel.Error);
            return;
        }

        var game = new RunningGame(loaded, playback, ProjectFolder, new GameLog(this));
        game.Start();
        if (game.Fault is not null)
        {
            game.Dispose();
            return;
        }

        Playback = playback;
        _game = game;
        Show();
    }

    // The host catches whatever the game throws and ends the game. The log keeps the stack, which is
    // how the person finds the line of their own code.
    private sealed class GameLog(EditorSession session) : IGameLog
    {
        public void Say(string message) => session.Say(message, SayLevel.Info);

        public void Error(string message, Exception error) =>
            session.Say($"{message} Play has stopped.", SayLevel.Error, error.ToString());
    }
}

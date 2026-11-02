namespace Orblit.Editor.Core;

using Orblit.Game;

// Playing a project's game: build it, load it, and run it in the playing copy of the scene.
public sealed partial class EditorSession
{
    private CancellationTokenSource? _building;
    private RunningGame? _game;

    /// <summary>Whether the project may build and run its game. Nothing is trusted until the editor is given an answer.</summary>
    public IProjectTrust Trust { get; init; } = new NoProjectTrust();

    public GameBuilder Builder { get; init; } = new(new DotnetBuild());

    public GameLoader Loader { get; init; } = new();

    /// <summary>Whether the game is being built. Play waits for the build, and Stop cancels it.</summary>
    public bool Building => _building is not null;

    // The project's game, or the reason it cannot be played. Nothing of the game's runs before the
    // person has trusted the project.
    private async Task PlayGame(Project project)
    {
        if (project.Game is not { } entry)
        {
            Say($"The game is not set up. {project.GameProblem}", SayLevel.Error);
            return;
        }

        if (!Trust.IsTrusted(ProjectFolder))
        {
            Say(
                "The game did not start. This project has not been trusted to run its code.",
                SayLevel.Warning,
                "Opening a project never builds or runs it. Trust the project to play its game.");
            return;
        }

        try
        {
            if (await BuildGame(entry) is { AssemblyPath: { } assembly })
            {
                StartGame(assembly, entry.Type);
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Say("Play stopped before the game started.", SayLevel.Error, error.ToString());
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

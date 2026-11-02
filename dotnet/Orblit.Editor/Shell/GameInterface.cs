using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Game;
using Orblit.Game.Avalonia;

/// <summary>
/// The control an interface game makes. It is asked for once when the game starts, kept for as long as the
/// game runs and handed to whichever game view is open.
/// </summary>
/// <remarks>
/// The window owns it and the views only show it, because a view can be closed and opened again while the
/// game runs, and the game is asked for its control once. When the game stops the control is let go, with
/// its data context, because anything that still points at it keeps the game's code from unloading.
/// </remarks>
internal sealed class GameInterface
{
    private readonly EditorSession _session;
    private IGame? _seen;

    public GameInterface(EditorSession session)
    {
        _session = session;
        _session.PlaybackChanged += Follow;
    }

    /// <summary>The playing game's control, or null when it has none or no game plays.</summary>
    public Control? Control { get; private set; }

    /// <summary>Raised when <see cref="Control"/> becomes another control or none.</summary>
    public event Action? Changed;

    /// <summary>Stops listening and lets the control go, for a window that is closing.</summary>
    public void Detach()
    {
        _session.PlaybackChanged -= Follow;
        Release();
    }

    // Raised for each frame that plays, so a game that has already been seen costs a comparison.
    private void Follow()
    {
        var game = _session.PlayingGame;
        if (ReferenceEquals(game, _seen))
        {
            return;
        }

        Release();
        _seen = game;
        if (game is IInterfaceGame interfaceGame)
        {
            _session.GuardGame("CreateInterface", () => Control = interfaceGame.CreateInterface());
        }

        Changed?.Invoke();
    }

    private void Release()
    {
        Control?.DataContext = null;
        Control = null;
        _seen = null;
    }
}

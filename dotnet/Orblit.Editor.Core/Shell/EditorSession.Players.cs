namespace Orblit.Editor.Core;

// Playing a game with more than one player: how many, and which one the game view shows.
public sealed partial class EditorSession
{
    /// <summary>The most players one Play can start. Each is a whole copy of the game and its scene.</summary>
    public const int MaxPlayers = 4;

    private int _playerCount = 1;

    /// <summary>
    /// How many players the next Play starts, from one to <see cref="MaxPlayers"/>. A game that has
    /// started keeps the count it started with.
    /// </summary>
    public int PlayerCount
    {
        get => _playerCount;
        set
        {
            var count = Math.Clamp(value, 1, MaxPlayers);
            if (count != _playerCount)
            {
                _playerCount = count;
                PlaybackChanged?.Invoke();
            }
        }
    }

    /// <summary>How many players are running, or zero when no game is.</summary>
    public int PlayersRunning => _games?.Count ?? 0;

    /// <summary>The player the game view shows, counted from zero. The host is zero.</summary>
    public int ShownPlayer { get; private set; }

    /// <summary>The copy of the scene that <see cref="ShownPlayer"/> plays in. It is <see cref="Playback"/> when there is one player.</summary>
    public ScenePlayback? ShownPlayback => _games?[ShownPlayer].Playback ?? Playback;

    /// <summary>Shows another player. Does nothing for a player that is not running or is shown already.</summary>
    public void ShowPlayer(int index)
    {
        if (_games is null || index < 0 || index >= _games.Count || index == ShownPlayer)
        {
            return;
        }

        ShownPlayer = index;
        PlaybackChanged?.Invoke();
    }
}

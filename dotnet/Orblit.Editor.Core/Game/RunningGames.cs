namespace Orblit.Editor.Core;

/// <summary>Every game of one Play: the host's first, then a guest for each other player.</summary>
/// <remarks>
/// With one player there is only the host and no net. With more, the host runs the server end of the
/// game and each guest a client end. Disposing stops every game and lets each assembly unload.
/// </remarks>
internal sealed class RunningGames : IDisposable
{
    private readonly RunningGame _host;
    private readonly List<RunningGame> _guests = [];
    private NetPlay? _net;

    private RunningGames(RunningGame host) => _host = host;

    public RunningGame Host => _host;

    public int Count => 1 + _guests.Count;

    public RunningGame this[int index] => index == 0 ? _host : _guests[index - 1];

    /// <summary>The copy of the scene each player plays in, in the order of the players.</summary>
    public IEnumerable<ScenePlayback> Playbacks => _guests.Select(guest => guest.Playback).Prepend(_host.Playback);

    /// <summary>The exception that ended the first game to throw, or null if none did.</summary>
    public Exception? Fault => _host.Fault ?? _guests.Select(guest => guest.Fault).FirstOrDefault(fault => fault is not null);

    /// <summary>
    /// Starts a game for each of <paramref name="players"/>, or none. <paramref name="make"/> makes the
    /// game of the player with the number it is given, without starting it, and returns null when it
    /// cannot, having said why. A game that throws as it starts ends the ones that started before it. The
    /// reason is in the log then, and the result is null.
    /// </summary>
    public static RunningGames? Start(int players, Func<int, RunningGame?> make)
    {
        if (make(1) is not { } host)
        {
            return null;
        }

        var games = new RunningGames(host);
        if (games.StartAll(players, make))
        {
            return games;
        }

        games.Dispose();
        return null;
    }

    /// <summary>
    /// Runs one frame of every game in the order of the players, and stops at the first that throws. Only
    /// the player at <paramref name="shown"/> is given <paramref name="input"/>. The others see nothing held.
    /// </summary>
    public void Update(double seconds, int shown, IGameInputFeed? input)
    {
        for (var index = 0; index < Count; index++)
        {
            this[index].Update(seconds, index == shown ? input : null);
            if (this[index].Fault is not null)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        foreach (var guest in _guests)
        {
            guest.Dispose();
        }

        _host.Dispose();
        _net?.Dispose();
    }

    private bool StartAll(int players, Func<int, RunningGame?> make)
    {
        var net = players > 1 ? new NetPlay(_host.Game) : null;
        _net = net;
        if (net is not null)
        {
            _host.UseNet(net.Server);
        }

        _host.Start();
        return _host.Fault is null && (net is null || StartGuests(net, players, make));
    }

    private bool StartGuests(NetPlay net, int players, Func<int, RunningGame?> make)
    {
        for (var number = 2; number <= players; number++)
        {
            if (make(number) is not { } guest)
            {
                return false;
            }

            _guests.Add(guest);
            net.Join(NetPlay.ClientId(number), guest);
            if (guest.Fault is not null)
            {
                return false;
            }
        }

        net.GiveOwners(_host.Scene);
        return true;
    }
}

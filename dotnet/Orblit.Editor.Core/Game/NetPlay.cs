namespace Orblit.Editor.Core;

using Orblit.Game;
using Orblit.Net;
using Orblit.Net.Game;
using Doc = Orblit.Scene;

/// <summary>
/// The wire of a Play with more than one player: the server end the host's game runs on, and a client
/// end over a loopback link for each guest.
/// </summary>
/// <remarks>
/// The editor is the one place that makes these classes. A game sees only <see cref="IGameNet"/>.
/// Disposing stops the ends from listening. The links hold nothing else open, so they are not closed.
/// </remarks>
internal sealed class NetPlay : IDisposable
{
    private readonly NetworkedServer _server;
    private readonly NetVarSchema _vars;
    private readonly List<NetworkedClient> _clients = [];
    private readonly List<string> _guests = [];

    /// <param name="host">The game of the host. If it names variables, every end is built from them.</param>
    public NetPlay(IGame host)
    {
        _vars = host is INetGame net ? net.NetVars : NetVarSchema.Empty;
        _server = new NetworkedServer(_vars);
    }

    public IGameNet Server => _server;

    /// <summary>What the server calls the guest that is player <paramref name="number"/>. The host is player 1.</summary>
    public static string ClientId(int number) => $"player{number}";

    /// <summary>
    /// Makes a client of the guest's game, starts the game on it and connects it to the server. A guest
    /// that throws as it starts is still connected. The caller sees its fault and ends the Play.
    /// </summary>
    public void Join(string clientId, RunningGame guest)
    {
        var link = new LoopbackLink();
        var client = new NetworkedClient(new NetworkedClientOptions { ClientId = clientId, Transport = link.Client, Vars = _vars });
        _clients.Add(client);
        guest.UseNet(client);
        guest.Start();
        _server.AddClient(clientId, link.Host);
        _guests.Add(clientId);
    }

    /// <summary>Gives the guests that joined their entities. See <see cref="OwnersFor"/>.</summary>
    public void GiveOwners(Doc.SceneDocument scene)
    {
        foreach (var (entity, client) in OwnersFor(scene, _guests))
        {
            _server.Assign(entity, client);
        }
    }

    /// <summary>
    /// The entity each guest owns. The entities with owner authority are taken in the order of the scene.
    /// The first stays with the host, whose game moves it. Each later one goes to the next guest in the
    /// order they joined. One beyond the guests stays with the host.
    /// </summary>
    public static IReadOnlyList<(string Entity, string Client)> OwnersFor(Doc.SceneDocument scene, IReadOnlyList<string> guests) =>
    [
        .. scene.Entities
            .Where(entity => entity[Doc.SceneComponents.Net] is Doc.NetComponent { Authority: Doc.NetAuthority.Owner })
            .Skip(1)
            .Zip(guests, (entity, client) => (entity.Id, client)),
    ];

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        _server.Dispose();
    }
}

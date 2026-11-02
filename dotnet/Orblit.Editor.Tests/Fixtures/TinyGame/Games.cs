using Avalonia.Controls;
using Orblit.Game;
using Orblit.Game.Avalonia;
using Orblit.Net.Game;
using Orblit.Scene;
using Orblit.VectorMath;
using TinyPart;

namespace TinyGame;

// Several small types, one for each way the loader can say no and one for the way it says yes.
public abstract class Idle : IGame
{
    public virtual void Start(GameContext context)
    {
    }

    public virtual void Update(GameContext context, double seconds)
    {
    }

    public virtual void Stop(GameContext context)
    {
    }
}

// A game that puts one crate in the scene when it starts and then says something about itself by moving it.
public abstract class Crated : Idle
{
    public override void Start(GameContext context) =>
        context.Scene.Apply(new SceneDiff([new AddEntity(new SceneEntity(Names.Crate, Names.Crate).ToJson())]));

    protected static void PutCrateAt(GameContext context, double x) => PutCrateAt(context, new Vector3(x, 0, 0));

    protected static void PutCrateAt(GameContext context, Vector3 at)
    {
        var document = context.Scene.Document;
        var crate = document[Names.Crate]!;
        var after = document.WithEntity(Names.Crate, crate.WithComponent(SceneComponents.Transform, new TransformComponent(at)));
        context.Scene.Apply(SceneDiff.Between(document, after));
    }
}

public sealed class Tiny : Crated;

// The crate's x is the number of frames the game has seen, so a test counts them.
public sealed class Mover : Crated
{
    private int _frames;

    public override void Update(GameContext context, double seconds) => PutCrateAt(context, ++_frames);
}

// The crate's x is the camera's x in the game's own scene, so a test sees an edit reach the game.
public sealed class Echo : Crated
{
    public override void Update(GameContext context, double seconds)
    {
        var camera = (TransformComponent)context.Scene.Document["camera"]![SceneComponents.Transform]!;
        PutCrateAt(context, camera.Position.X);
    }
}

// The crate's x is 1 while the G key is held and 0 otherwise, so a test sees which keys the game was given.
public sealed class KeyReader : Crated
{
    public override void Update(GameContext context, double seconds) =>
        PutCrateAt(context, context.Input.Controls.Keys.Contains("KeyG") ? 1 : 0);
}

// A click puts the crate where its ray points. The crate's y counts the updates that saw a click.
public sealed class PointerReader : Crated
{
    private int _clicks;

    public override void Update(GameContext context, double seconds)
    {
        if (context.Input.Pointer is { Pressed: true } pointer)
        {
            PutCrateAt(context, new Vector3(pointer.Direction.X, ++_clicks, pointer.Direction.Z));
        }
    }
}

// A game with a control of its own. The control's data context is the game, so a game that was not let go
// of shows up as an assembly that does not unload.
public sealed class Faced : Crated, IInterfaceGame
{
    public int Made { get; private set; }

    public Control CreateInterface()
    {
        Made++;
        return new TextBlock { Name = "game_interface_label", Text = "Crates", DataContext = this };
    }
}

public sealed class XamlFaced : Crated, IInterfaceGame
{
    public Control CreateInterface() => new Faces();
}

public sealed class BreaksMakingItsInterface : Idle, IInterfaceGame
{
    public Control CreateInterface() => throw new InvalidOperationException("the label is missing");
}

public sealed class FallsInUpdate : Idle
{
    public override void Update(GameContext context, double seconds) => throw new InvalidOperationException("the crate fell");
}

public sealed class NotAGame;

public sealed class NeedsAnArgument(int size) : Idle
{
    public int Size { get; } = size;
}

public sealed class Breaks : Idle
{
    public Breaks() => throw new InvalidOperationException("the crate is missing");
}

// Two players and a crate, put in on every copy as a scene file would hold them. The server moves the crate
// once and sets its score. The client that owns p2 moves it. A client copies the score to the readout, so a
// test sees that the schema reached the client.
public sealed class Replicated : Idle, INetGame
{
    private bool _placed;

    public NetVarSchema NetVars { get; } = NetVarSchema.Empty.With<int>("score");

    public override void Start(GameContext context) => context.Scene.Apply(new SceneDiff(
    [
        Tagged("p1", null, new NetComponent(NetAuthority.Owner)),
        Tagged("p2", "p1", new NetComponent(NetAuthority.Owner)),
        Tagged("p3", "p2", new NetComponent(NetAuthority.Owner)),
        Tagged("crate", "p3", new NetComponent(NetAuthority.Server, ["transform", "score"])),
        Tagged("readout", "crate", null),
    ]));

    public override void Update(GameContext context, double seconds)
    {
        var net = context.Net;
        if (net.IsServer && !_placed)
        {
            _placed = true;
            PutAt(context, "crate", 7);
            net.Var<int>("crate", "score").Value = 9;
        }

        if (net.IsClient)
        {
            PutAt(context, "readout", net.Var<int>("crate", "score").Value);
        }

        foreach (var id in new[] { "p1", "p2", "p3" })
        {
            if (net.LocalClient is not null && net.OwnerOf(id) == net.LocalClient)
            {
                PutAt(context, id, 3);
            }
        }
    }

    // An entity added with no neighbour goes to the head of the scene, so each is placed after the last.
    private static AddEntity Tagged(string id, string? after, NetComponent? tag)
    {
        var entity = new SceneEntity(id, id);
        return new AddEntity((tag is null ? entity : entity.WithComponent(SceneComponents.Net, tag)).ToJson(), after);
    }

    private static void PutAt(GameContext context, string id, double x)
    {
        var document = context.Scene.Document;
        var after = document.WithEntity(id, document[id]!.WithComponent(SceneComponents.Transform, new TransformComponent(new Vector3(x, 0, 0))));
        context.Scene.Apply(SceneDiff.Between(document, after));
    }
}

// Throws on a client and nowhere else, so a test sees which player is named.
public sealed class FallsOnAClient : Idle
{
    public override void Update(GameContext context, double seconds)
    {
        if (context.Net.IsClient)
        {
            throw new InvalidOperationException("the client fell");
        }
    }
}

// Says it updated, except on the second player, which throws first. A test sees that the players after it
// do not run in that frame.
public sealed class FallsOnTheSecondPlayer : Idle
{
    public override void Update(GameContext context, double seconds)
    {
        if (context.Net.LocalClient == "player2")
        {
            throw new InvalidOperationException("the second player fell");
        }

        context.Log.Say("updated");
    }
}

// Throws as it starts on the server and says it is ready anywhere else, so a test sees that no guest starts
// after a host that cannot.
public sealed class FallsStartingOnTheServer : Idle
{
    public override void Start(GameContext context)
    {
        if (context.Net.IsServer)
        {
            throw new InvalidOperationException("the server could not start");
        }

        context.Log.Say("ready");
    }
}

// Throws as it starts on a client, so a test sees that a player that cannot start stops the rest from starting.
public sealed class FallsStartingOnAClient : Idle
{
    public override void Start(GameContext context)
    {
        if (context.Net.IsClient)
        {
            throw new InvalidOperationException("the client could not start");
        }
    }
}

// Says one line as it starts, so a test sees which player the line is put down to.
public sealed class Talker : Idle
{
    public override void Start(GameContext context) => context.Log.Say("ready");
}

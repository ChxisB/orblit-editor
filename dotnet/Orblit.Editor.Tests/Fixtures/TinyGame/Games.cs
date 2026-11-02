using Avalonia.Controls;
using Orblit.Game;
using Orblit.Game.Avalonia;
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

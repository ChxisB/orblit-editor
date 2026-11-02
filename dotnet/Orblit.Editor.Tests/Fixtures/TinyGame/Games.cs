using Orblit.Game;
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

    protected static void PutCrateAt(GameContext context, double x)
    {
        var document = context.Scene.Document;
        var crate = document[Names.Crate]!;
        var after = document.WithEntity(Names.Crate, crate.WithComponent(SceneComponents.Transform, new TransformComponent(new Vector3(x, 0, 0))));
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

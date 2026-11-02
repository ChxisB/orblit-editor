using Orblit.Game;
using Orblit.Scene;
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

public sealed class Tiny : Idle
{
    public override void Start(GameContext context) =>
        context.Scene.Apply(new SceneDiff([new AddEntity(new SceneEntity(Names.Crate, Names.Crate).ToJson())]));
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

using Orblit.Filament;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Game;
using Orblit.Game.Avalonia;
using Orblit.VectorMath;

/// <summary>The keys and the pointer over the game view, as the playing game reads them.</summary>
/// <param name="source">What the player does over the picture.</param>
/// <param name="camera">The camera the picture is drawn through, or null when the scene has none.</param>
internal sealed class GameViewInput(GameInputSource source, Func<RenderCamera?> camera) : IGameInputFeed
{
    // The source cannot read keys without a camera. A scene with none has no picture to point into, so the
    // keys are read through this one and the pointer is dropped.
    private static readonly RenderCamera NoCamera = new() { Position = new Vector3(0, 0, 0), Target = new Vector3(0, 0, -1) };

    public GameInput Read() =>
        camera() is { } looking ? source.Read(looking) : source.Read(NoCamera) with { Pointer = null };

    public void EndFrame() => source.EndFrame();
}

namespace Orblit.Editor.Core;

using Orblit.Game;

/// <summary>
/// What the player is doing, for the game that plays. The game view supplies it, because the keys and
/// the pointer are the view's to read.
/// </summary>
/// <remarks>
/// The session reads once for each update and ends the frame after it, which is what makes a click one
/// update and not many.
/// </remarks>
public interface IGameInputFeed
{
    GameInput Read();

    /// <summary>Forgets the click, the wheel and the drag so far. Held keys stay held.</summary>
    void EndFrame();
}

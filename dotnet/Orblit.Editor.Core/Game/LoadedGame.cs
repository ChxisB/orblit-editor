namespace Orblit.Editor.Core;

using Orblit.Game;

/// <summary>A game made from a built assembly, and the context that holds it.</summary>
/// <remarks>
/// Dispose lets the context unload. Nothing may keep the game, its types or its objects after that,
/// including the scene the game changed, or the unload waits for ever.
/// </remarks>
public sealed class LoadedGame : IDisposable
{
    private readonly GameLoadContext _context;
    private readonly IGame _game;
    private bool _disposed;

    internal LoadedGame(GameLoadContext context, IGame game)
    {
        _context = context;
        _game = game;
    }

    public IGame Game => _disposed ? throw new ObjectDisposedException(nameof(LoadedGame)) : _game;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _context.Unload();
    }
}

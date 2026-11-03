namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.Filament;

/// <summary>
/// The render scene of the last frame, kept until something it was built from changes.
/// </summary>
/// <remarks>
/// Building one reads every object in the scene, and a view that only moved its camera has no
/// object to read again. The owner says when the scene changed by calling <see cref="Forget"/>.
/// What the cache can see for itself is a new scene, a new shared scene, and the clock of a scene
/// that moves on its own: a still scene looks the same at any clock, so its clock is not part of
/// what it is built from.
/// </remarks>
internal sealed class RenderSceneCache
{
    private RenderScene? _kept;
    private Made _made;

    /// <summary>The next <see cref="Get"/> builds the scene again.</summary>
    public void Forget() => _kept = null;

    /// <summary>The kept scene, or the one <paramref name="build"/> makes when there is none to keep.</summary>
    public RenderScene Get(EditorScene? scene, EditorScene? shared, Func<RenderScene> build)
    {
        var made = new Made(scene, shared, scene is { IsAnimated: true } ? scene.Clock : null);
        if (_kept is null || made != _made)
        {
            _kept = build();
            _made = made;
        }

        return _kept;
    }

    private readonly record struct Made(EditorScene? Scene, EditorScene? Shared, double? Clock);
}

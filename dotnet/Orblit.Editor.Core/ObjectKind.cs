namespace Orblit.Editor.Core;

/// <summary>
/// What kind of thing an object is, which decides what components it has and therefore what
/// the inspector shows.
/// </summary>
public enum ObjectKind
{
    Scene,
    Mesh,
    Light,
    Camera,
    Group,
    Weather,
    Canvas,
    Shape,
}

/// <summary>
/// An object being dragged.
/// </summary>
/// <remarks>
/// A type of its own rather than the bare id, because an asset path is also a string and the
/// outliner, the viewport and the project browser all take drops. With one type for both,
/// dragging a crate into the browser would look exactly like dragging a file.
/// </remarks>
/// <param name="Id">The object being dragged.</param>
/// <param name="Name">What to call it while it is in the air, and what to name the file it lands in.</param>
public sealed record ObjectDrag(string Id, string Name);

/// <summary>Something an edit could not do, worth saying out loud.</summary>
public sealed class SceneException(string message) : InvalidOperationException(message);

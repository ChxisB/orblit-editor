namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.Editor.Core;
using Orblit.Mesh;

/// <summary>
/// Geometry built in the editor, on its way to the renderer.
/// </summary>
/// <remarks>
/// An object is drawn either as the built-in cube or from a glTF file, and there is no third way in.
/// So a shape becomes a file: written once, kept while it has not changed, and handed to the
/// renderer as any model exported from Blender would be. One loader, one instancing pool, one shadow
/// pass, and no second path through the renderer for geometry that came from here.
/// </remarks>
/// <param name="projectRoot">The project folder the files are written inside.</param>
public sealed class GeometryStore(string projectRoot)
{
    // How long a "yes, it is still there" is trusted for. Somebody deleting a build folder under a
    // running editor can wait two seconds to be noticed.
    private const long TrustMilliseconds = 2000;

    private readonly Dictionary<string, Written> _written = [];

    // A monotonic clock rather than the wall clock, which is the expensive part of doing nothing
    // when every shape in the project is asked about on every change.
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// Where the written files go: inside the project, out of the way, and disposable. Nothing here is
    /// authored, so every one of them can be written again from the scene.
    /// </summary>
    public string Folder => Path.Combine(projectRoot, ".orblit", "geometry");

    /// <summary>
    /// The file an object's geometry lives in, writing it if it has changed. A path relative to the
    /// project, or null when the object has no geometry or the write failed.
    /// </summary>
    /// <remarks>
    /// A failed write is not worth an exception: the object draws as a cube, which is visible and
    /// recoverable, and the alternative is an editor that will not open a scene because a temporary
    /// folder was read-only.
    /// </remarks>
    public string? PathFor(SceneObject item)
    {
        if (item.CurrentMesh is not { IsEmpty: false } mesh)
        {
            return null;
        }

        var materials = HashOf(item.Surfaces);
        var now = _clock.ElapsedMilliseconds;
        if (_written.TryGetValue(item.Id, out var was) && IsCurrent(was, mesh, materials) && StillThere(item.Id, was, now))
        {
            return was.Relative;
        }

        var relative = Path.Combine(".orblit", "geometry", $"{item.Id}.glb");
        if (!Write(item, mesh, relative))
        {
            return null;
        }

        _written[item.Id] = new Written(mesh, materials, relative, now);
        return relative;
    }

    /// <summary>Whether anything has to be written for this object.</summary>
    public bool IsStale(SceneObject item) =>
        item.CurrentMesh is { } mesh
        && (!_written.TryGetValue(item.Id, out var was) || !IsCurrent(was, mesh, HashOf(item.Surfaces)));

    /// <summary>Forgets an object, so the next ask writes again.</summary>
    public void Forget(string id) => _written.Remove(id);

    /// <summary>Throws away every file, for when a project is closed.</summary>
    public void Clear()
    {
        _written.Clear();
        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A folder that will not delete is a folder with some stale files in it, which the next
            // write replaces anyway.
        }
    }

    // The mesh itself, not a summary of it. Geometry is replaced when it changes, so "is this the same
    // geometry" is a pointer compare. A change is every frame of a drag.
    private static bool IsCurrent(Written was, Mesh mesh, int materials) =>
        ReferenceEquals(was.Mesh, mesh) && was.Materials == materials;

    // The material slots, as one number. These are edited in place, so identity says nothing and
    // they are summed every time. There are a handful of them, which is the difference.
    private static int HashOf(List<Surface> surfaces)
    {
        unchecked
        {
            return surfaces.Aggregate(surfaces.Count, (total, one) => (total * 31) + one.GetHashCode());
        }
    }

    // Written already. Whether it is still there is worth asking, but not sixty times a second.
    private bool StillThere(string id, Written was, long now)
    {
        if (now - was.SeenAt < TrustMilliseconds)
        {
            return true;
        }

        if (!File.Exists(Path.Combine(projectRoot, was.Relative)))
        {
            return false;
        }

        _written[id] = was with { SeenAt = now };
        return true;
    }

    private bool Write(SceneObject item, Mesh mesh, string relative)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(
                Path.Combine(projectRoot, relative),
                mesh.ToGlb(item.Name, [.. item.Surfaces.Select(one => one.ToGlb())]));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed record Written(Mesh Mesh, int Materials, string Relative, long SeenAt);
}

namespace Orblit.Editor.Core;

using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// How big an imported model says it is.
/// </summary>
/// <remarks>
/// The editor does not hold the geometry of a model the renderer loaded. It knows a path and
/// nothing else. But every glTF file carries the minimum and maximum of its own positions,
/// precisely so a reader can frame, cull or pick against a file it has not decoded, and reading
/// that is a few hundred bytes off the front of the file. Without it every imported model is
/// picked and outlined as a two-metre cube whatever it actually is.
/// </remarks>
/// <param name="projectRoot">The directory a model's relative path is read from.</param>
public sealed class ModelBounds(string projectRoot)
{
    // A model that cannot be read is remembered as unreadable, because a scene with forty objects
    // naming a missing file would otherwise read the disk forty times a frame.
    private readonly Dictionary<string, (Vector3 Min, Vector3 Max)?> _known = [];

    /// <summary>What <paramref name="item"/> occupies, or null when it has no file or the file says nothing.</summary>
    public (Vector3 Min, Vector3 Max)? Of(SceneObject item)
    {
        if (item.MeshAsset is not { Length: > 0 } asset)
        {
            return null;
        }

        if (_known.TryGetValue(asset, out var known))
        {
            return known;
        }

        var found = Read(Path.IsPathRooted(asset) ? asset : Path.Combine(projectRoot, asset));
        _known[asset] = found;
        return found;
    }

    /// <summary>Forgets one file, for when it has been written again.</summary>
    public void Forget(string asset) => _known.Remove(asset);

    public void Clear() => _known.Clear();

    // Both glTF containers carry the figure in the document itself, so neither has its buffers
    // read. A .gltf beside its textures is how most model libraries publish, and leaving it out
    // would make the commonest kind of model the one that cannot say how big it is. An .obj has
    // no such figure, so its points are read.
    private static (Vector3 Min, Vector3 Max)? Read(string path)
    {
        try
        {
            return !File.Exists(path)
                ? null
                : Path.GetExtension(path).ToLowerInvariant() switch
                {
                    ".glb" => MeshExport.BoundsOfGlb(File.ReadAllBytes(path)),
                    ".gltf" => MeshExport.BoundsOfGltf(File.ReadAllText(path)),
                    ".obj" => ObjFiles.BoundsOfObj(File.ReadAllBytes(path)),
                    _ => null,
                };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

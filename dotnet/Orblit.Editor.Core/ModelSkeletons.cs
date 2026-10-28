namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>
/// The skeleton an imported model says it has.
/// </summary>
/// <remarks>
/// Read off the file for the same reason <see cref="ModelBounds"/> reads its bounds: the renderer owns the
/// loaded model and the editor knows a path. The bones are named as the clips imported from the same file
/// name them, so a clip's bone channels land on the bones of the skeleton read here.
/// </remarks>
/// <param name="projectRoot">The directory a model's relative path is read from.</param>
public sealed class ModelSkeletons(string projectRoot)
{
    // Misses are kept too, so a scene naming a missing file does not read the disk every frame.
    private readonly Dictionary<string, RestSkeleton?> _known = [];

    /// <summary>
    /// The skeleton of the model at project path <paramref name="asset"/>, or null when it has no file, the
    /// file has no skin or the file cannot be read.
    /// </summary>
    /// <remarks>A file with several skins answers the one with the most bones, which is the character and not a prop riding on it.</remarks>
    public RestSkeleton? OfAsset(string? asset)
    {
        if (string.IsNullOrEmpty(asset))
        {
            return null;
        }

        if (_known.TryGetValue(asset, out var known))
        {
            return known;
        }

        // Combine keeps a rooted path as it is, so an absolute path needs no case of its own.
        var found = Read(Path.Combine(projectRoot, asset));
        _known[asset] = found;
        return found;
    }

    /// <summary>Forgets one file, for when it has been written again.</summary>
    public void Forget(string asset) => _known.Remove(asset);

    private static RestSkeleton? Read(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        // A file that is not there is an IOException below.
        if (extension is not (".glb" or ".gltf"))
        {
            return null;
        }

        try
        {
            // The buffers a .gltf names are not passed. A rest skeleton is read from the nodes alone.
            var skins = GltfImport.Skeletons(File.ReadAllBytes(path));
            return skins.Count == 0 ? null : skins.Aggregate(static (best, skin) => skin.Length > best.Length ? skin : best);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ClipFormatException)
        {
            return null;
        }
    }
}

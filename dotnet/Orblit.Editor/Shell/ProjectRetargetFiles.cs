namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Motion;

/// <summary>Where the retarget dialog finds models and keeps bone maps: the project folder.</summary>
public sealed class ProjectRetargetFiles(AssetTree assets, ModelSkeletons skeletons) : IRetargetFiles
{
    /// <summary>The project folder a saved map goes in.</summary>
    public const string Folder = "retargets";

    public IReadOnlyList<string> Models =>
        assets.Find(static asset => asset.Kind == AssetKind.Mesh && Path.GetExtension(asset.Path).ToLowerInvariant() is ".glb" or ".gltf");

    public IReadOnlyList<string> Maps => assets.Find(static asset => asset.Kind == AssetKind.BoneMap);

    public RestSkeleton? SkeletonOf(string model) => skeletons.OfAsset(model);

    public BoneMapLoad ReadMap(string path)
    {
        try
        {
            return BoneMap.Decode(File.ReadAllText(Path.Combine(assets.Root, path)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new BoneMapFormatException(error.Message);
        }
    }

    public AssetMade WriteMap(string name, BoneMap map)
    {
        var directory = Path.Combine(assets.Root, Folder);
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new AssetMade(error.Message, null);
        }

        var made = assets.Write(directory, name + BoneMap.Extension, map.Encode());
        return made.Path is { } path ? made with { Path = assets.Relative(path) } : made;
    }
}

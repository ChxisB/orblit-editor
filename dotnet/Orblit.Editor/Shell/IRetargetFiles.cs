namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;

/// <summary>What the retarget dialog needs of a project: models to pick, maps to load and a place to keep a map.</summary>
public interface IRetargetFiles
{
    /// <summary>Project paths of the models that may have a skeleton.</summary>
    IReadOnlyList<string> Models { get; }

    /// <summary>Project paths of the bone maps.</summary>
    IReadOnlyList<string> Maps { get; }

    /// <summary>The skeleton of <paramref name="model"/>, or null for a file that has none.</summary>
    RestSkeleton? SkeletonOf(string model);

    /// <summary>The bone map at <paramref name="path"/>. Throws <see cref="BoneMapFormatException"/> for a file that cannot be read or is not one.</summary>
    BoneMapLoad ReadMap(string path);

    /// <summary>Writes <paramref name="map"/> as a new file called <paramref name="name"/> and answers its project path, or what went wrong.</summary>
    AssetMade WriteMap(string name, BoneMap map);
}

/// <summary>A clip made to move another model.</summary>
/// <param name="Clip">The clip, named for the model.</param>
/// <param name="Model">The project path of the model the clip was made to move.</param>
/// <param name="Problems">What of the clip could not be carried, as retargeting words it.</param>
public sealed record RetargetMade(ClipDocument Clip, string Model, IReadOnlyList<string> Problems);

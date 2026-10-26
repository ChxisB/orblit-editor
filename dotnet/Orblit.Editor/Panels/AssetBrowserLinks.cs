namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// What the asset browser asks the rest of the editor to do for it. Each is null where nothing can do
/// it, and the browser then leaves out the command that would ask.
/// </summary>
internal sealed record AssetBrowserLinks
{
    /// <summary>Opens a file, as opposed to a folder.</summary>
    public Action<Asset>? OnOpenAsset { get; init; }

    /// <summary>Told when the pick changes, with null when it is cleared. One click, not two.</summary>
    public Action<Asset?>? OnSelectAsset { get; init; }

    /// <summary>Says that a file operation failed, so the shell can show it.</summary>
    public Action<string>? OnProblem { get; init; }

    /// <summary>Compiles a source file. The menu offers it only for the files that can be built.</summary>
    public Action<Asset>? OnBuild { get; init; }

    /// <summary>Makes a prefab from an object dragged in, given the object's id and the folder it landed on.</summary>
    public Action<string, string>? OnMakePrefab { get; init; }
}

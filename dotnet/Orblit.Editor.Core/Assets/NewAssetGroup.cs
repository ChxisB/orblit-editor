namespace Orblit.Editor.Core;

/// <summary>How the things somebody can make are laid out on the menu.</summary>
/// <remarks>
/// A flat list of six was already a wall, and every kind added makes it worse. Grouped, the menu
/// answers the question somebody actually has (am I writing code, or shading something, or making
/// a scene) before it asks them to pick a file extension.
/// <para>
/// The commonest thing by far, a folder, stays at the top level: putting it behind a submenu would
/// be tidier and slower.
/// </para>
/// </remarks>
public sealed class NewAssetGroup
{
    public static readonly NewAssetGroup Script = new("Script", [NewAsset.Native, NewAsset.Header]);

    public static readonly NewAssetGroup Data = new("Data", [NewAsset.DataObject, NewAsset.Json]);

    public static readonly NewAssetGroup Shader = new("Shader", [NewAsset.Shader, NewAsset.UnlitShader]);

    /// <summary>The groups, in the order the menu shows them.</summary>
    public static readonly IReadOnlyList<NewAssetGroup> Values = [Script, Data, Shader];

    /// <summary>The kinds that sit on the menu itself rather than in a group.</summary>
    public static readonly IReadOnlyList<NewAsset> Loose =
        [NewAsset.Folder, NewAsset.Scene, NewAsset.Clip, NewAsset.Blend, NewAsset.Cutscene];

    private NewAssetGroup(string label, IReadOnlyList<NewAsset> members)
    {
        Label = label;
        Members = members;
    }

    public string Label { get; }

    public IReadOnlyList<NewAsset> Members { get; }
}

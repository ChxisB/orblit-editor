namespace Orblit.Editor.Core;

/// <summary>What an asset is, decided by its extension.</summary>
/// <remarks>
/// Extension rather than content, because the browser has to label a thousand files fast enough to
/// scroll, and opening each one to be certain is what makes a project window feel slow.
/// <para>
/// A class rather than an enum so the label sits with the kind. The icon does not: it is how a
/// window draws the kind, and a window keeps its own table.
/// </para>
/// </remarks>
public sealed class AssetKind
{
    public static readonly AssetKind Folder = new("Folder");
    public static readonly AssetKind Scene = new("Scene");
    public static readonly AssetKind Mesh = new("Mesh");
    public static readonly AssetKind Texture = new("Texture");
    public static readonly AssetKind Material = new("Material");
    public static readonly AssetKind Surface = new("Surface");
    public static readonly AssetKind Script = new("Script");
    public static readonly AssetKind Style = new("Stylesheet");
    public static readonly AssetKind Native = new("C++");
    public static readonly AssetKind Prefab = new("Prefab");
    public static readonly AssetKind DataObject = new("Data object");
    public static readonly AssetKind Canvas = new("Canvas");
    public static readonly AssetKind Clip = new("Clip");
    public static readonly AssetKind Blend = new("Blend");
    public static readonly AssetKind BoneMap = new("Bone map");
    public static readonly AssetKind Cutscene = new("Cutscene");
    public static readonly AssetKind Audio = new("Audio");
    public static readonly AssetKind Data = new("Data");
    public static readonly AssetKind Other = new("File");

    // Declared after the kinds, because static fields are set in the order they are written.
    private static readonly Dictionary<string, AssetKind> ByExtension = new()
    {
        [".oscene"] = Scene,
        [".gltf"] = Mesh,
        [".glb"] = Mesh,
        [".obj"] = Mesh,
        [".fbx"] = Mesh,
        [".png"] = Texture,
        [".jpg"] = Texture,
        [".jpeg"] = Texture,
        [".ktx2"] = Texture,
        [".hdr"] = Texture,
        [".exr"] = Texture,
        [".fmat"] = Material,
        [".mat"] = Material,
        [".oshader"] = Surface,
        [".oshaderinc"] = Surface,
        [".ts"] = Script,
        [".tsx"] = Script,
        [".js"] = Script,
        [".jsx"] = Script,
        [".dart"] = Script,
        [".css"] = Style,
        [".cpp"] = Native,
        [".cc"] = Native,
        [".h"] = Native,
        [".hpp"] = Native,
        [".oprefab"] = Prefab,
        [".odata"] = DataObject,
        [".oui"] = Canvas,
        [".oclip"] = Clip,
        [".oblend"] = Blend,
        [".obonemap"] = BoneMap,
        [".ocutscene"] = Cutscene,
        [".wav"] = Audio,
        [".mp3"] = Audio,
        [".ogg"] = Audio,
        [".json"] = Data,
        [".yaml"] = Data,
        [".yml"] = Data,
    };

    private AssetKind(string label) => Label = label;

    public string Label { get; }

    public static AssetKind Of(string path) =>
        ByExtension.GetValueOrDefault(AssetNames.Extension(path).ToLowerInvariant(), Other);
}

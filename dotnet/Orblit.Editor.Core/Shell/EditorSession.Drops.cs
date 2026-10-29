namespace Orblit.Editor.Core;

// A file dropped on the scene. What it does depends on what the file is: a scene opens, a mesh or a
// prefab is added, and a picture or a shader goes on whatever is selected.
public sealed partial class EditorSession
{
    /// <summary>Does what dropping the file at <paramref name="path"/> on the scene means for its kind.</summary>
    public void DropAsset(string path)
    {
        var kind = AssetKind.Of(path);
        if (kind == AssetKind.Scene)
        {
            OpenScene(path);
        }
        else if (kind == AssetKind.Prefab)
        {
            PlacePrefab(path);
        }
        else if (kind == AssetKind.DataObject)
        {
            AttachData(path);
        }
        else if (kind == AssetKind.Texture || kind == AssetKind.Surface)
        {
            ApplyMaterial(path);
        }
        else if (kind == AssetKind.Mesh)
        {
            AddMesh(path);
        }
        else
        {
            // No kind named, since "a audio" and "a c++" read badly and the file's name says what it is.
            Say(
                $"{Path.GetFileName(path)} cannot go in a scene. Meshes, prefabs and scenes can, and a texture or shader goes on whatever is selected.",
                SayLevel.Info);
        }
    }

    /// <summary>Puts the texture or shader at <paramref name="path"/> on the selected object.</summary>
    public void ApplyMaterial(string path)
    {
        if (AssetNames.Extension(path).Equals(".oshaderinc", StringComparison.OrdinalIgnoreCase))
        {
            Say($"{Path.GetFileName(path)} is an include. A shader pastes it in with #include, and it goes on nothing by itself.", SayLevel.Info);
            return;
        }

        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to texture anything in.", SayLevel.Warning);
            return;
        }

        if (Primary is not { } primary || scene[primary] is not { } worn)
        {
            Say("Select an object first. A texture or shader goes on whatever is selected.", SayLevel.Info);
            return;
        }

        if (!worn.IsDrawable)
        {
            Say($"{worn.Name} is a {worn.Kind.ToString().ToLowerInvariant()}, and has nothing to draw a texture or shader on.", SayLevel.Info);
            return;
        }

        Run(new SetMaterialAsset(open.Id, worn.Id, worn.Name, worn.MaterialAsset, Assets.Relative(path)));
    }

    // Named after its file, and kept as a path from the project so the scene opens on another machine.
    private void AddMesh(string path)
    {
        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, Path.GetFileNameWithoutExtension(path)),
            kind: ObjectKind.Mesh,
            meshAsset: Assets.Relative(path));
        Run(new AddObject(added, open.Id));
        SelectObject(added.Id);
    }
}

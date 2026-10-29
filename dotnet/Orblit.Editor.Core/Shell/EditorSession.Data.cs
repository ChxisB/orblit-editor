namespace Orblit.Editor.Core;

using IoFile = System.IO.File;

// Data objects: which one the inspector shows, the links from scene objects to them, and the bindings
// a script reads one through.
public sealed partial class EditorSession
{
    /// <summary>The project's data objects, each read once and shared by the panel and whatever reads them.</summary>
    public DataStore Data { get; }

    /// <summary>The data object the inspector shows in place of the selection, relative to the project. Null shows the selection.</summary>
    public string? DataAsset { get; private set; }

    /// <summary>
    /// Told what was picked in the project. Only a data object claims the inspector, so picking a mesh
    /// does not take the panel away from the object being edited.
    /// </summary>
    public void PickAsset(Asset? asset)
    {
        var picked = asset?.Kind == AssetKind.DataObject ? Assets.Relative(asset.Path) : null;
        if (picked != DataAsset)
        {
            DataAsset = picked;
            Changed?.Invoke();
        }
    }

    /// <summary>Shows a data object in the inspector, from wherever it was named.</summary>
    public void ShowData(string relative)
    {
        if (Data[relative] is null)
        {
            Say($"{relative} is not in the project any more.", SayLevel.Info);
            return;
        }

        DataAsset = relative;
        Changed?.Invoke();
    }

    /// <summary>
    /// Points everything selected at a data object. Everything and not one, because attaching the same
    /// settings to forty crates is the case this exists for.
    /// </summary>
    public void AttachData(string path)
    {
        if (Working is not { Scene: { } scene } open)
        {
            return;
        }

        var relative = Assets.Relative(path);
        var name = Path.GetFileName(path);
        if (Data[relative] is null)
        {
            Say($"{name} is not a readable data object.", SayLevel.Info);
            return;
        }

        var wanted = Selected.Select(id => scene[id]).OfType<SceneObject>().Where(one => !one.Data.Contains(relative)).ToList();
        if (wanted.Count == 0)
        {
            Say(Selected.Count == 0 ? $"Select something first, then drop {name} on it." : $"Already using {name}.", SayLevel.Info);
            return;
        }

        foreach (var one in wanted)
        {
            Run(new SetDataLinks(open.Id, one.Id, one.Name, [.. one.Data, relative], $"Add {Path.GetFileNameWithoutExtension(path)}"));
        }

        Say(wanted.Count == 1 ? $"Added {name} to {wanted[0].Name}." : $"Added {name} to {wanted.Count} objects.", SayLevel.Info);
    }

    /// <summary>Stops an object reading a data object.</summary>
    public void DetachData(string id, string relative)
    {
        if (Workspace.SceneHolding(id) is not { Scene: { } scene } open || scene[id] is not { } one)
        {
            return;
        }

        Run(new SetDataLinks(open.Id, id, one.Name, [.. one.Data.Where(path => path != relative)], $"Remove {Path.GetFileNameWithoutExtension(relative)}"));
    }

    /// <summary>
    /// Writes a .h beside a data object. It comes from the one declaration, so a field renamed here
    /// breaks the build of whatever reads it rather than quietly reading nothing.
    /// </summary>
    public void WriteBindings(string relative)
    {
        if (Data[relative] is not { } data)
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(relative);
        var folder = Path.GetDirectoryName(Path.Combine(Workspace.ProjectDirectory, relative)) ?? Workspace.ProjectDirectory;
        var file = $"{name}.h";
        try
        {
            IoFile.WriteAllText(Path.Combine(folder, file), data.ToCpp(name));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"Could not write {file}: {error.Message}", SayLevel.Error);
            return;
        }

        Say($"Wrote {file}.", SayLevel.Info);
    }
}

namespace Orblit.Editor.Core;

using Orblit.Mesh;

// A shape leaving the editor as a model file. The file is not part of the scene, so none of this is undone.
public sealed partial class EditorSession
{
    /// <summary>Which format an export writes. A view setting: not saved, not undone.</summary>
    public MeshFormat ExportFormat { get; private set; } = MeshFormat.Obj;

    public void SetExportFormat(MeshFormat format)
    {
        ExportFormat = format;
        Changed?.Invoke();
    }

    /// <summary>Writes the selected shape into the project's exports folder, and says what it wrote.</summary>
    /// <remarks>
    /// Inside the project rather than wherever a file dialog was last pointed. An export is a thing
    /// somebody made and will want again, and a folder beside the scenes is where they will look.
    /// </remarks>
    /// <param name="name">The file's name without its extension. A path in it is refused.</param>
    public void ExportShape(string name)
    {
        if (ShapeSelected() is not { } chosen)
        {
            Say("There is no geometry to export.", SayLevel.Warning);
            return;
        }

        if (name.IndexOfAny(['/', '\\']) >= 0)
        {
            Say("A file name cannot contain a path.", SayLevel.Warning);
            return;
        }

        var files = chosen.Mesh.WriteAs(ExportFormat, name, [.. chosen.Object.Surfaces.Select(one => one.ToGlb())]);
        var folder = Path.Combine(Workspace.ProjectDirectory, "exports");
        try
        {
            Directory.CreateDirectory(folder);
            foreach (var file in files)
            {
                File.WriteAllBytes(Path.Combine(folder, file.Name), file.Bytes);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"Could not write the export: {error.Message}", SayLevel.Error);
            return;
        }

        // Both names when there are two: an OBJ without the library it names is a grey model and no clue why.
        Say($"Exported {string.Join(" and ", files.Select(one => one.Name))} to exports/.", SayLevel.Info);
    }
}

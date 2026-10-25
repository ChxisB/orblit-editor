namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Doc = Orblit.Scene;

/// <summary>A project's scenes laid out in a workspace, and whatever could not be read on the way.</summary>
public sealed record OpenedProject(Workspace Workspace, IReadOnlyList<string> Problems);

public static class ProjectWorkspace
{
    /// <summary>
    /// Opens a project folder: its main scene loaded, every other scene listed but not loaded so
    /// they can be reached without going hunting, and the shared objects read.
    /// <para>
    /// A scene that cannot be read opens as the starter scene with no file, so there is something to
    /// look at and nothing that would overwrite the file that could not be read.
    /// </para>
    /// </summary>
    public static OpenedProject Open(string directory, Doc.PrefabSource? prefabs = null)
    {
        var workspace = new Workspace(directory);
        var problems = new List<string>();
        var main = Path.Combine(directory, "scenes", "main" + Doc.SceneDocument.SceneExtension);
        var (scene, path, isNew) = ReadScene(main, prefabs, problems);

        workspace.Add(new SceneEntry("scene0", scene.Name, path, scene, neverWritten: isNew));
        ListSiblings(workspace, directory);
        ReadShared(workspace, prefabs, problems);
        return new OpenedProject(workspace, problems);
    }

    /// <summary>
    /// Reads a scene file and touches nothing else. A missing file is the starter scene at that path,
    /// marked new. A file that cannot be read is the starter scene with no path.
    /// </summary>
    internal static (EditorScene Scene, string? Path, bool IsNew) ReadScene(
        string path,
        Doc.PrefabSource? prefabs,
        List<string> problems)
    {
        if (!IoFile.Exists(path))
        {
            return (EditorScene.Starter(), path, true);
        }

        try
        {
            var load = SceneDocument.Decode(IoFile.ReadAllText(path), prefabs);
            problems.AddRange(load.Problems);
            return (load.Scene, path, false);
        }
        catch (Doc.SceneFormatException error)
        {
            problems.Add(error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Could not read that scene: {error.Message}");
        }

        return (EditorScene.Starter(), null, false);
    }

    // shortcut: sorted by name so the list is the same on every machine, where Dart lists them in
    // whatever order the folder gives. Revisit if somebody wants them in the order they were made.
    private static void ListSiblings(Workspace workspace, string directory)
    {
        var folder = Path.Combine(directory, "scenes");
        if (!Directory.Exists(folder))
        {
            return;
        }

        var next = workspace.Entries.Count;
        foreach (var file in Directory.EnumerateFiles(folder).Order(StringComparer.Ordinal))
        {
            if (Path.GetExtension(file) != Doc.SceneDocument.SceneExtension || workspace.EntryFor(file) is not null)
            {
                continue;
            }

            workspace.Add(new SceneEntry($"scene{next++}", Path.GetFileNameWithoutExtension(file), file));
        }
    }

    /// <summary>
    /// What every scene in the project has in it. A project without one is not a problem: it means
    /// nobody has put anything there yet, and the empty set behaves exactly like an empty scene.
    /// </summary>
    private static void ReadShared(Workspace workspace, Doc.PrefabSource? prefabs, List<string> problems)
    {
        var file = workspace.SharedEntry.Path!;
        if (!IoFile.Exists(file))
        {
            return;
        }

        try
        {
            var load = SceneDocument.Decode(IoFile.ReadAllText(file), prefabs);
            workspace.SharedEntry.Scene = load.Scene;
            workspace.SharedEntry.NeverWritten = false;
            problems.AddRange(load.Problems);
        }
        catch (Doc.SceneFormatException error)
        {
            problems.Add($"The shared objects could not be read: {error.Message}");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            problems.Add($"The shared objects could not be read: {error.Message}");
        }
    }
}

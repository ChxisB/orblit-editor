namespace Orblit.Editor.Core;

using Orblit.Asset;
using Orblit.Scene;
using IoDirectory = System.IO.Directory;
using IoFile = System.IO.File;
using IoPath = System.IO.Path;

/// <summary>
/// Cooks every shader in a project and answers with one result for each.
/// </summary>
public static class ShaderCook
{
    private const string IncludeExtension = ".oshaderinc";

    /// <summary>Whether a file is a shader or something a shader includes, which is all the cook here reads.</summary>
    public static bool IsShaderFile(string path)
    {
        var extension = IoPath.GetExtension(path);
        return extension == MaterialDocument.ShaderExtension || extension == IncludeExtension;
    }

    /// <summary>
    /// The real cook, run off the interface thread so a slow <c>matc</c>, or one that runs out of
    /// memory, costs the editor a note and not a frozen frame.
    /// </summary>
    public static Task<IReadOnlyList<ShaderCooked>> CookAsync(string project) => Task.Run(() => CookShadersAsync(project));

    private static async Task<IReadOnlyList<ShaderCooked>> CookShadersAsync(string project)
    {
        var work = IoPath.Join(project, ".orblit", "shaders");
        var stage = IoPath.Join(work, "assets");
        if (IoDirectory.Exists(stage))
        {
            IoDirectory.Delete(stage, recursive: true);
        }

        var ids = Stage(IoPath.Join(project, "assets"), stage);
        if (ids.Count == 0)
        {
            return [];
        }

        var cooking = new CookProject(stage, IoPath.Join(work, "cooked"), CookProject.CurrentCookTarget);
        var report = await cooking.RunAsync().ConfigureAwait(false);
        var failed = report.WithStatus(CookStatus.Failed).ToDictionary(result => result.Id.ToString(), ReasonOf);
        var packages = IoPath.Join(work, "packages");
        return [.. ids.Select(id => ResultOf(id, failed.GetValueOrDefault(id), cooking.BundlePath, packages))];
    }

    /// <summary>
    /// Copies the shaders and what they include into <paramref name="stage"/>, and names each shader.
    /// Only those are cooked. Cooking the whole assets folder on every save would convert every
    /// texture the first time.
    /// </summary>
    private static List<string> Stage(string assets, string stage)
    {
        var ids = new List<string>();
        if (!IoDirectory.Exists(assets))
        {
            return ids;
        }

        var files = IoDirectory.EnumerateFiles(assets, "*", SearchOption.AllDirectories).Where(IsShaderFile).Order(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var relative = IoPath.GetRelativePath(assets, file);
            var copy = IoPath.Join(stage, relative);
            IoDirectory.CreateDirectory(IoPath.GetDirectoryName(copy)!);
            IoFile.Copy(file, copy, overwrite: true);
            if (IoPath.GetExtension(file) == MaterialDocument.ShaderExtension)
            {
                ids.Add(relative.Replace(IoPath.DirectorySeparatorChar, '/'));
            }
        }

        return ids;
    }

    // The importer's own reason when it gave one, because that carries the author's line.
    private static string ReasonOf(CookResult result) => result.Error switch
    {
        ImportFailureException failure => failure.Reason,
        { } error => error.Message,
        null => $"{result.Id} did not cook.",
    };

    private static string Under(string folder, string id, string suffix) =>
        IoPath.Join(folder, (id + suffix).Replace('/', IoPath.DirectorySeparatorChar));

    /// <summary>
    /// What the cook made of the shader at <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// The cook empties its bundle on every run, and the renderer reads a package again whenever its
    /// file changes. So a good package is copied out to a folder of its own, and only when the bytes
    /// differ. A save of one shader then leaves the others as they were, and a save that fails leaves
    /// the last good package on disk for objects that are added after it.
    /// </remarks>
    // shortcut: the package of a deleted shader stays in `packages` until the `.orblit` folder is cleared, it is a few hundred kilobytes.
    private static ShaderCooked ResultOf(string id, string? problem, string bundle, string packages)
    {
        var path = $"assets/{id}";
        if (problem is not null)
        {
            return new ShaderCooked(path, Problem: problem);
        }

        try
        {
            var kept = Under(packages, id, ".filamat");
            var bytes = IoFile.ReadAllBytes(Under(bundle, id, ".material.filamat"));
            if (!IoFile.Exists(kept) || !IoFile.ReadAllBytes(kept).AsSpan().SequenceEqual(bytes))
            {
                IoDirectory.CreateDirectory(IoPath.GetDirectoryName(kept)!);
                IoFile.WriteAllBytes(kept, bytes);
            }

            return new ShaderCooked(path, kept, IoFile.ReadAllText(Under(bundle, id, ".params.json")));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new ShaderCooked(path, Problem: $"{path} did not cook: {error.Message}");
        }
    }
}

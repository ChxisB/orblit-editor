namespace Orblit.Editor.Core;

/// <summary>The result of building a game. <see cref="AssemblyPath"/> is null when it did not build.</summary>
public sealed record GameBuild(string? AssemblyPath, string Output)
{
    public bool Succeeded => AssemblyPath is not null;
}

/// <summary>Builds the game a project names into a folder inside that project.</summary>
public sealed class GameBuilder(IBuildCommand command)
{
    /// <summary>Where a project's game is built. The editor owns this folder and may clear it.</summary>
    public static string OutputFolder(string projectFolder) =>
        Path.GetFullPath(Path.Combine(projectFolder, ".orblit", "game"));

    public async Task<GameBuild> BuildAsync(string projectFolder, GameEntry entry, CancellationToken cancel = default)
    {
        var projectFile = Path.GetFullPath(Path.Combine(projectFolder, entry.Project.Replace('\\', '/')));
        var output = OutputFolder(projectFolder);

        // The assembly takes the project file's name unless the project says otherwise. Say what was
        // looked for when it is not there, since a person has to rename one or the other.
        var assembly = Path.Combine(output, Path.GetFileNameWithoutExtension(projectFile) + ".dll");

        // One an earlier build left would pass for this build's when the project names its assembly
        // another way, so only an assembly made by this build counts.
        if (File.Exists(assembly))
        {
            File.Delete(assembly);
        }

        var result = await command.RunAsync(projectFile, output, cancel);
        if (!result.Succeeded)
        {
            return new GameBuild(null, result.Text);
        }

        return File.Exists(assembly)
            ? new GameBuild(assembly, result.Text)
            : new GameBuild(null, $"{result.Text}\nThe build made no {Path.GetFileName(assembly)} in {output}.");
    }
}

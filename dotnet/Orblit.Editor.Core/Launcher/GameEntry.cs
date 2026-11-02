namespace Orblit.Editor.Core;

/// <summary>
/// The C# project a game lives in, and the class in it that is the game, as the project file names them.
/// </summary>
/// <remarks>
/// Checked when made. The path comes from a file the editor did not write, and the editor builds and runs
/// what it names. A path that leaves the project folder would run code from a place the person never opened.
/// </remarks>
public sealed record GameEntry
{
    public GameEntry(string project, string type)
    {
        if (ProblemWith(project, type) is { } problem)
        {
            throw new ArgumentException(problem);
        }

        Project = project;
        Type = type;
    }

    /// <summary>The <c>.csproj</c> file, relative to the project folder. A slash or a backslash divides folders.</summary>
    public string Project { get; }

    /// <summary>The full name of the class that implements the game.</summary>
    public string Type { get; }

    /// <summary>
    /// Reads the <c>game</c> field of a project file. No field is no game and no problem. A field that
    /// cannot be used is no game and the reason it cannot.
    /// </summary>
    internal static (GameEntry? Entry, string? Problem) Read(object? field)
    {
        if (field is null)
        {
            return (null, null);
        }

        if (field is not IReadOnlyDictionary<string, object?> map
            || map.GetValueOrDefault("project") is not string project
            || map.GetValueOrDefault("type") is not string type)
        {
            return (null, "The game must be an object with a project and a type, both text.");
        }

        return ProblemWith(project, type) is { } problem ? (null, problem) : (new GameEntry(project, type), null);
    }

    private static string? ProblemWith(string project, string type)
    {
        if (!project.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return "The game's project must be a .csproj file.";
        }

        if (LeavesTheFolder(project))
        {
            return "The game's project must be inside the project folder.";
        }

        return string.IsNullOrWhiteSpace(type) ? "The game's type must name a class." : null;
    }

    // A drive letter has a colon, and a backslash divides folders on Windows, so both count here on every system.
    private static bool LeavesTheFolder(string path) =>
        path.StartsWith('/')
        || path.StartsWith('\\')
        || path.Contains(':')
        || path.Split('/', '\\').Contains("..");
}

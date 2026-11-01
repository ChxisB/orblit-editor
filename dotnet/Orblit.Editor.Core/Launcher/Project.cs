namespace Orblit.Editor.Core;

using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using IoFile = System.IO.File;
using Orblit.Json;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>What a new project starts as.</summary>
public enum ProjectTemplate
{
    /// <summary>Nothing but a scene. For someone who knows what they are building.</summary>
    Empty,

    /// <summary>A lit scene with a ground plane and something to look at.</summary>
    Scene,

    /// <summary>A scene plus a controllable camera rig.</summary>
    ThirdPerson,
}

public static class ProjectTemplates
{
    public static string Label(this ProjectTemplate template) => template switch
    {
        ProjectTemplate.Empty => "Empty",
        ProjectTemplate.Scene => "Lit scene",
        ProjectTemplate.ThirdPerson => "Third person",
        _ => throw new UnreachableException(),
    };

    public static string Description(this ProjectTemplate template) => template switch
    {
        ProjectTemplate.Empty => "A single empty scene, and nothing else.",
        ProjectTemplate.Scene => "A ground plane, a sun and an object, enough to see that it works.",
        ProjectTemplate.ThirdPerson => "A lit scene with a follow camera and a character to drive.",
        _ => throw new UnreachableException(),
    };

    /// <summary>
    /// What a template starts a project with.
    /// <para>
    /// Built as a real scene and written through <see cref="SceneDocument"/>, so a new project opens on
    /// exactly what the editor would have saved. A separate hand-written shape here would drift from
    /// the format the editor reads, and the drift would only show up as a new project failing to open.
    /// </para>
    /// </summary>
    public static EditorScene SceneFor(this ProjectTemplate template)
    {
        var sun = new SceneObject(
            id: "sun",
            name: "Sun",
            kind: ObjectKind.Light,
            rotation: new Vector3(-55, 35, 0),
            colour: new Colour(0xFFFFF3E0),
            power: 1400);
        var ground = new SceneObject(
            id: "ground",
            name: "Ground",
            kind: ObjectKind.Mesh,
            position: new Vector3(0, -1.05, 0),
            scale: new Vector3(8, 0.05, 8),
            colour: new Colour(0xFF3B424C));

        return template switch
        {
            ProjectTemplate.Empty => new EditorScene([]),
            ProjectTemplate.Scene => new EditorScene(
            [
                sun,
                ground,
                new SceneObject(id: "cube", name: "Cube", kind: ObjectKind.Mesh, rotation: new Vector3(0, 25, 0)),
            ]),
            ProjectTemplate.ThirdPerson => new EditorScene(
            [
                sun,
                ground,
                new SceneObject(
                    id: "character",
                    name: "Character",
                    kind: ObjectKind.Mesh,
                    scale: new Vector3(0.5, 0.9, 0.5),
                    colour: new Colour(0xFFE5B84F)),
                new SceneObject(
                    id: "camera",
                    name: "Follow Camera",
                    kind: ObjectKind.Camera,
                    position: new Vector3(0, 3, 7),
                    rotation: new Vector3(-15, 0, 0)),
            ]),
            _ => throw new UnreachableException(),
        };
    }
}

/// <summary>
/// A project the editor knows about.
/// </summary>
/// <remarks>
/// What a project looks like on disk: one file at the root, named so a folder is recognisably a
/// project without opening it, and versioned so an editor that meets a newer one can say so rather
/// than misread it.
/// </remarks>
public sealed record Project(string Name, string Directory, DateTime LastOpened, string EngineVersion = "0.1.0")
{
    public const string FileName = "orblit.project.json";

    public const int FormatVersion = 1;

    /// <summary>Whether the folder is still where it was last seen.</summary>
    public bool Exists => IoFile.Exists(Path.Combine(Directory, FileName));

    public static Project FromJson(IReadOnlyDictionary<string, object?> json, string directory) => new(
        json.GetValueOrDefault("name") as string ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)),
        directory,
        ReadTime(json.GetValueOrDefault("lastOpened") as string ?? ""),
        json.GetValueOrDefault("engine") as string ?? "unknown");

    /// <summary>The path with the home folder as a tilde, for a list that has to fit on a line.</summary>
    public string DisplayPath(string? home) =>
        home is not null && Directory.StartsWith(home, StringComparison.Ordinal)
            ? $"~{Directory[home.Length..]}"
            : Directory;

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["formatVersion"] = (long)FormatVersion,
        ["name"] = Name,
        ["engine"] = EngineVersion,
        ["lastOpened"] = IsoText(LastOpened),
    };

    // Written to the millisecond always, to the microsecond only when there are some, and with a Z for UTC.
    private static string IsoText(DateTime at)
    {
        var micro = at.Ticks % TimeSpan.TicksPerMillisecond / 10;
        return at.ToString("yyyy-MM-dd'T'HH:mm:ss.fff", CultureInfo.InvariantCulture)
            + (micro == 0 ? "" : micro.ToString("D3", CultureInfo.InvariantCulture))
            + (at.Kind == DateTimeKind.Utc ? "Z" : "");
    }

    // shortcut: .NET also reads a few loose dates that the format does not allow, which only matters for a
    // file somebody edited by hand.
    private static DateTime ReadTime(string text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
            ? at
            : DateTime.UnixEpoch;
}

/// <summary>
/// Reading, creating and remembering projects.
/// </summary>
/// <param name="recentsFile">
/// Where the list of recent projects lives. The caller owns the choice, because the folder for an
/// application's data is the host's to say and not the model's.
/// </param>
/// <param name="time">The clock, so a test can say when a project was opened.</param>
public sealed class ProjectStore(string recentsFile, TimeProvider? time = null)
{
    private const int KeptRecent = 20;

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <summary>
    /// Projects the editor has opened, most recent first.
    /// <para>
    /// A project whose folder has since moved or been deleted is kept in the list rather than dropped
    /// silently. The launcher shows it as missing, so somebody who moved a folder sees why it is not
    /// opening instead of wondering where their work went.
    /// </para>
    /// </summary>
    public IReadOnlyList<Project> Recents()
    {
        if (!IoFile.Exists(recentsFile))
        {
            return [];
        }

        try
        {
            return JsonText.Decode(IoFile.ReadAllText(recentsFile)) is IReadOnlyList<object?> decoded
                ? decoded
                    .OfType<IReadOnlyDictionary<string, object?>>()
                    .Select(entry => entry.GetValueOrDefault("directory") is string directory
                        ? Project.FromJson(entry, directory)
                        : null)
                    .OfType<Project>()
                    .OrderByDescending(project => project.LastOpened)
                    .ToList()
                : [];
        }
        catch (JsonFormatException)
        {
            // A corrupt list is not worth failing to launch over.
            return [];
        }
    }

    public void Remember(Project project) => Write(
        Recents()
            .Where(other => other.Directory != project.Directory)
            .Prepend(project with { LastOpened = Now() })
            .Take(KeptRecent));

    public void Forget(Project project) =>
        Write(Recents().Where(other => other.Directory != project.Directory));

    /// <summary>Reads a project from a folder, or null if there is not one there.</summary>
    public static Project? Open(string directory)
    {
        var file = Path.Combine(directory, Project.FileName);
        if (!IoFile.Exists(file))
        {
            return null;
        }

        try
        {
            return JsonText.Decode(IoFile.ReadAllText(file)) is IReadOnlyDictionary<string, object?> decoded
                ? Project.FromJson(decoded, directory)
                : null;
        }
        catch (JsonFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a project folder and everything a template puts in it.
    /// <para>
    /// Refuses rather than merges if the folder already holds a project, since silently adopting
    /// somebody's existing work is the worse failure.
    /// </para>
    /// </summary>
    public Project Create(string name, string parentDirectory, ProjectTemplate template)
    {
        var directory = DestinationFor(name, parentDirectory);
        if (IoFile.Exists(Path.Combine(directory, Project.FileName)))
        {
            throw new InvalidOperationException($"There is already a project in {directory}.");
        }

        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(Path.Combine(directory, "scenes"));
        Directory.CreateDirectory(Path.Combine(directory, "assets"));

        var project = new Project(name, directory, Now());
        IoFile.WriteAllText(
            Path.Combine(directory, Project.FileName),
            JsonText.Encode(project.ToJson(), indent: "  "));
        IoFile.WriteAllText(
            Path.Combine(directory, "scenes", "main" + Doc.SceneDocument.SceneExtension),
            SceneDocument.Encode(template.SceneFor()));
        IoFile.WriteAllText(Path.Combine(directory, ".gitignore"), "build/\n.orblit/\n");

        Remember(project);
        return project;
    }

    /// <summary>
    /// Where the folder for a project lands. A name is for people and a path is for filesystems, so the
    /// launcher shows this before anything is made.
    /// </summary>
    public static string DestinationFor(string name, string parentDirectory)
    {
        var slug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return Path.Combine(parentDirectory, slug.Length == 0 ? "orblit-project" : slug);
    }

    private DateTime Now() => _time.GetLocalNow().DateTime;

    private void Write(IEnumerable<Project> projects)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(recentsFile)!); // A file path always has a folder.
        IoFile.WriteAllText(
            recentsFile,
            JsonText.Encode(
                projects.Select(project => (object?)new OrderedDictionary<string, object?>(project.ToJson())
                {
                    ["directory"] = project.Directory,
                }).ToList(),
                indent: "  "));
    }
}

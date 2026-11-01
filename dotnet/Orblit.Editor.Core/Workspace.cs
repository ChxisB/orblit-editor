namespace Orblit.Editor.Core;

using Doc = Orblit.Scene;

/// <summary>
/// A scene the editor knows about, loaded or not.
/// </summary>
/// <remarks>
/// One scene is loaded at a time. The others are listed so they can be reached without going hunting
/// in the project browser, and hold nothing but where they are: an unloaded scene is a name and a
/// path, not a copy of a document sitting in memory waiting to disagree with the file.
/// </remarks>
public sealed class SceneEntry
{
    // F2: an entry is its id, its name, where it lives, its document and whether it was ever written.
    public SceneEntry(
        string id,
        string name,
        string? path = null,
        EditorScene? scene = null,
        bool neverWritten = false)
    {
        Id = id;
        Name = name;
        Path = path;
        Scene = scene;
        NeverWritten = neverWritten;
    }

    public string Id { get; }

    /// <summary>What it is called in the tree.</summary>
    public string Name { get; set; }

    /// <summary>The file it came from and goes back to. Null for one never written.</summary>
    public string? Path { get; set; }

    /// <summary>The document, when it is loaded. Null otherwise.</summary>
    public EditorScene? Scene { get; set; }

    /// <summary>Whether it has never reached disk.</summary>
    public bool NeverWritten { get; set; }

    /// <summary>The stamp the history had for it when it was last written.</summary>
    public int SavedStamp { get; set; }

    public bool IsLoaded => Scene is not null;

    /// <summary>
    /// What to call it: its file, or its own name when it has none.
    /// <para>
    /// The shared set keeps its name rather than taking its file's. It is not one of the scenes
    /// somebody is choosing between, and listing it as "shared" among "main" and "level2" reads as a
    /// scene that happens to be lower case rather than as the thing every scene has.
    /// </para>
    /// </summary>
    public string Title => Path is null || Id == Workspace.SharedSceneId
        ? Name
        : System.IO.Path.GetFileNameWithoutExtension(Path);
}

/// <summary>
/// The scenes the editor knows about, and the one being worked on.
/// </summary>
public sealed class Workspace(string projectDirectory) : ISceneHost
{
    /// <summary>What every scene in a project has in it.</summary>
    public const string SharedSceneId = "shared";

    /// <summary>What the shared set is written to, under the project.</summary>
    public const string SharedFileName = "shared" + Doc.SceneDocument.SceneExtension;

    private readonly List<SceneEntry> _entries = [];
    private string? _loadedId;

    /// <summary>
    /// The objects every scene has, whichever one is open.
    /// <para>
    /// A scene of its own, held apart from the list rather than in it: it is never loaded, never
    /// closed and never one of the things somebody is choosing between. What makes it worth having is
    /// that everything else already works on scenes. A command names one, the outliner draws one, the
    /// inspector edits one, so the managers and the props that belong to the whole project need no
    /// machinery of their own.
    /// </para>
    /// </summary>
    public SceneEntry SharedEntry { get; } = new(
        SharedSceneId,
        "Shared",
        Path.Combine(projectDirectory, SharedFileName),
        new EditorScene([], name: "Shared"),
        neverWritten: true);

    public event Action? Changed;

    public string ProjectDirectory { get; } = projectDirectory;

    public EditorScene Shared => SharedEntry.Scene!;

    public IReadOnlyList<SceneEntry> Entries => [.. _entries];

    public bool IsEmpty => _entries.Count == 0;

    /// <summary>The scene being edited and drawn. Null when none is loaded.</summary>
    public SceneEntry? Loaded => _loadedId is null ? null : this[_loadedId];

    public SceneEntry? this[string sceneId] =>
        sceneId == SharedSceneId ? SharedEntry : _entries.FirstOrDefault(entry => entry.Id == sceneId);

    public EditorScene? SceneFor(string sceneId) => this[sceneId]?.Scene;

    /// <summary>
    /// Which scene an object belongs to. Only a loaded scene has objects, and the shared set, which
    /// is always there.
    /// </summary>
    public SceneEntry? SceneHolding(string objectId)
    {
        var open = Loaded;
        if (open?.Scene?.Contains(objectId) ?? false)
        {
            return open;
        }

        return Shared.Contains(objectId) ? SharedEntry : null;
    }

    public SceneEntry? EntryFor(string path) =>
        _entries.FirstOrDefault(entry => entry.Path is { } existing && SamePath(existing, path));

    public void Add(SceneEntry entry)
    {
        _entries.Add(entry);
        if (entry.IsLoaded)
        {
            _loadedId = entry.Id;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Puts a document into an entry and makes it the loaded one.
    /// <para>
    /// Whatever was loaded is emptied: only one scene's objects exist at a time, so there is never a
    /// question about which one an edit or a draw belongs to.
    /// </para>
    /// </summary>
    public void Load(SceneEntry entry, EditorScene scene)
    {
        foreach (var other in _entries.Where(other => !ReferenceEquals(other, entry)))
        {
            other.Scene = null;
        }

        // The shared set is not one of the things being swapped out. It is what both scenes have in
        // common, and unloading it with the scene would make it the opposite of shared.
        entry.Scene = scene;
        _loadedId = entry.Id;
        Changed?.Invoke();
    }

    /// <summary>Empties an entry without forgetting that it exists.</summary>
    public void Unload(SceneEntry entry)
    {
        if (ReferenceEquals(entry, SharedEntry))
        {
            return;
        }

        entry.Scene = null;
        if (_loadedId == entry.Id)
        {
            _loadedId = null;
        }

        Changed?.Invoke();
    }

    public void Remove(string sceneId)
    {
        _entries.RemoveAll(entry => entry.Id == sceneId);
        if (_loadedId == sceneId)
        {
            _loadedId = null;
        }

        Changed?.Invoke();
    }

    /// <summary>A name nothing else in the list is using.</summary>
    public string AvailableName(string baseName)
    {
        var taken = _entries.Select(entry => entry.Title).ToHashSet();
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        for (var i = 2; ; i++)
        {
            if (!taken.Contains($"{baseName} {i}"))
            {
                return $"{baseName} {i}";
            }
        }
    }

    /// <summary>Where a scene of this name would be written.</summary>
    public string PathFor(string name) => Path.Combine(
        ProjectDirectory,
        "scenes",
        name.EndsWith(Doc.SceneDocument.SceneExtension, StringComparison.Ordinal)
            ? name
            : name + Doc.SceneDocument.SceneExtension);

    /// <summary>Tells whoever is listening that something about a scene changed.</summary>
    public void Touched() => Changed?.Invoke();

    internal static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.Ordinal);
}

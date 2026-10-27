namespace Orblit.Editor.Core;

/// <summary>What to do with a scene that has changes when it is about to stop being loaded.</summary>
public enum Leaving
{
    // First, so a dialog closed any other way than a button means stay.
    Stop,
    Save,
    Discard,
}

// Which scene is loaded: reading one, starting one and closing one, with the changes asked about first.
public sealed partial class EditorSession
{
    private int _nextSceneId;

    /// <summary>
    /// Asked when a scene with changes is about to stop being loaded, which is the one moment the work
    /// could be lost. A window sets it to a dialog. Without one the answer is to stop, so nothing is
    /// lost by a session that has nobody to ask.
    /// </summary>
    public Func<SceneEntry, Task<Leaving>> ConfirmLeaving { get; set; } = _ => Task.FromResult(Leaving.Stop);

    /// <summary>
    /// Given every prefab the scenes in memory hold instances of, before a scene is read. Whatever it
    /// does not name is read again, so a prefab changed in another program shows up.
    /// </summary>
    public Action<IReadOnlySet<string>>? KeepPrefabs { get; init; }

    /// <summary>The scene whose own settings are being looked at, when no object is.</summary>
    public string? SelectedScene { get; private set; }

    /// <summary>The scene whose settings the inspector shows: the loaded one, or one somebody has clicked to look at without opening.</summary>
    public SceneEntry? Inspected => SelectedScene is null ? Workspace.Loaded : Workspace[SelectedScene];

    /// <summary>The primary selection, in the scene being inspected. Null when the scene itself is selected.</summary>
    public SceneObject? InspectedItem => Primary is null ? null : Inspected?.Scene?[Primary];

    /// <summary>Raised when a different scene takes the place of the loaded one, so a view can start from its camera again.</summary>
    public event Action? SceneReplaced;

    /// <summary>Selects a scene row, which leaves no object selected.</summary>
    public void SelectScene(string sceneId)
    {
        Selected = [];
        Primary = null;
        SelectedScene = sceneId;
        FollowMotion();
        Changed?.Invoke();
    }

    /// <summary>Starts a new scene for a menu or a key.</summary>
    public void NewScene() => Start(NewSceneAsync);

    public void LoadScene(SceneEntry entry) => Start(() => LoadSceneAsync(entry));

    public void CloseScene(SceneEntry entry) => Start(() => CloseSceneAsync(entry));

    /// <summary>
    /// Loads a listed scene in place of the loaded one. One at a time, so the view shows one document
    /// and an edit never has two scenes it might belong to.
    /// </summary>
    public async Task LoadSceneAsync(SceneEntry entry)
    {
        if (entry.IsLoaded)
        {
            return;
        }

        var leaving = Workspace.Loaded;
        if (leaving is not null && !await MayLeave(leaving))
        {
            return;
        }

        if (entry.Path is not { } path)
        {
            Say($"{entry.Title} has never been saved, so there is nothing to load.", SayLevel.Info);
            return;
        }

        KeepPrefabs?.Invoke(PrefabsInUse());
        var problems = new List<string>();
        var (scene, read, missing) = ProjectWorkspace.ReadScene(path, Prefabs, problems);
        if (missing)
        {
            problems.Add($"There is no scene at {path}.");
        }

        if (read is null)
        {
            Report(problems);
            return;
        }

        if (leaving is not null)
        {
            // Its steps go with it. Undoing into a scene that is not loaded would be a step that
            // appears to do nothing.
            History.Forget(leaving.Id);
            _preview.Forget();
            Workspace.Unload(leaving);
        }

        Workspace.Load(entry, scene);
        entry.Name = scene.Name;
        entry.NeverWritten = false;
        entry.SavedStamp = History.StampFor(entry.Id);
        Replaced();
        Report(problems);
    }

    /// <summary>Starts a new scene in place of the loaded one.</summary>
    public async Task NewSceneAsync()
    {
        var leaving = Workspace.Loaded;
        if (leaving is not null && !await MayLeave(leaving))
        {
            return;
        }

        if (leaving is not null)
        {
            History.Forget(leaving.Id);
            _preview.Forget();
            Workspace.Unload(leaving);
        }

        var name = Workspace.AvailableName("Untitled");
        var entry = new SceneEntry($"scene{_nextSceneId++}", name, neverWritten: true);
        var scene = EditorScene.Starter();
        scene.Name = name;
        Workspace.Add(entry);
        Workspace.Load(entry, scene);
        Replaced();
    }

    /// <summary>Takes a scene off the list, asking first when it has changes.</summary>
    public async Task CloseSceneAsync(SceneEntry entry)
    {
        if (!await MayLeave(entry))
        {
            return;
        }

        History.Forget(entry.Id);
        if (entry.IsLoaded)
        {
            _preview.Forget();
        }

        Workspace.Remove(entry.Id);
        Selected = [];
        Primary = null;
        if (SelectedScene == entry.Id)
        {
            SelectedScene = null;
        }

        Changed?.Invoke();
    }

    // Saving a scene with no file asks for a name and does not wait for it, so that too stops here:
    // the change is still only in memory.
    private async Task<bool> MayLeave(SceneEntry entry)
    {
        if (!IsUnsaved(entry))
        {
            return true;
        }

        var answer = await ConfirmLeaving(entry);
        if (answer == Leaving.Save)
        {
            Save();
        }

        return answer switch
        {
            Leaving.Discard => true,
            Leaving.Save => !IsUnsaved(entry),
            _ => false,
        };
    }

    // What has to stay as it was read: an instance is saved as what it changed about exactly that version.
    private HashSet<string> PrefabsInUse() =>
    [
        .. Workspace.Entries
            .Where(entry => entry.Scene is not null)
            .SelectMany(entry => entry.Scene!.Objects)
            .Select(one => one.Prefab?.Asset)
            .OfType<string>(),
    ];

    private void Replaced()
    {
        Selected = [];
        Primary = null;
        SelectedScene = null;
        FollowMotion();
        OnClipsChanged();
        SceneReplaced?.Invoke();
        Changed?.Invoke();
    }
}

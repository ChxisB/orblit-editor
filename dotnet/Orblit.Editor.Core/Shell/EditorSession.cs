namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Scene;

public enum SayLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>
/// What the editor window is showing and doing, with no control in it.
/// </summary>
/// <remarks>
/// The mode, how its panels are arranged, whether the view is focused, who is selected, and the
/// actions that change them. A window draws this and calls it. Keeping it here means a test can drive
/// the editor without a window, and the window cannot get ahead of what is saved.
/// <para>
/// Layouts are files with the project, one for each mode, in the names the Dart editor uses, so a
/// project opens in either editor with its panels where they were left.
/// </para>
/// </remarks>
public sealed partial class EditorSession
{
    private readonly HashSet<string> _focused = [];
    private readonly LayoutLibrary _library;

    public EditorSession(Workspace workspace, History history, Registry<EditorMode> modes, Registry<PanelType> panels)
    {
        Workspace = workspace;
        History = history;
        Modes = modes;
        Panels = panels;
        _nextSceneId = workspace.Entries.Count;
        _library = new LayoutLibrary(Path.Combine(workspace.ProjectDirectory, ".orblit", "layouts.json"), panels.Kinds());
        Mode = modes.All[0];
        Layout = LayoutOf(Mode);
        Assets = new AssetTree(workspace.ProjectDirectory);
        Clips = new ClipBench(history, () => workspace.Loaded?.Scene);
        Clips.Changed += OnClipsChanged;
        Blends = new BlendBench(history);
        Blends.Changed += OnBlendsChanged;
        Terrains = new TerrainBench(history, workspace.ProjectDirectory, problem => Say(problem, SayLevel.Error));
        Terrains.Changed += OnTerrainsChanged;
        history.Changed += OnHistoryChanged;
    }

    /// <summary>Something said to the person at the keyboard, such as a file that could not be written.</summary>
    public event Action<string, SayLevel>? Said;

    /// <summary>Raised when a scene is to be saved and needs a name first, for a window to ask for one.</summary>
    public event Action? NameWanted;

    /// <summary>Raised when the mode, the arrangement, the focus or the selection changes.</summary>
    public event Action? Changed;

    /// <summary>What was said last, for a window that opens after something was already said.</summary>
    public (string Text, SayLevel Level)? Last { get; private set; }

    public Workspace Workspace { get; }

    public History History { get; }

    public Registry<EditorMode> Modes { get; }

    public Registry<PanelType> Panels { get; }

    /// <summary>What a scene is folded against when it is written. Null writes every instance whole.</summary>
    public PrefabSource? Prefabs { get; init; }

    /// <summary>What reads the pictures a terrain's sets name. Null leaves the ground drawing with the plain picture.</summary>
    public IPictureDecoder? PictureDecoder
    {
        get => Terrains.Pictures;
        init => Terrains.ReadPicturesWith(value);
    }

    public EditorMode Mode { get; private set; }

    /// <summary>How the panels are arranged. Data, so it survives being closed.</summary>
    public DockLayout Layout { get; private set; }

    public bool ShowStats { get; private set; }

    public IReadOnlyList<string> Selected { get; private set; } = [];

    public string? Primary { get; private set; }

    public bool IsFocused => _focused.Contains(Mode.Name);

    /// <summary>
    /// What the window draws. When focused, one group holding the view, locked, so nothing can be
    /// dragged out of an arrangement that is only hidden.
    /// </summary>
    public DockLayout VisibleLayout
    {
        get
        {
            if (!IsFocused)
            {
                return Layout;
            }

            // shortcut: the interface canvas is not looked for until that mode has a panel (task 16).
            var panel = Layout.GroupOf("scene")?.Current
                ?? Layout.Panels.FirstOrDefault(one => one.Kind == PanelKind.Viewport)
                ?? Layout.Panels[0];
            return Layout.CopyWith(root: new DockGroup("focused", [panel]), locked: true);
        }
    }

    public IReadOnlyList<string> SavedLayouts => _library.Names(Mode.Name);

    /// <summary>Whether a scene has changes that are not on disk.</summary>
    public bool IsUnsaved(SceneEntry entry) =>
        entry.IsLoaded && (entry.NeverWritten || History.StampFor(entry.Id) != entry.SavedStamp);

    /// <summary>Whether a scene, a clip, a blend or a terrain has changes that are not on disk.</summary>
    public bool AnyUnsaved => Workspace.Entries.Any(IsUnsaved) || Clips.AnyUnsaved || Blends.AnyUnsaved || Terrains.AnyUnsaved;

    /// <summary>Switches to a mode, with the panels as they were last left in it.</summary>
    public void EnterMode(EditorMode mode)
    {
        if (mode.Name == Mode.Name)
        {
            return;
        }

        Mode.OnLeave?.Invoke();
        Mode = mode;
        Layout = LayoutOf(mode);
        mode.OnEnter?.Invoke();
        FollowMotion(force: true);
        OnClipsChanged();
        Changed?.Invoke();
    }

    /// <summary>Switches to the mode called <paramref name="name"/>, when something registered one.</summary>
    public void EnterMode(string name)
    {
        if (Modes[name] is { } mode)
        {
            EnterMode(mode);
        }
    }

    /// <summary>Takes a new arrangement and keeps it with the project.</summary>
    public void Relayout(DockLayout layout)
    {
        _focused.Remove(Mode.Name);
        Layout = layout;
        WriteLayout(Mode, layout);
        Changed?.Invoke();
    }

    /// <summary>Says what could not be read, as one line however many things there were.</summary>
    public void Report(IReadOnlyList<string> problems)
    {
        if (problems.Count > 0)
        {
            Say(
                problems.Count == 1
                    ? problems[0]
                    : $"{problems.Count} things in that scene could not be read. First: {problems[0]}",
                SayLevel.Error);
        }
    }

    /// <summary>Opens the panel registered for a kind, or shows it if it is open.</summary>
    public void OpenPanel(PanelKind kind)
    {
        if (Panels.TypeOf(kind) is not { } type)
        {
            return;
        }

        Layout = Layout.Add(type.Panel);
        Changed?.Invoke();
    }

    /// <summary>Puts back the arrangement the mode opens with, keeping the lock.</summary>
    public void ResetPanels() => Relayout(Mode.Layout().CopyWith(locked: Layout.Locked));

    public void ToggleFocus()
    {
        if (!_focused.Remove(Mode.Name))
        {
            _focused.Add(Mode.Name);
        }

        Changed?.Invoke();
    }

    public void ToggleStats()
    {
        ShowStats = !ShowStats;
        Changed?.Invoke();
    }

    public void SaveLayout(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        try
        {
            _library.Save(Mode.Name, name.Trim(), Layout);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"Could not save the layout: {error.Message}", SayLevel.Error);
            return;
        }

        Changed?.Invoke();
    }

    public void LoadLayout(string name)
    {
        if (_library.Find(Mode.Name, name) is not { } saved)
        {
            Say("That layout could not be read.", SayLevel.Error);
            return;
        }

        Relayout(saved.CopyWith(revision: Mode.Layout().Revision));
    }

    public void DeleteLayout(string name)
    {
        try
        {
            _library.Remove(Mode.Name, name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"Could not delete the layout: {error.Message}", SayLevel.Error);
            return;
        }

        Changed?.Invoke();
    }

    public void Select(IReadOnlyList<string> ids)
    {
        var distinct = ids.Distinct().ToList();
        Choose(distinct, distinct.Count == 0 ? null : distinct[^1]);
    }

    private void Choose(IReadOnlyList<string> ids, string? primary)
    {
        var moved = primary != Primary;
        Selected = [.. ids];
        Primary = primary;
        SelectedScene = null;
        if (moved)
        {
            ForgetParts();
        }

        FollowMotion();
        Changed?.Invoke();
    }

    // A face number means something only on the mesh it was picked from. Kept across to another
    // object it would pick that object's faces by position, and Delete or Paint would hit them.
    private void ForgetParts()
    {
        Elements = new ElementSelection();
        if (Context == EditContext.Element && !CanEditParts)
        {
            Context = EditContext.Whole;
        }
    }

    /// <summary>Undoes a step, then shows what it changed, so a step in another scene is not invisible.</summary>
    public void Undo()
    {
        var sceneId = History.UndoSceneId;
        History.Undo();
        Reveal(sceneId);
    }

    public void Redo()
    {
        var sceneId = History.RedoSceneId;
        History.Redo();
        Reveal(sceneId);
    }

    /// <summary>
    /// Writes the open scene, and the shared objects with it.
    /// <para>
    /// Asking somebody to save two files to keep one project consistent is asking them to forget one.
    /// </para>
    /// </summary>
    /// <remarks>
    /// shortcut: interfaces are not written here until they have a bench (tasks 16 and 17).
    /// </remarks>
    public void Save()
    {
        SaveClips();
        SaveBlends();
        SaveTerrains();
        if (Workspace.Loaded is not { Scene: not null } entry)
        {
            return;
        }

        if (entry.Path is null)
        {
            NameWanted?.Invoke();
            return;
        }

        SaveEntry(entry);
    }

    /// <summary>Asks for a name to save the open scene under, as saving a scene with no file does.</summary>
    public void AskForName()
    {
        if (Workspace.Loaded is { Scene: not null })
        {
            NameWanted?.Invoke();
        }
    }

    /// <summary>
    /// Gives the open scene a file in the project's scenes folder and writes it.
    /// <para>
    /// The scene keeps the file it had when the write fails. Dart keeps the new path, so a later save
    /// quietly tries the same failing file.
    /// </para>
    /// </summary>
    public void SaveAs(string name)
    {
        if (Workspace.Loaded is not { Scene: not null } entry || string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (name.Contains(Path.DirectorySeparatorChar))
        {
            Say("A scene name cannot contain a path.", SayLevel.Error);
            return;
        }

        var path = Workspace.PathFor(name);
        if (IoFile.Exists(path) && (entry.Path is null || !Workspace.SamePath(path, entry.Path)))
        {
            Say($"There is already a scene called {name}.", SayLevel.Error);
            return;
        }

        var before = entry.Path;
        entry.Path = path;
        if (!SaveEntry(entry))
        {
            entry.Path = before;
        }
    }

    private bool SaveEntry(SceneEntry entry)
    {
        if (!Write(entry))
        {
            return false;
        }

        if (entry.Id != Workspace.SharedSceneId)
        {
            SaveShared();
        }

        return true;
    }

    private void Say(string text, SayLevel level)
    {
        Last = (text, level);
        Said?.Invoke(text, level);
    }

    private void SaveShared()
    {
        var entry = Workspace.SharedEntry;
        if (!entry.NeverWritten && entry.SavedStamp == History.StampFor(entry.Id))
        {
            return;
        }

        // Nothing in it and never written: no reason to leave an empty file in somebody's repository.
        if (entry.Scene!.Length == 0 && entry.NeverWritten)
        {
            return;
        }

        // Said only when it fails, because the scene's own line is what somebody is waiting to read.
        if (TryWrite(entry) is { } failure)
        {
            Say($"Could not save the shared objects: {failure}", SayLevel.Error);
        }
    }

    private bool Write(SceneEntry entry)
    {
        if (TryWrite(entry) is { } failure)
        {
            Say($"Could not save {entry.Title}: {failure}", SayLevel.Error);
            return false;
        }

        Say($"Saved {Path.GetRelativePath(Workspace.ProjectDirectory, entry.Path!)}", SayLevel.Info);
        return true;
    }

    // Returns why it could not, or null once it is written and marked saved.
    private string? TryWrite(SceneEntry entry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(entry.Path!))!);
            IoFile.WriteAllText(entry.Path!, AtRest(() => SceneDocument.Encode(entry.Scene!, prefabs: Prefabs)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }

        entry.NeverWritten = false;
        entry.SavedStamp = History.StampFor(entry.Id);
        Workspace.Touched();
        return null;
    }

    private void Reveal(string? sceneId)
    {
        if (sceneId is null || Workspace.SceneFor(sceneId) is not { } scene)
        {
            return;
        }

        Selected = [.. Selected.Where(scene.Contains)];
        if (Primary is { } primary && !scene.Contains(primary))
        {
            Primary = Selected.Count == 0 ? null : Selected[^1];
            ForgetParts();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// The panels as they were last left in a mode, or its own arrangement when they never were, or
    /// were left before that arrangement changed.
    /// </summary>
    private DockLayout LayoutOf(EditorMode mode)
    {
        var own = mode.Layout();
        var saved = ReadLayout(mode);
        return saved is not null && saved.Revision >= own.Revision ? saved : own;
    }

    // One file for each mode, with the project. The scene's keeps the name it had before there were
    // modes, so a layout saved then still opens.
    private string LayoutFile(EditorMode mode) => Path.Combine(
        Workspace.ProjectDirectory,
        ".orblit",
        mode.Name == "scene" ? "layout.json" : $"layout.{mode.Name}.json");

    private DockLayout? ReadLayout(EditorMode mode)
    {
        try
        {
            var file = LayoutFile(mode);
            return IoFile.Exists(file) ? DockLayout.Read(IoFile.ReadAllText(file), Panels.Kinds()) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void WriteLayout(EditorMode mode, DockLayout layout)
    {
        try
        {
            var file = LayoutFile(mode);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            IoFile.WriteAllText(file, layout.ToText());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Not worth a message. A layout that cannot be saved comes back as the mode's own one,
            // which is a small loss and not one worth interrupting somebody over.
        }
    }
}

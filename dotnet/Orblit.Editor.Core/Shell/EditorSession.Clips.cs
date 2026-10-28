namespace Orblit.Editor.Core;

using Orblit.Motion;
using Doc = Orblit.Scene;

// Clips on the timeline: opened, attached to the chosen object, saved, and shown on the loaded scene.
// A scene is only ever read as a document with the clip's pose lifted off it.
public sealed partial class EditorSession
{
    private readonly ClipPreview _preview = new();
    private (string? Scene, string? Object, Doc.MotionComponent? Motion) _motionSelection;
    private bool _followingMotion;
    private bool _clipsUnsaved;

    /// <summary>The clips open on the timeline.</summary>
    public ClipBench Clips { get; }

    /// <summary>The project's files, which a new clip is made through.</summary>
    public AssetTree Assets { get; }

    /// <summary>The skeleton each model of the project says it has, read once a file.</summary>
    public ModelSkeletons Skeletons { get; }

    /// <summary>What the scene views show of a selected model's skeleton, as the timeline's chips set it.</summary>
    public SkeletonLook SkeletonLook { get; } = new();

    /// <summary>The model of the object the shown clip belongs to, or null when it has no owner or the owner has no model.</summary>
    public string? OwnerModel => Clips.Owner is { } owner ? Inspected?.Scene?[owner]?.MeshAsset : null;

    /// <summary>The animation an object plays, or null when it has none.</summary>
    public static Doc.MotionComponent? MotionOf(SceneObject target) =>
        target.Components.GetValueOrDefault(Doc.SceneComponents.Motion) as Doc.MotionComponent;

    /// <summary>Opens the clip at <paramref name="path"/> on the timeline, in the mode where the timeline is.</summary>
    public void OpenClip(string path)
    {
        EnterMode("animation");
        if (!ReadClip(path))
        {
            return;
        }

        _followingMotion = false;
        OpenPanel(PanelKind.Timeline);
    }

    /// <summary>Makes a clip in the project's clips folder, gives it to the chosen object and opens it.</summary>
    public void NewClip()
    {
        if (MakeIn("clips", NewAsset.Clip) is not { } path)
        {
            return;
        }

        AttachClip(path);
        OpenClip(path);
        Clips.Owner = InspectedItem?.Id;
        _followingMotion = InspectedItem is not null;
    }

    /// <summary>
    /// Saves <paramref name="clip"/>, made to move <paramref name="model"/>, in the project's clips folder, and
    /// opens it. The first is left as it was.
    /// </summary>
    /// <param name="clip">The retargeted clip.</param>
    /// <param name="model">The project path of the model it moves.</param>
    /// <param name="problems">What of the first clip could not be carried.</param>
    public void SaveRetargeted(ClipDocument clip, string model, IReadOnlyList<string> problems)
    {
        if (FolderOf("clips") is not { } folder)
        {
            return;
        }

        var written = Assets.Write(folder, $"{clip.Name}.oclip", clip.Encode());
        if (written.Path is not { } path)
        {
            Say($"Could not save the clip: {written.Problem}", SayLevel.Error);
            return;
        }

        OpenClip(path);
        // Played by the model it was made for, so it goes on the object that is chosen only when that is the
        // model. Giving it the clip makes the timeline follow that object's motion, which is what shows it.
        if (InspectedItem is { } chosen && chosen.MeshAsset == model)
        {
            AttachClip(path);
        }

        var name = Path.GetFileName(path);
        Say(
            problems.Count == 0 ? $"Made {name}." : $"Made {name}. {problems.Count} things could not be carried. First: {problems[0]}",
            problems.Count == 0 ? SayLevel.Info : SayLevel.Warning);
    }

    /// <summary>
    /// Links the clip through the same component the game reads. The first one an object is given is
    /// the one it plays on start.
    /// </summary>
    public void AttachClip(string path)
    {
        if (InspectedItem is not { } target || Inspected is not { } entry)
        {
            return;
        }

        var before = MotionOf(target);
        var relative = Assets.Relative(path);
        if (before?.Clips.Contains(relative) ?? false)
        {
            return;
        }

        History.Seal();
        Run(
            new SetObjectComponent(
                entry.Id,
                target.Id,
                $"Add animation to {target.Name}",
                Doc.SceneComponents.Motion,
                before,
                new Doc.MotionComponent([.. before?.Clips ?? [], relative], before is null ? relative : before.Autoplay)));
        History.Seal();
    }

    private bool ReadClip(string path) =>
        ReadFile(path, "clip", Clips.OpenFile, error => error is ClipFormatException);

    // Opens a file of a kind a bench reads, says what could not be read, and answers whether it is
    // open. The noun is what the messages call the file.
    private bool ReadFile(string path, string noun, Func<string, IReadOnlyList<string>> open, Func<Exception, bool> isFormat)
    {
        IReadOnlyList<string> problems;
        try
        {
            problems = open(path);
        }
        catch (Exception error) when (isFormat(error))
        {
            Say($"{Path.GetFileName(path)} is not a {noun}: {error.Message}", SayLevel.Error);
            return false;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"{Path.GetFileName(path)} could not be read: {error.Message}", SayLevel.Error);
            return false;
        }

        if (problems.Count > 0)
        {
            Say(
                problems.Count == 1
                    ? problems[0]
                    : $"{problems.Count} things in that {noun} could not be read. First: {problems[0]}",
                SayLevel.Warning);
        }

        return true;
    }

    // A folder of the project, made when it is not there. Null, having said why, when it cannot be.
    private string? FolderOf(string folder)
    {
        var directory = Path.Combine(Workspace.ProjectDirectory, folder);
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"Could not make the {folder} folder: {error.Message}", SayLevel.Error);
            return null;
        }

        return directory;
    }

    // Makes a new file of a kind in a folder of the project, and the folder first. Null, having said
    // why, when either cannot be made.
    private string? MakeIn(string folder, NewAsset kind)
    {
        if (FolderOf(folder) is not { } directory)
        {
            return null;
        }

        var made = Assets.Create(directory, kind, kind.Suggested);
        if (made.Path is null)
        {
            Say($"Could not make a new {kind.Label.ToLowerInvariant()}: {made.Problem}", SayLevel.Error);
        }

        return made.Path;
    }

    // Shows the chosen object's own clip on the timeline, and takes it off again when something with
    // none is chosen. Only where the timeline is, and only when the choice is new.
    private void FollowMotion(bool force = false)
    {
        var target = InspectedItem;
        var motion = target is null ? null : MotionOf(target);
        var selection = (Workspace.Loaded?.Id, target?.Id, motion);
        if (!force && selection == _motionSelection)
        {
            return;
        }

        _motionSelection = selection;
        if (Mode.Name is not ("scene" or "animation"))
        {
            return;
        }

        if (target is not null && motion is not null)
        {
            ShowMotion(target, motion);
            return;
        }

        if (_followingMotion)
        {
            Clips.Hide();
        }

        _followingMotion = false;
        FoldMotionPanel();
    }

    private void ShowMotion(SceneObject target, Doc.MotionComponent motion)
    {
        Clips.Hide();
        _followingMotion = true;
        if (motion.Clips.Count > 0 && ReadClip(Path.Combine(Workspace.ProjectDirectory, motion.Clips[0])))
        {
            Clips.Owner = target.Id;
        }

        if (Mode.Name == "scene")
        {
            Layout = Layout.OpenBottom(new DockPanel("timeline", PanelKind.Timeline));
        }
    }

    // In the scene mode the timeline comes with an animated object and goes with it.
    private void FoldMotionPanel()
    {
        if (Mode.Name != "scene" || Layout.GroupOf("timeline") is not { } group)
        {
            return;
        }

        if (group.Current?.Id == "timeline")
        {
            Layout = Layout.Collapse(group.Id);
        }
    }

    // Writes every clip and cutscene with changes, and says which could not be written.
    private void SaveClips()
    {
        foreach (var problem in Clips.SaveAll().Concat(Cutscenes.SaveAll()))
        {
            Say(problem, SayLevel.Error);
        }
    }

    // Runs read on the loaded scene as it rests. The pose comes off first and goes back on after, so
    // what is saved or copied is the scene and not wherever the playhead happened to be.
    private T AtRest<T>(Func<T> read)
    {
        if (Workspace.Loaded?.Scene is not { } scene || !_preview.Posing)
        {
            return read();
        }

        _preview.Lift(scene);
        try
        {
            return read();
        }
        finally
        {
            OnClipsChanged();
        }
    }

    private void AtRest(Action work) => AtRest(() =>
    {
        work();
        return true;
    });

    // Posing is looking, not editing, so it is not a step on the undo stack. The scene's panels are
    // told so they redraw, and the window only when whether a clip has changes does, not at every
    // playhead move.
    //
    // shortcut: a pose that only moves things tells every panel, where a drag tells the views that
    // show movement. Split them when playing a clip makes the outliner stutter.
    private void OnClipsChanged()
    {
        var change = Workspace.Loaded?.Scene is { } scene ? Pose(scene) : PoseChange.None;
        if (change != PoseChange.None)
        {
            Workspace.Touched();
        }

        var flipped = ClipsUnsaved != _clipsUnsaved;
        _clipsUnsaved = ClipsUnsaved;
        if (flipped)
        {
            Changed?.Invoke();
        }
    }

    // Cinematics shows the cutscene, which names things by the scene's own ids. Every other mode shows
    // the clip, which is played by one object.
    private PoseChange Pose(EditorScene scene) => Mode.Name == "cinematics"
        ? _preview.PoseAll(scene, Cutscenes.Clip, Cutscenes.At)
        : _preview.Pose(scene, Clips.Clip, Clips.Owner, Clips.At);

    private bool ClipsUnsaved => Clips.AnyUnsaved || Cutscenes.AnyUnsaved;

    // A change from the undo stack can add or take away an animation, which the timeline follows.
    private void OnHistoryChanged()
    {
        // An edit applies before the stack records it, so the bench's own change reads the old answer.
        // Taken here, the bench telling us it was saved is a flip we see.
        _blendsUnsaved = Blends.AnyUnsaved;
        var layout = Layout;
        FollowMotion();
        LetGoOfAMissingCamera();
        var flipped = ClipsUnsaved != _clipsUnsaved;
        _clipsUnsaved = ClipsUnsaved;
        if (flipped || !ReferenceEquals(layout, Layout))
        {
            Changed?.Invoke();
        }
    }
}

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

    // Says what could not be read, and answers whether the clip is open.
    private bool ReadClip(string path)
    {
        IReadOnlyList<string> problems;
        try
        {
            problems = Clips.OpenFile(path);
        }
        catch (ClipFormatException error)
        {
            Say($"{Path.GetFileName(path)} is not a clip: {error.Message}", SayLevel.Error);
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
                    : $"{problems.Count} things in that clip could not be read. First: {problems[0]}",
                SayLevel.Warning);
        }

        return true;
    }

    // Makes a new file of a kind in a folder of the project, and the folder first. Null, having said
    // why, when either cannot be made.
    private string? MakeIn(string folder, NewAsset kind)
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

    // Writes every clip with changes, and says which could not be written.
    private void SaveClips()
    {
        foreach (var problem in Clips.SaveAll())
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
        var change = Workspace.Loaded?.Scene is { } scene
            ? _preview.Pose(scene, Clips.Clip, Clips.Owner, Clips.At)
            : PoseChange.None;
        if (change != PoseChange.None)
        {
            Workspace.Touched();
        }

        if (Clips.AnyUnsaved != _clipsUnsaved)
        {
            _clipsUnsaved = Clips.AnyUnsaved;
            Changed?.Invoke();
        }
    }

    // A change from the undo stack can add or take away an animation, which the timeline follows.
    private void OnHistoryChanged()
    {
        var layout = Layout;
        FollowMotion();
        if (!ReferenceEquals(layout, Layout))
        {
            Changed?.Invoke();
        }
    }
}

namespace Orblit.Editor.Core;

using Orblit.Motion;

// Cutscenes: opened under the scene view, cut to the scene's cameras, and framed by steering a camera
// from a view. A cutscene is played on the loaded scene and names things by that scene's own ids.
public sealed partial class EditorSession
{
    /// <summary>The cutscenes open on the cutscene timeline.</summary>
    public ClipBench Cutscenes { get; }

    /// <summary>What a key or a mark goes into: the clip in most modes, and the cutscene in cinematics.</summary>
    public IKeying Keying => Mode.Name == "cinematics" ? Cutscenes : Clips;

    /// <summary>
    /// The camera a view is looking through, and which view. Null when none is. Steering that view moves
    /// the camera instead of the view's own eye.
    /// </summary>
    public (string Camera, string View)? Piloting { get; private set; }

    /// <summary>Opens the cutscene at <paramref name="path"/> under the scene view, in the mode where the cutscene timeline is.</summary>
    public void OpenCutscene(string path)
    {
        EnterMode("cinematics");
        if (ReadFile(path, "cutscene", Cutscenes.OpenFile, error => error is CutsceneFormatException))
        {
            OpenPanel(PanelKind.CutsceneTimeline);
        }
    }

    /// <summary>Makes a cutscene in the project's cutscenes folder and opens it.</summary>
    public void NewCutscene()
    {
        if (MakeIn("cutscenes", NewAsset.Cutscene) is { } path)
        {
            OpenCutscene(path);
        }
    }

    /// <summary>
    /// Cuts to the selected camera, or the scene's first, from the playhead to the end of the cutscene.
    /// Does nothing when no cutscene is open.
    /// </summary>
    public void AddShot()
    {
        if (Cutscenes.Shown?.Shots is null || Workspace.Loaded?.Scene is not { } scene)
        {
            return;
        }

        var chosen = Primary is { } primary ? scene[primary] : null;
        var camera = chosen?.Kind == ObjectKind.Camera ? chosen : scene.Objects.FirstOrDefault(one => one.Kind == ObjectKind.Camera);
        if (camera is null)
        {
            Say("Add a camera to the scene first, or choose Use this view.", SayLevel.Warning);
            return;
        }

        CutTo(camera.Id);
    }

    /// <summary>
    /// Adds a camera where a view is looking, selects it, and cuts to it from the playhead. Two undo
    /// steps, so taking the cut back leaves the camera. Does nothing when no cutscene is open.
    /// </summary>
    public void UseThisView(Placement view)
    {
        if (Cutscenes.Shown?.Shots is null || Workspace.Loaded is not { Scene: { } scene } open)
        {
            return;
        }

        var place = view.Copy();
        var made = new SceneObject(NextObjectId(), UniqueName(scene, "Camera"), ObjectKind.Camera, position: place.Position, rotation: place.Rotation);
        Run(new AddObject(made, open.Id));
        SelectObject(made.Id);
        CutTo(made.Id);
    }

    /// <summary>
    /// Has <paramref name="view"/> look through a camera, or stop when <paramref name="camera"/> is null
    /// or is not in the scene. Steering after this is a new undo step.
    /// </summary>
    public void LookThrough(string? camera, string view = "scene")
    {
        History.Seal();
        Pilot(camera is not null && Workspace.Loaded?.Scene?[camera] is not null ? (camera, view) : null);
    }

    /// <summary>
    /// Puts the camera <paramref name="view"/> is looking through where that view now is, as one undo
    /// step however long the drag. Does nothing for a view that is not looking through one.
    /// </summary>
    public void Steer(string view, Placement to)
    {
        if (Piloting is not { } look || look.View != view || Workspace.Loaded is not { } open)
        {
            return;
        }

        if (open.Scene?[look.Camera] is not { } camera)
        {
            Pilot(null);
            return;
        }

        Run(new PlaceObject(open.Id, camera.Id, camera.Name, new Placement(camera.Position, camera.Rotation), to));
    }

    private void CutTo(string camera)
    {
        if (Cutscenes.Clip is not { } clip)
        {
            return;
        }

        var at = Cutscenes.Frame;
        Cutscenes.EditShots("Add shot", shots => Cinematics.CutTo(shots, camera, at, clip.Duration));
    }

    private void Pilot((string Camera, string View)? next)
    {
        if (Piloting == next)
        {
            return;
        }

        Piloting = next;
        Changed?.Invoke();
    }
}

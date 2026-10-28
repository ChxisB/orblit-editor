namespace Orblit.Editor.Core;

using Orblit.Mesh;
using Orblit.VectorMath;

// Where the texture of the selected faces sits, and moving it.
public sealed partial class EditorSession
{
    // Shared by the frames of one drag or slider, so they are one step to undo. Null between them.
    private object? _uvRun;

    public UvGesture UvGesture { get; private set; }

    public void UseUvGesture(UvGesture gesture)
    {
        UvGesture = gesture;
        Changed?.Invoke();
    }

    /// <summary>The mesh the coordinate view draws, or null with no shape selected.</summary>
    public Mesh? UvMesh => ShapeSelected()?.Mesh;

    /// <summary>
    /// The faces the coordinate view shows. Only faces have coordinates of their own: a vertex has
    /// as many as it has faces, and asking which one somebody means has no good answer.
    /// </summary>
    public ElementSelection UvSelection => ElementMode == ElementMode.Face ? Elements : new ElementSelection();

    /// <summary>Whether the parts are being edited in a mode that has no coordinates to show.</summary>
    public bool UvWrongMode => Context == EditContext.Element && ElementMode != ElementMode.Face;

    public void RunUvAction(UvAction action) => EditUvs(action.Label(), (mesh, faces) =>
    {
        switch (action)
        {
            case UvAction.Freeze:
                mesh.FreezeUvs(faces);
                break;
            case UvAction.Release:
                MeshUvs.ReleaseUvs(faces);
                break;
            case UvAction.Fit:
                mesh.FitUvs(faces);
                break;
            case UvAction.Planar:
                mesh.ProjectPlanar(faces);
                break;
            default:
                mesh.ProjectBox(faces);
                break;
        }
    }, live: false);

    /// <summary>A frame of a drag. <see cref="EndUvRun"/> says it is over.</summary>
    public void NudgeUvs(Vector2 by) => EditUvs("Move texture", (_, faces) => MeshUvs.NudgeUvs(faces, by), live: true);

    public void ScaleUvs(Vector2 by) => EditUvs("Scale texture", (mesh, faces) => mesh.ScaleUvs(faces, by), live: true);

    public void TurnUvs(double degrees) => EditUvs("Turn texture", (mesh, faces) => mesh.TurnUvs(faces, degrees), live: true);

    /// <summary>
    /// Gives the selected faces a new rule. A slider passes <paramref name="live"/> while it is held,
    /// and the field host seals the step when it lets go.
    /// </summary>
    public void SetUvRule(FaceUv rule, bool live) => EditUvs("Texture", (_, faces) =>
    {
        foreach (var face in faces)
        {
            face.Uv = rule;
        }
    }, live);

    /// <summary>A drag ended, so the next one is a step of its own.</summary>
    public void EndUvRun()
    {
        _uvRun = null;
        History.Seal();
    }

    // One coordinate edit, on a copy, through the undo stack. A run of live ones folds into one step,
    // which a drag needs and a button must not have: two presses of Fit are two things somebody did.
    private void EditUvs(string what, Action<Mesh, List<Face>> change, bool live)
    {
        if (ShapeSelected() is not { } chosen)
        {
            return;
        }

        var next = chosen.Mesh.Copy();
        var faces = UvSelection.FacesIn(next);
        if (faces.Count == 0)
        {
            return;
        }

        change(next, faces);
        _uvRun = live ? _uvRun ?? new object() : null;
        Run(new SetGeometry(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, next, what, _uvRun));
        Changed?.Invoke();
    }
}

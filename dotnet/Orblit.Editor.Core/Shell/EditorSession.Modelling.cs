namespace Orblit.Editor.Core;

using Orblit.Mesh;

// What the modelling panel asks for: running a mesh action, how much it does, painting faces and the
// materials a shape wears.
public sealed partial class EditorSession
{
    private readonly Dictionary<string, double> _amounts = [];

    /// <summary>Whether picking reaches the parts on the far side of the shape.</summary>
    public bool SeeThrough { get; private set; }

    /// <summary>The selected shape, or null when what is selected is not one or nothing is.</summary>
    public SceneObject? ChosenShape => ShapeSelected()?.Object;

    /// <summary>
    /// The name of the selected object when it is not a shape, or null.
    /// </summary>
    /// <remarks>
    /// So a selected model is not answered as if nothing were selected.
    /// </remarks>
    public string? NotShapeName => ShapeSelected() is null
        && Primary is { } primary
        && Workspace.SceneHolding(primary)?.Scene?[primary] is { Kind: not ObjectKind.Shape } other
            ? other.Name
            : null;

    public void SetSeeThrough(bool on)
    {
        SeeThrough = on;
        Changed?.Invoke();
    }

    /// <summary>How much an action does: what somebody set, or the number it starts with.</summary>
    public double AmountOf(MeshAction action) => _amounts.GetValueOrDefault(action.Label, action.Amount?.Value ?? 1);

    public void SetAmount(MeshAction action, double amount)
    {
        _amounts[action.Label] = amount;
        Changed?.Invoke();
    }

    /// <summary>
    /// Does one of the mesh actions and puts the result on the undo stack.
    /// </summary>
    /// <remarks>
    /// On the selected shape, not the one being edited part by part, so an action on the whole object
    /// works without going into its geometry first.
    /// </remarks>
    public void RunMeshAction(MeshAction action)
    {
        if (ShapeSelected() is not { } chosen)
        {
            return;
        }

        var next = chosen.Mesh.Copy();
        var after = action.Run(next, Elements, AmountOf(action));
        Run(new SetGeometry(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, next, action.Label));
        Elements = after;
        Changed?.Invoke();
    }

    /// <summary>Paints the selected faces with the material slot at this position.</summary>
    public void PaintFaces(int slot)
    {
        if (ShapeSelected() is not { } chosen)
        {
            return;
        }

        var next = chosen.Mesh.Copy();
        var faces = Elements.FacesIn(next);
        if (faces.Count == 0)
        {
            return;
        }

        foreach (var face in faces)
        {
            face.Material = slot;
        }

        Run(new SetGeometry(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, next, "Paint"));
        Changed?.Invoke();
    }

    /// <summary>
    /// Replaces the selected shape's material slots. A slider passes <paramref name="live"/> while it
    /// is held, and the field host seals the step when it lets go.
    /// </summary>
    public void ReplaceSurfaces(List<Surface> surfaces, bool live)
    {
        if (ShapeSelected() is not { } chosen)
        {
            return;
        }

        // A key per shape, so the frames of one slider run fold into one step and a run on another
        // shape never folds into this one.
        object? run = live ? $"surfaces/{chosen.Object.Id}" : null;
        Run(new SetSurfaces(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, surfaces, "Materials", run));
        Changed?.Invoke();
    }
}

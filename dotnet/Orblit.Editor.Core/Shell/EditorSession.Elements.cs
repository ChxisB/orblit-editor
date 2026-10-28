namespace Orblit.Editor.Core;

using Orblit.Mesh;

// Whether the selection is being edited as a whole or part by part, and which parts.
public sealed partial class EditorSession
{
    /// <summary>The selected shape with the scene it is in and the mesh it draws.</summary>
    private sealed record ShapeChosen(SceneEntry Entry, SceneObject Object, Mesh Mesh);

    public EditContext Context { get; private set; }

    public ElementMode ElementMode { get; private set; }

    /// <summary>What is selected inside the shape. Replaced, not changed, so a view can tell it moved.</summary>
    public ElementSelection Elements { get; private set; } = new();

    /// <summary>Whether the selection could be edited part by part.</summary>
    public bool CanEditParts => ShapeSelected() is not null;

    /// <summary>
    /// The selected shape, whichever context is on.
    /// </summary>
    /// <remarks>
    /// Separate from editing parts. Conforming the normals of a whole shape is something somebody
    /// does to the object without going into it, and requiring them to would be a mode for no reason.
    /// </remarks>
    private ShapeChosen? ShapeSelected()
    {
        if (Primary is not { } primary || Workspace.SceneHolding(primary) is not { Scene: { } scene } entry)
        {
            return null;
        }

        return scene[primary] is { Kind: ObjectKind.Shape } one && one.CurrentMesh is { IsEmpty: false } mesh
            ? new ShapeChosen(entry, one, mesh)
            : null;
    }

    public void SetContext(EditContext context)
    {
        if (context == EditContext.Element && !CanEditParts)
        {
            return;
        }

        Context = context;
        if (context == EditContext.Whole)
        {
            Elements.Clear();
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// Backs out one level: a drawing is given up first, and otherwise the parts go back to the object.
    /// </summary>
    /// <remarks>
    /// Somebody halfway through an outline who presses escape means the outline, not the geometry.
    /// </remarks>
    public void LeaveEdit()
    {
        if (Sketch.Tool.IsDrawing())
        {
            GiveUpDrawing();
        }
        else
        {
            SetContext(EditContext.Whole);
        }
    }
}

namespace Orblit.Editor.Core;

using Orblit.Mesh;

// Whether the selection is being edited as a whole or part by part, and which parts.
public sealed partial class EditorSession
{
    /// <summary>The selected shape with the scene it is in and the mesh it draws.</summary>
    private sealed record ShapeChosen(SceneEntry Entry, SceneObject Object, Mesh Mesh);

    private object? _dragRun;

    public EditContext Context { get; private set; }

    public ElementMode ElementMode { get; private set; } = ElementMode.Face;

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

    public void SetElementMode(ElementMode mode)
    {
        ElementMode = mode;
        Changed?.Invoke();
    }

    /// <summary>A click on a part: replaces the selection, or with <paramref name="add"/> toggles it in or out.</summary>
    public void PickElements(ElementSelection picked, bool add) => Reselect(add, next =>
    {
        Toggle(next.Vertices, picked.Vertices);
        Toggle(next.Edges, picked.Edges);
        Toggle(next.Faces, picked.Faces);
    });

    /// <summary>A box round parts: replaces the selection, or with <paramref name="add"/> adds to it. Empty clears it.</summary>
    public void BoxElements(ElementSelection boxed, bool add) => Reselect(add, next =>
    {
        next.Vertices.AddRange(boxed.Vertices);
        next.Edges.AddRange(boxed.Edges);
        next.Faces.AddRange(boxed.Faces);
    });

    /// <summary>
    /// Takes a mesh a viewport drag has changed, as a step named for the gesture.
    /// </summary>
    /// <remarks>
    /// Every frame after the first folds into the first, so a drag across the screen is one thing to
    /// undo however many frames it took.
    /// </remarks>
    /// <param name="mesh">The whole mesh as the drag has taken it.</param>
    /// <param name="selection">What is selected now. An extrude replaces it with the faces it made.</param>
    /// <param name="what">What the step is called: "Move", "Turn" or "Extrude".</param>
    /// <param name="merge">Whether this frame follows one of the same drag.</param>
    public void DragElements(Mesh mesh, ElementSelection selection, string what, bool merge)
    {
        if (ShapeSelected() is not { } chosen)
        {
            return;
        }

        // A key that lasts the gesture, new on its first frame, so two drags of one face never fold together.
        if (!merge)
        {
            _dragRun = new object();
        }

        Run(new SetGeometry(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, mesh, what, _dragRun));
        Elements = selection;
        Changed?.Invoke();
    }

    private void Reselect(bool add, Action<ElementSelection> change)
    {
        var next = add ? Elements.Copy() : new ElementSelection();
        change(next);
        Elements = next;
        Changed?.Invoke();
    }

    private static void Toggle<T>(OrderedSet<T> set, IEnumerable<T> these)
        where T : notnull
    {
        foreach (var one in these)
        {
            if (!set.Remove(one))
            {
                set.Add(one);
            }
        }
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

namespace Orblit.Editor.Core;

using Orblit.Mesh;
using Orblit.VectorMath;

// Drawing a shape with the pointer, and cutting one that is already there.
public sealed partial class EditorSession
{
    /// <summary>The drawing in progress, shared by every view of the scene.</summary>
    public Sketch Sketch { get; } = new();

    /// <summary>Starts a drawing tool, or stops it when it is already the one in use.</summary>
    public void UseTool(ViewportTool tool)
    {
        if (Sketch.Tool == tool)
        {
            Sketch.Clear();
        }
        else
        {
            Sketch.Start(tool);
            if (tool == ViewportTool.Cut && ShapeSelected() is null)
            {
                Sketch.Clear();
                Say("Select a shape to cut first.", SayLevel.Warning);
            }
        }

        Changed?.Invoke();
    }

    /// <summary>Puts down one point.</summary>
    public void DrawPoint(DrawnPoint point)
    {
        Sketch.PlaneAt(point.Origin, point.Normal, point.Face);
        Sketch.Add(point.At);
        Changed?.Invoke();
    }

    /// <summary>Finishes whatever is being drawn.</summary>
    public void FinishDrawing()
    {
        if (!Sketch.Tool.IsDrawing())
        {
            return;
        }

        if (!Sketch.CanFinish)
        {
            Say(
                Sketch.Tool == ViewportTool.Cut
                    ? "A cut needs two points, both on the edge of a face."
                    : "A shape needs three points.",
                SayLevel.Warning);
        }
        else if (Sketch.Tool == ViewportTool.Cut)
        {
            ApplyCut();
        }
        else
        {
            MakeDrawnShape();
        }
    }

    /// <summary>Takes the last point back.</summary>
    public void TakeBackPoint()
    {
        Sketch.Undo();
        Changed?.Invoke();
    }

    public void GiveUpDrawing()
    {
        Sketch.Clear();
        Changed?.Invoke();
    }

    /// <summary>Takes a point back while drawing, and otherwise deletes what is selected.</summary>
    public void Backspace()
    {
        if (Sketch.Tool.IsDrawing())
        {
            TakeBackPoint();
        }
        else
        {
            DeleteSelection();
        }
    }

    // Turns the outline into an object.
    private void MakeDrawnShape()
    {
        if (Working is not { Scene: { } scene } open)
        {
            return;
        }

        if (Sketch.Crosses())
        {
            Say("That outline crosses itself.", SayLevel.Warning);
            return;
        }

        // The points are kept relative to where the object stands, so moving the object later moves
        // the outline with it rather than leaving the two describing different places.
        var middle = Vector3.Zero();
        foreach (var at in Sketch.Points)
        {
            middle.Add(at);
        }

        middle.Scale(1.0 / Sketch.Points.Count);

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, "Shape"),
            kind: ObjectKind.Shape,
            position: middle,
            outline: new PolyShape([.. Sketch.Points.Select(at => at - middle)], height: 2),
            colour: new Colour(0xFF8E99A8));

        Run(new AddObject(added, open.Id));
        Sketch.Clear();
        SelectObject(added.Id);
        Say($"Drew {added.Name}. Its height is in the inspector.", SayLevel.Info);
    }

    // Cuts the face the path was drawn on.
    private void ApplyCut()
    {
        if (ShapeSelected() is not { } chosen || Sketch.Face is not { } face)
        {
            Say("There is no face to cut.", SayLevel.Warning);
            return;
        }

        var next = chosen.Mesh.Copy();
        if (face < 0 || face >= next.Faces.Count)
        {
            GiveUpDrawing();
            return;
        }

        // Into the object's own space, which is where its geometry lives.
        if (SceneMath.TryInvert(chosen.Entry.Scene!.WorldOf(chosen.Object.Id)) is not { } inverse)
        {
            Say("That shape has no size, so it cannot be cut.", SayLevel.Warning);
            return;
        }

        var path = Sketch.Points.Select(at => inverse.Transformed3(at.Clone())).ToList();
        var made = next.CutFace(next.Faces[face], path);
        if (made.Count == 0)
        {
            Say("A cut has to start and end on the edge of the face, or come back to where it began.", SayLevel.Warning);
            return;
        }

        Run(new SetGeometry(chosen.Entry.Id, chosen.Object.Id, chosen.Object.Name, next, "Cut"));
        Sketch.Clear();

        // What came out of the cut, because that is what somebody is about to extrude, which is why
        // they cut it.
        Elements = new ElementSelection(faces: Enumerable.Range(0, next.Faces.Count).Where(at => made.Contains(next.Faces[at])));
        ElementMode = ElementMode.Face;
        Context = EditContext.Element;
        Changed?.Invoke();
        Say($"Cut into {made.Count} faces.", SayLevel.Info);
    }
}

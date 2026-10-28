namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

// Drawing a shape and cutting one, as the session does it. The Dart puts the points down through
// the viewport's callback and finishes through another. Here they are the session's own methods.
public sealed class DrawingSessionTests : IDisposable
{
    private static readonly Vector3 Up = new(0, 1, 0);
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_drawing");
    private readonly List<(string Text, SayLevel Level)> _said = [];

    public void Dispose() => _project.Delete(recursive: true);

    private EditorSession Open()
    {
        var workspace = new Workspace(_project.FullName);
        var path = Path.Combine(_project.FullName, "scenes", "main.oscene");
        workspace.Add(new SceneEntry("a", "A", path, EditorScene.Starter(), neverWritten: true));
        var session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn())
        {
            Clock = new FixedClock(new DateTime(2026, 10, 28, 9, 0, 0)),
        };
        session.Said += (text, level) => _said.Add((text, level));
        return session;
    }

    private static EditorScene SceneOf(EditorSession session) => session.Workspace["a"]!.Scene!;

    private static List<SceneObject> ShapesIn(EditorSession session) =>
        [.. SceneOf(session).Objects.Where(one => one.Kind == ObjectKind.Shape)];

    // On the ground, which is where the viewport puts the first point when nothing is under it.
    private static void Point(EditorSession session, double x, double z) =>
        session.DrawPoint(new DrawnPoint(new Vector3(x, 0, z), Vector3.Zero(), Up, null));

    private static void Triangle(EditorSession session, double x = 0, double z = 0)
    {
        Point(session, x, z);
        Point(session, x, z + 2);
        Point(session, x + 2, z + 2);
    }

    private (EditorSession Session, SceneObject Cube, int Top) EditedCube()
    {
        var session = Open();
        session.AddShape(ShapeKind.Cube);
        var cube = ShapesIn(session).Single();
        session.SetContext(EditContext.Element);
        var mesh = cube.CurrentMesh!;
        return (session, cube, mesh.Faces.FindIndex(face => mesh.NormalOf(face).Y > 0.99));
    }

    private static void CutAcrossTheTop(EditorSession session, SceneObject cube, int top, bool fromMiddle = false)
    {
        var mesh = cube.CurrentMesh!;
        var corners = mesh.PointsOf(mesh.Faces[top]);
        session.UseTool(ViewportTool.Cut);
        var first = fromMiddle ? mesh.CentreOf(mesh.Faces[top]) : (corners[0] + corners[1]) / 2;
        session.DrawPoint(new DrawnPoint(first, first, Up, top));
        var second = (corners[2] + corners[3]) / 2;
        session.DrawPoint(new DrawnPoint(second, second, Up, top));
        session.FinishDrawing();
    }

    [Fact]
    public void ThreePointsAndAFinishMakeAnObject()
    {
        var session = Open();
        var before = ShapesIn(session).Count;

        session.UseTool(ViewportTool.PolyShape);
        Triangle(session);
        session.FinishDrawing();

        var made = ShapesIn(session);
        Assert.Equal(before + 1, made.Count);
        Assert.Equal(3, made[^1].Outline!.Points.Count);
        // A solid: a top, a bottom and three walls.
        Assert.Equal(5, made[^1].CurrentMesh!.FaceCount);
    }

    [Fact]
    public void TheToolStopsItselfWhenTheShapeIsMade()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Triangle(session);

        session.FinishDrawing();

        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
        Assert.Empty(session.Sketch.Points);
    }

    [Fact]
    public void TheNewShapeIsWhatIsSelected()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Triangle(session);

        session.FinishDrawing();

        Assert.Equal(ShapesIn(session)[^1].Id, session.Primary);
        Assert.Contains(_said, said => said.Text.StartsWith("Drew Shape.", StringComparison.Ordinal));
    }

    [Fact]
    public void ThePointsAreKeptRelativeToWhereTheObjectStands()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Triangle(session, 10, 10);
        session.FinishDrawing();

        var made = ShapesIn(session)[^1];

        // Standing at the middle of what was drawn, with the points around the object's own origin,
        // so moving it later moves the outline with it.
        Assert.Equal(32.0 / 3, made.Position.X, 1e-9);
        var middle = Vector3.Zero();
        foreach (var at in made.Outline!.Points)
        {
            middle.Add(at);
        }

        Assert.Equal(0, middle.Length, 1e-9);
    }

    [Fact]
    public void TwoPointsAreNotEnoughAndItSaysSo()
    {
        var session = Open();
        var before = ShapesIn(session).Count;

        session.UseTool(ViewportTool.PolyShape);
        Point(session, 0, 0);
        Point(session, 0, 2);
        session.FinishDrawing();

        Assert.Equal(before, ShapesIn(session).Count);
        // Still drawing, so the next click carries on.
        Assert.Equal(ViewportTool.PolyShape, session.Sketch.Tool);
        Assert.Contains(_said, said => said is ("A shape needs three points.", SayLevel.Warning));
    }

    [Fact]
    public void AnOutlineThatCrossesItselfIsRefused()
    {
        var session = Open();
        var before = ShapesIn(session).Count;

        session.UseTool(ViewportTool.PolyShape);
        Point(session, 0, 0);
        Point(session, 2, 2);
        Point(session, 2, 0);
        Point(session, 0, 2);
        session.FinishDrawing();

        Assert.Equal(before, ShapesIn(session).Count);
        Assert.Contains(_said, said => said.Text == "That outline crosses itself.");
    }

    [Fact]
    public void BackspaceTakesAPointBackAndEscapeGivesUp()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Point(session, 0, 0);
        Point(session, 0, 2);

        session.Backspace();
        Assert.Single(session.Sketch.Points);

        session.LeaveEdit();
        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
    }

    [Fact]
    public void BackspaceDoesNotDeleteTheSelectionWhileDrawing()
    {
        var session = Open();
        session.AddShape(ShapeKind.Cube);
        var was = ShapesIn(session).Count;

        session.UseTool(ViewportTool.PolyShape);
        Point(session, 0, 0);
        session.Backspace();

        // A very unwelcome surprise halfway through an outline.
        Assert.Equal(was, ShapesIn(session).Count);
    }

    [Fact]
    public void BackspaceDeletesTheSelectionWhenNothingIsBeingDrawn()
    {
        var session = Open();
        session.AddShape(ShapeKind.Cube);

        session.Backspace();

        Assert.Empty(ShapesIn(session));
    }

    [Fact]
    public void FinishingWithNothingBeingDrawnDoesNothing()
    {
        var session = Open();

        session.FinishDrawing();

        Assert.Empty(_said);
    }

    [Fact]
    public void PressingTheToolAgainPutsItAway()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Point(session, 0, 0);

        session.UseTool(ViewportTool.PolyShape);

        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
        Assert.Empty(session.Sketch.Points);
    }

    [Fact]
    public void TheShapeIsOneStepToUndo()
    {
        var session = Open();
        var before = ShapesIn(session).Count;
        session.UseTool(ViewportTool.PolyShape);
        Triangle(session);
        session.FinishDrawing();

        session.Undo();

        Assert.Equal(before, ShapesIn(session).Count);
    }

    [Fact]
    public void TheHeightStartsAtTwo()
    {
        var session = Open();
        session.UseTool(ViewportTool.PolyShape);
        Triangle(session);
        session.FinishDrawing();

        var made = ShapesIn(session)[^1];

        Assert.Equal(2, made.Outline!.Height);
        Assert.Equal(2, made.CurrentMesh!.Bounds.Max.Y, 1e-9);
    }

    [Fact]
    public void ACutAcrossAFaceMakesTwoOfIt()
    {
        var (session, cube, top) = EditedCube();

        CutAcrossTheTop(session, cube, top);

        Assert.Equal(7, cube.CurrentMesh!.FaceCount);
        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
    }

    [Fact]
    public void WhatCameOutOfTheCutIsWhatIsSelected()
    {
        var (session, cube, top) = EditedCube();

        CutAcrossTheTop(session, cube, top);

        // Because the next thing somebody does is extrude it, which is why they cut it.
        Assert.Equal(2, session.Elements.Faces.Count);
        Assert.Equal(ElementMode.Face, session.ElementMode);
        Assert.Equal(EditContext.Element, session.Context);
    }

    [Fact]
    public void ACutThatStartsInTheMiddleIsRefused()
    {
        var (session, cube, top) = EditedCube();

        CutAcrossTheTop(session, cube, top, fromMiddle: true);

        // Nothing was divided, so nothing changed.
        Assert.Equal(6, cube.CurrentMesh!.FaceCount);
        Assert.Contains(_said, said => said.Text.StartsWith("A cut has to start and end", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCutIsOneStepToUndo()
    {
        var (session, cube, top) = EditedCube();
        CutAcrossTheTop(session, cube, top);

        session.Undo();

        Assert.Equal(6, cube.CurrentMesh!.FaceCount);
    }

    [Fact]
    public void ACutFollowsTheObjectToWhereItStands()
    {
        var (session, cube, top) = EditedCube();
        cube.Position.SetFrom(new Vector3(5, 0, 0));
        SceneOf(session).Invalidate();
        var mesh = cube.CurrentMesh!;
        var corners = mesh.PointsOf(mesh.Faces[top]);
        var moved = new Vector3(5, 0, 0);

        session.UseTool(ViewportTool.Cut);
        var first = ((corners[0] + corners[1]) / 2) + moved;
        var second = ((corners[2] + corners[3]) / 2) + moved;
        session.DrawPoint(new DrawnPoint(first, first, Up, top));
        session.DrawPoint(new DrawnPoint(second, second, Up, top));
        session.FinishDrawing();

        // The path is in the world and the geometry is in the object's own space.
        Assert.Equal(7, cube.CurrentMesh!.FaceCount);
    }

    [Fact]
    public void ACutNeedsTwoPoints()
    {
        var (session, cube, top) = EditedCube();
        session.UseTool(ViewportTool.Cut);
        var at = cube.CurrentMesh!.CentreOf(cube.CurrentMesh.Faces[top]);
        session.DrawPoint(new DrawnPoint(at, at, Up, top));

        session.FinishDrawing();

        Assert.Contains(_said, said => said is ("A cut needs two points, both on the edge of a face.", SayLevel.Warning));
        Assert.Equal(ViewportTool.Cut, session.Sketch.Tool);
    }

    [Fact]
    public void ACutOnAFaceTheMeshNoLongerHasJustStops()
    {
        var (session, cube, _) = EditedCube();
        session.UseTool(ViewportTool.Cut);
        var near = new Vector3(0, 1, 0);
        session.DrawPoint(new DrawnPoint(near, near, Up, 99));
        session.DrawPoint(new DrawnPoint(near + new Vector3(1, 0, 0), near, Up, 99));

        session.FinishDrawing();

        Assert.Equal(6, cube.CurrentMesh!.FaceCount);
        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
    }

    [Fact]
    public void TheCutToolRefusesToStartWithoutAShape()
    {
        var session = Open();

        session.UseTool(ViewportTool.Cut);

        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
        Assert.Contains(_said, said => said is ("Select a shape to cut first.", SayLevel.Warning));
    }

    [Fact]
    public void ThePartsCannotBeEditedWithoutAShape()
    {
        var session = Open();

        session.SetContext(EditContext.Element);

        Assert.Equal(EditContext.Whole, session.Context);
        Assert.False(session.CanEditParts);
    }

    [Fact]
    public void EscapeWithNothingDrawnGoesBackToTheWholeObject()
    {
        var (session, cube, top) = EditedCube();
        CutAcrossTheTop(session, cube, top);
        Assert.False(session.Elements.IsEmpty);

        session.LeaveEdit();

        Assert.Equal(EditContext.Whole, session.Context);
        Assert.True(session.Elements.IsEmpty);
    }

    [Fact]
    public void EscapeGivesUpTheDrawingBeforeLeavingTheGeometry()
    {
        var (session, _, _) = EditedCube();
        session.UseTool(ViewportTool.PolyShape);

        session.LeaveEdit();

        Assert.Equal(ViewportTool.None, session.Sketch.Tool);
        Assert.Equal(EditContext.Element, session.Context);
    }
}

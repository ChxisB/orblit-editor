using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;
using Offset = Orblit.Editor.Viewport.Offset;

// Added in C#: the Dart shell takes these through widget callbacks that its tests call directly, so a
// click, a box or a handle drag in a real view never met the shell's undo stack in a test.
public sealed class ElementEditsTests
{
    private static ElementSelection Faces(params int[] faces) => new(faces: [.. faces]);

    private static Mesh Moved(Mesh from, Vector3 by)
    {
        var next = from.Copy();
        next.Positions[0] = next.Positions[0] + by;
        return next;
    }

    [Fact]
    public void ADragIsOneStepToUndoHoweverManyFramesItTook()
    {
        using var rig = new ModellingRig(cube: true);
        var start = rig.Mesh;
        var added = rig.Session.History.UndoLabel;

        rig.Session.DragElements(Moved(start, new Vector3(0, 1, 0)), Faces(0), "Move", merge: false);
        rig.Session.DragElements(Moved(start, new Vector3(0, 2, 0)), Faces(0), "Move", merge: true);
        rig.Session.DragElements(Moved(start, new Vector3(0, 3, 0)), Faces(0), "Move", merge: true);

        Assert.Equal(3, rig.Mesh.Positions[0].Y - start.Positions[0].Y);
        Assert.Equal($"Move {rig.Session.ChosenShape!.Name}", rig.Session.History.UndoLabel);
        rig.Session.Undo();
        Assert.Equal(added, rig.Session.History.UndoLabel);
        Assert.True(rig.Session.ChosenShape!.IsParametric);
    }

    [Fact]
    public void TwoDragsOfTheSameFaceAreTwoSteps()
    {
        using var rig = new ModellingRig(cube: true);
        var start = rig.Mesh;
        rig.Session.DragElements(Moved(start, new Vector3(0, 1, 0)), Faces(0), "Move", merge: false);
        var afterFirst = rig.Mesh;
        rig.Session.DragElements(Moved(afterFirst, new Vector3(0, 1, 0)), Faces(0), "Move", merge: false);

        rig.Session.Undo();

        Assert.Equal(1, rig.Mesh.Positions[0].Y - start.Positions[0].Y);
        Assert.Equal($"Move {rig.Session.ChosenShape!.Name}", rig.Session.History.UndoLabel);
    }

    [Fact]
    public void WhatIsSelectedAfterADragIsWhatTheDragReports()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Edit(0);

        rig.Session.DragElements(rig.Mesh.Copy(), Faces(4, 5), "Extrude", merge: false);

        Assert.Equal([4, 5], rig.Session.Elements.Faces);
    }

    [Fact]
    public void ADragWithNoShapeSelectedChangesNothing()
    {
        using var rig = new ModellingRig();
        var before = rig.Session.History.UndoLabel;

        rig.Session.DragElements(new Mesh(), Faces(0), "Move", merge: false);

        Assert.Equal(before, rig.Session.History.UndoLabel);
    }

    [Fact]
    public void TheFirstCycleGoesIntoTheGeometryAndLeavesTheModeAlone()
    {
        using var rig = new ModellingRig(cube: true);
        var mode = rig.Session.ElementMode;

        rig.Session.CycleMode();

        Assert.Equal(EditContext.Element, rig.Session.Context);
        Assert.Equal(mode, rig.Session.ElementMode);
    }

    [Fact]
    public void ThenEachCycleGoesToTheNextMode()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Session.CycleMode();
        var start = rig.Session.ElementMode;
        var seen = new List<ElementMode>();

        for (var press = 0; press < 3; press++)
        {
            rig.Session.CycleMode();
            seen.Add(rig.Session.ElementMode);
        }

        Assert.Equal(3, seen.Distinct().Count());
        Assert.Equal(start.Next(), seen[0]);
        Assert.Equal(EditContext.Element, rig.Session.Context);
    }

    [Fact]
    public void ACycleWithNothingToEditStaysOutsideTheGeometry()
    {
        using var rig = new ModellingRig();

        rig.Session.CycleMode();

        Assert.Equal(EditContext.Whole, rig.Session.Context);
    }

    private static Offset OnTopFace(ViewportRig rig)
    {
        var shape = rig.Session.ChosenShape!;
        var mesh = shape.CurrentMesh!;
        var world = rig.Session.Workspace.Loaded!.Scene!.WorldOf(shape.Id);
        var projection = new ViewportProjection(rig.View.State.Camera, new Size(rig.View.Bounds.Width, rig.View.Bounds.Height));
        var top = mesh.Faces.First(face => mesh.NormalOf(face).Y > 0.99);
        var corners = top.Vertices.Select(index => projection.Project(world.Transformed3(mesh.Positions[index]))!.Value).ToList();
        return new Offset(corners.Average(c => c.Dx), corners.Average(c => c.Dy));
    }

    private static Point Over(ViewportRig rig, Offset at) => rig.Where(new Point(at.Dx, at.Dy));

    private static ViewportRig Editing()
    {
        var rig = new ViewportRig();
        rig.Session.AddShape(ShapeKind.Cube);
        rig.Session.SetContext(EditContext.Element);
        Headless.Render();
        return rig;
    }

    [Fact]
    public Task AClickOnAFaceChoosesItInTheSession() => Headless.Run(() =>
    {
        using var rig = Editing();
        var mesh = rig.Session.ChosenShape!.CurrentMesh!;

        rig.Click(Over(rig, OnTopFace(rig)));

        var chosen = Assert.Single(rig.Session.Elements.Faces);
        Assert.True(mesh.NormalOf(mesh.Faces[chosen]).Y > 0.99);
    });

    [Fact]
    public Task ABoxRoundTheShapeChoosesWhatIsInsideIt() => Headless.Run(() =>
    {
        using var rig = Editing();
        var from = rig.Where(new Point(20, 20));
        var to = rig.Where(new Point(rig.View.Bounds.Width - 20, rig.View.Bounds.Height - 20));

        rig.Window.MouseDown(from, MouseButton.Left);
        rig.Window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
        rig.Window.MouseMove(to);
        rig.Window.MouseUp(to, MouseButton.Left);

        Assert.NotEmpty(rig.Session.Elements.Faces);
    });

    [Fact]
    public Task DraggingAHandleMovesTheChosenFaceAsOneStepToUndo() => Headless.Run(() =>
    {
        using var rig = Editing();
        var mesh = rig.Session.ChosenShape!.CurrentMesh!;
        var top = mesh.Faces.FindIndex(face => mesh.NormalOf(face).Y > 0.99);
        rig.Session.PickElements(Faces(top), false);
        Headless.Render();
        var gizmo = rig.View.State.Handles!;
        var centre = gizmo.Projection.Project(gizmo.Pivot)!.Value;
        var end = gizmo.Projection.Project(gizmo.EndOf(GizmoAxis.Y))!.Value;
        var arm = Over(rig, centre + ((end - centre) * 0.6));
        var before = rig.Session.ChosenShape!.CurrentMesh!.Positions[mesh.Faces[top].Vertices[0]].Y;

        rig.Window.MouseMove(arm);
        rig.Window.MouseDown(arm, MouseButton.Left);
        rig.Window.MouseMove(new Point(arm.X, arm.Y - 20));
        rig.Window.MouseMove(new Point(arm.X, arm.Y - 40));
        rig.Window.MouseUp(new Point(arm.X, arm.Y - 40), MouseButton.Left);

        var moved = rig.Session.ChosenShape!;
        Assert.False(moved.IsParametric);
        Assert.NotEqual(before, moved.CurrentMesh!.Positions[mesh.Faces[top].Vertices[0]].Y);
        rig.Session.Undo();
        Assert.True(rig.Session.ChosenShape!.IsParametric);
    });

    [Fact]
    public void ChoosingAnotherObjectLeavesThePartsOfTheFirstBehind()
    {
        using var rig = new ModellingRig(cube: true);
        var first = rig.Session.Primary!;
        rig.Session.AddShape(ShapeKind.Cube);
        var second = rig.Session.Primary!;
        rig.Session.SelectObject(first);
        rig.Edit(0, 1);

        rig.Session.SelectObject(second);

        // Still editing, now the other shape, and with none of its faces chosen: a face number means
        // nothing on a mesh it was not picked from.
        Assert.Equal(EditContext.Element, rig.Session.Context);
        Assert.Empty(rig.Session.Elements.Faces);
    }

    [Fact]
    public void ChoosingSomethingThatHasNoPartsEndsTheEdit()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Edit(0);

        rig.Session.ClearSelection();

        Assert.Equal(EditContext.Whole, rig.Session.Context);
        Assert.Empty(rig.Session.Elements.Faces);
    }

    [Fact]
    public void ChoosingTheSameObjectAgainKeepsItsParts()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Edit(0, 1);

        rig.Session.SelectObject(rig.Session.Primary!);

        Assert.Equal([0, 1], rig.Session.Elements.Faces);
    }

    [Fact]
    public Task ShiftHeldOnTheHandleExtrudesTheFaceInsteadOfMovingIt() => Headless.Run(() =>
    {
        using var rig = Editing();
        var mesh = rig.Session.ChosenShape!.CurrentMesh!;
        var top = mesh.Faces.FindIndex(face => mesh.NormalOf(face).Y > 0.99);
        rig.Session.PickElements(Faces(top), false);
        Headless.Render();
        var gizmo = rig.View.State.Handles!;
        var centre = gizmo.Projection.Project(gizmo.Pivot)!.Value;
        var end = gizmo.Projection.Project(gizmo.EndOf(GizmoAxis.Y))!.Value;
        var arm = Over(rig, centre + ((end - centre) * 0.6));
        var held = RawInputModifiers.Shift | RawInputModifiers.LeftMouseButton;

        rig.Window.MouseMove(arm);
        rig.Window.MouseDown(arm, MouseButton.Left, RawInputModifiers.Shift);
        rig.Window.MouseMove(new Point(arm.X, arm.Y - 20), held);
        rig.Window.MouseMove(new Point(arm.X, arm.Y - 40), held);
        rig.Window.MouseUp(new Point(arm.X, arm.Y - 40), MouseButton.Left, RawInputModifiers.Shift);

        Assert.True(rig.Session.ChosenShape!.CurrentMesh!.Faces.Count > mesh.Faces.Count);
    });
}

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

public sealed class SketchTests
{
    private static readonly Vector3 Up = new(0, 1, 0);

    private static Sketch Started(ViewportTool tool)
    {
        var sketch = new Sketch();
        sketch.Start(tool);
        return sketch;
    }

    [Fact]
    public void ThePlaneIsFixedByTheFirstPoint()
    {
        var sketch = Started(ViewportTool.PolyShape);
        sketch.PlaneAt(Vector3.Zero(), Up);
        sketch.PlaneAt(new Vector3(9, 9, 9), new Vector3(1, 0, 0));

        // The second one is ignored.
        Assert.Equal(1, sketch.Normal!.Y);
        Assert.Equal(0, sketch.Origin!.X);
    }

    [Fact]
    public void ASecondClickInTheSamePlaceIsOnePoint()
    {
        var sketch = Started(ViewportTool.PolyShape);

        sketch.Add(new Vector3(1, 0, 1));
        sketch.Add(new Vector3(1, 0, 1));

        Assert.Single(sketch.Points);
    }

    [Fact]
    public void TakingBackTheFirstPointGivesBackThePlaneToo()
    {
        var sketch = Started(ViewportTool.PolyShape);
        sketch.PlaneAt(Vector3.Zero(), Up, onFace: 3);
        sketch.Add(Vector3.Zero());

        sketch.Undo();

        // The next click is free to choose a different surface.
        Assert.Null(sketch.Normal);
        Assert.Null(sketch.Origin);
        Assert.Null(sketch.Face);
    }

    [Fact]
    public void TakingBackAPointKeepsThePlaneWhileOthersRemain()
    {
        var sketch = Started(ViewportTool.PolyShape);
        sketch.PlaneAt(Vector3.Zero(), Up);
        sketch.Add(Vector3.Zero());
        sketch.Add(new Vector3(0, 0, 2));

        sketch.Undo();

        Assert.Single(sketch.Points);
        Assert.NotNull(sketch.Normal);
    }

    [Fact]
    public void AClickNearTheFirstPointClosesAShapeAndNotACut()
    {
        Vector3[] corners = [Vector3.Zero(), new(0, 0, 2), new(2, 0, 2)];
        var shape = Started(ViewportTool.PolyShape);
        var cut = Started(ViewportTool.Cut);
        foreach (var at in corners)
        {
            shape.Add(at);
            cut.Add(at);
        }

        Assert.True(shape.WouldClose(new Vector3(0.05, 0, 0)));
        Assert.False(shape.WouldClose(new Vector3(5, 0, 5)));
        // A cut that comes back to its start is a loop, not a finish.
        Assert.False(cut.WouldClose(new Vector3(0.05, 0, 0)));
    }

    [Fact]
    public void HowMuchIsEnoughDiffersBetweenTheTwo()
    {
        var shape = Started(ViewportTool.PolyShape);
        var cut = Started(ViewportTool.Cut);
        foreach (var at in new[] { Vector3.Zero(), new Vector3(0, 0, 1) })
        {
            shape.Add(at);
            cut.Add(at);
        }

        // Two points enclose nothing, and are a line to cut on.
        Assert.False(shape.CanFinish);
        Assert.True(cut.CanFinish);
        Assert.False(new Sketch().CanFinish);
    }

    [Fact]
    public void ThePlaneIsAnOwnCopyOfWhatItWasGiven()
    {
        var sketch = Started(ViewportTool.PolyShape);
        var origin = new Vector3(1, 2, 3);

        sketch.PlaneAt(origin, Up);
        origin.SetFrom(new Vector3(9, 9, 9));

        Assert.Equal(1, sketch.Origin!.X);
    }

    [Fact]
    public void StartingAgainDropsWhatWasThere()
    {
        var sketch = Started(ViewportTool.PolyShape);
        sketch.PlaneAt(Vector3.Zero(), Up);
        sketch.Add(Vector3.Zero());
        sketch.Hovering = new Vector3(4, 0, 4);

        sketch.Start(ViewportTool.Cut);

        Assert.Empty(sketch.Points);
        Assert.Null(sketch.Normal);
        Assert.Null(sketch.Hovering);
        Assert.Equal(ViewportTool.Cut, sketch.Tool);
    }

    [Fact]
    public void AnOutlineThatCrossesItselfIsSaidToCross()
    {
        var sketch = Started(ViewportTool.PolyShape);
        sketch.PlaneAt(Vector3.Zero(), Up);
        foreach (var at in new[] { Vector3.Zero(), new Vector3(2, 0, 2), new Vector3(2, 0, 0), new Vector3(0, 0, 2) })
        {
            sketch.Add(at);
        }

        Assert.True(sketch.Crosses());
    }

    [Fact]
    public void PlacingOnThePlaneNeedsAPlane()
    {
        var sketch = Started(ViewportTool.PolyShape);
        var down = new Vector3(0, -1, 0);
        var eye = new Vector3(3, 5, 4);

        Assert.Null(sketch.PlaceOn(eye, down));

        sketch.PlaneAt(Vector3.Zero(), Up, onFace: 2);
        var placed = sketch.PlaceOn(eye, down)!;

        Assert.Equal(3, placed.At.X, 1e-9);
        Assert.Equal(0, placed.At.Y, 1e-9);
        Assert.Equal(2, placed.Face);
    }

    [Fact]
    public void TheToolsHaveWordsAndIcons()
    {
        Assert.Equal("Draw shape", ViewportTool.PolyShape.Label());
        Assert.Equal("content_cut", ViewportTool.Cut.Icon());
        Assert.False(ViewportTool.None.IsDrawing());
        Assert.True(ViewportTool.Cut.IsDrawing());
    }
}

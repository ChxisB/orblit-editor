namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// A snapped drag that fed its own snapped place back into the snapping made a still pointer flicker between
// two lines. The cube stands off the grid, as anything placed by hand does.
public sealed class DragStabilityTests
{
    private const double Step = 0.5;
    private readonly Workspace _workspace = new("/project");

    private SceneViewportState View(bool snapped = true)
    {
        _workspace.Add(new SceneEntry(
            "s",
            "S",
            scene: new EditorScene([new SceneObject("a", "A", ObjectKind.Shape, position: new Vector3(0, 0.3, 0), shape: Shape.Of(ShapeKind.Cube))])));
        return new SceneViewportState(_workspace, new Snapping(on: snapped, step: Step), new History(_workspace))
        {
            Camera = new OrbitCamera(yaw: 0.6, pitch: 0.4, distance: 8),
            Surface = new Size(800, 600),
            Primary = "a",
            Selected = new HashSet<string> { "a" },
        };
    }

    // From the up handle, the height after each move, and let go where the last one ended.
    private List<double> Drag(SceneViewportState view, params Offset[] moves)
    {
        var handles = view.Handles ?? throw new InvalidOperationException("No handles.");
        var at = handles.Projection.Project(handles.Pivot + (GizmoAxis.Y.Direction() * (handles.Length * 0.8)))
            ?? throw new InvalidOperationException("Behind the camera.");
        view.Touch(ViewportPhase.DragStart, at);
        var heights = new List<double>();
        foreach (var move in moves)
        {
            at += move;
            view.Touch(ViewportPhase.DragUpdate, at);
            heights.Add(_workspace.Loaded!.Scene!["a"]!.Position.Y);
        }

        view.Touch(ViewportPhase.DragEnd, at);
        return heights;
    }

    private static Offset[] Up(int times, double by) => [.. Enumerable.Repeat(new Offset(0, -by), times)];

    [Fact]
    public void ADragThatIsNotMovingDoesNotMoveWhatItIsHolding()
    {
        // Enough to be a drag, and then the same place reported again, which is what a held pointer sends.
        var heights = Drag(View(), [.. Up(4, 2), .. Up(8, 0)]);

        Assert.Single(heights.Skip(4).Distinct());
    }

    [Fact]
    public void ASlowDragOnlyEverGoesOneWay()
    {
        var heights = Drag(View(), Up(40, 2));

        for (var i = 1; i < heights.Count; i++)
        {
            Assert.True(heights[i] >= heights[i - 1] - 1e-9, $"Went back at step {i}: {heights[i - 1]:0.000} to {heights[i]:0.000}.");
        }

        Assert.True(heights[^1] > heights[0]);
    }

    [Fact]
    public void AndItStillLandsOnTheGrid()
    {
        var y = Drag(View(), Up(30, 3))[^1];

        Assert.Equal(0, Math.IEEERemainder(y, Step), 1e-6);
    }

    [Fact]
    public void UnsnappedItFollowsThePointerExactly()
    {
        var heights = Drag(View(snapped: false), Up(20, 2));

        for (var i = 1; i < heights.Count; i++)
        {
            Assert.True(heights[i] >= heights[i - 1] - 1e-12, $"Went back at step {i}.");
        }

        Assert.True(heights[^1] > heights[0] + 0.1);
    }
}

namespace Orblit.Editor.Tests;

using Orblit.Editor.Viewport;
using Orblit.Sprite;
using Orblit.VectorMath;

public sealed class GridTests : IAsyncLifetime
{
    private const double Tolerance = 1e-9;

    private DirectoryInfo _root = null!; // set in InitializeAsync, which runs before every test
    private GridStore _grid = null!; // set in InitializeAsync, which runs before every test

    public async ValueTask InitializeAsync()
    {
        _root = Directory.CreateTempSubdirectory("orblit_grid");
        _grid = new GridStore(_root.FullName);
        await _grid.PrepareAsync();
    }

    public ValueTask DisposeAsync()
    {
        if (_root.Exists)
        {
            _root.Delete(recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    // The largest scale on any axis, which for a matrix of a translation and a scale is the
    // longest of its three axis columns.
    private static double MaxScaleOnAxis(Matrix4 matrix) =>
        Math.Sqrt(Enumerable.Range(0, 3).Max(column => matrix.GetColumn(column).Length2));

    [Fact]
    public void TheQuadAndTheLinesAreWrittenOnce()
    {
        Assert.True(File.Exists(Path.Combine(_root.FullName, ".orblit", "grid.glb")));
        Assert.True(File.Exists(Path.Combine(_root.FullName, ".orblit", "grid.png")));
    }

    // Added in C#: the Dart test only checks that the file is there, and the PNG is encoded here
    // rather than by the platform.
    [Fact]
    public void TheLinesReadBackAsTheTileTheGridDraws()
    {
        var tile = PngCodec.Decode(File.ReadAllBytes(Path.Combine(_root.FullName, ".orblit", "grid.png")));

        Assert.Equal(512, tile.Width);
        Assert.Equal(512, tile.Height);
        // Alpha is the fourth byte. The edge is heavy, the line inside the tile is light and the
        // middle of a square is clear.
        byte AlphaAt(int x, int y) => tile.Pixels[((y * 512) + x) * 4 + 3];
        Assert.Equal(150, AlphaAt(0, 100));
        Assert.Equal(60, AlphaAt(51, 100));
        Assert.Equal(0, AlphaAt(25, 25));
    }

    [Fact]
    public async Task ThereIsNoGridUntilItHasBeenMade()
    {
        var fresh = new GridStore(_root.FullName);
        // A frame or two without one at startup beats blocking.
        Assert.Null(fresh.PlanFor(new Snapping(), Vector3.Zero()));
        await fresh.PrepareAsync();
        Assert.NotNull(fresh.PlanFor(new Snapping(), Vector3.Zero()));
    }

    [Fact]
    public void TurningSnappingOffTurnsTheGridOff()
    {
        Assert.Null(_grid.PlanFor(new Snapping(on: false), Vector3.Zero()));
        Assert.NotNull(_grid.PlanFor(new Snapping(), Vector3.Zero()));
    }

    [Fact]
    public void ItIsAlwaysAHundredSquaresWhateverTheStep()
    {
        foreach (var step in Snapping.Steps)
        {
            var plan = _grid.PlanFor(new Snapping(step: step), Vector3.Zero())!;
            // So it says how big a snap is, not how big the world is.
            Assert.Equal(GridPlan.Across, plan.Extent / plan.Step, Tolerance);
        }
    }

    [Fact]
    public void ItFollowsTheCameraInWholeSquares()
    {
        var plan = _grid.PlanFor(new Snapping(step: 0.5), new Vector3(3.3, 9, -7.1))!;
        // Snapped, so it slides under the camera rather than appearing to move.
        Assert.Equal(0, Doubles.Mod(plan.Centre.X, 0.5), Tolerance);
        Assert.Equal(0, Doubles.Mod(plan.Centre.Z, 0.5), Tolerance);
        Assert.Equal(3.5, plan.Centre.X, Tolerance);
        // A grid is the ground, not a height.
        Assert.Equal(0, plan.Centre.Y);
    }

    [Fact]
    public void TheQuadIsScaledToTheWholeExtentAndSitsJustUnderNought()
    {
        var plan = _grid.PlanFor(new Snapping(step: 1), Vector3.Zero())!;
        var transform = plan.Transform();
        Assert.Equal(plan.Extent, MaxScaleOnAxis(transform), 1e-6);
        // On the ground exactly. What keeps it from fighting whatever shares its plane is the
        // material's depth bias, which works at every height rather than only at this one.
        Assert.Equal(0, transform.GetTranslation().Y);
        Assert.True(plan.ToMaterial().DepthBias > 0);
    }

    [Fact]
    public void ItIsDrawnAsAHintRatherThanAsAThingInTheWorld()
    {
        var plan = _grid.PlanFor(new Snapping(), Vector3.Zero())!;
        var drawn = plan.ToObject();
        var material = plan.ToMaterial();

        Assert.False(drawn.CastShadows);
        Assert.False(drawn.ReceiveShadows);
        // Blended, unlit and writing no depth: hidden by what is in front of it, and hiding
        // nothing itself.
        Assert.False(material.DepthWrite);
        Assert.True(material.BaseColour.W < 1);
        Assert.NotNull(material.BaseColourMap);
        // Ten across the image and ten images across the quad.
        Assert.Equal(GridPlan.Across, material.Tiling.X * 10, Tolerance);
    }

    [Fact]
    public void ItsKeyIsNowhereNearAnObjectKey()
    {
        // The grid is not an object and never will be one, so it can never collide with something
        // a scene put in the list.
        Assert.True(GridPlan.RenderKey < 0);
    }

    [Fact]
    public async Task AProjectFolderItCannotWriteToIsAProjectWithNoGrid()
    {
        var missing = new GridStore("/definitely/not/a/place");
        await missing.PrepareAsync();
        Assert.Null(missing.PlanFor(new Snapping(), Vector3.Zero()));
    }
}

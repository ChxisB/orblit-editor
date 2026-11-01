using System.Text.Json;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>
/// Asks the view what the frozen fixture asks: the ray through a grid of pixels, the object it meets
/// and the handle under it. The answers must be the recorded ones.
/// </summary>
/// <remarks>
/// A ray and a projection are compared to a billionth, because the fixture was made with the same sums
/// in the same order and a larger gap is a different formula. An object or a handle is compared exactly.
/// </remarks>
public sealed class FrozenPickTests
{
    private const double Tolerance = 1e-9;

    private static readonly JsonElement Fixture = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "frozen", "pick.json"))).RootElement;

    private static SceneObject Cube(string id, Vector3 at, Vector3? scale = null, Vector3? rotation = null) =>
        new(id, id, ObjectKind.Shape, position: at, scale: scale, rotation: rotation, shape: Shape.Of(ShapeKind.Cube));

    private static EditorScene Scene() => new(
    [
        Cube("a", Vector3.Zero()),
        Cube("b", new Vector3(3, 0, 0), scale: new Vector3(1, 3, 1)),
        Cube("c", new Vector3(-3, 0, 2), rotation: new Vector3(0, 45, 0)),
        Cube("d", new Vector3(0, 0, -4), scale: new Vector3(2, 2, 2)),
        Cube("e", new Vector3(0, 0, 2)),
    ]);

    private static double[] Numbers(JsonElement list) => [.. list.EnumerateArray().Select(one => one.GetDouble())];

    private static string? Name(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetString();

    private static void Near(double[] expected, Vector3 actual)
    {
        Assert.InRange(Math.Abs(actual.X - expected[0]), 0, Tolerance);
        Assert.InRange(Math.Abs(actual.Y - expected[1]), 0, Tolerance);
        Assert.InRange(Math.Abs(actual.Z - expected[2]), 0, Tolerance);
    }

    private static string? Said(GizmoAxis? axis) => axis?.ToString().ToLowerInvariant();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheViewsAnswersToAGridOfPixelsMatchTheFixture(int which)
    {
        var size = Numbers(Fixture.GetProperty("size"));
        var entry = Fixture.GetProperty("cameras")[which];
        var camera = new OrbitCamera(
            yaw: entry.GetProperty("yaw").GetDouble(),
            pitch: entry.GetProperty("pitch").GetDouble(),
            distance: entry.GetProperty("distance").GetDouble(),
            fieldOfView: entry.GetProperty("fieldOfView").GetDouble(),
            target: new Vector3(Numbers(entry.GetProperty("target"))[0], Numbers(entry.GetProperty("target"))[1], Numbers(entry.GetProperty("target"))[2]));
        var projection = new ViewportProjection(camera, new Orblit.Editor.Viewport.Size(size[0], size[1]));
        var move = new Gizmo(GizmoMode.Move, Vector3.Zero(), projection);
        var rotate = new Gizmo(GizmoMode.Rotate, Vector3.Zero(), projection);
        var scene = Scene();

        Near(Numbers(entry.GetProperty("eye")), projection.Eye);
        Near(Numbers(entry.GetProperty("forward")), projection.Forward);
        Assert.InRange(Math.Abs(move.Length - entry.GetProperty("handleLength").GetDouble()), 0, Tolerance);
        var pivot = projection.Project(Vector3.Zero());
        Assert.Equal(entry.GetProperty("pivotAt").ValueKind == JsonValueKind.Null, pivot is null);

        var pixels = entry.GetProperty("pixels");
        Assert.True(pixels.GetArrayLength() > 81);
        foreach (var pixel in pixels.EnumerateArray())
        {
            var at = Numbers(pixel.GetProperty("at"));
            var where = new Offset(at[0], at[1]);
            var ray = projection.RayThrough(where);

            Near(Numbers(pixel.GetProperty("origin")), ray.Origin);
            Near(Numbers(pixel.GetProperty("direction")), ray.Direction);
            Assert.Equal(Name(pixel.GetProperty("object")), scene.ObjectAlong(ray.Origin, ray.Direction));
            Assert.Equal(Name(pixel.GetProperty("move")), Said(move.AxisAt(where)));
            Assert.Equal(Name(pixel.GetProperty("rotate")), Said(rotate.AxisAt(where)));
        }
    }

    [Fact]
    public void TheGridReachesAnObjectAndAHandleOfEachKind()
    {
        var all = Fixture.GetProperty("cameras").EnumerateArray()
            .SelectMany(camera => camera.GetProperty("pixels").EnumerateArray())
            .ToList();

        Assert.Contains(all, pixel => Name(pixel.GetProperty("object")) is not null);
        Assert.Contains(all, pixel => Name(pixel.GetProperty("object")) is null);
        Assert.Contains(all, pixel => Name(pixel.GetProperty("move")) is not null);
        Assert.Contains(all, pixel => Name(pixel.GetProperty("rotate")) is not null);
        Assert.Equal(["a", "b", "c", "d", "e"], all.Select(pixel => Name(pixel.GetProperty("object"))).Where(id => id is not null).Distinct().Order().ToArray());
    }
}

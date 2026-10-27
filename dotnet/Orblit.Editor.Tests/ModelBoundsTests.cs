namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;

public sealed class ModelBoundsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_bounds").FullName;
    private readonly ModelBounds _models;

    public ModelBoundsTests() => _models = new ModelBounds(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SceneObject Object(string? asset) => new("m", "M", ObjectKind.Mesh, meshAsset: asset);

    [Fact]
    public void AnObjIsSizedFromItsPoints()
    {
        File.WriteAllText(Path.Combine(_root, "crate.obj"), "v -1 0 2\nv 3 4 5\nv 0 0 0\nf 1 2 3\n");

        var box = _models.Of(Object("crate.obj"))!.Value;

        Assert.Equal([-1.0, 0, 0], [box.Min.X, box.Min.Y, box.Min.Z]);
        Assert.Equal([3.0, 4, 5], [box.Max.X, box.Max.Y, box.Max.Z]);
    }

    [Fact]
    public void AnObjTheEditorWroteIsSizedAsTheMeshItCameFrom()
    {
        var cube = Shape.Of(ShapeKind.Cube).Build();
        File.WriteAllBytes(Path.Combine(_root, "cube.obj"), cube.ToObj()[0].Bytes);

        var box = _models.Of(Object("cube.obj"))!.Value;

        Assert.True(box.Min.DistanceTo(cube.Bounds.Min) < 1e-6);
        Assert.True(box.Max.DistanceTo(cube.Bounds.Max) < 1e-6);
    }

    [Fact]
    public void AFileThatIsNotAModelOrNotThereSaysNothing()
    {
        File.WriteAllText(Path.Combine(_root, "empty.obj"), "v 1 2 3\n");
        File.WriteAllBytes(Path.Combine(_root, "crate.fbx"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(_root, "broken.glb"), [1, 2, 3]);
        File.WriteAllText(Path.Combine(_root, "broken.gltf"), "{ not json");

        Assert.Null(_models.Of(Object("empty.obj")));
        Assert.Null(_models.Of(Object("crate.fbx")));
        Assert.Null(_models.Of(Object("broken.glb")));
        Assert.Null(_models.Of(Object("broken.gltf")));
        Assert.Null(_models.Of(Object("gone.obj")));
    }

    [Fact]
    public void AnObjectWithNoFileSaysNothing()
    {
        Assert.Null(_models.Of(Object(null)));
        Assert.Null(_models.Of(Object("")));
    }

    [Fact]
    public void AMissAndAnAnswerAreRememberedUntilForgotten()
    {
        var path = Path.Combine(_root, "late.obj");
        Assert.Null(_models.Of(Object("late.obj")));

        File.WriteAllText(path, "v 0 0 0\nv 1 1 1\nv 2 0 0\nf 1 2 3\n");
        Assert.Null(_models.Of(Object("late.obj")));

        _models.Forget("late.obj");
        Assert.NotNull(_models.Of(Object("late.obj")));

        File.Delete(path);
        Assert.NotNull(_models.Of(Object("late.obj")));
        _models.Clear();
        Assert.Null(_models.Of(Object("late.obj")));
    }
}

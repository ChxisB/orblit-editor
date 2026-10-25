namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;

// Added tests: the Dart checks the store only for its cost, in the frame cost test.
public sealed class GeometryStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_geometry").FullName;
    private readonly GeometryStore _store;
    private readonly SceneObject _cube = new(
        "a", "A", ObjectKind.Shape, position: new Vector3(0, 0, 0), shape: Shape.Of(ShapeKind.Cube));

    public GeometryStoreTests() => _store = new GeometryStore(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string FileOf(string relative) => Path.Combine(_root, relative);

    [Fact]
    public void AShapeIsWrittenInsideTheProjectAndTheAnswerIsRelative()
    {
        var relative = _store.PathFor(_cube);

        Assert.Equal(Path.Combine(".orblit", "geometry", "a.glb"), relative);
        Assert.True(File.Exists(FileOf(relative!)));
    }

    [Fact]
    public void AnObjectWithNoGeometryHasNoFile()
    {
        var group = new SceneObject("g", "G", ObjectKind.Group);

        Assert.Null(_store.PathFor(group));
        Assert.False(_store.IsStale(group));
    }

    [Fact]
    public void ANewShapeIsStaleUntilItIsAskedFor()
    {
        Assert.True(_store.IsStale(_cube));

        _store.PathFor(_cube);

        Assert.False(_store.IsStale(_cube));
    }

    [Fact]
    public void AskingAgainForAnUnchangedShapeWritesNothing()
    {
        var relative = _store.PathFor(_cube)!;
        var written = File.GetLastWriteTimeUtc(FileOf(relative));
        File.SetLastWriteTimeUtc(FileOf(relative), written.AddHours(-1));

        _store.PathFor(_cube);

        Assert.Equal(written.AddHours(-1), File.GetLastWriteTimeUtc(FileOf(relative)));
    }

    [Fact]
    public void AChangedMaterialIsWrittenAgain()
    {
        _store.PathFor(_cube);

        _cube.Surfaces.Add(new Surface { Name = "Red" });

        Assert.True(_store.IsStale(_cube));
    }

    [Fact]
    public void ForgettingAnObjectMakesTheNextAskWriteAgain()
    {
        var relative = _store.PathFor(_cube)!;
        File.Delete(FileOf(relative));
        _store.Forget("a");

        _store.PathFor(_cube);

        Assert.True(File.Exists(FileOf(relative)));
    }

    [Fact]
    public void ClearingThrowsAwayEveryFile()
    {
        _store.PathFor(_cube);

        _store.Clear();

        Assert.False(Directory.Exists(_store.Folder));
        Assert.True(_store.IsStale(_cube));
    }

    [Fact]
    public void AFolderThatCannotBeWrittenGivesNoPathAndNoException()
    {
        File.WriteAllText(Path.Combine(_root, ".orblit"), "in the way");

        Assert.Null(_store.PathFor(_cube));
    }
}

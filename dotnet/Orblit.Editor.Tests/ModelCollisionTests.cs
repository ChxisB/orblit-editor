namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;

public sealed class ModelCollisionTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_collision").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static SceneObject Model(string id, string path) => new(id, id, ObjectKind.Mesh, meshAsset: path);

    [Fact]
    public async Task TheEditorReadsImportedModelTrianglesFromTheProject()
    {
        var cube = Shape.Of(ShapeKind.Cube).Build();
        var geometry = cube.Triangulate();
        File.WriteAllBytes(Path.Combine(_root, "crate.glb"), cube.ToGlb());

        var collider = await new ModelCollision(_root).ReadAsync(Model("crate", "crate.glb"));

        Assert.Equal(geometry.Positions.Select(value => (double)value), collider.Vertices);
        Assert.Equal(geometry.Indices.Select(index => (int)index), collider.Indices);
        await Assert.ThrowsAnyAsync<Exception>(() => new ModelCollision(_root).ReadAsync(Model("missing", "missing.glb")));
    }

    [Fact]
    public async Task TheEditorReadsTheTrianglesOfAnObjInTheProject()
    {
        var cube = Shape.Of(ShapeKind.Cube).Build();
        File.WriteAllBytes(Path.Combine(_root, "crate.obj"), cube.ToObj().First().Bytes);

        var collider = await new ModelCollision(_root).ReadAsync(Model("crate", "crate.obj"));

        Assert.Equal(cube.Triangulate().Indices.Length, collider.Indices.Count);
    }

    [Fact]
    public async Task WhatAnObjectBuildsItselfNeedsNoFile()
    {
        var cube = new SceneObject("cube", "Cube", ObjectKind.Shape, shape: Shape.Of(ShapeKind.Cube));

        var collider = await new ModelCollision(_root).ReadAsync(cube);

        Assert.Equal(36, collider.Indices.Count);
    }

    [Fact]
    public async Task AnObjectWithNothingToReadSaysSo() =>
        await Assert.ThrowsAsync<FormatException>(() => new ModelCollision(_root).ReadAsync(new SceneObject("spot", "Spot", ObjectKind.Group)));
}

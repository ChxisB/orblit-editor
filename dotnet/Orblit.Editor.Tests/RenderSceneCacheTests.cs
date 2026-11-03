using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;
using Orblit.Mesh;
using Orblit.VectorMath;

// Keeping the render scene between frames: what is kept while only the camera moves, and what makes the
// view build it again.
public sealed class RenderSceneCacheTests
{
    private readonly RenderSceneCache _cache = new();
    private int _builds;

    private RenderScene Build()
    {
        _builds++;
        return new RenderScene { Objects = [], Camera = Camera(1) };
    }

    private static RenderCamera Camera(double x) => new() { Position = new Vector3(x, 0, 5), Target = Vector3.Zero() };

    // The last object is the scene's own whether or not a grid is under it, so it says whether the scene
    // was built again.
    private static RenderObject Last(RenderScene scene) => scene.Objects[^1];

    [Fact]
    public void ACameraOnlyFrameBuildsNothing()
    {
        var scene = EditorScene.Starter();

        for (var frame = 0; frame < 100; frame++)
        {
            _cache.Get(scene, null, Build);
        }

        Assert.Equal(1, _builds);
    }

    [Fact]
    public void AForgottenSceneIsBuiltAgain()
    {
        var scene = EditorScene.Starter();
        _cache.Get(scene, null, Build);

        _cache.Forget();
        _cache.Get(scene, null, Build);

        Assert.Equal(2, _builds);
    }

    [Fact]
    public void ANewSceneIsBuiltAgain()
    {
        _cache.Get(EditorScene.Starter(), null, Build);
        var other = EditorScene.Starter();

        _cache.Get(other, null, Build);
        _cache.Get(other, null, Build);

        Assert.Equal(2, _builds);
    }

    [Fact]
    public void ANewSharedSceneIsBuiltAgain()
    {
        var scene = EditorScene.Starter();
        _cache.Get(scene, null, Build);

        _cache.Get(scene, EditorScene.Starter(), Build);

        Assert.Equal(2, _builds);
    }

    [Fact]
    public void AnAnimatedSceneIsBuiltAtEachTick()
    {
        var scene = EditorScene.Starter();
        scene.DayCycle = true;

        foreach (var clock in new[] { 1.0, 2.0, 3.0 })
        {
            scene.Clock = clock;
            _cache.Get(scene, null, Build);
        }

        Assert.Equal(3, _builds);
    }

    [Fact]
    public void AStillSceneIgnoresItsClock()
    {
        var scene = EditorScene.Starter();

        foreach (var clock in new[] { 1.0, 2.0, 3.0 })
        {
            scene.Clock = clock;
            _cache.Get(scene, null, Build);
        }

        Assert.Equal(1, _builds);
    }

    [Fact]
    public void ASceneThatStopsMovingIsBuiltOnceMoreAtItsLastClock()
    {
        var scene = EditorScene.Starter();
        scene.DayCycle = true;
        scene.Clock = 4;
        _cache.Get(scene, null, Build);

        scene.DayCycle = false;
        _cache.Get(scene, null, Build);
        scene.Clock = 5;
        _cache.Get(scene, null, Build);

        Assert.Equal(2, _builds);
    }

    [Fact]
    public void ASceneSeenFromAnotherCameraKeepsItsExposure()
    {
        var metered = new RenderCamera
        {
            Position = new Vector3(0, 0, 5),
            Target = Vector3.Zero(),
            Aperture = 4,
            ShutterSpeed = 0.01,
            Sensitivity = 800,
        };
        var kept = new RenderScene { Objects = [], Camera = metered };
        var camera = Camera(9) with { FieldOfView = 30 };

        var seen = kept.SeenFrom(camera);

        Assert.Equal(9, seen.Camera.Position.X);
        Assert.Equal(30, seen.Camera.FieldOfView);
        Assert.Equal(4, seen.Camera.Aperture);
        Assert.Equal(0.01, seen.Camera.ShutterSpeed);
        Assert.Equal(800, seen.Camera.Sensitivity);
        Assert.Same(kept.Objects, seen.Objects);
    }

    [Fact]
    public void TheGridIsFirstInTheObjectsAndTheMaterials()
    {
        var scene = EditorScene.Starter();
        var plan = new GridPlan("/grid.glb", "/grid.png", Vector3.Zero(), 100, 1);
        var bare = scene.ToRenderScene(Camera(0));

        var together = scene.ToRenderScene(Camera(0), new SceneRenderOptions { Grid = plan });
        var after = bare.WithGrid(plan);

        foreach (var drawn in new[] { together, after })
        {
            Assert.Equal(GridPlan.RenderKey, drawn.Objects[0].Key);
            Assert.Equal(GridPlan.MaterialKey, drawn.Materials[0].Key);
            Assert.Equal(bare.Objects.Count + 1, drawn.Objects.Count);
            Assert.Equal(bare.Materials.Count + 1, drawn.Materials.Count);
            Assert.Equal(bare.Objects.Select(one => one.Key), drawn.Objects.Skip(1).Select(one => one.Key));
        }
    }

    [Fact]
    public void WithNoGridTheSceneIsTheSameScene()
    {
        var bare = EditorScene.Starter().ToRenderScene(Camera(0));

        Assert.Same(bare, bare.WithGrid(null));
    }

    [Fact]
    public Task ADragThatOrbitsKeepsTheScene() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var first = rig.View.Build();
        var from = rig.Centre;

        rig.Window.MouseDown(from, MouseButton.Left);
        rig.Window.MouseMove(from + new Avalonia.Vector(60, 0), RawInputModifiers.LeftMouseButton);
        rig.Window.MouseUp(from + new Avalonia.Vector(60, 0), MouseButton.Left);
        var second = rig.View.Build();

        Assert.NotEqual(first.Camera.Position.X, second.Camera.Position.X);
        Assert.Same(Last(first), Last(second));
    });

    [Fact]
    public Task AWheelTurnAndTheEaseThatFollowsKeepTheScene() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var first = rig.View.Build();

        rig.Window.MouseWheel(rig.Centre, new Avalonia.Vector(0, 1));
        foreach (var ms in new[] { 0, 16, 32 })
        {
            rig.View.State.Tick(TimeSpan.FromMilliseconds(ms));
        }

        var second = rig.View.Build();

        Assert.NotEqual(first.Camera.Position.X, second.Camera.Position.X);
        Assert.Same(Last(first), Last(second));
    });

    [Fact]
    public Task AnEditBuildsTheSceneAgain() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var first = rig.View.Build();

        rig.Session.AddShape(ShapeKind.Cube);
        var second = rig.View.Build();

        Assert.Equal(first.Objects.Count + 1, second.Objects.Count);
    });

    [Fact]
    public Task AnUndoBuildsTheSceneAgain() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Session.AddShape(ShapeKind.Cube);
        var added = rig.View.Build();

        rig.Session.Undo();
        var undone = rig.View.Build();

        Assert.Equal(added.Objects.Count - 1, undone.Objects.Count);
    });

    [Fact]
    public Task ASelectionBuildsTheSceneAgainWithItsOutline() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        var id = rig.Session.Workspace.Loaded!.Scene!.Objects.First(one => one.IsDrawable).Id;
        Assert.Same(Outline.None, rig.View.Build().Outline);

        rig.Session.SelectObject(id);

        Assert.NotSame(Outline.None, rig.View.Build().Outline);
    });
}

namespace Orblit.Editor.Tests;

using System.Globalization;
using System.Text.Json;
using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Filament;

// What the preview draws and from where. The renderer is not running here: these read the scene the
// preview would hand it.
public sealed class AssetPreviewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_preview_model").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // A glTF that declares how big it is and nothing else. Enough of a document for the bounds to be
    // read, which is all the preview asks of the file.
    private static string Gltf(double half) => JsonSerializer.Serialize(new
    {
        asset = new { version = "2.0" },
        meshes = new[] { new { primitives = new[] { new { attributes = new { POSITION = 0 } } } } },
        accessors = new[] { new { type = "VEC3", min = new[] { -half, -half, -half }, max = new[] { half, half, half } } },
    });

    private static string Obj(double half)
    {
        var h = half.ToString(CultureInfo.InvariantCulture);
        return $"v -{h} -{h} -{h}\nv {h} -{h} -{h}\nv 0 {h} {h}\nf 1 2 3\n";
    }

    private Asset Make(string name, string text)
    {
        var path = Path.Join(_root, name);
        File.WriteAllText(path, text);
        return AssetTree.Describe(path);
    }

    private static double Distance(AssetPreviewModel model)
    {
        var camera = model.Scene()!.Camera;
        return (camera.Position - camera.Target).Length;
    }

    private static AssetPreviewModel Showing(Asset? asset, bool renderer = true)
    {
        var model = new AssetPreviewModel(renderer);
        model.Show(asset);
        return model;
    }

    [Fact]
    public void AModelIsDrawnByPointingTheRendererAtTheFile()
    {
        var barrel = Make("barrel.gltf", Gltf(0.5));

        var scene = Showing(barrel).Scene()!;

        // The file itself, not a picture of it made earlier. That is the whole difference between a
        // preview and a thumbnail somebody has to remember to regenerate.
        Assert.Equal(barrel.Path, Assert.Single(scene.Objects).Mesh);
    }

    [Fact]
    public void AGltfIsFramedByTheSizeItDeclares()
    {
        // A fixed camera puts a door frame inside the lens and a doorknob in the far distance, and
        // both read as the renderer being broken.
        var near = Distance(Showing(Make("small.gltf", Gltf(0.25))));
        var far = Distance(Showing(Make("large.gltf", Gltf(2.5))));

        Assert.Equal(10, far / near, 0.01);
    }

    [Fact]
    public void AnObjIsFramedByThePointsItHolds()
    {
        var near = Distance(Showing(Make("small.obj", Obj(0.25))));
        var far = Distance(Showing(Make("large.obj", Obj(2.5))));

        Assert.Equal(10, far / near, 0.01);
    }

    [Fact]
    public void AModelThatWillNotSayHowBigItIsStillOpensAtArmsLength()
    {
        var crate = Make("crate.fbx", "x");

        var model = Showing(crate);

        // Radius 1, 2.6 radii back, looking at the origin.
        Assert.Equal(2.6, Distance(model), 1e-9);
        Assert.Equal(0, model.Scene()!.Camera.Target.Length, 1e-9);
        Assert.DoesNotContain(model.Facts(), fact => fact.Label == "Extent");
    }

    [Fact]
    public void AFileThatCannotBeReadOpensAtArmsLengthToo()
    {
        var broken = Make("broken.gltf", "{ not json");
        var gone = new Asset("gone.glb", Path.Join(_root, "gone.glb"), AssetKind.Mesh);

        Assert.Equal(2.6, Distance(Showing(broken)), 1e-9);
        Assert.Equal(2.6, Distance(Showing(gone)), 1e-9);
    }

    [Fact]
    public void ATinyModelIsNotFramedFromInsideItself()
    {
        var model = Showing(Make("dust.gltf", Gltf(0.0001)));

        Assert.Equal(0.05 * 2.6, Distance(model), 1e-9);
    }

    [Fact]
    public void TheCameraLooksAtTheMiddleOfTheBox()
    {
        var camera = Showing(Make("barrel.gltf", Gltf(0.5))).Scene()!.Camera;

        Assert.Equal(0, camera.Target.Length, 1e-9);
        Assert.Equal(45, camera.FieldOfView);
    }

    [Fact]
    public void ItKnowsWhatItCanDrawAndWhatItCanOnlyDescribe()
    {
        Assert.Equal(PreviewStage.Model, Showing(Make("barrel.glb", "x")).Stage);
        Assert.Equal(PreviewStage.Model, Showing(Make("barrel.gltf", Gltf(1))).Stage);
        Assert.Equal(PreviewStage.Picture, Showing(Make("colormap.png", "x")).Stage);
        // A GPU format the picture decoder has nothing for, and a script.
        Assert.Equal(PreviewStage.Kind, Showing(Make("rock.ktx2", "x")).Stage);
        Assert.Equal(PreviewStage.Kind, Showing(Make("player.cpp", "int x;")).Stage);
    }

    [Fact]
    public void AFolderAndNothingEachSaySoAndDrawNothing()
    {
        Directory.CreateDirectory(Path.Join(_root, "models"));

        var nothing = Showing(null);
        var folder = Showing(AssetTree.Describe(Path.Join(_root, "models")));

        Assert.Equal(PreviewStage.Nothing, nothing.Stage);
        Assert.Equal("Select a file to see what is in it", nothing.Saying);
        Assert.Equal("Preview", nothing.Heading);
        Assert.Empty(nothing.Facts());
        Assert.Equal(PreviewStage.Folder, folder.Stage);
        Assert.Equal("A folder has nothing to show", folder.Saying);
        Assert.False(folder.OffersTurn);
        Assert.Null(folder.Scene());
    }

    [Fact]
    public void OffThePlatformsTheRendererRunsOnItSaysSoRatherThanDrawingNothing()
    {
        var model = Showing(Make("barrel.gltf", Gltf(0.5)), renderer: false);

        Assert.Equal(PreviewStage.Unavailable, model.Stage);
        Assert.Contains("not available on this platform", model.Saying, StringComparison.Ordinal);
        Assert.Null(model.Scene());
        // The model is still a model: the turn control and the file's size are still true.
        Assert.True(model.OffersTurn);
        Assert.Contains(model.Facts(), fact => fact.Label == "Extent");
    }

    [Fact]
    public void ARendererlessMachineStillShowsAPictureAsAPicture()
    {
        Assert.Equal(PreviewStage.Picture, Showing(Make("colormap.png", "x"), renderer: false).Stage);
    }

    [Fact]
    public void AFileItCannotDrawIsNamedByItsKind()
    {
        var model = Showing(Make("player.cpp", "int x;"));

        // Naming the kind confirms the editor knows what the file is, which is the question somebody
        // has when nothing is drawn.
        Assert.Contains(AssetKind.Native.Label, model.Saying, StringComparison.Ordinal);
        Assert.Equal("player.cpp", model.Heading);
        Assert.False(model.OffersTurn);
        Assert.Null(model.Scene());
    }

    [Fact]
    public void TheFactsNameTheKindTheSizeOnDiskAndTheBoxAModelFills()
    {
        var facts = Showing(Make("barrel.gltf", Gltf(0.5))).Facts();

        Assert.Equal("Kind", facts[0].Label);
        Assert.Equal(AssetKind.Mesh.Label, facts[0].Value);
        Assert.Equal("Size", facts[1].Label);
        Assert.Contains(facts, fact => fact is ("Extent", "1.00 × 1.00 × 1.00"));
    }

    [Fact]
    public void ADroppedFileHasNoSizeLineWhereNothingIsKnown()
    {
        var facts = Showing(new Asset("a.fbx", Path.Join(_root, "a.fbx"), AssetKind.Mesh)).Facts();

        Assert.DoesNotContain(facts, fact => fact.Label == "Size");
    }

    [Fact]
    public void ThreeLightsAndAQuietSkyLeaveAShapeReadable()
    {
        var scene = Showing(Make("barrel.gltf", Gltf(0.5))).Scene()!;

        Assert.Equal([1L, 2L, 3L], scene.Lights.Select(light => light.Key));
        Assert.All(scene.Lights, light => Assert.Equal(RenderLightKind.Directional, light.Kind));
        Assert.Equal([true, false, false], scene.Lights.Select(light => light.CastShadows));
        Assert.False(scene.Sky.Drawn);
    }

    [Fact]
    public void TheModelTurnsWhileItIsOnScreenAndNotOtherwise()
    {
        var model = Showing(Make("barrel.gltf", Gltf(0.5)));
        var from = model.Yaw;

        model.Tick(2);

        Assert.Equal(from + 1, model.Yaw, 1e-9);

        model.Turn(false);
        model.Tick(2);
        Assert.Equal(from + 1, model.Yaw, 1e-9);

        // A picture has nothing to turn.
        var picture = Showing(Make("colormap.png", "x"));
        picture.Tick(2);
        Assert.Equal(from, picture.Yaw, 1e-9);
    }

    [Fact]
    public void TakingHoldOfItStopsTheTurnAndKeepsWhereItGot()
    {
        var model = Showing(Make("barrel.gltf", Gltf(0.5)));
        model.Tick(2);
        var turned = model.Yaw;

        model.Orbit(0, 0);

        // Stopped where it was. The turn it had made is not taken back.
        Assert.False(model.Turning);
        Assert.Equal(turned, model.Yaw, 1e-9);
    }

    [Fact]
    public void ADragMovesTheCameraRoundAndAboveAndStopsAtTheTop()
    {
        var model = Showing(Make("barrel.gltf", Gltf(0.5)));
        var yaw = model.Yaw;
        var pitch = model.Pitch;

        model.Orbit(100, 50);

        Assert.Equal(yaw - 0.8, model.Yaw, 1e-9);
        Assert.Equal(pitch + 0.4, model.Pitch, 1e-9);

        model.Orbit(0, 100000);
        Assert.Equal(1.4, model.Pitch);
        model.Orbit(0, -1000000);
        Assert.Equal(-1.4, model.Pitch);
    }

    [Fact]
    public void AScrollMovesTheCameraInAndOutWithinLimits()
    {
        var model = Showing(Make("barrel.gltf", Gltf(0.5)));
        var from = Distance(model);

        model.Scroll(100);

        Assert.True(Distance(model) > from);

        model.Scroll(-1e9);
        Assert.Equal(0.3, model.Zoom);
        model.Scroll(1e9);
        Assert.Equal(6, model.Zoom);
    }

    [Fact]
    public void ChoosingAnotherFileKeepsTheViewAndFramesTheNewOne()
    {
        var model = Showing(Make("small.gltf", Gltf(0.25)));
        model.Orbit(50, 0);
        var yaw = model.Yaw;

        model.Show(Make("large.gltf", Gltf(2.5)));

        Assert.Equal(yaw, model.Yaw);
        Assert.Equal(2.5 * Math.Sqrt(3) * 2.6, Distance(model), 1e-9);
    }
}

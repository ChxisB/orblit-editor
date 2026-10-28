using System.Text;
using System.Text.Json;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Added in C#. The Dart ModelSkeletons was only tested through the gizmos and the retarget files, which
// left the .gltf buffers and the choice between skins unread.
public sealed class ModelSkeletonsTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("orblit_skeletons").FullName;
    private readonly ModelSkeletons _models;

    public ModelSkeletonsTests() => _models = new ModelSkeletons(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Put(string path, byte[] bytes)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    private static byte[] Json(object document) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(document));

    [Fact]
    public void ReadsTheBonesOfAModelNamedInTheOrderTheFileHasThem()
    {
        Put("hero.glb", SkinnedGlb.ChainGlb("hips", "spine"));

        Assert.Equal(["hips", "spine"], _models.OfAsset("hero.glb")!.Names);
    }

    [Fact]
    public void ARootedPathIsReadAsItIs()
    {
        var path = Put("hero.glb", SkinnedGlb.ChainGlb("hips"));

        Assert.Equal(["hips"], new ModelSkeletons("/nowhere").OfAsset(path)!.Names);
    }

    [Fact]
    public void SaysNothingForAFileThatHasNoSkinIsNotThereOrIsNotAModel()
    {
        Put("plain.glb", SkinnedGlb.Glb(new { asset = new { version = "2.0" } }));
        Put("broken.glb", [1, 2, 3]);
        Put("crate.obj", Encoding.UTF8.GetBytes("v 1 2 3\n"));

        Assert.Null(_models.OfAsset("plain.glb"));
        Assert.Null(_models.OfAsset("broken.glb"));
        Assert.Null(_models.OfAsset("crate.obj"));
        Assert.Null(_models.OfAsset("missing.glb"));
        Assert.Null(_models.OfAsset(""));
        Assert.Null(_models.OfAsset(null));
    }

    [Fact]
    public void AFileWithSeveralSkinsAnswersTheOneWithTheMostBones()
    {
        var nodes = new[]
        {
            new { name = "prop", children = new[] { 1 } },
            new { name = "prop tip", children = Array.Empty<int>() },
            new { name = "hips", children = new[] { 3 } },
            new { name = "spine", children = new[] { 4 } },
            new { name = "head", children = Array.Empty<int>() },
        };
        Put(
            "two.glb",
            SkinnedGlb.Glb(new
            {
                asset = new { version = "2.0" },
                scene = 0,
                scenes = new[] { new { nodes = new[] { 0, 2 } } },
                nodes,
                skins = new[] { new { joints = new[] { 0, 1 } }, new { joints = new[] { 2, 3, 4 } } },
            }));

        Assert.Equal(["hips", "spine", "head"], _models.OfAsset("two.glb")!.Names);
    }

    [Fact]
    public void SkinsWithTheSameNumberOfBonesAnswerTheFirst()
    {
        var nodes = new[]
        {
            new { name = "first", children = Array.Empty<int>() },
            new { name = "second", children = Array.Empty<int>() },
        };
        Put(
            "tie.glb",
            SkinnedGlb.Glb(new
            {
                asset = new { version = "2.0" },
                scene = 0,
                scenes = new[] { new { nodes = new[] { 0, 1 } } },
                nodes,
                skins = new[] { new { joints = new[] { 0 } }, new { joints = new[] { 1 } } },
            }));

        Assert.Equal(["first"], _models.OfAsset("tie.glb")!.Names);
    }

    [Fact]
    public void AGltfIsReadWhetherOrNotItsBufferIsBesideIt()
    {
        Put("hero.gltf", Json(Gltf("hero.bin")));

        Assert.Equal(["hips", "spine"], _models.OfAsset("hero.gltf")!.Names);
    }

    [Fact]
    public void AnAnswerIsKeptUntilTheFileIsForgotten()
    {
        var path = Put("hero.glb", SkinnedGlb.ChainGlb("hips"));
        Assert.Equal(["hips"], _models.OfAsset("hero.glb")!.Names);

        File.WriteAllBytes(path, SkinnedGlb.ChainGlb("hips", "spine"));
        Assert.Equal(["hips"], _models.OfAsset("hero.glb")!.Names);

        _models.Forget("hero.glb");
        Assert.Equal(["hips", "spine"], _models.OfAsset("hero.glb")!.Names);
    }

    // A skeleton of two bones, in a document that names a buffer by [uri].
    private static object Gltf(string uri) => new
    {
        asset = new { version = "2.0" },
        buffers = new[] { new { uri, byteLength = 4 } },
        scene = 0,
        scenes = new[] { new { nodes = new[] { 0 } } },
        nodes = new[] { new { name = "hips", children = new[] { 1 } }, new { name = "spine", children = Array.Empty<int>() } },
        skins = new[] { new { joints = new[] { 0, 1 } } },
    };
}

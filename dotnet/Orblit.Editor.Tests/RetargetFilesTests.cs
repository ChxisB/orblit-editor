using System.Text;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;

// What the retarget dialog finds in a project folder and writes back to it.
public sealed class RetargetFilesTests : IDisposable
{
    private static readonly BoneMap Map = new(
        fromAsset: "hero.glb",
        toAsset: "dwarf.glb",
        bones: new Dictionary<string, string> { ["neck"] = "head" },
        unpaired: new HashSet<string> { "tail" });

    private readonly string _root = Directory.CreateTempSubdirectory("orblit_retarget").FullName;
    private readonly ProjectRetargetFiles _files;

    public RetargetFilesTests() => _files = new ProjectRetargetFiles(new AssetTree(_root), new ModelSkeletons(_root));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Put(string path, byte[] bytes)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
    }

    [Fact]
    public void ModelsAreTheGltfFilesAnywhereInTheProject()
    {
        Put("hero.glb", SkinnedGlb.ChainGlb("hips"));
        Put("models/dwarf.gltf", []);
        Put("models/rock.obj", []);
        Put("notes.txt", []);

        Assert.Equal(["hero.glb", "models/dwarf.gltf"], _files.Models.Order());
    }

    [Fact]
    public void ModelsReadTheirSkeletonAndAnswerNullForNone()
    {
        Put("hero.glb", SkinnedGlb.ChainGlb("hips", "spine"));
        Put("plain.glb", SkinnedGlb.Glb(new { asset = new { version = "2.0" } }));

        Assert.Equal(["hips", "spine"], _files.SkeletonOf("hero.glb")!.Names);
        Assert.Null(_files.SkeletonOf("plain.glb"));
        Assert.Null(_files.SkeletonOf("missing.glb"));
    }

    [Fact]
    public void MapsAreWrittenUnderRetargetsAndReadBack()
    {
        var made = _files.WriteMap("hero-to-dwarf", Map);

        Assert.Null(made.Problem);
        Assert.Equal("retargets/hero-to-dwarf.obonemap", made.Path);
        Assert.Equal(["retargets/hero-to-dwarf.obonemap"], _files.Maps);
        var read = _files.ReadMap(made.Path!);
        Assert.Empty(read.Problems);
        Assert.Equal(Map.Encode(), read.Map.Encode());
    }

    [Fact]
    public void MapsAreNeverWrittenOverAnother()
    {
        var first = _files.WriteMap("hero-to-dwarf", Map);
        var second = _files.WriteMap("hero-to-dwarf", Map);

        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal(2, _files.Maps.Count);
    }

    [Fact]
    public void AFileThatIsNotAMapIsSaidSo()
    {
        Put("retargets/junk.obonemap", Encoding.UTF8.GetBytes("not json"));

        Assert.Throws<BoneMapFormatException>(() => _files.ReadMap("retargets/junk.obonemap"));
    }

    [Fact]
    public void AFileThatIsNotThereIsSaidSo()
    {
        Assert.Throws<BoneMapFormatException>(() => _files.ReadMap("retargets/gone.obonemap"));
    }

    [Fact]
    public void ASaveThatCannotMakeTheFolderSaysWhy()
    {
        // A file where the folder goes.
        Put(ProjectRetargetFiles.Folder, [1]);

        var made = _files.WriteMap("hero-to-dwarf", Map);

        Assert.NotNull(made.Problem);
        Assert.Null(made.Path);
    }
}

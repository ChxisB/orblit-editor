namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

// Added in C#: the Dart bench decodes inside a private method that has no test of its own. The
// decoder is a fake here. Each picture file holds one byte, which the fake repeats as the pixels, so
// a test can tell which picture it was handed.
public sealed class TerrainPicturesTests : IDisposable
{
    private const string File = "terrain/hills/hills.oterrain";
    private const int Side = 512;

    private readonly string _root = Directory.CreateTempSubdirectory("terrain_pictures").FullName;

    private sealed class FakeDecoder : IPictureDecoder
    {
        private int _calls;

        // Closed by a test that wants to look at the ground before the decode has finished.
        public ManualResetEventSlim Gate { get; } = new(initialState: true);

        public int Calls => _calls;

        public byte[] Decode(byte[] file, int side)
        {
            Interlocked.Increment(ref _calls);
            Gate.Wait();
            if (file.Length == 0)
            {
                throw new InvalidDataException("It is empty.");
            }

            return [.. Enumerable.Repeat(file[0], side * side * 4)];
        }
    }

    private sealed record Rig(TerrainBench Bench, History History, OpenTerrain Open, FakeDecoder Decoder, List<string> Said);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void Picture(string name, params byte[] content)
    {
        var path = Path.Combine(_root, "textures", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, content);
    }

    private static Ground Hills(params TerrainSet[] sets)
    {
        var terrain = new Ground(regionSize: 32, sets: sets);
        terrain.AddRegion(new RegionKey(0, 0));
        return terrain;
    }

    private Rig MakeRig(params TerrainSet[] sets)
    {
        var said = new List<string>();
        var history = new History(new Workspace(_root));
        var decoder = new FakeDecoder();
        var bench = new TerrainBench(history, _root, said.Add);
        bench.ReadPicturesWith(decoder);
        Assert.Null(bench.Create(File, Hills(sets)));
        return new Rig(bench, history, bench[File]!, decoder, said);
    }

    private static byte[]? Albedo(TerrainBench bench, int set) =>
        bench.RenderFor([TerrainBenchTests.GroundObject("a")]).Single().Sets[set].Albedo;

    private static void Use(Rig rig, params TerrainSet[] sets) =>
        rig.Bench.Edit(rig.Open, "Pictures", settings => settings with { Sets = sets });

    private static void AssertPicture(byte[]? pixels, byte marker)
    {
        Assert.NotNull(pixels);
        Assert.Equal(Side * Side * 4, pixels.Length);
        Assert.Equal(marker, pixels[0]);
        Assert.Equal(marker, pixels[^1]);
    }

    // A decode finishes off the UI thread and says nothing when it fails, so a test waits for its effect.
    private static void Await(Func<bool> done) => Assert.True(Headless.Until(done), "Timed out waiting for the pictures.");

    [Fact]
    public Task APictureDrawsPlainUntilItIsDecodedAndThenArrivesAsPixels() => Headless.Run(() =>
    {
        Picture("rock.png", 7);
        var rig = MakeRig();
        rig.Decoder.Gate.Reset();
        var before = rig.Bench.PicturesRevision;

        Use(rig, new TerrainSet("Rock", albedo: "textures/rock.png"));
        Assert.Null(Albedo(rig.Bench, 0));

        rig.Decoder.Gate.Set();
        Await(() => rig.Bench.PicturesRevision == before + 2);

        AssertPicture(Albedo(rig.Bench, 0), 7);
        Assert.Empty(rig.Said);
    });

    [Fact]
    public Task APictureNamedTwiceIsDecodedOnceAndTheSameOnesAreNotDecodedAgainWhenTheSetsChange() => Headless.Run(() =>
    {
        Picture("rock.png", 7);
        Picture("rock_n.png", 8);
        var rig = MakeRig();
        var before = rig.Bench.PicturesRevision;

        Use(rig, new TerrainSet("Rock", "textures/rock.png", "textures/rock_n.png"), new TerrainSet("Scree", "textures/rock.png"));
        Await(() => rig.Bench.PicturesRevision == before + 3);
        Use(rig, new TerrainSet("Rock", "textures/rock.png", "textures/rock_n.png", tileSize: 8));

        Assert.Equal(2, rig.Decoder.Calls);
        AssertPicture(Albedo(rig.Bench, 0), 7);
    });

    [Fact]
    public Task APictureThatCannotBeReadIsSaidAndTheGroundStaysPlain() => Headless.Run(() =>
    {
        Picture("empty.png");
        var rig = MakeRig();

        Use(rig, new TerrainSet("Rock", "textures/gone.png", "textures/empty.png"));
        Await(() => rig.Said.Count == 2);

        Assert.All(rig.Said, said => Assert.StartsWith("The picture textures/", said));
        Assert.Contains(rig.Said, said => said.Contains("gone.png") && said.Contains("could not be read"));
        Assert.Contains(rig.Said, said => said.Contains("empty.png") && said.Contains("It is empty."));
        Assert.Null(Albedo(rig.Bench, 0));
    });

    [Fact]
    public Task APictureThatFailedIsTriedAgainWhenTheSetsChangeAndIsNotSaidTwice() => Headless.Run(() =>
    {
        Picture("rock.png");
        var rig = MakeRig();
        Use(rig, new TerrainSet("Rock", albedo: "textures/rock.png"));
        Await(() => rig.Said.Count == 1);

        Picture("rock.png", 7);
        Use(rig, new TerrainSet("Rock", albedo: "textures/rock.png", tileSize: 8));
        Await(() => Albedo(rig.Bench, 0) is not null);

        AssertPicture(Albedo(rig.Bench, 0), 7);
        Assert.Single(rig.Said);
    });

    [Fact]
    public Task ATerrainReadFromDiskAsksForItsPicturesAndTheyArrive() => Headless.Run(() =>
    {
        Picture("rock.png", 7);
        MakeRig(new TerrainSet("Rock", albedo: "textures/rock.png"));
        var bench = new TerrainBench(new History(new Workspace(_root)), _root);
        bench.ReadPicturesWith(new FakeDecoder());

        Assert.NotNull(bench.TerrainFor(File));
        Await(() => bench.PicturesRevision == 1);

        AssertPicture(Albedo(bench, 0), 7);
    });

    [Fact]
    public Task UndoingASettingsEditDrawsTheEarlierPicturesAtOnceWithoutReadingThemAgain() => Headless.Run(() =>
    {
        Picture("rock.png", 7);
        Picture("sand.png", 9);
        var rig = MakeRig();
        Use(rig, new TerrainSet("Ground", albedo: "textures/rock.png"));
        rig.History.Seal();
        Await(() => rig.Bench.PicturesRevision == 2);
        Use(rig, new TerrainSet("Ground", albedo: "textures/sand.png"));
        Await(() => rig.Bench.PicturesRevision == 4);
        AssertPicture(Albedo(rig.Bench, 0), 9);

        rig.History.Undo();

        AssertPicture(Albedo(rig.Bench, 0), 7);
        Assert.Equal(2, rig.Decoder.Calls);
    });

    [Fact]
    public Task ThePicturesSkiaReadsAreTheSizeTheRendererTakes() => Headless.Run(() =>
    {
        Picture("rock.png", SkiaPictureDecoderTests.Solid(1024, 1024, [200, 100, 50, 64]));
        var rig = MakeRig();
        rig.Bench.ReadPicturesWith(new SkiaPictureDecoder());
        var before = rig.Bench.PicturesRevision;

        Use(rig, new TerrainSet("Rock", albedo: "textures/rock.png"));
        Await(() => rig.Bench.PicturesRevision == before + 2);

        // The renderer refuses a picture of another size, so a terrain that draws at all is the proof.
        var albedo = Albedo(rig.Bench, 0);
        Assert.Equal(Side * Side * 4, albedo!.Length);
        Assert.InRange(albedo[0], 198, 202);
        Assert.InRange(albedo[3], 63, 65);
        Assert.Empty(rig.Said);
    });

    [Fact]
    public void WithNoDecoderThePicturesAreNotAskedForAndTheGroundDrawsPlain()
    {
        var said = new List<string>();
        var bench = new TerrainBench(new History(new Workspace(_root)), _root, said.Add);
        Assert.Null(bench.Create(File, Hills(new TerrainSet("Rock", albedo: "textures/rock.png"))));

        Assert.Null(Albedo(bench, 0));
        Assert.Empty(said);
    }
}

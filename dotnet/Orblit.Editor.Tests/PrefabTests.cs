namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Json;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// The three tests that copy an instance and the one that moves an instance between scenes go
// through the clipboard and the commands, and follow with them.
public sealed class PrefabTests : IDisposable
{
    private const string LampAsset = "prefabs/Lamp post" + PrefabFiles.Extension;
    private const string StreetAsset = "prefabs/Street" + PrefabFiles.Extension;

    private readonly List<string> _roots = [];

    public void Dispose()
    {
        foreach (var root in _roots)
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // A lamp post: a group with a post and a bulb under it.
    private static EditorScene LampScene() =>
        new(
        [
            new SceneObject("lamp", "Lamp post", ObjectKind.Group, position: new Vector3(40, 0, 12)),
            new SceneObject("post", "Post", ObjectKind.Mesh, parentId: "lamp", scale: new Vector3(0.2, 4, 0.2)),
            new SceneObject(
                "bulb",
                "Bulb",
                ObjectKind.Light,
                parentId: "lamp",
                position: new Vector3(0, 4, 0),
                colour: new Colour(0xFFFFCC88)),
        ]);

    // A project folder with the lamp post saved in it as a prefab.
    private PrefabFiles Project()
    {
        var root = Directory.CreateTempSubdirectory("orblit_prefabs").FullName;
        _roots.Add(root);
        var files = new PrefabFiles(root);
        var made = Doc.Instances.MakePrefab(SceneDocument.DocumentOf(LampScene()), "lamp", LampAsset, files.Find);
        Assert.Null(files.Write(LampAsset, made.Prefab));
        return files;
    }

    // A scene file holding a folded instance of the asset for each of the ids.
    private static string SceneWith(IEnumerable<string> ids, string asset = LampAsset) =>
        new Doc.SceneDocument(
            "Street",
            entities: [.. ids.Select(id => new Doc.SceneEntity(id, id, components: Linked(asset)))]).Encode();

    private static Dictionary<string, Doc.SceneComponent> Linked(string asset) =>
        new() { [Doc.SceneComponents.Prefab] = new Doc.PrefabComponent(asset: asset) };

    // The objects a scene file holds, by id.
    private static Dictionary<string, IReadOnlyDictionary<string, object?>> SavedObjects(string text)
    {
        var json = (IReadOnlyDictionary<string, object?>)JsonText.Decode(text)!;
        return ((IReadOnlyList<object?>)json["entities"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToDictionary(entry => (string)entry["id"]!);
    }

    private static void Overwrite(PrefabFiles files, string asset, Doc.PrefabDocument prefab) =>
        File.WriteAllText(Path.Combine(files.Root, asset), prefab.Encode());

    [Fact]
    public void TheProjectsPrefabsAreReadOnceAndKeptAsTheScenesWereOpenedAgainstThem()
    {
        var files = Project();
        var first = files.Find(LampAsset)!;

        // Changed in another program while a scene is open.
        Overwrite(files, LampAsset, new Doc.PrefabDocument("Changed", first.Root, first.Document));
        Assert.Equal("Lamp post", files.Find(LampAsset)!.Name);

        // Once nothing open uses it, the next look reads the file again.
        files.KeepOnly(new HashSet<string>());
        Assert.Equal("Changed", files.Find(LampAsset)!.Name);
    }

    [Fact]
    public void TheProjectsPrefabsOneThatIsNotThereSaysSoAndIsLookedForAgain()
    {
        var files = Project();
        const string later = "prefabs/Later.oprefab";

        Assert.Contains("not in the project", files.Read(later).Problem);
        Overwrite(files, later, files.Find(LampAsset)!);
        Assert.NotNull(files.Find(later));
    }

    [Fact]
    public void TheProjectsPrefabsOneThatIsNotAPrefabSaysWhy()
    {
        var files = Project();
        File.WriteAllText(Path.Combine(files.Root, "prefabs/Junk.oprefab"), "{");

        var read = files.Read("prefabs/Junk.oprefab");
        Assert.Null(read.Prefab);
        Assert.Contains("not a readable prefab", read.Problem);
    }

    [Fact]
    public void TheProjectsPrefabsWithOneAnswersForOnePrefabAndTheFilesForTheRest()
    {
        var files = Project();
        var other = new Doc.PrefabDocument("Other", "lamp", files.Find(LampAsset)!.Document);

        var source = files.WithOne("prefabs/Other.oprefab", other);
        Assert.Same(other, source("prefabs/Other.oprefab"));
        Assert.Same(files.Find(LampAsset), source(LampAsset));
    }

    [Fact]
    public void ASceneWithInstancesInItIsSavedAsTheLinkAndWhatIsDifferentNotTheParts()
    {
        var files = Project();
        var scene = SceneDocument.Decode(SceneWith(["lamp1"]), files.Find).Scene;
        Assert.NotNull(scene["lamp1/bulb"]);

        scene["lamp1/bulb"]!.Colour = new Colour(0xFF3366FF);
        var text = SceneDocument.Encode(scene, prefabs: files.Find);
        var saved = SavedObjects(text);

        Assert.Equal(["lamp1"], saved.Keys);
        var components = (IReadOnlyDictionary<string, object?>)saved["lamp1"]["components"]!;
        var link = (IReadOnlyDictionary<string, object?>)components["prefab"]!;
        Assert.Equal(LampAsset, link["asset"]);
        Assert.NotNull(link["overrides"]);
        Assert.DoesNotContain("lamp1/", text);

        var back = SceneDocument.Decode(text, files.Find).Scene;
        Assert.Equal(0xFF3366FFu, back["lamp1/bulb"]!.Colour.Argb);
        Assert.NotNull(back["lamp1/post"]);
    }

    [Fact]
    public void ASceneWithInstancesInItKeepsAnEditToOneInstanceInThatOneAndTakesAChangeToThePrefabInBoth()
    {
        var files = Project();
        var scene = SceneDocument.Decode(SceneWith(["lamp1", "lamp2"]), files.Find).Scene;
        scene["lamp1/bulb"]!.Colour = new Colour(0xFF3366FF);
        var text = SceneDocument.Encode(scene, prefabs: files.Find);

        // The prefab's post grows, in a later session.
        var was = files.Find(LampAsset)!;
        var taller = was.Document["post"]!.WithComponent(
            Doc.SceneComponents.Transform,
            new Doc.TransformComponent(scale: new Vector3(0.2, 9, 0.2)));
        Overwrite(files, LampAsset, new Doc.PrefabDocument(was.Name, was.Root, was.Document.WithEntity("post", taller)));

        var back = SceneDocument.Decode(text, new PrefabFiles(files.Root).Find).Scene;
        Assert.Equal(0xFF3366FFu, back["lamp1/bulb"]!.Colour.Argb);
        Assert.Equal(0xFFFFCC88u, back["lamp2/bulb"]!.Colour.Argb);
        Assert.Equal(9, back["lamp1/post"]!.Scale.Y);
        Assert.Equal(9, back["lamp2/post"]!.Scale.Y);
    }

    [Fact]
    public void ASceneWithInstancesInItOpensAPrefabInsideAPrefabAndSavesAnEditMadeInsideIt()
    {
        var files = Project();
        files.Write(
            StreetAsset,
            new Doc.PrefabDocument(
                "Street",
                "street",
                new Doc.SceneDocument(
                    "Street",
                    entities:
                    [
                        new Doc.SceneEntity("street", "Street"),
                        new Doc.SceneEntity("lamp3", "Corner lamp", parent: "street", components: Linked(LampAsset)),
                    ])));

        var scene = SceneDocument.Decode(SceneWith(["main"], StreetAsset), files.Find).Scene;
        Assert.Equal("main/lamp3", scene["main/lamp3/bulb"]!.ParentId);

        scene["main/lamp3/bulb"]!.Colour = new Colour(0xFF00FF00);
        var text = SceneDocument.Encode(scene, prefabs: files.Find);
        Assert.Equal(["main"], SavedObjects(text).Keys);

        var back = SceneDocument.Decode(text, files.Find).Scene;
        Assert.Equal(0xFF00FF00u, back["main/lamp3/bulb"]!.Colour.Argb);
    }

    [Fact]
    public void ASceneWithInstancesInItWhosePrefabHasGoneStaysALinkAndIsSavedAsOne()
    {
        var files = Project();
        var text = SceneWith(["lamp1"], "prefabs/Gone.oprefab");

        var load = SceneDocument.Decode(text, files.Find);
        Assert.Contains("could not be read", Assert.Single(load.Problems));
        Assert.Equal(["lamp1"], load.Scene.Objects.Select(o => o.Id));
        Assert.Equal(["lamp1"], SavedObjects(SceneDocument.Encode(load.Scene, prefabs: files.Find)).Keys);
    }
}

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

public sealed class SceneDiffCommandTests
{
    private sealed class Host(EditorScene scene) : ISceneHost
    {
        public EditorScene? SceneFor(string sceneId) => scene;
    }

    private static EditorScene SceneOf() =>
        new(
        [
            new SceneObject("sun", "Sun", ObjectKind.Light, power: 110),
            new SceneObject("crate", "Crate", ObjectKind.Mesh, position: new Vector3(1, 0, 0), colour: new Colour(0xFFD9634F)),
            new SceneObject("props", "Props", ObjectKind.Group),
        ]);

    // A change worked out on documents, landed on the editor's scene.
    private static ApplySceneDiff DiffTo(
        EditorScene scene,
        Func<Doc.SceneDocument, Doc.SceneDocument> change,
        string label = "Change")
    {
        var before = SceneDocument.DocumentOf(scene);
        return new ApplySceneDiff("main", label, Doc.SceneDiff.Between(before, change(before)));
    }

    [Fact]
    public void AnEditStatedAsADifferenceMovesAnObjectAndUndoingPutsItBack()
    {
        var scene = SceneOf();
        var host = new Host(scene);

        var command = DiffTo(
            scene,
            document => document.WithEntity(
                "crate",
                document["crate"]!.WithComponent(
                    Doc.SceneComponents.Transform,
                    new Doc.TransformComponent(position: new Vector3(1, 5, 0)))));

        command.Apply(host);
        Assert.Equal(5, scene["crate"]!.Position.Y);

        command.Revert(host);
        Assert.Equal(0, scene["crate"]!.Position.Y);
        Assert.Equal(1, scene["crate"]!.Position.X);
    }

    [Fact]
    public void AnEditStatedAsADifferenceAddsAndRemovesObjectsBothWaysRound()
    {
        var scene = SceneOf();
        var host = new Host(scene);

        var command = DiffTo(
            scene,
            document =>
            {
                var without = document.WithEntity("sun", null);
                var lamp = new Doc.SceneEntity(
                    "lamp",
                    "Lamp",
                    components: new Dictionary<string, Doc.SceneComponent>
                    {
                        [Doc.SceneComponents.Light] = new Doc.LightComponent(power: 40),
                    });
                return without.CopyWith(entities: [.. without.Entities, lamp]);
            });

        command.Apply(host);
        Assert.False(scene.Contains("sun"));
        Assert.Equal(ObjectKind.Light, scene["lamp"]!.Kind);
        Assert.Equal(40, scene["lamp"]!.Power);

        command.Revert(host);
        Assert.Null(scene["lamp"]);
        Assert.Equal(110, scene["sun"]!.Power);
        Assert.Equal(["sun", "crate", "props"], scene.Objects.Select(one => one.Id));
    }

    [Fact]
    public void AnEditStatedAsADifferenceReparentsAndUndoingUnparents()
    {
        var scene = SceneOf();
        var host = new Host(scene);

        var command = DiffTo(
            scene,
            document => document.WithEntity("crate", document["crate"]!.CopyWith(parent: "props")));

        command.Apply(host);
        Assert.Equal("props", scene["crate"]!.ParentId);
        Assert.Equal("crate", Assert.Single(scene.ChildrenOf("props")).Id);

        command.Revert(host);
        Assert.Null(scene["crate"]!.ParentId);
    }

    [Fact]
    public void AnEditStatedAsADifferenceCarriesTheScenesOwnSettingsToo()
    {
        var scene = SceneOf();
        var host = new Host(scene);

        var command = DiffTo(
            scene,
            document => document.CopyWith(settings: document.Settings.CopyWith(ambient: 400, timeOfDay: 21)));

        command.Apply(host);
        Assert.Equal(400, scene.Ambient);
        Assert.Equal(21, scene.TimeOfDay);

        command.Revert(host);
        Assert.Equal(28000, scene.Ambient);
        Assert.Equal(10, scene.TimeOfDay);
    }

    [Fact]
    public void AnEditStatedAsADifferenceKeepsWhatTheRendererKnowsAnObjectByThroughTheRoundTrip()
    {
        var scene = SceneOf();
        var host = new Host(scene);
        var was = scene.Objects.ToDictionary(one => one.Id, one => one.RenderKey);

        var command = DiffTo(
            scene,
            document => document.WithEntity("crate", document["crate"]!.CopyWith(name: "Barrel")));

        command.Apply(host);
        Assert.Equal("Barrel", scene["crate"]!.Name);
        Assert.Equal(was["crate"], scene["crate"]!.RenderKey);
        Assert.Equal(was["sun"], scene["sun"]!.RenderKey);

        command.Revert(host);
        Assert.Equal("Crate", scene["crate"]!.Name);
        Assert.Equal(was["crate"], scene["crate"]!.RenderKey);
    }

    [Fact]
    public void AnEditStatedAsADifferenceUndoneLeavesTheSceneByteIdenticalToWhatItWas()
    {
        var scene = SceneOf();
        var host = new Host(scene);
        var before = SceneDocument.Encode(scene);

        var command = DiffTo(
            scene,
            document => document
                .WithEntity(
                    "crate",
                    document["crate"]!
                        .WithComponent(Doc.SceneComponents.Mesh, new Doc.MeshComponent(asset: "models/barrel.glb"))
                        .CopyWith(name: "Barrel", visible: false))
                .WithEntity("props", null));

        command.Apply(host);
        Assert.NotEqual(before, SceneDocument.Encode(scene));

        command.Revert(host);
        Assert.Equal(before, SceneDocument.Encode(scene));
    }

    [Fact]
    public void AnEditStatedAsADifferenceCanBeWrittenDownAndReadBack()
    {
        var scene = SceneOf();
        var host = new Host(scene);

        var made = DiffTo(
            scene,
            document => document.WithEntity("crate", document["crate"]!.CopyWith(name: "Barrel")));

        // What a second editor on the same scene would be sent, and what an undo stack could be kept
        // on disk as.
        var stored = new ApplySceneDiff("main", made.Label, Doc.SceneDiff.FromJson(made.Diff.ToJson()));

        stored.Apply(host);
        Assert.Equal("Barrel", scene["crate"]!.Name);
        stored.Revert(host);
        Assert.Equal("Crate", scene["crate"]!.Name);
    }
}

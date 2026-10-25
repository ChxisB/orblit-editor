namespace Orblit.Editor.Tests;

using System.Text.RegularExpressions;
using Orblit.Editor.Core;
using Orblit.Light;
using Orblit.Weather;
using SceneFormatException = Orblit.Scene.SceneFormatException;

// The three tests that open each project template wait for the launcher's project file.
public sealed class SceneDocumentTests
{
    private static EditorScene RoundTrip(EditorScene scene) => SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;

    private static SceneObject Mesh(string id, string name = "") =>
        new(id, name.Length == 0 ? id : name, ObjectKind.Mesh);

    [Fact]
    public void EveryObjectComesBackTheSame()
    {
        var before = EditorScene.Starter();
        var cube = before["cube"]!;
        cube.Position.SetValues(1.5, -2.25, 0.125);
        cube.Rotation.SetValues(-40, 15.5, 90);
        cube.Scale.SetValues(2, 0.5, 3);
        cube.MeshAsset = "assets/meshes/crate.glb";
        cube.MaterialAsset = "assets/textures/colormap.png";
        cube.CastShadows = false;
        before.Invalidate();

        var after = RoundTrip(before);

        Assert.Equal(before.Length, after.Length);
        foreach (var original in before.Objects)
        {
            var copy = after[original.Id]!;
            Assert.Equal(original.Name, copy.Name);
            Assert.Equal(original.Kind, copy.Kind);
            Assert.Equal(original.ParentId, copy.ParentId);
            Assert.Equal(original.Colour.Argb, copy.Colour.Argb);
            Assert.Equal(original.CastShadows, copy.CastShadows);
            Assert.Equal(original.MeshAsset, copy.MeshAsset);
            Assert.Equal(original.MaterialAsset, copy.MaterialAsset);
            Assert.True((copy.Position - original.Position).Length < 1e-9, $"{original.Name} moved");
            Assert.True((copy.Rotation - original.Rotation).Length < 1e-9, $"{original.Name} moved");
            Assert.True((copy.Scale - original.Scale).Length < 1e-9, $"{original.Name} moved");
        }
    }

    [Fact]
    public void TheHierarchyComesBackNotJustTheObjects()
    {
        var after = RoundTrip(EditorScene.Starter());
        Assert.Equal("props", after["cube"]!.ParentId);
        Assert.Equal(2, after.ChildrenOf("props").Count);
    }

    [Fact]
    public void WritingTheSameSceneTwiceGivesTheSameBytes()
    {
        // A file that reorders itself between saves turns every commit into a diff nobody can
        // review.
        var scene = EditorScene.Starter();
        Assert.Equal(SceneDocument.Encode(scene), SceneDocument.Encode(scene));
    }

    [Fact]
    public void AnEmptySceneIsAValidFileNotAnError()
    {
        var load = SceneDocument.Decode(SceneDocument.Encode(new EditorScene([])));
        Assert.Equal(0, load.Scene.Length);
        Assert.False(load.HasProblems);
    }

    [Fact]
    public void ALightsTypeItsShapeAndItsPowerAllComeBack()
    {
        var before = new EditorScene(
        [
            new SceneObject(
                "lamp",
                "Lamp",
                ObjectKind.Light,
                lightType: LightType.Spot,
                power: 240,
                spotSize: 63,
                spotBlend: 0.4,
                sourceRadius: 0.35),
        ]);

        var after = RoundTrip(before)["lamp"]!;

        Assert.Equal(LightType.Spot, after.LightType);
        Assert.Equal(240, after.Power);
        Assert.Equal(63, after.SpotSize);
        Assert.Equal(0.4, after.SpotBlend);
        Assert.Equal(0.35, after.SourceRadius);
    }

    [Fact]
    public void ASunKeepsTheWidthItCastsShadowsWith()
    {
        var before = new EditorScene([new SceneObject("sun", "Sun", ObjectKind.Light, sunAngle: 4.5)]);
        Assert.Equal(4.5, RoundTrip(before)["sun"]!.SunAngle);
    }

    [Fact]
    public void ALightWrittenBeforeTypesWereStoredIsReadAsASun()
    {
        // Every light in a version-one file was drawn as a sun whatever it called itself, and its
        // power was turned into lux the way a bulb's would be. The same look is that number spread
        // over a sphere.
        const string text = """
            {
              "formatVersion": 1,
              "name": "Old",
              "objects": [
                {"id": "sun", "name": "Sun", "kind": "light", "power": 1256.6370614}
              ]
            }
            """;

        var sun = SceneDocument.Decode(text).Scene["sun"]!;
        Assert.Equal(LightType.Sun, sun.LightType);
        Assert.Equal(100, sun.Power, 0.001);
    }

    [Fact]
    public void TheWeatherComesBackAsItWasSet()
    {
        var air = WeatherState.Of(WeatherCondition.Misty) with
        {
            CloudCover = 0.43,
            FogColour = Tint.Hex(0x334455),
            MistSize = 45,
            WindSpeed = 7.5,
        };
        var before = new EditorScene(
        [
            new SceneObject(
                "weather",
                "Weather",
                ObjectKind.Weather,
                condition: WeatherCondition.Misty,
                weather: air,
                windDirection: 210,
                transitionSeconds: 14),
        ]);

        var after = RoundTrip(before)["weather"]!;

        Assert.Equal(WeatherCondition.Misty, after.Condition);
        Assert.Equal(210, after.WindDirection);
        Assert.Equal(14, after.TransitionSeconds);
        Assert.Equal(0.43, after.Weather.CloudCover);
        Assert.Equal(0xFF334455u, Colour.FromTint(after.Weather.FogColour).Argb);
        Assert.Equal(45, after.Weather.MistSize);
        Assert.Equal(7.5, after.Weather.WindSpeed);
    }

    [Fact]
    public void TheNamesOfThePhysicsLayersComeBackAsTheyWereSet()
    {
        var before = new EditorScene([]) { LayerNames = ["Player", "", "Enemy"] };
        Assert.Equal(["Player", "", "Enemy"], RoundTrip(before).LayerNames);
    }

    [Fact]
    public void ASceneThatNamesNoLayersWritesNoNames()
    {
        var text = SceneDocument.Encode(new EditorScene([]));

        Assert.DoesNotContain("layerNames", text);
        Assert.Empty(SceneDocument.Decode(text).Scene.LayerNames);
    }

    [Fact]
    public void ASceneThatHadFogOfItsOwnGetsAnObjectToHoldIt()
    {
        // Version two kept the air on the scene. Weather is a thing that changes, and a set of
        // fields can only hold one end of a change, so an old scene's fog becomes the object that
        // can hold both.
        const string text = """
            {
              "formatVersion": 2,
              "name": "Old",
              "fog": {
                "colour": "#C9D2DA",
                "density": 0.05,
                "height": -1.5,
                "falloff": 0.4,
                "mist": 0.6,
                "mistSpeed": 0.25,
                "mistSize": 22
              },
              "objects": []
            }
            """;

        var loaded = SceneDocument.Decode(text);
        var weather = loaded.Scene.Weather;

        Assert.NotNull(weather);
        Assert.Equal(0.05, weather.Weather.FogDensity);
        Assert.Equal(-1.5, weather.Weather.FogHeight);
        Assert.Equal(0.6, weather.Weather.Mist);
        Assert.Equal(22, weather.Weather.MistSize);
        // The old drift was a rate the layer breathed at. Wind is a speed across the ground.
        Assert.Equal(1.0, weather.Weather.WindSpeed, 1e-9);
        // Said out loud, because a scene that quietly grew an object is a scene somebody will
        // wonder about.
        Assert.NotEmpty(loaded.Problems);
    }

    [Fact]
    public void ASceneThatHadNoFogDoesNotGrowAnObjectForIt()
    {
        const string text = """{"formatVersion": 2, "name": "Old", "objects": []}""";
        Assert.Null(SceneDocument.Decode(text).Scene.Weather);
    }

    [Fact]
    public void HiddenObjectsAreRememberedShownOnesStayOutOfTheFile()
    {
        var hidden = Mesh("a", "A");
        hidden.Visible = false;
        var scene = new EditorScene([hidden, Mesh("b", "B")]);

        var text = SceneDocument.Encode(scene);
        // Once, for the one that is hidden: the default belongs in the reader rather than in every
        // line of everybody's diffs.
        Assert.Equal(1, Regex.Count(text, "\"visible\""));

        var after = SceneDocument.Decode(text).Scene;
        Assert.False(after["a"]!.Visible);
        Assert.True(after["b"]!.Visible);
    }

    [Fact]
    public void AnObjectThatStopsReceivingShadowsSaysSo()
    {
        var ground = Mesh("ground", "Ground");
        ground.ReceiveShadows = false;

        Assert.False(RoundTrip(new EditorScene([ground]))["ground"]!.ReceiveShadows);
    }

    [Fact]
    public void AFileIsRefusedOutrightWhenItIsNotJson() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("this is not a scene"));

    [Fact]
    public void AFileIsRefusedWhenItComesFromANewerEditor() =>
        // Rather than half-read: a newer file may mean something different by the same keys, and
        // guessing loses somebody's work.
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("""{"formatVersion": 99, "objects": []}"""));

    [Fact]
    public void AFileIsRefusedWhenItDoesNotSayWhatVersionItIs() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("""{"objects": []}"""));

    [Fact]
    public void AFileKeepsTheObjectsItCanReadAndReportsTheOnesItCannot()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "objects": [
                {"id": "a", "name": "Good", "kind": "mesh"},
                {"id": "b", "name": "Odd", "kind": "hologram"},
                {"name": "Nameless", "kind": "mesh"}
              ]
            }
            """);

        // The good one should survive.
        Assert.Equal(1, load.Scene.Length);
        Assert.Equal(2, load.Problems.Count);
        Assert.Contains("hologram", string.Concat(load.Problems));
    }

    [Fact]
    public void AnObjectWhoseParentIsMissingBecomesARootNotAGhost()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "objects": [{"id": "a", "name": "Orphan", "kind": "mesh", "parent": "gone"}]
            }
            """);

        Assert.Null(load.Scene["a"]!.ParentId);
        Assert.Single(load.Scene.Roots);
        Assert.Contains("Orphan", Assert.Single(load.Problems));
    }

    [Fact]
    public void AParentLoopIsCutRatherThanLeftToHangTheOutliner()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "objects": [
                {"id": "a", "name": "A", "kind": "group", "parent": "b"},
                {"id": "b", "name": "B", "kind": "group", "parent": "a"}
              ]
            }
            """);

        Assert.True(load.HasProblems);
        // Whichever was cut, walking up from either now terminates.
        Assert.True(load.Scene.DepthOf("a") < 3);
        Assert.NotEmpty(load.Scene.Roots);
    }

    [Fact]
    public void MissingNumbersFallBackRatherThanBecomingNaN()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "objects": [{"id": "a", "name": "A", "kind": "mesh", "scale": [1]}]
            }
            """);

        // A short scale array would otherwise read as zero and make the object vanish into a
        // matrix that cannot be inverted.
        var scale = load.Scene["a"]!.Scale;
        Assert.Equal([1.0, 1.0, 1.0], [scale.X, scale.Y, scale.Z]);
    }

    [Fact]
    public void AColourThatIsNotAColourFallsBackToSomethingVisible()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "objects": [{"id": "a", "name": "A", "kind": "mesh", "colour": "not a hex"}]
            }
            """);
        Assert.Equal(1.0, load.Scene["a"]!.Colour.A);
    }

    [Fact]
    public void ColoursAreWrittenAsHexSomebodyCanReadInADiff()
    {
        var scene = new EditorScene([new SceneObject("a", "A", ObjectKind.Mesh, colour: new Colour(0xFFD9634F))]);
        Assert.Contains("\"#D9634F\"", SceneDocument.Encode(scene));
    }

    [Fact]
    public void AnEmptyOneOpensAsAnEmptySceneNotAnError()
    {
        var load = SceneDocument.Decode("""{"formatVersion": 1, "name": "main", "entities": []}""");
        Assert.Equal(0, load.Scene.Length);
        Assert.False(load.HasProblems);
    }

    [Fact]
    public void ItsObjectsComeBackWithWhatCouldNotBeRecoveredSaid()
    {
        var load = SceneDocument.Decode("""
            {
              "formatVersion": 1,
              "name": "main",
              "entities": [
                {"name": "Sun", "components": ["DirectionalLight"]},
                {"name": "Ground", "components": ["Transform", "MeshRenderer"]},
                {"name": "Follow Camera", "components": ["Transform", "VirtualCamera"]}
              ]
            }
            """);

        Assert.Equal(3, load.Scene.Length);
        Assert.Equal(ObjectKind.Light, load.Scene.Objects[0].Kind);
        Assert.Equal(ObjectKind.Mesh, load.Scene.Objects[1].Kind);
        Assert.Equal(ObjectKind.Camera, load.Scene.Objects[2].Kind);
        // Said plainly rather than left as a puzzle about why nothing moved.
        Assert.Contains("at the origin", Assert.Single(load.Problems));
    }

    [Fact]
    public void AFileWithNeitherShapeIsStillRefused() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("""{"formatVersion": 1, "name": "main"}"""));

    [Fact]
    public void DataObjectsSurviveARoundTrip()
    {
        var crate = new SceneObject(
            "crate",
            "Crate",
            ObjectKind.Mesh,
            data: ["data/weight.odata", "data/faction.odata"]);

        var back = RoundTrip(new EditorScene([crate]));
        Assert.Equal(["data/weight.odata", "data/faction.odata"], back["crate"]!.Data);
    }

    [Fact]
    public void AnObjectWithNoneSaysNothingAboutThem() =>
        Assert.DoesNotContain("\"data\"", SceneDocument.Encode(new EditorScene([Mesh("crate", "Crate")])));

    [Fact]
    public void ACopyCarriesThemAndDoesNotShareTheList()
    {
        var crate = new SceneObject("crate", "Crate", ObjectKind.Mesh, data: ["data/weight.odata"]);
        var copy = crate.CopyAs("other");
        copy.Data.Add("data/faction.odata");

        Assert.Equal(["data/weight.odata"], crate.Data);
        Assert.Equal(2, copy.Data.Count);
    }

    [Fact]
    public void AFileNamingSomethingThatIsNotAPathIsNotReadIn()
    {
        var back = SceneDocument.Decode("""
            {"formatVersion":3,"objects":[
              {"id":"a","name":"A","kind":"mesh","data":["ok.odata",7,null]}
            ]}
            """).Scene;

        Assert.Equal(["ok.odata"], back["a"]!.Data);
    }
}

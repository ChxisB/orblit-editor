namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Light;
using Orblit.Mesh;
using Orblit.Weather;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// Added in C#. Dart exercises these commands only through widget tests.
public sealed class ObjectCommandTests
{
    private static (History History, EditorScene Scene) Rig()
    {
        var scene = EditorScene.Starter();
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return (new History(workspace), scene);
    }

    [Fact]
    public void ShowingAndHidingIsUndoneToTheOtherState()
    {
        var (history, scene) = Rig();

        history.Run(new SetVisible("a", "cube", "Cube", false));
        Assert.False(scene["cube"]!.Visible);
        Assert.Equal("Hide Cube", history.UndoLabel);

        history.Undo();
        Assert.True(scene["cube"]!.Visible);
    }

    [Fact]
    public void ShadowSwitchesUndoToTheirOldValue()
    {
        var (history, scene) = Rig();

        history.Run(new SetCastShadows("a", "cube", "Cube", false));
        history.Run(new SetReceiveShadows("a", "cube", "Cube", false));
        history.Undo();
        history.Undo();

        Assert.True(scene["cube"]!.CastShadows);
        Assert.True(scene["cube"]!.ReceiveShadows);
    }

    [Fact]
    public void ATextureChangeUndoesToTheOneBefore()
    {
        var (history, scene) = Rig();
        scene["cube"]!.MaterialAsset = "a.png";

        history.Run(new SetMaterialAsset("a", "cube", "Cube", "a.png", "b.png"));
        Assert.Equal("b.png", scene["cube"]!.MaterialAsset);

        history.Undo();
        Assert.Equal("a.png", scene["cube"]!.MaterialAsset);
    }

    [Fact]
    public void DraggingOneUniformIsOneStepThatUndoesToTheStart()
    {
        var (history, scene) = Rig();
        OrderedDictionary<string, object> Values(double value) => new() { ["glow"] = value };

        history.Run(new SetMaterialValue("a", "cube", "Cube", "glow", Values(0), Values(1)));
        history.Run(new SetMaterialValue("a", "cube", "Cube", "glow", Values(1), Values(2)));
        Assert.Equal(2.0, scene["cube"]!.MaterialValues["glow"]);

        history.Undo();
        Assert.Equal(0.0, scene["cube"]!.MaterialValues["glow"]);
    }

    [Fact]
    public void TwoUniformsAreTwoSteps()
    {
        var (history, scene) = Rig();
        OrderedDictionary<string, object> Values(double glow, double? rough = null)
        {
            var map = new OrderedDictionary<string, object> { ["glow"] = glow };
            if (rough is { } value)
            {
                map["rough"] = value;
            }

            return map;
        }

        history.Run(new SetMaterialValue("a", "cube", "Cube", "glow", Values(0), Values(1)));
        history.Run(new SetMaterialValue("a", "cube", "Cube", "rough", Values(1), Values(1, 1)));
        history.Undo();

        Assert.Equal(1.0, scene["cube"]!.MaterialValues["glow"]);
        Assert.False(scene["cube"]!.MaterialValues.ContainsKey("rough"));
    }

    [Fact]
    public void ChangingTheSurfacesIsUndoneEvenWhenTheSameListIsPutBack()
    {
        var (history, scene) = Rig();
        var was = scene["cube"]!.Surfaces.ToList();
        var next = new List<Surface> { new() { Name = "Paint" }, new() { Name = "Trim" } };

        history.Run(new SetSurfaces("a", "cube", "Cube", next, "Add slot"));
        Assert.Equal(["Paint", "Trim"], scene["cube"]!.Surfaces.Select(one => one.Name));

        history.Undo();
        Assert.Equal(was, scene["cube"]!.Surfaces);
    }

    [Fact]
    public void ADraggedOutlineIsOneStep()
    {
        var (history, scene) = Rig();
        var gesture = new object();
        PolyShape Outline(double height) => new([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1)], height);

        history.Run(new SetOutline("a", "cube", "Cube", Outline(1), gesture));
        history.Run(new SetOutline("a", "cube", "Cube", Outline(3), gesture));
        Assert.Equal(3, scene["cube"]!.Outline!.Height);

        history.Undo();
        Assert.Null(scene["cube"]!.Outline);
    }

    [Fact]
    public void PowerDraggedIsOneStep()
    {
        var (history, scene) = Rig();
        var was = scene["sun"]!.Power;

        history.Run(new SetPower("a", "sun", "Sun", was, 10));
        history.Run(new SetPower("a", "sun", "Sun", 10, 20));
        Assert.Equal(20, scene["sun"]!.Power);

        history.Undo();
        Assert.Equal(was, scene["sun"]!.Power);
    }

    [Fact]
    public void ChangingTheTypeOfLightBringsItsPowerAndUndoPutsBothBack()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var (type, power) = (sun.LightType, sun.Power);

        history.Run(new SetLightType("a", "sun", "Sun", type, LightType.Spot, power, 800));
        Assert.Equal(LightType.Spot, sun.LightType);
        Assert.Equal(800, sun.Power);
        Assert.Equal("Make Sun a spot", history.UndoLabel);

        history.Undo();
        Assert.Equal(type, sun.LightType);
        Assert.Equal(power, sun.Power);
    }

    [Fact]
    public void TheShapeOfALightIsChangedTogetherAndUndoneTogether()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var was = new LightShape(sun.SpotSize, sun.SpotBlend, sun.SourceRadius, sun.SunAngle);

        history.Run(new SetLightShape("a", "sun", "Sun", was, new LightShape(60, 0.5, 0.3, 1)));
        Assert.Equal(60, sun.SpotSize);
        Assert.Equal(1, sun.SunAngle);

        history.Undo();
        Assert.Equal(was.Size, sun.SpotSize);
        Assert.Equal(was.Blend, sun.SpotBlend);
        Assert.Equal(was.Radius, sun.SourceRadius);
        Assert.Equal(was.Sun, sun.SunAngle);
    }

    [Fact]
    public void DataLinksAreReplacedWholeAndPutBackInOrder()
    {
        var (history, scene) = Rig();
        scene["cube"]!.Data.AddRange(["a.odata", "b.odata"]);

        history.Run(new SetDataLinks("a", "cube", "Cube", ["b.odata"], "Remove weight"));
        Assert.Equal(["b.odata"], scene["cube"]!.Data);

        history.Undo();
        Assert.Equal(["a.odata", "b.odata"], scene["cube"]!.Data);
    }

    [Fact]
    public void AnInterfaceIsSetAndCleared()
    {
        var (history, scene) = Rig();

        history.Run(new SetInterface("a", "cube", "Cube", "menu.oui"));
        Assert.Equal("menu.oui", scene["cube"]!.InterfaceAsset);

        history.Run(new SetInterface("a", "cube", "Cube", null));
        Assert.Equal("Clear Cube", history.UndoLabel);

        history.Undo();
        Assert.Equal("menu.oui", scene["cube"]!.InterfaceAsset);
    }

    [Fact]
    public void AddingAComponentIsUndoneByTakingItOffAndASliderOnItIsOneStep()
    {
        var (history, scene) = Rig();
        var components = scene["cube"]!.Components;
        var first = new Doc.TransformComponent(position: new Vector3(1, 0, 0));
        var second = new Doc.TransformComponent(position: new Vector3(2, 0, 0));

        history.Run(new SetObjectComponent("a", "cube", "Add transform", "transform", null, first));
        history.Run(new SetObjectComponent("a", "cube", "Edit transform", "transform", first, second));
        Assert.Same(second, components["transform"]);

        // The slider and the adding are separate steps.
        history.Undo();
        Assert.Same(first, components["transform"]);
        history.Undo();
        Assert.False(components.ContainsKey("transform"));
    }

    [Fact]
    public void ASceneTimeChangeNamesWhetherTheDayStartedAndUndoesAllThreeFields()
    {
        var (history, scene) = Rig();
        var was = new SceneTime(scene.TimeOfDay, scene.DayCycle, scene.HoursPerSecond);

        history.Run(new SetSceneTime("a", was, new SceneTime(18, true, 2)));
        Assert.Equal("Start the day", history.UndoLabel);
        Assert.Equal(18, scene.TimeOfDay);

        history.Undo();
        Assert.Equal(was.Hour, scene.TimeOfDay);
        Assert.Equal(was.Cycle, scene.DayCycle);
        Assert.Equal(was.Speed, scene.HoursPerSecond);
    }

    [Fact]
    public void ABodyIsSwitchedBetweenSunAndMoon()
    {
        var (history, scene) = Rig();

        history.Run(new SetCelestialBody("a", "sun", "Sun", CelestialBody.Sun, CelestialBody.Moon));
        Assert.Equal(CelestialBody.Moon, scene["sun"]!.Body);

        history.Undo();
        Assert.Equal(CelestialBody.Sun, scene["sun"]!.Body);
    }

    [Fact]
    public void ACloudKindBringsItsHeightAndUndoRestoresBoth()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var height = sun.Weather.CloudHeight;

        history.Run(new SetCloudKind("a", "sun", null, CloudKind.Cirrus, height, 7000));
        Assert.Equal(CloudKind.Cirrus, sun.CloudKind);
        Assert.Equal(7000, sun.Weather.CloudHeight);

        history.Undo();
        Assert.Null(sun.CloudKind);
        Assert.Equal(height, sun.Weather.CloudHeight);
    }

    [Fact]
    public void AWeatherConditionStartsAFadeFromWhatIsOnScreen()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var from = sun.Weather;
        var to = WeatherState.Presets[WeatherCondition.Fair];

        history.Run(new SetWeatherCondition("a", "sun", sun.Condition, WeatherCondition.Fair, from, to));
        Assert.Equal(WeatherCondition.Fair, sun.Condition);
        Assert.Equal(to, sun.Weather);
        Assert.NotNull(sun.BlendFrom);

        history.Undo();
        Assert.Equal(from, sun.Weather);
    }

    [Fact]
    public void WeatherValuesDraggedAreOneStepAndArriveWithoutAFade()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var was = sun.Weather;

        history.Run(new SetWeatherValues("a", "sun", was, was with { Rain = 0.2 }));
        history.Run(new SetWeatherValues("a", "sun", was with { Rain = 0.2 }, was with { Rain = 0.7 }));
        Assert.Equal(0.7, sun.Weather.Rain);
        Assert.Null(sun.BlendFrom);

        history.Undo();
        Assert.Equal(was, sun.Weather);
    }

    [Fact]
    public void WindDraggedIsOneStepThatUndoesBothValues()
    {
        var (history, scene) = Rig();
        var sun = scene["sun"]!;
        var was = new WindValues(sun.WindDirection, sun.TransitionSeconds);

        history.Run(new SetWeatherWind("a", "sun", was, new WindValues(10, 2)));
        history.Run(new SetWeatherWind("a", "sun", new WindValues(10, 2), new WindValues(20, 4)));
        Assert.Equal(20, sun.WindDirection);

        history.Undo();
        Assert.Equal(was.Direction, sun.WindDirection);
        Assert.Equal(was.Transition, sun.TransitionSeconds);
    }

    [Fact]
    public void ADraggedShapeIsOneStepThatUndoesToTheShapeBefore()
    {
        var (history, scene) = Rig();
        var cube = scene["cube"]!;
        var was = cube.Shape;

        history.Run(new SetShape("a", "cube", "Cube", new Shape { Kind = ShapeKind.Cylinder, Sides = 8 }));
        history.Run(new SetShape("a", "cube", "Cube", new Shape { Kind = ShapeKind.Cylinder, Sides = 12 }));
        Assert.Equal(12, cube.Shape!.Sides);

        history.Undo();
        Assert.Equal(was, cube.Shape);
    }

    [Fact]
    public void GeometryEditedOverAGestureIsOneStepAndUndoPutsBackTheShapeToo()
    {
        var (history, scene) = Rig();
        var cube = scene["cube"]!;
        var (shape, geometry) = (cube.Shape, cube.Geometry);
        var gesture = new object();

        history.Run(new SetGeometry("a", "cube", "Cube", new Mesh([new Vector3(0, 0, 0)]), "Extrude", gesture));
        history.Run(new SetGeometry("a", "cube", "Cube", new Mesh([new Vector3(0, 0, 0), new Vector3(1, 0, 0)]), "Extrude", gesture));
        Assert.Equal(2, cube.Geometry!.VertexCount);

        history.Undo();
        Assert.Equal(shape, cube.Shape);
        Assert.Equal(geometry, cube.Geometry);
    }

    [Fact]
    public void ABoundaryIsChangedAndUndone()
    {
        var (history, scene) = Rig();
        var cube = scene["cube"]!;
        var was = cube.Boundary;

        history.Run(new SetBoundary("a", "cube", "Cube", new Boundary(BoundaryKind.Box, 0.5)));
        Assert.Equal(BoundaryKind.Box, cube.Boundary.Kind);

        history.Undo();
        Assert.Same(was, cube.Boundary);
    }

    [Fact]
    public void ACommandForAnObjectThatIsGoneDoesNothingRatherThanThrow()
    {
        var (history, _) = Rig();

        history.Run(new SetVisible("a", "ghost", "Ghost", false));
        history.Run(new SetShape("a", "ghost", "Ghost", new Shape { Kind = ShapeKind.Cube }));
        history.Run(new SetWeatherWind("a", "ghost", new WindValues(0, 0), new WindValues(1, 1)));
        history.Undo();
        history.Undo();
        history.Undo();
    }
}

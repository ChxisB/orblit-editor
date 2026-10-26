namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// A car and its wheels on objects: carried through the scene file and edited as one step each.
public sealed class VehicleTests
{
    // A car with a wheel under it. Only the wheel under the car has a wheel component.
    private static EditorScene Scene(Doc.VehicleComponent? vehicle = null, Doc.WheelComponent? wheel = null)
    {
        var car = new OrderedDictionary<string, Doc.SceneComponent> { [Doc.SceneComponents.Body] = new Doc.BodyComponent(mass: 1200) };
        if (vehicle is not null)
        {
            car[Doc.SceneComponents.Vehicle] = vehicle;
        }

        var wheels = new OrderedDictionary<string, Doc.SceneComponent>();
        if (wheel is not null)
        {
            wheels[Doc.SceneComponents.Wheel] = wheel;
        }

        return new EditorScene(
        [
            new SceneObject("car", "Car", ObjectKind.Group, components: car),
            new SceneObject("wheel", "Wheel", ObjectKind.Group, parentId: "car", position: new Vector3(0.8, -0.2, -1.2), components: wheels),
        ]);
    }

    private static History HistoryOf(EditorScene scene)
    {
        var workspace = new Workspace("/project");
        workspace.Add(new SceneEntry("a", "A", scene: scene));
        return new History(workspace);
    }

    private static SetObjectComponent PutVehicle(EditorScene scene, Doc.VehicleComponent? to) =>
        new("a", "car", "Set Car vehicle", Doc.SceneComponents.Vehicle, PhysicsComponents.VehicleOf(scene["car"]!), to);

    [Fact]
    public void ACarComesBackAsItWasSaved()
    {
        var vehicle = new Doc.VehicleComponent(maxSteer: 35, antiRoll: 4000, ratios: [3.0, 1.8, 1.1], @lock: 300);
        var wheel = new Doc.WheelComponent(radius: 0.4, steers: true, handbrake: true, hertz: 2);

        var back = SceneDocument.Decode(SceneDocument.Encode(Scene(vehicle, wheel))).Scene;

        Assert.Equal(vehicle.ToJson(), PhysicsComponents.VehicleOf(back["car"]!)!.ToJson());
        Assert.Equal(wheel.ToJson(), PhysicsComponents.WheelOf(back["wheel"]!)!.ToJson());
    }

    [Fact]
    public void AddingACarIsUndoneByTakingItOffAgain()
    {
        var world = Scene();
        var history = HistoryOf(world);

        history.Run(PutVehicle(world, new Doc.VehicleComponent()));
        Assert.NotNull(PhysicsComponents.VehicleOf(world["car"]!));

        history.Undo();
        Assert.Null(PhysicsComponents.VehicleOf(world["car"]!));
    }

    [Fact]
    public void ADragThroughAFieldIsOneStep()
    {
        var world = Scene(new Doc.VehicleComponent());
        var history = HistoryOf(world);

        for (var i = 1; i <= 20; i++)
        {
            history.Run(PutVehicle(world, PhysicsComponents.VehicleOf(world["car"]!)!.CopyWith(maxSteer: 30.0 + i)));
        }

        Assert.Equal(50, PhysicsComponents.VehicleOf(world["car"]!)!.MaxSteer);

        history.Undo();
        Assert.Equal(30, PhysicsComponents.VehicleOf(world["car"]!)!.MaxSteer);
        Assert.NotNull(PhysicsComponents.VehicleOf(world["car"]!));
    }
}

using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The sections for a car and its wheels. The torque curve and the gears are in the file; the rows
/// here are the ones a car is tuned by.
/// </summary>
internal static class VehicleSection
{
    /// <summary>
    /// A body the solver moves, which is the only kind a car can be. A vehicle a file already put on
    /// another kind is still shown, so it can be seen and taken off.
    /// </summary>
    public static bool AppliesToVehicle(InspectorTarget target) =>
        PhysicsComponents.VehicleOf(target.Item) is not null
        || (PhysicsComponents.PhysicsBodyOf(target.Item) is { } body && PhysicsComponents.MovesFreely(body));

    /// <summary>A direct child of a car, because that is where the scene looks for a wheel, and anything that already has one.</summary>
    public static bool AppliesToWheel(InspectorTarget target) =>
        PhysicsComponents.WheelOf(target.Item) is not null
        || (target.Item.ParentId is { } parentId && target.Scene[parentId] is { } parent && PhysicsComponents.VehicleOf(parent) is not null);

    public static Control Vehicle(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var rows = new ComponentRows<VehicleComponent>(fields, target, SceneComponents.Vehicle);
        if (rows.Current is null)
        {
            return fields.Section(
                "Vehicle",
                rows.Add("Add vehicle", "Hold this body up on the wheels that are its children.", new VehicleComponent(), $"Add vehicle to {name}"));
        }

        return fields.Section(
            "Vehicle",
            Fields.Column(
            [
                rows.Drag("Steer lock", v => v.MaxSteer, (v, value) => v.CopyWith(maxSteer: value), new DragOptions { Minimum = 0, Step = 0.2, Decimals = 1 }),
                rows.Drag("Anti-roll", v => v.AntiRoll, (v, value) => v.CopyWith(antiRoll: value), new DragOptions { Minimum = 0, Step = 20, Decimals = 0 }),
                rows.Drag("Idle", v => v.Idle, (v, value) => v.CopyWith(idle: value), new DragOptions { Minimum = 100, Step = 5, Decimals = 0 }),
                rows.Drag("Final drive", v => v.FinalDrive, (v, value) => v.CopyWith(finalDrive: value), new DragOptions { Minimum = 0.1 }),
                rows.Drag("Reverse", v => v.Reverse, (v, value) => v.CopyWith(reverse: value), new DragOptions { Minimum = 0.1 }),
                rows.Drag("Diff lock", v => v.Lock, (v, value) => v.CopyWith(@lock: value), new DragOptions { Minimum = 0, Step = 2, Decimals = 0 }),
                rows.Remove("Remove vehicle", "Make this an ordinary body again.", $"Remove vehicle from {name}"),
            ]));
    }

    public static Control Wheel(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var rows = new ComponentRows<WheelComponent>(fields, target, SceneComponents.Wheel);
        if (rows.Current is null)
        {
            return fields.Section(
                "Wheel",
                rows.Add("Add wheel", "Make this object a wheel. Put it where the wheel sits at rest.", new WheelComponent(), $"Add wheel to {name}"));
        }

        return fields.Section(
            "Wheel",
            Fields.Column(
            [
                rows.Toggle("Steers", w => w.Steers, (w, on) => w.CopyWith(steers: on)),
                rows.Toggle("Drives", w => w.Drives, (w, on) => w.CopyWith(drives: on)),
                rows.Toggle("Handbrake", w => w.Handbrake, (w, on) => w.CopyWith(handbrake: on)),
                rows.Drag("Radius", w => w.Radius, (w, value) => w.CopyWith(radius: value), new DragOptions { Minimum = 0.05 }),
                rows.Drag("Travel", w => w.Travel, (w, value) => w.CopyWith(travel: value), new DragOptions { Minimum = 0.02 }),
                rows.Drag("Spring", w => w.Hertz, (w, value) => w.CopyWith(hertz: value), new DragOptions { Minimum = 0.5 }),
                rows.Drag("Damping", w => w.Damping, (w, value) => w.CopyWith(damping: value), new DragOptions { Minimum = 0.05 }),
                rows.Drag("Grip", w => w.Grip, (w, value) => w.CopyWith(grip: value), new DragOptions { Minimum = 0.05 }),
                rows.Drag("Brake", w => w.Brake, (w, value) => w.CopyWith(brake: value), new DragOptions { Minimum = 0, Step = 20, Decimals = 0 }),
                rows.Remove("Remove wheel", "Make this an ordinary object again.", $"Remove wheel from {name}"),
            ]));
    }
}

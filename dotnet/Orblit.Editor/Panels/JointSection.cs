using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The section for a joint: which two bodies it holds together, and how they may move against each other.
/// </summary>
internal sealed class JointSection
{
    private static readonly string[] OffOn = ["Off", "On"];
    private static readonly string[] Drives = ["Speed", "Place"];
    private static readonly string[] Rests = ["As made", "Set"];

    private readonly Fields _fields;
    private readonly InspectorTarget _target;
    private readonly ComponentRows<JointComponent> _rows;

    private JointSection(Fields fields, InspectorTarget target)
    {
        _fields = fields;
        _target = target;
        _rows = new ComponentRows<JointComponent>(fields, target, SceneComponents.Joint);
    }

    private SceneObject Item => _target.Item;

    /// <summary>
    /// Anything a joint on it would hold: a body, or something under one, which is how a hinge sits at
    /// a door's edge rather than its middle. Anything with no body at or above it would hold nothing,
    /// so it is not offered there unless a file already put one on.
    /// </summary>
    public static bool AppliesTo(InspectorTarget target) =>
        PhysicsComponents.JointOf(target.Item) is not null || PhysicsComponents.JointEndsIn(target.Scene, target.Item.Id).Body is not null;

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var section = new JointSection(fields, target);
        return fields.Section("Joint", section._rows.Current is { } joint ? section.Present(joint) : section.Absent());
    }

    private Control Absent() =>
        _rows.Add("Add joint", "Connect this object to another physics body.", new JointComponent(), $"Add joint to {Item.Name}");

    private StackPanel Present(JointComponent joint)
    {
        var (holds, nothing) = JointModel.Holds(_target.Scene, Item.Id);
        return Fields.Column(
        [
            Fields.Row("Holds", Fields.Note(holds, nothing ? OrblitColors.Warn : OrblitColors.Ink)),
            _fields.Pick(
                "Kind",
                JointModel.LabelOf(joint.Kind),
                JointModel.Kinds,
                kind => _rows.Change(current => JointModel.Seeded(current.CopyWith(kind: kind)))),
            .. joint.Axes.SelectMany(axis => Limits(joint, axis)),
            .. Soft(joint),
            .. Swing(joint),
            .. Machine(joint),
            .. Drive(joint),
            .. JointModel.GiveAxes(joint).SelectMany(axis => Give(joint, axis)),
            _rows.Drag("Breaks at", current => current.BreakingForce, (current, value) => current.CopyWith(breakingForce: value), Measured("N", 1, 0)),
            _rows.Drag(string.Empty, current => current.BreakingTorque, (current, value) => current.CopyWith(breakingTorque: value), Measured("N·m", 1, 0)),
            _rows.Toggle("Collide", current => current.Collide, (current, on) => current.CopyWith(collide: on)),
            _rows.Remove("Remove joint", "Remove the connection between these bodies.", $"Remove joint from {Item.Name}"),
        ]);
    }

    // A fresh set of options each time, because the unit after the field is a control and has one parent.
    private static DragOptions Measured(string unit, double step = 0.01, double? minimum = null) => new()
    {
        Step = step,
        Minimum = minimum,
        Trailing = new TextBlock
        {
            Text = unit,
            MinWidth = unit.Length > 3 ? 38 : 28,
            FontSize = OrblitText.Caption,
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };

    // Whether it is free, locked or a range, then the number or the two numbers.
    private List<Control> Limits(JointComponent joint, JointAxis axis)
    {
        var modes = JointModel.Modes(joint.Kind);
        List<Control> shown =
        [
            _rows.Choice(
                JointModel.LimitLabel(joint.Kind, axis),
                modes,
                current => modes[JointModel.ModeOf(current.Limits.GetValueOrDefault(axis))],
                (current, label) => JointModel.Moded(current, axis, Array.IndexOf(modes, label), JointModel.PlacedLength(_target.Scene, Item.Id))),
        ];
        if (joint.Limits.GetValueOrDefault(axis) is { } range)
        {
            shown.Add(Limited(joint.Kind, axis, range));
        }

        return shown;
    }

    private Control Limited(JointKind kind, JointAxis axis, JointRange range)
    {
        var turns = JointAxes.Turns(axis);
        var options = Measured(turns ? "°" : "m", turns ? 1 : 0.01, kind == JointKind.Distance ? 0 : null);
        return range.Locked
            ? _rows.Drag(
                string.Empty,
                current => current.Limits.GetValueOrDefault(axis)?.Low ?? 0,
                (current, value) => current.Limit(axis, JointRange.At(value)),
                options)
            : _rows.Drags(
                string.Empty,
                current => current.Limits.GetValueOrDefault(axis) is { } now ? [now.Low, now.High] : [0, 0],
                (current, values) => current.Limit(axis, JointModel.Ordered(current, axis, values)),
                options);
    }

    private List<Control> Soft(JointComponent joint)
    {
        if (!JointModel.HasRange(joint) && joint.Kind != JointKind.Cone)
        {
            return [];
        }

        List<Control> shown = [_rows.Drag("Soft ends", current => current.SoftHertz, (current, value) => current.CopyWith(softHertz: value), Measured("Hz", 0.1, 0))];
        if (joint.SoftHertz > 0)
        {
            shown.Add(_rows.Drag("Damping", current => current.SoftRatio, (current, value) => current.CopyWith(softRatio: value), Measured(string.Empty, 0.05, 0)));
        }

        return shown;
    }

    private List<Control> Swing(JointComponent joint) => joint.Kind != JointKind.Cone
        ? []
        : [_fields.Slider("Swing", Math.Clamp(joint.Swing, 0, 180), value => _rows.Change(current => current.CopyWith(swing: value)), new SliderOptions { Min = 0, Max = 180, Unit = "°" })];

    // What a gear, a rack, a pulley or a path adds: the numbers that tie it to the body above, or the line it runs along.
    private List<Control> Machine(JointComponent joint) => joint.Kind switch
    {
        JointKind.Gear or JointKind.Rack =>
        [
            Ratio(joint),
            _rows.Drags("Turns about", current => JointModel.Numbers(current.AxisB), (current, values) => current.CopyWith(axisB: JointModel.Vector(values)), Measured(string.Empty)),
        ],
        JointKind.Pulley => [Ratio(joint), .. Enumerable.Range(0, 2).Select(Over)],
        JointKind.Path => PathRows(joint),
        _ => [],
    };

    private Control Ratio(JointComponent joint) =>
        _rows.Drag("Ratio", current => current.Ratio, (current, value) => current.CopyWith(ratio: value), Measured(joint.Kind == JointKind.Rack ? "m/rad" : "×", 0.05));

    private Control Over(int index) => _rows.Drags(
        $"Over {index + 1}",
        current => JointModel.PointOf(current.Over, index),
        (current, values) => current.CopyWith(over: JointModel.WithPoint(current.Over, index, values, take: 2)),
        Measured("m"));

    private List<Control> PathRows(JointComponent joint)
    {
        var add = Fields.Button(
            "Add point",
            "Carry the path on past its last point.",
            () => _fields.Host.Apply(() => _rows.Change(current => current.CopyWith(path: [.. current.Path, JointModel.Beyond(current.Path)]))));
        var remove = Fields.Button(
            "Remove point",
            "Take off the last point. A path keeps two.",
            () => _fields.Host.Apply(() => _rows.Change(current => current.CopyWith(path: [.. current.Path.Take(current.Path.Count - 1)]))));
        remove.IsEnabled = joint.Path.Count > 2;
        return
        [
            .. joint.Path.Select((_, index) => _rows.Drags(
                $"Point {index + 1}",
                current => JointModel.PointOf(current.Path, index),
                (current, values) => current.CopyWith(path: JointModel.WithPoint(current.Path, index, values)),
                Measured("m"))),
            add,
            remove,
            _rows.Toggle("Follow", current => current.Follow, (current, on) => current.CopyWith(follow: on)),
        ];
    }

    // At a speed or to a place. Both are a motor, so both are held to the strength below them.
    private List<Control> Drive(JointComponent joint)
    {
        if (!JointModel.IsDriven(joint.Kind))
        {
            return [];
        }

        var turning = joint.Kind == JointKind.Hinge;
        var step = turning ? 1 : 0.01;
        List<Control> shown =
        [
            _rows.Choice("Drive", Drives, current => current.GoalHertz > 0 ? "Place" : "Speed", (current, label) => JointModel.Driven(current, label == "Place")),
        ];
        if (joint.GoalHertz > 0)
        {
            shown.Add(_rows.Drag("Hold at", current => current.Goal, (current, value) => current.CopyWith(goal: value), Measured(turning ? "°" : "m", step)));
            shown.Add(_rows.Drag("Stiffness", current => current.GoalHertz, (current, value) => current.CopyWith(goalHertz: value), Measured("Hz", 0.1, 0.1)));
            shown.Add(_rows.Drag("Damping", current => current.GoalRatio, (current, value) => current.CopyWith(goalRatio: value), Measured(string.Empty, 0.05, 0)));
        }
        else
        {
            shown.Add(_rows.Drag("Motor", current => current.Speed, (current, value) => current.CopyWith(speed: value), Measured(turning ? "°/s" : "m/s", step)));
        }

        shown.Add(_rows.Drag("Strength", current => current.Strength, (current, value) => current.CopyWith(strength: value), Measured(turning ? "N·m" : "N", 0.1, 0)));
        return shown;
    }

    // Whether there is a spring on one way, and when there is, how it is tuned.
    private List<Control> Give(JointComponent joint, JointAxis axis)
    {
        List<Control> shown =
        [
            _rows.Choice(
                JointModel.SpringLabel(joint, axis),
                OffOn,
                current => current.Springs.ContainsKey(axis) ? "On" : "Off",
                (current, label) => label == "On" ? current.Spring(axis, JointModel.NewGive) : current.Unspring(axis)),
        ];
        if (joint.Springs.GetValueOrDefault(axis) is { } give)
        {
            shown.AddRange(Tune(axis, give));
        }

        return shown;
    }

    private List<Control> Tune(JointAxis axis, JointGive give)
    {
        var turns = JointAxes.Turns(axis);

        // The spring is read as it is at each step, or as a new one if a drag outlives it.
        static JointGive Now(JointComponent joint, JointAxis axis) => joint.Springs.GetValueOrDefault(axis) ?? JointModel.NewGive;

        Control Drag(string label, Func<JointGive, double> read, Func<JointGive, double, JointGive> make, DragOptions options) =>
            _rows.Drag(label, current => read(Now(current, axis)), (current, value) => current.Spring(axis, make(Now(current, axis), value)), options);

        List<Control> shown =
        [
            Drag("Stiffness", now => now.Hertz, (now, value) => now with { Hertz = value }, Measured("Hz", 0.1, 0.1)),
            Drag("Damping", now => now.Ratio, (now, value) => now with { Ratio = value }, Measured(string.Empty, 0.05, 0)),
            _rows.Choice(
                "Rest",
                Rests,
                current => Now(current, axis).Rest is null ? "As made" : "Set",
                (current, label) => current.Spring(axis, Now(current, axis) with { Rest = label == "Set" ? 0 : null })),
        ];
        if (give.Rest is not null)
        {
            shown.Add(Drag(string.Empty, now => now.Rest ?? 0, (now, value) => now with { Rest = value }, Measured(turns ? "°" : "m", turns ? 1 : 0.01)));
        }

        return shown;
    }
}

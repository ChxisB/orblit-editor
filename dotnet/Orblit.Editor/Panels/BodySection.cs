using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Editor.Viewport;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// The section for a body: what the physics does with an object.
/// </summary>
internal sealed class BodySection
{
    private static readonly string[] OffOn = ["Off", "On"];
    private static readonly string[] Starts = ["Awake", "Asleep"];
    private static readonly string[] Acts = ["Solid", "Trigger"];
    private static readonly string[] Axes = ["X", "Y", "Z"];
    private static readonly DragOptions Positive = new() { Minimum = 0.01 };
    private static readonly DragOptions NoLess = new() { Minimum = 0 };

    private readonly Fields _fields;
    private readonly InspectorTarget _target;
    private readonly ComponentRows<BodyComponent> _rows;

    private BodySection(Fields fields, InspectorTarget target)
    {
        _fields = fields;
        _target = target;
        _rows = new ComponentRows<BodyComponent>(fields, target, SceneComponents.Body);
    }

    private SceneObject Item => _target.Item;

    /// <summary>
    /// What something can stand on or knock over: anything drawn, and a place in the tree for a trigger
    /// with nothing to draw. Lights, cameras and the weather are left alone unless a file already gave
    /// one a body.
    /// </summary>
    public static bool AppliesTo(InspectorTarget target) =>
        PhysicsComponents.PhysicsBodyOf(target.Item) is not null
        || target.Item.IsDrawable
        || target.Item.Kind == ObjectKind.Group;

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var section = new BodySection(fields, target);
        return fields.Section("Physics body", section._rows.Current is { } body ? section.Present(body) : section.Absent());
    }

    private (Vector3 Min, Vector3 Max)? Reported => _target.BoundsOf?.Invoke(Item);

    private StackPanel Absent() =>
        Fields.Column(
        [
            .. Collision(),
            // A drawn thing gets a body its own shape, which is what adding one to a crate means. A place
            // in the tree gets a metre box to start.
            _rows.Add(
                "Add body",
                "Give this object a shape for physics.",
                Item.IsDrawable ? BodyFit.Fitted(new BodyComponent(), Item, Reported) : new BodyComponent(),
                $"Add body to {Item.Name}"),
        ]);

    // Makes the model's own triangles the body, for something a box or a hull would only approximate.
    private IEnumerable<Control> Collision() =>
        _target.CollisionOf is { } read && Item.IsDrawable
            ? [CollisionButton.Build(() => read(Item), geometry => _fields.Host.Apply(() => _rows.Put(BodyPartsModel.Collider(_rows.Current, geometry), $"Collide with {Item.Name}")))]
            : [];

    private StackPanel Present(BodyComponent body)
    {
        var free = PhysicsComponents.MovesFreely(body);
        var character = body.Motion == BodyMotion.Character;
        List<Control> rows = [.. ShapeRows(), Motion(body)];
        // A character is swept by the world and is neither a place nor moved by a belt, so those rows
        // would be choices it ignores.
        rows.AddRange(character ? Walking() : Role(body, free));
        rows.AddRange(Extent(body));
        rows.AddRange(Made(body, free));
        if (free)
        {
            rows.AddRange(Moving(body));
        }

        rows.AddRange(Layers(body));
        rows.Add(Buttons(body));
        return Fields.Column(rows);
    }

    // Every shape, in rows of three because the labels do not fit across the panel.
    private IEnumerable<Control> ShapeRows() =>
        BodyModel.ShapeRows().Select((labels, row) => _rows.Choice(
            row == 0 ? "Shape" : "",
            labels,
            current => BodyModel.LabelOf(current.Shape),
            (current, label) => BodyFit.Reshaped(current, BodyModel.ShapeOf(label), Item, Reported)));

    private Control Motion(BodyComponent body) =>
        // Ground never moves, whatever the file says, so there is nothing to choose and the row says so
        // instead of offering a choice it ignores.
        body.Shape is BodyShape.Plane or BodyShape.Mesh
            ? _fields.Choice("Motion", ["Fixed"], "Fixed")
            : _rows.Choice(
                "Motion",
                [.. BodyModel.Motions.Select(pair => pair.Value)],
                current => BodyModel.LabelOf(current.Motion),
                (current, label) => current.CopyWith(motion: BodyModel.MotionOf(label)));

    // Whether the body is a thing or a place, and whether it hears every step of what touches it.
    private List<Control> Role(BodyComponent body, bool free)
    {
        List<Control> rows = [];
        if (!free)
        {
            // The solver moves a free body and cannot move a place. A zone is a place whatever the
            // body says, so that row reads as fact.
            rows.Add(PhysicsComponents.ZoneOf(Item) is not null
                ? _fields.Choice("Acts as", ["Trigger"], "Trigger")
                : _rows.Choice("Acts as", Acts, current => current.Trigger ? "Trigger" : "Solid", (current, label) => current.CopyWith(trigger: label == "Trigger")));
        }

        rows.Add(OnOff("Stay events", current => current.Stay, (current, on) => current.CopyWith(stay: on)));
        return rows;
    }

    // What a character climbs, stands on, pushes and crouches to.
    private List<Control> Walking() =>
    [
        Number("Step height", current => current.StepHeight, (current, value) => current.CopyWith(stepHeight: value), 0, 0.01),
        Number("Steepest", current => current.Steepest, (current, value) => current.CopyWith(steepest: value), 0, 0.01),
        Number("Skin", current => current.Skin, (current, value) => current.CopyWith(skin: value), 0, 0.001),
        Number("Strength", current => current.Strength, (current, value) => current.CopyWith(strength: value), 0, 10),
        Number("Crouch height", current => current.CrouchHeight, (current, value) => current.CopyWith(crouchHeight: value), 0, 0.05),
    ];

    private List<Control> Extent(BodyComponent body)
    {
        List<Control> rows = [];
        if (body.Shape == BodyShape.Box)
        {
            rows.Add(Vector("Size", current => current.Size, (current, value) => current.CopyWith(size: value), Positive));
        }

        if (body.Shape is BodyShape.Sphere or BodyShape.Capsule or BodyShape.Cylinder)
        {
            rows.Add(Number("Radius", current => current.Radius, (current, value) => current.CopyWith(radius: value), 0.01));
        }

        if (body.Shape is BodyShape.Capsule or BodyShape.Cylinder)
        {
            rows.Add(Number("Height", current => current.Height, (current, value) => current.CopyWith(height: value), 0.01));
        }

        rows.AddRange(Described(body));
        if (body.Shape == BodyShape.Compound)
        {
            rows.AddRange(BodyPartRows.Build(_rows, body, BodyFit.Fitted(body.CopyWith(shape: BodyShape.Hull), Item, Reported).Hull));
        }

        if (body.Shape != BodyShape.Plane)
        {
            rows.Add(Vector("Shape scale", current => current.ShapeScale, (current, value) => current.CopyWith(shapeScale: value), Positive));
        }

        rows.Add(Vector(body.Shape == BodyShape.Plane ? "Surface at" : "Centre", current => current.Centre, (current, value) => current.CopyWith(centre: value)));
        return rows;
    }

    // What a hull or a mesh is made of. Their points are only in the file, so this says how many there are.
    private static List<Control> Described(BodyComponent body)
    {
        if (body.Shape == BodyShape.Hull)
        {
            var outline = ConvexOutline.Of(body.Hull);
            return [Fields.Note(BodyModel.HullNote(outline.Corners.Count, outline.HasVolume))];
        }

        return body.Shape == BodyShape.Mesh ? [Fields.Note(BodyModel.MeshNote(body.MeshIndices.Count))] : [];
    }

    // How heavy it is, what it is made of and how it grips.
    private List<Control> Made(BodyComponent body, bool free)
    {
        List<Control> rows = [];
        if (free)
        {
            rows.Add(Number("Mass", current => current.Mass, (current, value) => current.CopyWith(mass: value), 0.01, 0.1));
        }

        rows.Add(_fields.Pick(
            "Material",
            BodyMaterial.Of(body)?.Name ?? "Custom",
            BodyMaterial.Presets.Select(material => new KeyValuePair<BodyMaterial, string>(material, material.Name)),
            material => _rows.Change(current => current.MadeOf(material))));
        rows.Add(Unit("Friction", body.Friction, (current, value) => current.CopyWith(friction: value)));
        rows.Add(Unit("Bounce", body.Restitution, (current, value) => current.CopyWith(restitution: value)));
        // A place pushes nothing and a free body is not fixed to anything, so only a solid body that
        // stays put can be a belt.
        if (!free && !PhysicsComponents.IsPlace(Item) && body.Motion != BodyMotion.Character)
        {
            rows.Add(Vector("Belt speed", current => current.Surface, (current, value) => current.CopyWith(surface: value), new DragOptions { Step = 0.05 }));
        }

        return rows;
    }

    // How a free body slows down, is held and limited, and where its weight sits.
    private List<Control> Moving(BodyComponent body) =>
    [
        Unit("Drag", body.LinearDamping, (current, value) => current.CopyWith(linearDamping: value)),
        Unit("Spin drag", body.AngularDamping, (current, value) => current.CopyWith(angularDamping: value)),
        _rows.Choice("Starts", Starts, current => current.StartsAsleep ? "Asleep" : "Awake", (current, label) => current.CopyWith(startsAsleep: label == "Asleep")),
        Number("Gravity", current => current.GravityScale, (current, value) => current.CopyWith(gravityScale: value), null, 0.02),
        Locks("Lock move", body, [BodyLock.MoveX, BodyLock.MoveY, BodyLock.MoveZ]),
        Locks("Lock turn", body, [BodyLock.TurnX, BodyLock.TurnY, BodyLock.TurnZ]),
        Number("Max speed", current => current.MaxSpeed, (current, value) => current.CopyWith(maxSpeed: value), 0, 0.1),
        Number("Max spin", current => current.MaxSpin, (current, value) => current.CopyWith(maxSpin: value), 0, 0.1),
        Fields.Note("Zero is no limit."),
        OnOff("Continuous", current => current.Continuous, (current, on) => current.CopyWith(continuous: on)),
        Fields.Note("Stops a small fast body at a thin fixed wall. Costs a cast."),
        Vector("Weight at", current => current.CentreOfMass, (current, value) => current.CopyWith(centreOfMass: value)),
        Vector("Inertia", current => current.Inertia, (current, value) => current.CopyWith(inertia: value), NoLess),
        Fields.Note("Give all three or the shape's own inertia is used."),
    ];

    // The layers a body is in and the layers it looks for.
    private List<Control> Layers(BodyComponent body) =>
    [
        _fields.Layers("Is in", unchecked((int)body.Layers), _target.Scene.LayerNames, index => _rows.Change(current => current.CopyWith(layers: current.Layers ^ (1L << index)))),
        _fields.Layers("Sees", unchecked((int)body.Cares), _target.Scene.LayerNames, index => _rows.Change(current => current.CopyWith(cares: current.Cares ^ (1L << index)))),
        Fields.Note("A pair meets when either one sees the other's layer."),
    ];

    private StackPanel Buttons(BodyComponent body)
    {
        List<Control> buttons = [.. Collision()];
        if (Item.IsDrawable && body.Shape != BodyShape.Mesh)
        {
            buttons.Add(Fields.Button(
                "Fit to mesh",
                "Resize the physics shape to fit this object.",
                () => _fields.Host.Apply(() => _rows.Change(current => BodyFit.Fitted(current, Item, Reported), $"Fit {Item.Name} body to its mesh"))));
        }

        buttons.Add(_rows.Remove("Remove body", "Stop simulating physics for this object.", $"Remove body from {Item.Name}"));
        return Fields.Column(buttons);
    }

    private Control OnOff(string label, Func<BodyComponent, bool> read, Func<BodyComponent, bool, BodyComponent> change) =>
        _rows.Choice(label, OffOn, current => read(current) ? "On" : "Off", (current, chosen) => change(current, chosen == "On"));

    private Control Locks(string label, BodyComponent body, BodyLock[] held) =>
        _fields.Flags(
            label,
            [.. held.Select((axis, index) => (Axes[index], body.Locks.Contains(axis)))],
            index => _rows.Change(current => BodyModel.Locked(current, held[index])));

    private Control Unit(string label, double value, Func<BodyComponent, double, BodyComponent> change) =>
        _fields.Slider(label, value, chosen => _rows.Change(current => change(current, chosen)), new SliderOptions { Min = 0, Max = 1, Decimals = 2 });

    private Control Number(string label, Func<BodyComponent, double> read, Func<BodyComponent, double, BodyComponent> change, double? minimum, double step = 0.01) =>
        _rows.Drag(label, read, change, new DragOptions { Minimum = minimum, Step = step });

    private Control Vector(string label, Func<BodyComponent, Vector3> read, Func<BodyComponent, Vector3, BodyComponent> change, DragOptions? options = null) =>
        _rows.Drags(
            label,
            current => [read(current).X, read(current).Y, read(current).Z],
            (current, values) => change(current, new Vector3(values[0], values[1], values[2])),
            options);
}

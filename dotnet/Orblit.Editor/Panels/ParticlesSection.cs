using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>
/// The section for particles: what an object gives off, how fast, how long it lasts and where it goes.
/// Offered on every object, since any of them can be a source.
/// </summary>
internal static class ParticlesSection
{
    private static readonly string[] Spaces = ["World", "Local"];

    private static readonly string[] Axes = ["X", "Y", "Z"];

    public static Control Build(Fields fields, InspectorTarget target)
    {
        var name = target.Item.Name;
        var rows = new ComponentRows<ParticleEmitterComponent>(fields, target, SceneComponents.Particles);
        if (rows.Current is not { } emitter)
        {
            return fields.Section(
                "Particles",
                rows.Add("Add particle emitter", "Make this object give off particles.", new ParticleEmitterComponent(), $"Add particle emitter to {name}"));
        }

        return fields.Section(
            "Particles",
            Fields.Column(
            [
                .. Shape(rows, emitter),
                .. Motion(rows),
                rows.Drag(
                    "Max",
                    e => e.MaxParticles,
                    (e, value) => e.CopyWith(maxParticles: (long)Math.Round(value, MidpointRounding.AwayFromZero)),
                    new DragOptions { Minimum = 1, Step = 10, Decimals = 0 }),
                rows.Remove("Remove particle emitter", "Stop this object giving off particles.", $"Remove particle emitter from {name}"),
            ]));
    }

    private static List<Control> Shape(ComponentRows<ParticleEmitterComponent> rows, ParticleEmitterComponent emitter)
    {
        var floor = new DragOptions { Minimum = 0 };
        List<Control> shown =
        [
            rows.Choice(
                "Shape",
                ParticlesModel.ShapeLabels,
                e => e.Shape.ToString(),
                (e, option) => e.CopyWith(shape: ParticlesModel.ShapeFor(option))),
        ];
        if (emitter.Shape is EmitterShape.Sphere or EmitterShape.Cone)
        {
            shown.Add(rows.Drag("Radius", e => e.Radius, (e, value) => e.CopyWith(radius: value), floor));
        }

        if (emitter.Shape == EmitterShape.Cone)
        {
            shown.Add(rows.Drag("Angle", e => e.Angle, (e, value) => e.CopyWith(angle: Math.Min(value, Math.PI)), floor));
        }

        if (emitter.Shape == EmitterShape.Box)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                var at = axis;
                shown.Add(rows.Drag($"Half {Axes[at]}", e => e.Extents[at], (e, value) => ParticlesModel.WithExtent(e, at, value), floor));
            }
        }

        return shown;
    }

    private static List<Control> Motion(ComponentRows<ParticleEmitterComponent> rows)
    {
        var floor = new DragOptions { Minimum = 0 };
        var life = new DragOptions { Minimum = ParticlesModel.Shortest, Step = 0.05 };
        return
        [
            rows.Drag("Rate", e => e.Rate, (e, value) => e.CopyWith(rate: value), new DragOptions { Minimum = 0, Step = 0.5, Decimals = 1 }),
            rows.Drag("Life min", e => e.LifetimeMin, ParticlesModel.WithLifetimeMin, life),
            rows.Drag("Life max", e => e.LifetimeMax, ParticlesModel.WithLifetimeMax, life),
            rows.Drag("Speed min", e => e.SpeedMin, ParticlesModel.WithSpeedMin, floor),
            rows.Drag("Speed max", e => e.SpeedMax, ParticlesModel.WithSpeedMax, floor),
            rows.Drag("Gravity", e => e.Gravity.Y, ParticlesModel.WithGravityY, new DragOptions { Step = 0.1, Decimals = 1 }),
            rows.Drag("Drag", e => e.Drag, (e, value) => e.CopyWith(drag: value), floor),
            rows.Drag("Noise", e => e.NoiseStrength, (e, value) => e.CopyWith(noiseStrength: value), floor),
            rows.Choice(
                "Space",
                Spaces,
                e => e.Space == EmitterSpace.Local ? "Local" : "World",
                (e, option) => e.CopyWith(space: option == "Local" ? EmitterSpace.Local : EmitterSpace.World)),
        ];
    }
}

namespace Orblit.Editor.Panels;

using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>What the particles section decides about an emitter, apart from drawing its fields.</summary>
public static class ParticlesModel
{
    /// <summary>How short a life may be. A particle that lives no time is no particle, and the scene file does not accept one.</summary>
    public const double Shortest = 0.05;

    /// <summary>The shapes by the name each is shown under, which is its own.</summary>
    public static IReadOnlyList<string> ShapeLabels { get; } = Enum.GetNames<EmitterShape>();

    public static EmitterShape ShapeFor(string label) => Enum.Parse<EmitterShape>(label);

    /// <summary>
    /// <paramref name="emitter"/> with the shortest life at <paramref name="value"/>, and the longest
    /// raised to meet it, so dragging one end past the other moves both and leaves a range.
    /// </summary>
    public static ParticleEmitterComponent WithLifetimeMin(ParticleEmitterComponent emitter, double value) =>
        emitter.CopyWith(lifetimeMin: value, lifetimeMax: Math.Max(emitter.LifetimeMax, value));

    /// <summary><paramref name="emitter"/> with the longest life at <paramref name="value"/>, and the shortest lowered to meet it.</summary>
    public static ParticleEmitterComponent WithLifetimeMax(ParticleEmitterComponent emitter, double value) =>
        emitter.CopyWith(lifetimeMax: value, lifetimeMin: Math.Min(emitter.LifetimeMin, value));

    public static ParticleEmitterComponent WithSpeedMin(ParticleEmitterComponent emitter, double value) =>
        emitter.CopyWith(speedMin: value, speedMax: Math.Max(emitter.SpeedMax, value));

    public static ParticleEmitterComponent WithSpeedMax(ParticleEmitterComponent emitter, double value) =>
        emitter.CopyWith(speedMax: value, speedMin: Math.Min(emitter.SpeedMin, value));

    /// <summary><paramref name="emitter"/> with the box half-extent on <paramref name="axis"/> at <paramref name="value"/>.</summary>
    public static ParticleEmitterComponent WithExtent(ParticleEmitterComponent emitter, int axis, double value)
    {
        var extents = emitter.Extents.Clone();
        extents[axis] = value;
        return emitter.CopyWith(extents: extents);
    }

    /// <summary><paramref name="emitter"/> with gravity along y at <paramref name="value"/>. The sideways parts stay as the file has them.</summary>
    public static ParticleEmitterComponent WithGravityY(ParticleEmitterComponent emitter, double value) =>
        emitter.CopyWith(gravity: new Vector3(emitter.Gravity.X, value, emitter.Gravity.Z));
}

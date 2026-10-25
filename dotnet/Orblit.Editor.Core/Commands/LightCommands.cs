namespace Orblit.Editor.Core;

using Orblit.Light;

// A light's own settings: how much, what kind, what shape.
//
// F2: a command takes the scene, the object, its name for the label and the change, so a step is
// built by name from what the panel already holds.

/// <summary>Changes a light's power, in watts.</summary>
public sealed class SetPower(string sceneId, string id, string name, double from, double to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public double From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public double To { get; private set; } = to;

    public override string Label => $"Set {Name} power";

    public override object? MergeKey => (Id, "power");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetPower other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Power = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Power = From;
}

/// <summary>
/// Changes what kind of light an object is.
/// <para>
/// The power comes with it, because the units change with the type: a sun is stated in watts per
/// square metre and everything else in watts, and keeping the number while changing what it means is
/// how a scene ends up blinding.
/// </para>
/// </summary>
public sealed class SetLightType(
    string sceneId,
    string id,
    string name,
    LightType from,
    LightType to,
    double fromPower,
    double toPower) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public LightType From { get; } = from;

    public LightType To { get; } = to;

    public double FromPower { get; } = fromPower;

    public double ToPower { get; } = toPower;

    public override string Label => $"Make {Name} a {To.ToString().ToLowerInvariant()}";

    public override void Apply(ISceneHost host) => Write(host, To, ToPower);

    public override void Revert(ISceneHost host) => Write(host, From, FromPower);

    private void Write(ISceneHost host, LightType type, double power)
    {
        if (host.SceneFor(SceneId)?[Id] is not { } target)
        {
            return;
        }

        target.LightType = type;
        target.Power = power;
    }
}

/// <summary>A light's cone, the softness of its edge, the size of its source and the sun's width.</summary>
public readonly record struct LightShape(double Size, double Blend, double Radius, double Sun);

/// <summary>
/// Changes a light's cone or the size of its source.
/// <para>
/// One command for the three, because they are three sliders on one shape and an undo stack that
/// distinguishes them buys nothing.
/// </para>
/// </summary>
public sealed class SetLightShape(string sceneId, string id, string name, LightShape from, LightShape to)
    : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public LightShape From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public LightShape To { get; private set; } = to;

    public override string Label => $"Shape {Name}";

    public override object? MergeKey => (Id, "light shape");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetLightShape other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => Write(host, To);

    public override void Revert(ISceneHost host) => Write(host, From);

    private void Write(ISceneHost host, LightShape values)
    {
        if (host.SceneFor(SceneId)?[Id] is not { } target)
        {
            return;
        }

        target.SpotSize = values.Size;
        target.SpotBlend = values.Blend;
        target.SourceRadius = values.Radius;
        target.SunAngle = values.Sun;
    }
}

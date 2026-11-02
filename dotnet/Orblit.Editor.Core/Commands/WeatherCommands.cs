namespace Orblit.Editor.Core;

using Orblit.Weather;

// The time of day and the sky: what hour it is, which body lights it, and the weather in it.
//
// F2: a command takes the scene, the object, its name for the label and the change, so a step is
// built by name from what the panel already holds.

/// <summary>The hour, whether the day runs, and how many hours a second.</summary>
public readonly record struct SceneTime(double Hour, bool Cycle, double Speed);

/// <summary>Sets the time of day and whether it moves.</summary>
public sealed class SetSceneTime(string sceneId, SceneTime from, SceneTime to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public SceneTime From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public SceneTime To { get; private set; } = to;

    public override string Label => From.Cycle != To.Cycle
        ? (To.Cycle ? "Start the day" : "Stop the day")
        : "Set the time";

    public override object? MergeKey => (SceneId, "time");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetSceneTime other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => Write(host, To);

    public override void Revert(ISceneHost host) => Write(host, From);

    private void Write(ISceneHost host, SceneTime value)
    {
        if (host.SceneFor(SceneId) is not { } scene)
        {
            return;
        }

        scene.TimeOfDay = value.Hour;
        scene.DayCycle = value.Cycle;
        scene.HoursPerSecond = value.Speed;

        // A cycle that is starting begins at the hour it was set to rather than wherever the clock
        // had got to before it was last stopped.
        scene.Clock = 0;
    }
}

/// <summary>Switches a light between being the sun and being the moon.</summary>
public sealed class SetCelestialBody(string sceneId, string id, string name, CelestialBody from, CelestialBody to)
    : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public CelestialBody From { get; } = from;

    public CelestialBody To { get; } = to;

    public override string Label => $"Make {Name} the {To.Label().ToLowerInvariant()}";

    public override bool FitsLayout => true;

    public override void Apply(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Body = To;

    public override void Revert(ISceneHost host) => host.SceneFor(SceneId)?[Id]?.Body = From;
}

/// <summary>
/// Chooses which shape of cloud a sky has.
/// <para>
/// Separate from the condition because it is a separate decision: the same weather makes very
/// different skies, and somebody who has asked for cirrus over a fair afternoon should keep it when
/// they nudge the cover.
/// </para>
/// </summary>
public sealed class SetCloudKind(
    string sceneId,
    string id,
    CloudKind? from,
    CloudKind? to,
    double fromHeight,
    double toHeight) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public CloudKind? From { get; } = from;

    public CloudKind? To { get; } = to;

    /// <summary>
    /// Where the base sits, which moves with the shape. Cirrus is ice seven kilometres up and
    /// cumulus condense below one, so a shape that arrived without its height would arrive in the
    /// wrong place. The slider still moves it afterwards. This is only where it starts.
    /// </summary>
    public double FromHeight { get; } = fromHeight;

    public double ToHeight { get; } = toHeight;

    public override string Label => To is { } kind
        ? $"Set the cloud to {kind.Label().ToLowerInvariant()}"
        : "Follow the condition";

    public override bool FitsLayout => true;

    public override void Apply(ISceneHost host) => Set(host, To, ToHeight);

    public override void Revert(ISceneHost host) => Set(host, From, FromHeight);

    private void Set(ISceneHost host, CloudKind? kind, double height)
    {
        if (host.SceneFor(SceneId)?[Id] is not { } target)
        {
            return;
        }

        target.CloudKind = kind;
        target.Weather = target.Weather with { CloudHeight = height };
    }
}

/// <summary>
/// Puts the scene into a different sort of weather.
/// <para>
/// The values come with the name, because a condition is a set of them rather than a mode: once it
/// has been applied, every one of them is free to be moved, and the name is only a record of where
/// they started.
/// </para>
/// </summary>
public sealed class SetWeatherCondition(
    string sceneId,
    string id,
    WeatherCondition from,
    WeatherCondition to,
    WeatherState fromState,
    WeatherState toState) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public WeatherCondition From { get; } = from;

    public WeatherCondition To { get; } = to;

    public WeatherState FromState { get; } = fromState;

    public WeatherState ToState { get; } = toState;

    public override string Label => $"Set the weather to {To.Label().ToLowerInvariant()}";

    public override bool FitsLayout => true;

    public override void Apply(ISceneHost host) => Change(host, To, ToState);

    public override void Revert(ISceneHost host) => Change(host, From, FromState);

    // Sets the weather going rather than switching it. The change starts from what is on screen this
    // instant, which may itself be halfway through an earlier one. Otherwise changing your mind during
    // a transition snaps back to where it set off from before starting again.
    private void Change(ISceneHost host, WeatherCondition condition, WeatherState state)
    {
        if (host.SceneFor(SceneId) is not { } scene || scene[Id] is not { } target)
        {
            return;
        }

        target.BlendFrom = scene.WeatherNow ?? target.Weather;
        target.BlendSince = scene.Clock;
        target.Condition = condition;
        target.Weather = state;
    }
}

/// <summary>Adjusts one of the numbers behind the weather.</summary>
public sealed class SetWeatherValues(string sceneId, string id, WeatherState from, WeatherState to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public WeatherState From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public WeatherState To { get; private set; } = to;

    public override string Label => "Set the weather";

    public override bool FitsLayout => true;

    public override object? MergeKey => (Id, "weather");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetWeatherValues other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => Write(host, To);

    public override void Revert(ISceneHost host) => Write(host, From);

    private void Write(ISceneHost host, WeatherState state)
    {
        if (host.SceneFor(SceneId)?[Id] is not { } target)
        {
            return;
        }

        target.Weather = state;

        // A slider is somebody's hand on the weather. It arrives as they move it rather than easing
        // in behind them.
        target.BlendFrom = null;
    }
}

/// <summary>Which way the wind blows, and how long a change of weather takes.</summary>
public readonly record struct WindValues(double Direction, double Transition);

/// <summary>Sets which way the wind blows, and how long a change of weather takes.</summary>
public sealed class SetWeatherWind(string sceneId, string id, WindValues from, WindValues to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public string Id { get; } = id;

    public WindValues From { get; } = from;

    /// <summary>Not fixed: a merged run of drags rewrites where it ends up.</summary>
    public WindValues To { get; private set; } = to;

    public override string Label => "Set the wind";

    public override bool FitsLayout => true;

    public override object? MergeKey => (Id, "wind");

    public override void Absorb(EditorCommand later)
    {
        if (later is SetWeatherWind other)
        {
            To = other.To;
        }
    }

    public override void Apply(ISceneHost host) => Write(host, To);

    public override void Revert(ISceneHost host) => Write(host, From);

    private void Write(ISceneHost host, WindValues values)
    {
        if (host.SceneFor(SceneId)?[Id] is not { } target)
        {
            return;
        }

        target.WindDirection = values.Direction;
        target.TransitionSeconds = values.Transition;
    }
}

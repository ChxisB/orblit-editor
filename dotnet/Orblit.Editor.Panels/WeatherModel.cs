namespace Orblit.Editor.Panels;

using Orblit.Editor.Core;
using Orblit.Weather;

/// <summary>What the weather fields offer and say, apart from drawing them.</summary>
public static class WeatherModel
{
    /// <summary>
    /// Six across one row would be six words nobody can read. The first row is the conditions you can
    /// see through, and the second is the ones you cannot.
    /// </summary>
    private const int FirstConditionRow = 4;

    private static readonly string[][] CloudRows =
    [
        ["Auto", "Cumulus", "Stratocumulus"],
        ["Stratus", "Cirrus", "Cumulonimbus"],
    ];

    public static IReadOnlyList<string> ConditionRow(int row)
    {
        var labels = Enum.GetValues<WeatherCondition>().Select(condition => condition.Label());
        return row == 0 ? [.. labels.Take(FirstConditionRow)] : [.. labels.Skip(FirstConditionRow)];
    }

    public static WeatherCondition ConditionFor(string label) =>
        Enum.GetValues<WeatherCondition>().First(condition => condition.Label() == label);

    public static IReadOnlyList<string> CloudRow(int row) => CloudRows[row];

    /// <summary>
    /// Which of the two cloud rows lights a choice, so the chosen one is lit and the other is not. A row
    /// that always answered would light two.
    /// </summary>
    public static string CloudLabel(CloudKind? kind, int row)
    {
        var name = kind is { } shape ? shape.Label() : "Auto";
        return CloudRows[row].Contains(name) ? name : "";
    }

    /// <summary>The shape a label names. Null is automatic, which follows the condition.</summary>
    public static CloudKind? CloudKindFor(string label) =>
        label == "Auto" ? null : Enum.GetValues<CloudKind>().First(kind => kind.Label() == label);

    /// <summary>Whether another weather object is the one deciding what the air does, so this one is ignored.</summary>
    public static bool IsSpare(EditorScene scene, SceneObject weather) =>
        scene.HasSpareWeather && !ReferenceEquals(weather, scene.Weather);

    /// <summary>Which of the three things worth saying about this weather applies.</summary>
    public static string Caption(WeatherState air, bool spare)
    {
        if (spare)
        {
            return "Another Weather object is already deciding what the air is doing. This one is ignored. A scene answers that question once.";
        }

        return air.Rain > 0 && air.Snow > 0
            ? "Both at once is the temperature where both are falling. The curtain is one thing part of the way from streaks to flakes."
            : "Cloud takes the strength out of whatever is above the scene and spreads it across the sky. Shadows lose their edges before they lose their depth.";
    }

    public static string AirNote(double mist) => mist > 0
        ? "Mist is the air at ground level given a shape, moving with the wind. The cloud in the sky is the setting above. These are two different pieces of weather."
        : "Density is the even haze that distance looks like. Mist gives it a shape near the ground.";
}

namespace Orblit.Editor.Panels;

using System.Globalization;
using Orblit.Editor.Core;
using Orblit.Sequence;

/// <summary>What the timeline calls what it edits, and says when nothing is open.</summary>
public sealed record TimelineNames(string None, string About, string Make, string MakeTip, string Length);

/// <summary>The words the timeline's menus and rows use.</summary>
public static class TimelineWords
{
    /// <summary>
    /// A clip in the Animation workspace, where it is played on one thing, and a cutscene in Cinematics,
    /// where it moves the whole scene.
    /// </summary>
    public static TimelineNames For(PlayedOn playedOn) => playedOn switch
    {
        PlayedOn.Owner => new TimelineNames(
            "No clip open",
            "A clip is how something moves over time, such as a door that swings or a light that flickers. "
            + "Make one here, or open one from Project.",
            "Make a clip",
            "Create an animation clip for the selected object.",
            "Clip length"),
        _ => new TimelineNames(
            "No cutscene open",
            "A cutscene moves things in the scene and cuts between cameras, such as an intro before a level. "
            + "Make one here, or open one from Project.",
            "New cutscene",
            "Create a cutscene for this scene.",
            "Cutscene length"),
    };

    public static string Ending(WhenDone done) => done switch
    {
        WhenDone.Hold => "Holds at the end",
        WhenDone.Loop => "Loops",
        WhenDone.Bounce => "Bounces",
        WhenDone.Release => "Lets go at the end",
        _ => throw new ArgumentOutOfRangeException(nameof(done), done, null),
    };

    public static string Holding(Hold hold) => hold switch
    {
        Hold.Step => "Step",
        Hold.Linear => "Linear",
        Hold.Smooth => "Smooth",
        Hold.Shaped => "Shaped",
        Hold.Curve => "Curve",
        _ => throw new ArgumentOutOfRangeException(nameof(hold), hold, null),
    };

    public static string Shape(Easing shape) => shape switch
    {
        Easing.Linear => "Even",
        Easing.In => "Ease in",
        Easing.Out => "Ease out",
        Easing.InOut => "Ease in and out",
        Easing.Smooth => "Smooth",
        Easing.Back => "Overshoot",
        Easing.Bounce => "Bounce",
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
    };

    /// <summary>A channel by its field, <c>power</c> of <c>light.power</c>, and by its bone first when it has one.</summary>
    public static string Channel(ChannelAddress address)
    {
        var dot = address.Property.IndexOf('.', StringComparison.Ordinal);
        var field = dot < 0 ? address.Property : address.Property[(dot + 1)..];
        return address.Bone is null ? field : $"{address.Bone} · {field}";
    }

    /// <summary>Seconds as few figures as say them: 1, 0.5, 0.25.</summary>
    public static string Seconds(double seconds)
    {
        var text = seconds.ToString("0.00", CultureInfo.InvariantCulture);
        return text.TrimEnd('0').TrimEnd('.');
    }
}

namespace Orblit.Editor.Core;

using System.Diagnostics;

/// <summary>What a drag in the coordinate view does.</summary>
public enum UvGesture
{
    /// <summary>Moves what is selected.</summary>
    Move,

    /// <summary>Scales it about its own middle.</summary>
    Scale,

    /// <summary>Turns it about its own middle.</summary>
    Turn,
}

/// <summary>The buttons that are not a drag.</summary>
public enum UvAction
{
    /// <summary>Turn the rule into coordinates, so they can be edited one at a time.</summary>
    Freeze,

    /// <summary>Back to the rule, throwing away what was drawn.</summary>
    Release,

    /// <summary>Put the whole selection in the square.</summary>
    Fit,

    /// <summary>One texture across every selected face, from their average direction.</summary>
    Planar,

    /// <summary>Each face from whichever way it points.</summary>
    Box,
}

public static class UvTools
{
    public static string Label(this UvGesture gesture) => gesture switch
    {
        UvGesture.Move => "Move",
        UvGesture.Scale => "Scale",
        UvGesture.Turn => "Turn",
        _ => throw new UnreachableException(),
    };

    public static string Label(this UvAction action) => action switch
    {
        UvAction.Freeze => "Freeze",
        UvAction.Release => "Follow shape",
        UvAction.Fit => "Fit",
        UvAction.Planar => "Project flat",
        UvAction.Box => "Project per face",
        _ => throw new UnreachableException(),
    };
}

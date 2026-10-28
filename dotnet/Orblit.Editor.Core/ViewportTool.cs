namespace Orblit.Editor.Core;

using System.Diagnostics;

/// <summary>
/// A tool that is drawn rather than clicked.
/// </summary>
/// <remarks>
/// Both of these work the same way and it is worth them looking the same: click to put down points,
/// click the first one again or press enter to finish, backspace to take one back, escape to give
/// up. What differs is what the points are for.
/// </remarks>
public enum ViewportTool
{
    /// <summary>Nothing. Clicks select as usual.</summary>
    None,

    /// <summary>An outline on a plane, pulled up into a solid.</summary>
    PolyShape,

    /// <summary>A path across a face, dividing it.</summary>
    Cut,
}

public static class ViewportTools
{
    public static string Label(this ViewportTool tool) => tool switch
    {
        ViewportTool.None => string.Empty,
        ViewportTool.PolyShape => "Draw shape",
        ViewportTool.Cut => "Cut",
        _ => throw new UnreachableException(),
    };

    /// <summary>The icon's name, drawn by the panel.</summary>
    public static string Icon(this ViewportTool tool) => tool switch
    {
        ViewportTool.None => "near_me",
        ViewportTool.PolyShape => "polyline",
        ViewportTool.Cut => "content_cut",
        _ => throw new UnreachableException(),
    };

    public static bool IsDrawing(this ViewportTool tool) => tool != ViewportTool.None;
}

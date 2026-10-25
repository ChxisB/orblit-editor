namespace Orblit.Editor.Core;

/// <summary>
/// A way of working in the editor.
/// </summary>
/// <remarks>
/// Laying out a level, shaping a model and posing a character want the same scene under different
/// hands. What a mode changes is how the panels are arranged. The scene and the selection stay as
/// they were, so switching never loses anybody's place.
/// </remarks>
public sealed record EditorMode : IRegistered
{
    /// <summary>Also what its layout is saved under, so a word that is safe in a file name.</summary>
    public required string Name { get; init; }

    public required string Label { get; init; }

    /// <summary>The name of the glyph the rail shows. The shell maps it to a drawing.</summary>
    public required string Icon { get; init; }

    /// <summary>
    /// How the panels are arranged the first time it is used. After that they are however they were
    /// left, kept for each mode separately.
    /// </summary>
    public required Func<DockLayout> Layout { get; init; }

    /// <summary>Sets up the job as the editor switches to it. Null leaves everything as it was.</summary>
    public Action? OnEnter { get; init; }

    /// <summary>Puts back what <see cref="OnEnter"/> set up, as the editor switches away.</summary>
    public Action? OnLeave { get; init; }
}

public static class EditorModes
{
    /// <summary>
    /// The modes whose panels exist in the core so far, in the order work usually goes. Terrain,
    /// cinematics and interface join them with their panels.
    /// </summary>
    public static Registry<EditorMode> Standard()
    {
        var modes = new Registry<EditorMode>();
        modes.Register(new EditorMode { Name = "scene", Label = "Scene", Icon = "open_with", Layout = DockLayout.Standard });
        modes.Register(new EditorMode { Name = "modelling", Label = "Modelling", Icon = "view_in_ar_outlined", Layout = DockLayout.Modelling });
        modes.Register(new EditorMode { Name = "animation", Label = "Animation", Icon = "animation", Layout = DockLayout.Animation });
        return modes;
    }
}

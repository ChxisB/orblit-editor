namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// What the timeline asks the rest of the editor to do for it. Each is null where nothing can do it, and
/// the timeline then leaves out the command that would ask.
/// </summary>
internal sealed record TimelineLinks
{
    /// <summary>Makes a clip in the project and opens it here.</summary>
    public required Action OnNew { get; init; }

    /// <summary>Says that a clip could not be saved, so the shell can show it.</summary>
    public Action<string>? OnProblem { get; init; }

    /// <summary>Asks which other model the shown clip should move. Null where a clip has no bones to carry, such as a cutscene.</summary>
    public Action? OnRetarget { get; init; }

    /// <summary>What is selected in the scene, which is what the clip can be played on.</summary>
    public Func<SceneObject?>? Selected { get; init; }
}

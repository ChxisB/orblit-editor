namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;

/// <summary>Which of the editor's colours a stroke is drawn in. The window maps each to its theme.</summary>
public enum GizmoTone
{
    Good,
    Warn,
    InkMid,
    AxisZ,
}

/// <summary>Lines the window strokes in a tone, over a dark pass so they read against any floor.</summary>
public sealed record GizmoStroke(WorldLines Lines, GizmoTone Tone);

/// <summary>
/// A kind of thing a scene view draws for what is selected, and what it does with the pointer.
/// </summary>
/// <remarks>
/// Keyed by what it applies to rather than listed in the view, so a body, a joint or a brush ring
/// brings its own and the view does not have to know it exists.
/// </remarks>
/// <param name="Name">Unique among the gizmos.</param>
/// <param name="AppliesTo">Whether it has anything to show for the target's object.</param>
/// <param name="Overlay">
/// What it draws, in the order the gizmos were registered: over the scene and under the view's own
/// chips.
/// </param>
/// <param name="Input">
/// What it does with the pointer. Asked after the mode's tool and anything being drawn, and before
/// the selection: a drag that starts on a handle is a handle's, and one that starts anywhere else is
/// not.
/// </param>
public sealed record GizmoType(
    string Name,
    Func<GizmoTarget, bool> AppliesTo,
    Func<GizmoTarget, IReadOnlyList<GizmoStroke>>? Overlay = null,
    Func<GizmoTarget, ViewportGesture, bool>? Input = null) : IRegistered;

namespace Orblit.Editor.Core;

using Orblit.Scene;

// Which way up the loaded scene is being edited: the base, or one of its layouts.
public sealed partial class EditorSession
{
    /// <summary>The layout the loaded scene is showing, or null while it shows the base.</summary>
    public LayoutShape? ActiveLayout => Workspace.Loaded?.Scene?.Layouts.Active;

    /// <summary>
    /// Shows a layout, or the base for null. A step on the undo stack, so undo walks back out of a
    /// layout through the edits made in it.
    /// </summary>
    public void ShowLayout(LayoutShape? shape)
    {
        if (Workspace.Loaded is not { Scene: { } scene } open || scene.Layouts.Active == shape)
        {
            return;
        }

        // At rest, so a clip posed on the timeline is not taken for the base or for an edit of the layout.
        AtRest(() =>
        {
            // Counted before the switch, since leaving is what lets them go.
            var left = scene.Layouts.Active;
            var lost = scene.Layouts.CountLost(scene);
            Run(new SwitchLayout(open.Id, left, shape));
            if (lost > 0)
            {
                Say($"{lost} {(lost == 1 ? "edit" : "edits")} made in {left} could not be kept. A layout holds how things look.", SayLevel.Warning);
            }
        });
    }

    /// <summary>What the layout on show sets on an object, for the inspector to say. Empty in the base.</summary>
    public IReadOnlyList<string> LayoutOverrides(string objectId) =>
        Workspace.Loaded?.Scene is { } scene ? scene.Layouts.Overrides(scene, objectId) : [];

    /// <summary>
    /// Takes everything the layout on show sets on an object back to how the base has it. A step on
    /// the undo stack, and none when there is nothing to take back.
    /// </summary>
    public void ResetToBase(string objectId)
    {
        if (Workspace.Loaded is not { Scene: { } scene } open || scene.Layouts.Overrides(scene, objectId).Count == 0)
        {
            return;
        }

        Run(new ResetToBase(open.Id, objectId, scene[objectId]?.Name ?? objectId));
    }

    // What a command a layout cannot hold is told, whichever way it was run.
    private static string LayoutRefusal(string what) => $"{what} cannot be made in a layout. Switch to Base to make it.";
}

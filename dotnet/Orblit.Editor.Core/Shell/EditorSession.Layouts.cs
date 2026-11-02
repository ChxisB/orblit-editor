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

        // Counted before the switch, since leaving is what lets them go.
        var left = scene.Layouts.Active;
        var lost = scene.Layouts.CountLost(scene);
        Run(new SwitchLayout(open.Id, left, shape));
        if (lost > 0)
        {
            Say($"{lost} {(lost == 1 ? "edit" : "edits")} made in {left} could not be kept. A layout holds how things look.", SayLevel.Warning);
        }
    }

    // What a command a layout cannot hold is told, whichever way it was run.
    private static string LayoutRefusal(string what) => $"{what} cannot be made in a layout. Switch to Base to make it.";
}

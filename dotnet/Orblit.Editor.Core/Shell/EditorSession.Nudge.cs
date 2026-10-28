namespace Orblit.Editor.Core;

using Orblit.VectorMath;

// Moving what is selected by a set distance, for the arrow keys.
public sealed partial class EditorSession
{
    /// <summary>Moves every selected object by a distance in the world, as one step to undo.</summary>
    /// <remarks>
    /// Sealed after, unlike a drag, whose frames fold into one step. Pressing an arrow twice is two
    /// things somebody did. The scene's own settings have no place, so they stay where they are.
    /// </remarks>
    /// <param name="by">How far, already worked out. The grid belongs to the window, so this takes metres.</param>
    public void Nudge(Vector3 by)
    {
        if (Working is not { Scene: { } scene } open)
        {
            return;
        }

        var moved = Selected.Select(id => scene[id]).OfType<SceneObject>().Where(one => one.Kind != ObjectKind.Scene).ToList();
        if (moved.Count == 0)
        {
            return;
        }

        var changes = moved.ToDictionary(
            one => one.Id,
            one => new TransformChange(one.Position.Clone(), one.Position + SceneMath.ShiftInParent(scene, one, by)));
        Run(new TransformMany(open.Id, TransformField.Position, moved.Count == 1 ? moved[0].Name : $"{moved.Count} objects", changes));
        History.Seal();
    }
}

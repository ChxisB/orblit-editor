namespace Orblit.Editor.Core;

using Orblit.Scene;
using Doc = Orblit.Scene;

/// <summary>
/// The layouts of one scene, and the one being edited.
/// </summary>
/// <remarks>
/// The editor edits one list of rows. Showing a layout puts the layout's edits over the base and
/// leaving it works out what was changed since and folds that back into the layout. So every
/// command, gizmo and panel edits a layout the way it edits the base, and none of them knows which
/// is showing.
/// <para>
/// The layout is rebuilt from what it was and not from the rows alone. A layout may hold an edit
/// the rows have no field for, such as a lens written by hand, and rows read back from the layout
/// would lose it.
/// </para>
/// </remarks>
public sealed class EditedLayouts(SceneLayouts? held = null)
{
    private Showing? _showing;

    /// <summary>The layouts as they stand, not counting the one on show until it is left.</summary>
    public SceneLayouts Held { get; private set; } = held ?? SceneLayouts.None;

    /// <summary>The layout on show, or null while the base is.</summary>
    public LayoutShape? Active => _showing?.Shape;

    /// <summary>Shows a layout, or the base for null, in place of whatever is showing.</summary>
    public void Switch(EditorScene scene, LayoutShape? to)
    {
        Leave(scene);
        if (to is { } shape)
        {
            Enter(scene, shape);
        }
    }

    /// <summary>How many edits made in the layout on show cannot be written into it.</summary>
    public int CountLost(EditorScene scene) => _showing is { } showing ? Changes(scene, showing).Lost : 0;

    /// <summary>
    /// The scene as a file holds it: the base, and every layout with the one on show brought up to
    /// date. The rows themselves are the base only while the base is showing.
    /// </summary>
    public Doc.SceneDocument Stored(EditorScene scene, string? name)
    {
        if (_showing is not { } showing)
        {
            return SceneDocument.DocumentOf(scene, name).CopyWith(layouts: Held);
        }

        return showing.Base.CopyWith(name: name, layouts: Held.With(showing.Shape, Changes(scene, showing).Layout));
    }

    private void Enter(EditorScene scene, LayoutShape shape)
    {
        var baseline = SceneDocument.DocumentOf(scene);
        var layout = baseline.CopyWith(layouts: Held).InLayout(shape);
        SceneDocument.Reconcile(scene, layout);

        // Taken after the rows are set, so it is what they say and not what the layout said.
        _showing = new Showing(shape, baseline, layout, SceneDocument.DocumentOf(scene));
    }

    private void Leave(EditorScene scene)
    {
        if (_showing is not { } showing)
        {
            return;
        }

        Held = Held.With(showing.Shape, Changes(scene, showing).Layout);
        SceneDocument.Reconcile(scene, showing.Base);
        _showing = null;
    }

    // What the rows have had done to them since the layout went on show, put over the layout as it
    // was, then stated against the base. A value put back to the base's drops out on its own.
    private static (SceneDiff Layout, int Lost) Changes(EditorScene scene, Showing showing)
    {
        var made = SceneDiff.Between(showing.Entered, SceneDocument.DocumentOf(scene)).Operations;
        var kept = made.Where(SceneLayouts.Allows).ToList();
        var edited = new SceneDiff(kept).ApplyTo(showing.Layout);
        return (SceneDiff.Between(showing.Base, edited), made.Count - kept.Count);
    }

    // The base is what the rows said before the layout went over them. The layout is that with the
    // layout's edits made. Entered is the layout read back through the rows, which is what the rows
    // hold when nothing has been touched.
    private sealed record Showing(LayoutShape Shape, Doc.SceneDocument Base, Doc.SceneDocument Layout, Doc.SceneDocument Entered);
}

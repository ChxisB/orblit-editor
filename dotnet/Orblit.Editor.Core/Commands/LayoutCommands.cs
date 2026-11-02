namespace Orblit.Editor.Core;

using Orblit.Scene;
using Doc = Orblit.Scene;

// Which way up the scene is being shown while it is edited.

/// <summary>Chooses whether the rows show the base or one of the layouts.</summary>
/// <remarks>
/// A step on the stack, so undo walks back through the edits made in a layout and then out of it. If
/// leaving a layout were not a step, undo would put a move back against a view of the scene that the
/// move was never made in.
/// </remarks>
public sealed class SwitchLayout(string sceneId, LayoutShape? from, LayoutShape? to) : EditorCommand
{
    public override string SceneId { get; } = sceneId;

    public LayoutShape? From { get; } = from;

    public LayoutShape? To { get; } = to;

    public override string Label => $"Switch to {To?.ToString() ?? "Base"}";

    // It picks the layout, so it has to run from inside one.
    public override bool FitsLayout => true;

    public override bool ChangesScene => false;

    // Showing a layout takes the rows as its base, so a posed clip would be kept in the base.
    public override bool NeedsRest => true;

    public override void Apply(ISceneHost host) => Show(host, To);

    public override void Revert(ISceneHost host) => Show(host, From);

    private void Show(ISceneHost host, LayoutShape? shape)
    {
        if (host.SceneFor(SceneId) is { } scene)
        {
            scene.Layouts.Switch(scene, shape);
        }
    }
}

/// <summary>Puts one object back to how the base has it, in the layout being edited.</summary>
/// <remarks>
/// Holds the layout before and after as documents and not as a list of fields, because a layout may
/// set something the rows have no field for, and a reset has to take that back too.
/// </remarks>
public sealed class ResetToBase(string sceneId, string objectId, string objectName) : EditorCommand
{
    private Doc.SceneDocument? _before;
    private Doc.SceneDocument? _after;

    public override string SceneId { get; } = sceneId;

    public string ObjectId { get; } = objectId;

    public override string Label => $"Reset {objectName} to base";

    public override bool FitsLayout => true;

    public override void Apply(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is not { } scene)
        {
            return;
        }

        _before ??= scene.Layouts.Edited(scene);
        _after ??= scene.Layouts.WithoutOverrides(scene, ObjectId);
        Install(scene, _after);
    }

    public override void Revert(ISceneHost host)
    {
        if (host.SceneFor(SceneId) is { } scene)
        {
            Install(scene, _before);
        }
    }

    private static void Install(EditorScene scene, Doc.SceneDocument? layout)
    {
        if (layout is not null)
        {
            scene.Layouts.Install(scene, layout);
        }
    }
}

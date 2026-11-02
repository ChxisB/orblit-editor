namespace Orblit.Editor.Core;

using Orblit.Scene;

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

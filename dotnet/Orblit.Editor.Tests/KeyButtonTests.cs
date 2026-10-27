using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The diamond after a field a clip can move: where it is, what it says and what pressing it does.</summary>
public sealed class KeyButtonTests
{
    private static List<KeyButton> Shown(Window window) =>
        [.. Headless.All<KeyButton>(window).Where(one => one.IsVisible)];

    private static (EditorSession Session, Window Window) Rigged(string id, EditorScene? scene, bool open)
    {
        var (session, window) = Selecting(id, scene);
        if (open)
        {
            session.Clips.OpenDocument("/project/clip.oclip", new ClipDocument("clip", 2));
            session.Clips.Owner = id;
            Headless.Render();
        }

        return (session, window);
    }

    [Fact]
    public Task AreNotThereWithNoClipOpen() => Headless.Run(() =>
    {
        var (_, window) = Rigged("cube", null, open: false);

        Assert.Empty(Shown(window));
    });

    [Fact]
    public Task AppearWhenAClipIsOpenedWithoutTheInspectorBeingBuiltAgain() => Headless.Run(() =>
    {
        var (session, window) = Rigged("cube", null, open: false);
        var before = Headless.All<KeyButton>(window).ToList();

        session.Clips.OpenDocument("/project/clip.oclip", new ClipDocument("clip", 2));
        session.Clips.Owner = "cube";

        Assert.Equal(3, Shown(window).Count);
        Assert.Equal(before, Headless.All<KeyButton>(window).Take(before.Count));
    });

    [Fact]
    public Task KeyATransformFieldAndShowThatItIsKeyed() => Headless.Run(() =>
    {
        var (session, window) = Rigged("cube", null, open: true);

        // Position, rotation and scale.
        Assert.Equal([KeyMark.Unkeyed, KeyMark.Unkeyed, KeyMark.Unkeyed], Shown(window).Select(one => one.Mark));

        Headless.Click(window, "key_transform_position");
        Headless.Render();

        Assert.Equal(KeyMark.Keyed, session.Clips.MarkFor(session.InspectedItem!, "transform.position"));
        Assert.Equal([KeyMark.Keyed, KeyMark.Unkeyed, KeyMark.Unkeyed], Shown(window).Select(one => one.Mark));
    });

    [Fact]
    public Task AKeyIsOneStepOnTheUndoStack() => Headless.Run(() =>
    {
        var (session, window) = Rigged("cube", null, open: true);

        Headless.Click(window, "key_transform_scale");
        Headless.Render();
        Assert.Equal(KeyMark.Keyed, Shown(window)[2].Mark);
        session.History.Undo();
        Headless.Render();

        Assert.False(session.Clips.AnyUnsaved);
        Assert.Equal([KeyMark.Unkeyed, KeyMark.Unkeyed, KeyMark.Unkeyed], Shown(window).Select(one => one.Mark));
    });

    [Fact]
    public Task ALightsPowerCanBeKeyed() => Headless.Run(() =>
    {
        var scene = new EditorScene([new SceneObject("lamp", "Lamp", ObjectKind.Light)]);
        var (session, window) = Rigged("lamp", scene, open: true);

        Press(window, "key_light_power");

        Assert.Equal(KeyMark.Keyed, session.Clips.MarkFor(session.InspectedItem!, "light.power"));
    });
}

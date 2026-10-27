using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The animation section of the inspector: which clips an object plays, and the links that change it.</summary>
public sealed class MotionSectionTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_motion_section");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private (EditorSession Session, Window Window) Showing(Doc.MotionComponent? motion = null)
    {
        Directory.CreateDirectory(PathOf("scenes"));
        var scene = EditorScene.Starter();
        if (motion is not null)
        {
            scene["cube"]!.Components[Doc.SceneComponents.Motion] = motion;
        }

        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(scene));
        foreach (var name in new[] { "a.oclip", "b.oclip" })
        {
            File.WriteAllText(PathOf(name), new ClipDocument(name, 2).Encode());
        }

        var (session, window) = Show(ProjectWorkspace.Open(_project.FullName, null).Workspace, null);
        session.Select(["cube"]);
        Headless.Render();
        return (session, window);
    }

    private static Doc.MotionComponent? MotionOfCube(EditorSession session) => EditorSession.MotionOf(session.Workspace.Loaded!.Scene!["cube"]!);

    [Fact]
    public Task AnObjectWithNoAnimationCanOnlyAttachAClipThatIsOpen() => Headless.Run(() =>
    {
        var (session, window) = Showing();

        Assert.False(Headless.Find<Button>(window, "button_attach_open_clip").IsEnabled);
        Assert.False(Has(window, "button_remove_animation"));

        session.OpenClip(PathOf("a.oclip"));
        Headless.Render();
        Assert.True(Headless.Find<Button>(window, "button_attach_open_clip").IsEnabled);

        Press(window, "button_attach_open_clip");

        Assert.Equal(["a.oclip"], MotionOfCube(session)!.Clips);
        Assert.Equal("a.oclip", MotionOfCube(session)!.Autoplay);
        session.History.Undo();
        Assert.Null(MotionOfCube(session));
    });

    [Fact]
    public Task AClipTheObjectPlaysIsOpenedFromItsSection() => Headless.Run(() =>
    {
        var (session, window) = Showing(new Doc.MotionComponent(["a.oclip", "b.oclip"], "a.oclip"));

        Press(window, "button_b_oclip");

        Assert.Equal("animation", session.Mode.Name);
        Assert.Equal(PathOf("b.oclip"), session.Clips.Shown!.Path);
    });

    [Fact]
    public Task OnStartChoosesWhichClipStartsAndNoneClearsIt() => Headless.Run(() =>
    {
        var (session, window) = Showing(new Doc.MotionComponent(["a.oclip", "b.oclip"], "a.oclip"));

        Press(window, "choice_on_start_b_oclip");
        Assert.Equal("b.oclip", MotionOfCube(session)!.Autoplay);
        Press(window, "choice_on_start_none");
        Assert.Null(MotionOfCube(session)!.Autoplay);

        session.History.Undo();
        Assert.Equal("b.oclip", MotionOfCube(session)!.Autoplay);
    });

    [Fact]
    public Task RemovingTheAnimationUnlinksItAndKeepsTheFiles() => Headless.Run(() =>
    {
        var (session, window) = Showing(new Doc.MotionComponent(["a.oclip"], "a.oclip"));

        Press(window, "button_remove_animation");

        Assert.Null(MotionOfCube(session));
        Assert.True(File.Exists(PathOf("a.oclip")));
        session.History.Undo();
        Assert.Equal(["a.oclip"], MotionOfCube(session)!.Clips);
    });

    [Fact]
    public Task TheSceneItselfHasNoAnimationToGive() => Headless.Run(() =>
    {
        var (session, window) = Showing();
        session.Select([]);
        Headless.Render();

        Assert.False(Has(window, "button_attach_open_clip"));
    });
}

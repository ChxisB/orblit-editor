namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>Play, pause and stop: the loaded scene run on a copy, with the files it names read from the project.</summary>
public sealed class EditorSessionPlaybackTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_playback");
    private readonly List<string> _outside = [];
    private readonly List<string> _besides = [];

    public void Dispose()
    {
        _project.Delete(recursive: true);
        foreach (var file in _outside)
        {
            File.Delete(file);
        }

        foreach (var folder in _besides)
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private string PathOf(string name) => TestPaths.In(_project, name);

    private static ClipDocument Bounce() => new(
        "Bounce",
        1,
        WhenDone.Hold,
        channels:
        [
            new ClipChannel<Vector3>(
                "",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
        ]);

    // A cube that plays the clip it names when the scene starts, beside the starter's camera.
    private EditorSession Open(string clip = "bounce.oclip", string ending = "")
    {
        var cube = new SceneObject(
            "cube",
            "Cube",
            ObjectKind.Mesh,
            components: new OrderedDictionary<string, Doc.SceneComponent>
            {
                [Doc.SceneComponents.Motion] = new Doc.MotionComponent([clip], clip),
            });
        var scene = new EditorScene([EditorScene.Starter()["camera"]!, cube]);
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(scene));
        var opened = ProjectWorkspace.Open(_project.FullName + ending, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static double HeightOf(EditorScene scene) => scene["cube"]!.Position.Y;

    [Fact]
    public async Task PlayRunsACopyOfTheLoadedSceneAndShowsTheGameView()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        session.OpenPanel(PanelKind.Viewport);

        await session.TogglePlayback();

        Assert.True(session.Playing);
        Assert.NotSame(session.Workspace.Loaded!.Scene, session.Playback!.Workspace.Loaded!.Scene);
        Assert.Equal("game", session.Layout.GroupOf("game")!.Current!.Id);
    }

    [Fact]
    public async Task TimePassesOnlyInTheCopyAndOnlyWhilePlaying()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        var copy = session.Playback!.Workspace.Loaded!.Scene!;

        session.AdvancePlayback(0.5);
        Assert.Equal(1, HeightOf(copy), 1e-9);
        Assert.Equal(0, HeightOf(session.Workspace.Loaded!.Scene!));

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);
        Assert.Equal(1, HeightOf(copy), 1e-9);
    }

    [Fact]
    public async Task PausingKeepsTheCopyAndPlayGoesOnFromThere()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        var running = session.Playback;
        session.AdvancePlayback(0.25);

        await session.TogglePlayback();
        Assert.False(session.Playing);
        Assert.Same(running, session.Playback);

        await session.TogglePlayback();
        session.AdvancePlayback(0.25);
        Assert.True(session.Playing);
        Assert.Same(running, session.Playback);
        Assert.Equal(1, HeightOf(running!.Workspace.Loaded!.Scene!), 1e-9);
    }

    [Fact]
    public async Task StopThrowsTheCopyAwayAndPlayStartsOverFromTheScene()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        session.AdvancePlayback(1);
        var told = 0;
        session.PlaybackChanged += () => told++;

        session.StopPlayback();
        session.StopPlayback();

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        Assert.Equal(1, told);

        await session.TogglePlayback();
        Assert.Equal(0, HeightOf(session.Playback!.Workspace.Loaded!.Scene!));
    }

    [Fact]
    public async Task PlayingStopsAnyClipOrCutsceneThatTheTimelineWasPlaying()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        session.Clips.OpenFile(PathOf("bounce.oclip"));
        session.Clips.Playing = true;
        Directory.CreateDirectory(PathOf("cutscenes"));
        var cutscene = PathOf("cutscenes/intro.ocutscene");
        File.WriteAllText(cutscene, new CutsceneDocument(new ClipDocument("intro", 1), []).Encode());
        session.Cutscenes.OpenFile(cutscene);
        session.Cutscenes.Playing = true;
        Assert.True(session.Clips.Playing);
        Assert.True(session.Cutscenes.Playing);

        await session.TogglePlayback();

        Assert.False(session.Clips.Playing);
        Assert.False(session.Cutscenes.Playing);
    }

    [Fact]
    public async Task ThereIsNothingToPlayWithoutAScene()
    {
        var empty = new Workspace(_project.FullName);
        var session = new EditorSession(empty, new History(empty), EditorModes.Standard(), PanelTypes.BuiltIn());
        var told = 0;
        session.PlaybackChanged += () => told++;

        await session.TogglePlayback();

        Assert.False(session.Playing);
        Assert.Null(session.Playback);
        Assert.Equal(0, told);
    }

    [Fact]
    public async Task LoadingAnotherSceneStopsTheOneThatWasPlaying()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();

        await session.NewSceneAsync();

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
    }

    private static double CameraX(EditorScene scene) => scene["camera"]!.Position.X;

    [Fact]
    public async Task AnEditMadeWhilePlayingShowsInTheCopyAndUndoTakesItBack()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        var copy = session.Playback!.Workspace.Loaded!.Scene!;
        var start = CameraX(copy);
        var told = 0;
        session.PlaybackChanged += () => told++;

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));
        Assert.Equal(start + 2, CameraX(copy));
        Assert.Equal(1, told);

        session.Undo();
        Assert.Equal(start, CameraX(copy));
        Assert.Equal(2, told);
    }

    [Fact]
    public async Task AnEditMadeWhilePausedShowsInTheCopyToo()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        await session.TogglePlayback();
        Assert.False(session.Playing);
        var start = CameraX(session.Playback!.Workspace.Loaded!.Scene!);

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));

        Assert.Equal(start + 2, CameraX(session.Playback!.Workspace.Loaded!.Scene!));
    }

    [Fact]
    public async Task AnObjectTheClipMovesKeepsItsPlaceWhenAnotherObjectIsEdited()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        session.AdvancePlayback(1);
        var copy = session.Playback!.Workspace.Loaded!.Scene!;

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));

        Assert.Equal(2, HeightOf(copy), 1e-9);
        Assert.Equal(0, HeightOf(session.Workspace.Loaded!.Scene!));
    }

    [Fact]
    public void WithNothingPlayingAnEditRaisesNothing()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        var told = 0;
        session.PlaybackChanged += () => told++;

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));

        Assert.Equal(0, told);
        Assert.Null(session.Playback);
    }

    [Fact]
    public async Task AfterAnotherSceneStopsThePlaybackAnEditRaisesNothing()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open();
        await session.TogglePlayback();
        await session.NewSceneAsync();
        var told = 0;
        session.PlaybackChanged += () => told++;

        session.Select(["camera"]);
        session.Nudge(new Vector3(2, 0, 0));

        Assert.Equal(0, told);
    }

    [Fact]
    public async Task AClipOpenOnTheTimelinePlaysAsItIsNowAndNotAsLastSaved()
    {
        var path = PathOf("bounce.oclip");
        File.WriteAllText(path, Bounce().Encode());
        var session = Open();
        session.Clips.OpenFile(path);
        File.Delete(path);

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);

        Assert.Equal(1, HeightOf(session.Playback!.Workspace.Loaded!.Scene!), 1e-9);
        Assert.Null(session.Last);
    }

    [Fact]
    public async Task AClipThatIsNotAClipIsSaidAndTheSceneStillPlays()
    {
        File.WriteAllText(PathOf("bounce.oclip"), "not a clip");
        var session = Open();

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);

        Assert.True(session.Playing);
        Assert.Equal(0, HeightOf(session.Playback!.Workspace.Loaded!.Scene!));
        Assert.StartsWith("bounce.oclip is not a clip", session.Last!.Value.Text);
        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
    }

    [Fact]
    public async Task AClipThatCannotBeFoundIsSaidAndTheSceneStillPlays()
    {
        var session = Open();

        await session.TogglePlayback();

        Assert.True(session.Playing);
        Assert.StartsWith("bounce.oclip could not be read", session.Last!.Value.Text);
    }

    [Fact]
    public async Task AClipOutsideTheProjectIsNotFollowed()
    {
        var name = $"orblit_outside_{Guid.NewGuid():N}.oclip";
        var outside = Path.Combine(Path.GetDirectoryName(_project.FullName)!, name);
        File.WriteAllText(outside, Bounce().Encode());
        _outside.Add(outside);
        var session = Open($"../{name}");

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);

        Assert.Equal(0, HeightOf(session.Playback!.Workspace.Loaded!.Scene!));
        Assert.Equal($"../{name} is outside the project, so it was not played.", session.Last!.Value.Text);
        Assert.Equal(SayLevel.Warning, session.Last!.Value.Level);
    }

    [Fact]
    public async Task AProjectFolderNamedWithAnEndingSeparatorStillPlaysItsClips()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        var session = Open(ending: Path.DirectorySeparatorChar.ToString());

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);

        Assert.Equal(1, HeightOf(session.Playback!.Workspace.Loaded!.Scene!), 1e-9);
        Assert.Null(session.Last);
    }

    [Fact]
    public async Task AFolderBesideTheProjectThatStartsWithItsNameIsOutsideToo()
    {
        var beside = _project.FullName + "_beside";
        Directory.CreateDirectory(beside);
        File.WriteAllText(Path.Combine(beside, "bounce.oclip"), Bounce().Encode());
        _besides.Add(beside);
        var relative = $"../{Path.GetFileName(beside)}/bounce.oclip";
        var session = Open(relative);

        await session.TogglePlayback();
        session.AdvancePlayback(0.5);

        Assert.Equal(0, HeightOf(session.Playback!.Workspace.Loaded!.Scene!));
        Assert.Equal($"{relative} is outside the project, so it was not played.", session.Last!.Value.Text);
    }

    [Fact]
    public async Task TheProjectsCutscenesAreOfferedByName()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        Directory.CreateDirectory(PathOf("cutscenes"));
        File.WriteAllText(PathOf("cutscenes/b.ocutscene"), new CutsceneDocument(new ClipDocument("second", 1), []).Encode());
        File.WriteAllText(PathOf("cutscenes/a.ocutscene"), new CutsceneDocument(new ClipDocument("first", 1), []).Encode());
        File.WriteAllText(PathOf("cutscenes/notes.txt"), "not a cutscene");
        var session = Open();

        await session.TogglePlayback();

        Assert.Equal(["first", "second"], session.Playback!.Cutscenes.Keys.Order());
    }

    [Fact]
    public async Task TwoCutscenesWithOneNameAreSaidBecauseTheGameWillNotLoadBoth()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        Directory.CreateDirectory(PathOf("cutscenes"));
        File.WriteAllText(PathOf("cutscenes/a.ocutscene"), new CutsceneDocument(new ClipDocument("intro", 1), []).Encode());
        File.WriteAllText(PathOf("cutscenes/b.ocutscene"), new CutsceneDocument(new ClipDocument("intro", 2), []).Encode());
        var session = Open();

        await session.TogglePlayback();

        Assert.Equal("Two cutscenes are called intro. The game will not load both, so rename one.", session.Last!.Value.Text);
        Assert.Equal(SayLevel.Warning, session.Last!.Value.Level);
        Assert.Equal(1, session.Playback!.Cutscenes["intro"].Motion.Duration);
    }

    [Fact]
    public async Task ACutsceneOpenOnTheBenchIsOfferedAsItIsNowAndNotAsLastSaved()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        Directory.CreateDirectory(PathOf("cutscenes"));
        var path = PathOf("cutscenes/intro.ocutscene");
        File.WriteAllText(path, new CutsceneDocument(new ClipDocument("intro", 1), []).Encode());
        var session = Open();
        session.Cutscenes.OpenFile(path);
        File.WriteAllText(path, new CutsceneDocument(new ClipDocument("saved-over", 1), []).Encode());

        await session.TogglePlayback();

        Assert.Equal(["intro"], session.Playback!.Cutscenes.Keys);
    }

    [Fact]
    public async Task ABrokenCutsceneIsSaidAndTheRestAreStillOffered()
    {
        File.WriteAllText(PathOf("bounce.oclip"), Bounce().Encode());
        Directory.CreateDirectory(PathOf("cutscenes"));
        File.WriteAllText(PathOf("cutscenes/a.ocutscene"), "not a cutscene");
        File.WriteAllText(PathOf("cutscenes/b.ocutscene"), new CutsceneDocument(new ClipDocument("fine", 1), []).Encode());
        var session = Open();

        await session.TogglePlayback();

        Assert.Equal(["fine"], session.Playback!.Cutscenes.Keys);
        Assert.StartsWith("a.ocutscene is not a cutscene", session.Last!.Value.Text);
    }
}

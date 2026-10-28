namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

/// <summary>The Cinematics mode: cutscenes made and opened, shots cut to cameras, and a camera steered.</summary>
public sealed class EditorSessionCinematicsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_cinematics");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static SceneObject Camera(EditorSession session) => session.Workspace.Loaded!.Scene!["camera"]!;

    private static List<(string, double, double)> Spans(IEnumerable<CutsceneShot> shots) =>
        [.. shots.Select(shot => (shot.Camera, shot.Start, shot.End))];

    private List<(string, double, double)> Saved(string name) =>
        Spans(CutsceneDocument.Decode(File.ReadAllText(PathOf($"cutscenes/{name}.ocutscene"))).Cutscene.Shots);

    private void WriteIntro(CutsceneDocument cutscene)
    {
        Directory.CreateDirectory(PathOf("cutscenes"));
        File.WriteAllText(PathOf("cutscenes/intro.ocutscene"), cutscene.Encode());
    }

    private static CutsceneDocument Intro(params CutsceneShot[] shots) => new(new ClipDocument("intro", 4), shots);

    private EditorSession OpenNewCutscene()
    {
        var session = Open();
        session.EnterMode("cinematics");
        session.NewCutscene();
        return session;
    }

    [Fact]
    public void CinematicsComesAfterAnimationAndHasTheShotBesideTheViewAndTheShotsFirstOnTheRight()
    {
        var modes = EditorModes.Standard();
        var layout = modes["cinematics"]!.Layout();

        Assert.Equal(["scene", "modelling", "terrain", "animation", "cinematics"], modes.All.Select(mode => mode.Name));
        Assert.Equal(
            ["outliner", "scene", "shot", "cutsceneTimeline", "shots", "inspector"],
            layout.Panels.Select(panel => panel.Id));
        Assert.Equal(
            [PanelKind.ShotPreview, PanelKind.CutsceneTimeline, PanelKind.ShotList],
            layout.Panels.Where(panel => panel.Id is "shot" or "cutsceneTimeline" or "shots").Select(panel => panel.Kind));
        Assert.False(layout.GroupOf("cutsceneTimeline")!.Collapsed);
    }

    [Fact]
    public void ALayoutWithTheCinematicsPanelsIsReadBackAsItWasWritten()
    {
        var written = DockLayout.Cinematics().ToText();

        var read = DockLayout.Read(written, PanelKind.BuiltIn);

        Assert.NotNull(read);
        Assert.Equal(written, read.ToText());
    }

    [Fact]
    public void OpeningACutsceneShowsItUnderTheViewInTheCinematicsMode()
    {
        WriteIntro(Intro(new CutsceneShot("camera", 0, 4)));
        var session = Open();

        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));

        Assert.Equal("cinematics", session.Mode.Name);
        Assert.Equal([("camera", 0d, 4d)], Spans(session.Cutscenes.Shown!.Shots!));
        Assert.True(session.Layout.Holds("cutsceneTimeline"));
        Assert.Null(session.Clips.Shown);
    }

    [Fact]
    public void AFileThatIsNotACutsceneIsSaidAndNotShown()
    {
        File.WriteAllText(PathOf("bad.ocutscene"), "not a cutscene");
        var session = Open();

        session.OpenCutscene(PathOf("bad.ocutscene"));

        Assert.Null(session.Cutscenes.Shown);
        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("bad.ocutscene is not a cutscene: ", session.Last.Value.Text);
    }

    [Fact]
    public void AFileThatIsGoneIsSaidAndNotShown()
    {
        var session = Open();

        session.OpenCutscene(PathOf("gone.ocutscene"));

        Assert.Null(session.Cutscenes.Shown);
        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("gone.ocutscene could not be read: ", session.Last.Value.Text);
    }

    [Fact]
    public void ANewCutsceneIsMadeInTheCutscenesFolderAndShown()
    {
        var session = OpenNewCutscene();

        Assert.True(File.Exists(PathOf("cutscenes/cutscene.ocutscene")));
        Assert.Equal(PathOf("cutscenes/cutscene.ocutscene"), session.Cutscenes.Shown!.Path);
        Assert.Empty(session.Cutscenes.Shown.Shots!);
    }

    [Fact]
    public void AddShotCutsToTheScenesCameraFromThePlayheadToTheEndAndSavesIt()
    {
        var session = OpenNewCutscene();

        session.AddShot();
        session.Save();

        Assert.Equal([("camera", 0d, 4d)], Saved("cutscene"));
    }

    [Fact]
    public void AddShotPrefersTheSelectedCameraToTheFirstOne()
    {
        var session = OpenNewCutscene();
        session.Add(ObjectKind.Camera);
        var chosen = session.Primary!;

        session.AddShot();

        Assert.Equal([(chosen, 0d, 4d)], Spans(session.Cutscenes.Shown!.Shots!));
    }

    [Fact]
    public void AddShotIsOneUndoStep()
    {
        var session = OpenNewCutscene();

        session.AddShot();
        session.Undo();

        Assert.Empty(session.Cutscenes.Shown!.Shots!);
    }

    [Fact]
    public void AddShotWithNoCameraSaysHowToGetOne()
    {
        var session = Open();
        session.Delete("camera");
        session.EnterMode("cinematics");
        session.NewCutscene();

        session.AddShot();

        Assert.Equal(("Add a camera to the scene first, or choose Use this view.", SayLevel.Warning), session.Last);
        Assert.Empty(session.Cutscenes.Shown!.Shots!);
    }

    [Fact]
    public void AddShotWithNothingOpenDoesNothing()
    {
        var session = Open();
        session.EnterMode("cinematics");

        session.AddShot();

        Assert.Null(session.Cutscenes.Shown);
    }

    [Fact]
    public void UseThisViewPutsACameraWhereTheViewIsAndCutsToItAsTwoSteps()
    {
        var session = OpenNewCutscene();

        session.UseThisView(new Placement(new Vector3(1, 2, 3), new Vector3(-10, 20, 0)));

        var made = session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == "Camera 2");
        Assert.Equal(ObjectKind.Camera, made.Kind);
        Assert.Equal(2, made.Position.Y);
        Assert.Equal(20, made.Rotation.Y);
        Assert.Equal(made.Id, session.Primary);
        Assert.Equal([(made.Id, 0d, 4d)], Spans(session.Cutscenes.Shown!.Shots!));

        session.Undo();
        Assert.Empty(session.Cutscenes.Shown.Shots!);
        session.Undo();
        Assert.DoesNotContain(session.Workspace.Loaded.Scene.Objects, one => one.Name == "Camera 2");
    }

    [Fact]
    public void TheNewCameraStaysWhereTheViewWasWhenTheViewMovesOn()
    {
        var session = OpenNewCutscene();
        var eye = new Placement(new Vector3(1, 2, 3), new Vector3(-10, 20, 0));

        session.UseThisView(eye);
        eye.Position.SetFrom(new Vector3(9, 9, 9));

        Assert.Equal(2, session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == "Camera 2").Position.Y);
    }

    [Fact]
    public void AddShotCutsFromThePlayhead()
    {
        var session = OpenNewCutscene();
        session.Cutscenes.At = 1;

        session.AddShot();

        Assert.Equal([("camera", 1d, 4d)], Spans(session.Cutscenes.Shown!.Shots!));
    }

    [Fact]
    public void OpeningACutsceneBringsBackTheTimelineWhenItWasClosed()
    {
        WriteIntro(Intro(new CutsceneShot("camera", 0, 4)));
        var session = Open();
        session.EnterMode("cinematics");
        session.Relayout(session.Layout.Close("cutsceneTimeline"));
        Assert.False(session.Layout.Holds("cutsceneTimeline"));

        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));

        Assert.True(session.Layout.Holds("cutsceneTimeline"));
    }

    [Fact]
    public void UseThisViewWithNothingOpenAddsNoCamera()
    {
        var session = Open();
        session.EnterMode("cinematics");

        session.UseThisView(new Placement(Vector3.Zero(), Vector3.Zero()));

        Assert.DoesNotContain(session.Workspace.Loaded!.Scene!.Objects, one => one.Name == "Camera 2");
    }

    [Fact]
    public void LookingThroughACameraLetsTheViewSteerItAndLeavingLetsGo()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene");
        Assert.Equal(("camera", "scene"), session.Piloting);

        session.Steer("scene", new Placement(new Vector3(6, 4, 7), new Vector3(-20, 35, 0)));

        Assert.Equal(7, Camera(session).Position.Z);
        Assert.Equal(-20, Camera(session).Rotation.X);

        session.EnterMode("scene");
        Assert.Null(session.Piloting);
    }

    [Fact]
    public void SteeringKeepsTheAnglesAsTheyWereTypedAndSavesWithTheScene()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene");

        session.Steer("scene", new Placement(new Vector3(6, 4, 7), Camera(session).Rotation.Clone()));
        session.Save();

        var saved = SceneDocument.Decode(File.ReadAllText(PathOf("scenes/main.oscene"))).Scene["camera"]!;
        Assert.Equal(7, saved.Position.Z, 1e-9);
        Assert.Equal(-20, saved.Rotation.X, 1e-9);
        Assert.Equal(35, saved.Rotation.Y, 1e-9);
        Assert.Equal(0, saved.Rotation.Z);
    }

    [Fact]
    public void ALongSteerIsOneStepAndTheNextStartsAnother()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene");

        session.Steer("scene", new Placement(new Vector3(6, 4, 7), new Vector3(-20, 35, 0)));
        session.Steer("scene", new Placement(new Vector3(6, 4, 6), new Vector3(-20, 35, 0)));
        session.LookThrough("camera", "scene");
        session.Steer("scene", new Placement(new Vector3(6, 4, 5), new Vector3(-20, 35, 0)));

        session.Undo();
        Assert.Equal(6, Camera(session).Position.Z);
        session.Undo();
        Assert.Equal(8, Camera(session).Position.Z);
    }

    [Fact]
    public void OnlyTheViewThatIsLookingThroughSteers()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene2");

        session.Steer("scene", new Placement(new Vector3(0, 0, 0), Vector3.Zero()));

        Assert.Equal(8, Camera(session).Position.Z);
    }

    [Fact]
    public void SteeringACameraThatIsGoneLetsGoOfIt()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene");
        session.Delete("camera");

        session.Steer("scene", new Placement(Vector3.Zero(), Vector3.Zero()));

        Assert.Null(session.Piloting);
    }

    [Fact]
    public void LookingThroughACameraThatIsNotThereLetsGo()
    {
        var session = OpenNewCutscene();
        session.LookThrough("camera", "scene");

        session.LookThrough("nothing", "scene");

        Assert.Null(session.Piloting);
    }

    [Fact]
    public void ACutsceneWithChangesCountsAsUnsavedAndSavingWritesIt()
    {
        var session = OpenNewCutscene();
        session.Save();
        Assert.False(session.AnyUnsaved);

        session.AddShot();
        Assert.True(session.AnyUnsaved);

        session.Save();
        Assert.False(session.AnyUnsaved);
    }

    [Fact]
    public void TheWindowIsToldWhenACutsceneGainsChangesAndNotAtEveryPlayheadMove()
    {
        var session = OpenNewCutscene();
        session.Save();
        var told = 0;
        session.Changed += () => told++;

        session.Cutscenes.At = 1;
        session.Cutscenes.At = 2;
        Assert.Equal(0, told);

        session.AddShot();
        Assert.Equal(1, told);
    }

    [Fact]
    public void ACutscenePosesTheSceneByItsOwnIdsInCinematicsAndNotElsewhere()
    {
        Directory.CreateDirectory(PathOf("cutscenes"));
        var lift = new ClipDocument(
            "intro",
            2,
            channels:
            [
                new ClipChannel<Vector3>(
                    "cube",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(2, new Vector3(0, 4, 0))]),
            ]);
        File.WriteAllText(PathOf("cutscenes/intro.ocutscene"), new CutsceneDocument(lift).Encode());
        var session = Open();
        var rest = session.Workspace.Loaded!.Scene!["cube"]!.Position.Y;

        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));
        session.Cutscenes.At = 1;
        Assert.Equal(2, session.Workspace.Loaded.Scene["cube"]!.Position.Y, 1e-9);

        session.EnterMode("scene");
        Assert.Equal(rest, session.Workspace.Loaded.Scene["cube"]!.Position.Y, 1e-9);
    }

    [Fact]
    public void TheInspectorKeysTheCutsceneInCinematicsAndTheClipElsewhere()
    {
        var session = Open();

        Assert.Same(session.Clips, session.Keying);

        session.EnterMode("cinematics");
        Assert.Same(session.Cutscenes, session.Keying);
    }

    [Fact]
    public void ACutsceneIsKeyedOnTheObjectChosenWithoutAnOwner()
    {
        var session = OpenNewCutscene();
        session.Select(["cube"]);

        session.Cutscenes.Key(session.Workspace.Loaded!.Scene!["cube"]!, "transform.position");

        Assert.Contains("transform.position", session.Cutscenes.Clip!.Channels.Select(channel => channel.Property));
    }
}

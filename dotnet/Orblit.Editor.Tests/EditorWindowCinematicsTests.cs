using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// A cutscene made and cut in the window, from the shelf and the shot list, and a camera steered from the
// scene view.
public sealed class EditorWindowCinematicsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_cinematics");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => Path.Combine(_project.FullName, name);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    // A cutscene with nothing in it, open in the cinematics mode.
    private (EditorSession Session, EditorWindow Window) OpenNew()
    {
        var (session, window) = Open();
        session.EnterMode("cinematics");
        Headless.Render();
        Press(window, "shelf_new_cutscene");
        Headless.Render();
        return (session, window);
    }

    private void WriteIntro(params CutsceneShot[] shots)
    {
        Directory.CreateDirectory(PathOf("cutscenes"));
        File.WriteAllText(PathOf("cutscenes/intro.ocutscene"), new CutsceneDocument(new ClipDocument("intro", 4), shots).Encode());
    }

    private List<(string, double, double)> Saved(string name) =>
        [.. CutsceneDocument.Decode(File.ReadAllText(PathOf($"cutscenes/{name}.ocutscene"))).Cutscene.Shots.Select(shot => (shot.Camera, shot.Start, shot.End))];

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static void Press(Control root, string name) =>
        Headless.Find<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    // Types into a box and lets go of it, which is when the card is built again from what the shots are.
    private static void Type(Window window, string name, string text)
    {
        var box = Headless.Find<TextBox>(window, name);
        Assert.True(box.Focus());
        box.Text = text;
        Headless.Render();
        Assert.True(Headless.Find<Button>(window, "shelf_add_shot").Focus());
        Headless.Render();
    }

    private static CutsceneShot First(EditorSession session) => session.Cutscenes.Shown!.Shots![0];

    private (EditorSession Session, EditorWindow Window) OpenIntro(params CutsceneShot[] shots)
    {
        WriteIntro(shots);
        var (session, window) = Open();
        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));
        Headless.Render();
        return (session, window);
    }

    private static SceneObject Camera(EditorSession session) => session.Workspace.Loaded!.Scene!["camera"]!;

    [Fact]
    public Task WithNothingOpenTheShotListOffersToMakeACutsceneAndNewCutsceneMakesOne() => Headless.Run(() =>
    {
        var (session, window) = Open();
        session.EnterMode("cinematics");
        Headless.Render();

        Assert.True(Has(window, "shots_none"));
        Assert.Contains("New cutscene", Assert.IsType<TextBlock>(Headless.Find<Control>(window, "shots_none")).Text);
        Assert.False(Headless.Find<Button>(window, "shelf_add_shot").IsEnabled);
        Assert.False(Headless.Find<Button>(window, "shelf_use_view").IsEnabled);
        Assert.Equal("No cutscene is open.", Headless.All<ShotPreviewPanel>(window).Single().Saying);

        Press(window, "shelf_new_cutscene");
        Headless.Render();

        Assert.True(File.Exists(PathOf("cutscenes/cutscene.ocutscene")));
        Assert.True(Has(window, "shots_empty"));
        Assert.True(Headless.Find<Button>(window, "shelf_add_shot").IsEnabled);
        Assert.True(Headless.Find<Button>(window, "shelf_use_view").IsEnabled);
        Assert.True(session.Layout.Holds("cutsceneTimeline"));
        window.Close();
    });

    [Fact]
    public Task TheShelfShowsInTheCinematicsModeAndNowhereElse() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var shelf = Headless.Find<Control>(window, "cinematics_shelf");
        Assert.False(shelf.IsVisible);

        session.EnterMode("cinematics");
        Headless.Render();
        Assert.True(shelf.IsVisible);

        session.EnterMode("scene");
        Headless.Render();
        Assert.False(shelf.IsVisible);
        window.Close();
    });

    [Fact]
    public Task ACutsceneOpenedFromTheProjectPanelShowsItsShotsAndWhatTheyLookAt() => Headless.Run(() =>
    {
        WriteIntro(new CutsceneShot("camera", 0, 4));
        var (session, window) = Open();

        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "cutscenes"));
        Headless.DoubleClick(window, Headless.All<AssetTile>(window).First(tile => tile.Asset.Name == "intro.ocutscene"));

        Assert.True(Headless.Until(() => session.Cutscenes.Shown is not null));
        Assert.Equal("cinematics", session.Mode.Name);
        Assert.True(Has(window, "section_shot_1"));
        Assert.True(session.Layout.Holds("shot"));
        Assert.Null(Headless.All<ShotPreviewPanel>(window).Single().Saying);
        window.Close();
    });

    [Fact]
    public Task AddShotCutsToTheCameraAndDeleteTakesItOutAndUndoBringsItBack() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();

        Press(window, "shelf_add_shot");
        Assert.True(Headless.Until(() => Has(window, "section_shot_1")));
        session.Save();
        Assert.Equal([("camera", 0d, 4d)], Saved("cutscene"));

        Press(window, "shot_0_delete");
        Assert.True(Headless.Until(() => !Has(window, "section_shot_1")));
        Assert.True(Has(window, "shots_empty"));

        session.Undo();
        Assert.True(Headless.Until(() => Has(window, "section_shot_1")));
        window.Close();
    });

    [Fact]
    public Task AddShotWithNoCameraSaysHowToGetOneAndMakesNoCard() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.Delete("camera");
        Headless.Render();

        Press(window, "shelf_add_shot");
        Headless.Render();

        Assert.Equal("Add a camera to the scene first, or choose Use this view.", session.Last!.Value.Text);
        Assert.False(Has(window, "section_shot_1"));
        Assert.True(Has(window, "shots_empty"));
        window.Close();
    });

    [Fact]
    public Task UseThisViewAddsACameraAndTheShotAndTwoUndosTakeThemBack() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();

        Press(window, "shelf_use_view");
        Assert.True(Headless.Until(() => Has(window, "section_shot_1")));

        var made = session.Workspace.Loaded!.Scene!.Objects.Single(one => one.Name == "Camera 2");
        Assert.Equal(made.Id, session.Primary);
        Assert.Equal(made.Id, session.Cutscenes.Shown!.Shots![0].Camera);

        session.Undo();
        Assert.True(Headless.Until(() => !Has(window, "section_shot_1")));
        session.Undo();

        Assert.DoesNotContain(session.Workspace.Loaded!.Scene!.Objects, one => one.Name == "Camera 2");
        window.Close();
    });

    [Fact]
    public Task LookThroughLetsTheSceneViewSteerTheCameraAndLeavingTheModeLetsGo() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.AddShot();
        var was = Camera(session).Position.Clone();
        Assert.True(Headless.Until(() => Has(window, "shot_0_look")));
        Assert.Equal("Look through", Headless.Find<Button>(window, "shot_0_look").Content);

        Press(window, "shot_0_look");
        Assert.True(Headless.Until(() => Equals(Headless.Find<Button>(window, "shot_0_look").Content, "Stop looking")));

        // The view was flown to the camera, so a zoom moves the camera along the way it faces.
        var state = Headless.All<SceneViewport>(window).First().State;
        state.PanZoomStart();
        state.PanZoomUpdate(1.4, Offset.Zero, false);
        state.PanZoomEnd();
        session.Save();

        var saved = SceneDocument.Decode(File.ReadAllText(PathOf("scenes/main.oscene"))).Scene["camera"]!;
        Assert.Equal(-20, saved.Rotation.X, 1e-6);
        Assert.Equal(35, saved.Rotation.Y, 1e-6);
        Assert.Equal(0, saved.Rotation.Z, 1e-6);
        Assert.True((saved.Position - was).Length > 0.1);

        session.EnterMode("scene");
        Assert.Null(session.Piloting);
        session.EnterMode("cinematics");
        Assert.True(Headless.Until(() => Equals(Headless.Find<Button>(window, "shot_0_look").Content, "Look through")));
        window.Close();
    });

    [Fact]
    public Task AStartTypedPastTheNextShotMovesTheCardAlongOnceTheBoxLetsGo() => Headless.Run(() =>
    {
        WriteIntro(new CutsceneShot("camera", 0, 2), new CutsceneShot("camera", 2, 2));
        var (session, window) = Open();
        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));
        Headless.Render();

        var box = Headless.Find<TextBox>(window, "shot_0_start");
        Assert.True(box.Focus());
        box.Text = "3";
        Headless.Render();

        // Held while it is typed in, so the caret is not taken from the box.
        Assert.Same(box, Headless.Find<TextBox>(window, "shot_0_start"));
        Assert.True(box.IsFocused);

        Assert.True(Headless.Find<Button>(window, "shelf_add_shot").Focus());
        Headless.Render();
        session.Save();

        Assert.Equal([("camera", 2d, 4d), ("camera", 3d, 5d)], Saved("intro"));
        window.Close();
    });

    [Fact]
    public Task TheCameraMenuMarksTheCameraInUseAndChoosingAnotherRecutsTheShot() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.Add(ObjectKind.Camera);
        var second = session.Primary!;
        session.AddShot();
        Assert.True(Headless.Until(() => Has(window, "shot_0_camera")));
        var picker = Headless.Find<Button>(window, "shot_0_camera");
        Assert.Equal("Camera 2", Assert.IsType<TextBlock>(picker.Content).Text);
        Assert.Equal(OrblitColors.Ink, Assert.IsType<TextBlock>(picker.Content).Foreground);

        picker.Flyout!.ShowAt(picker);
        Headless.Render();
        var items = Assert.IsType<MenuFlyout>(picker.Flyout).Items.OfType<MenuItem>().ToList();

        Assert.Equal(["    Camera", "✓ Camera 2"], items.Select(item => item.Header));
        items[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.True(Headless.Until(() => session.Cutscenes.Shown!.Shots![0].Camera == "camera"));
        Assert.NotEqual(second, session.Cutscenes.Shown!.Shots![0].Camera);
        window.Close();
    });

    [Fact]
    public Task AShotOfACameraTheSceneLacksNamesItAndCannotBeLookedThrough() => Headless.Run(() =>
    {
        WriteIntro(new CutsceneShot("gone", 0, 4));
        var (session, window) = Open();
        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));
        Headless.Render();

        var picker = Assert.IsType<TextBlock>(Headless.Find<Button>(window, "shot_0_camera").Content);
        Assert.Equal("gone", picker.Text);
        Assert.Equal(OrblitColors.Warn, picker.Foreground);
        Assert.False(Headless.Find<Button>(window, "shot_0_look").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task AStartOrLengthIsTakenOnlyWhenItIsANumberWithinTenMinutes() => Headless.Run(() =>
    {
        var (session, window) = OpenIntro(new CutsceneShot("camera", 2, 2));

        Type(window, "shot_0_start", "-1");
        Type(window, "shot_0_start", "601");
        Type(window, "shot_0_start", "soon");
        Assert.Equal(2, First(session).Start);

        Type(window, "shot_0_start", "0");
        Assert.Equal(0, First(session).Start);

        Type(window, "shot_0_length", "0");
        Type(window, "shot_0_length", "601");
        Type(window, "shot_0_length", "long");
        Assert.Equal(2, First(session).Duration);

        Type(window, "shot_0_length", "600");
        Assert.Equal(600, First(session).Duration);
        window.Close();
    });

    [Fact]
    public Task ATypedNumberThatChangesNothingMakesNoUndoStep() => Headless.Run(() =>
    {
        var (session, window) = OpenIntro(new CutsceneShot("camera", 2, 2));
        session.Save();

        Type(window, "shot_0_start", "2.0");
        Type(window, "shot_0_length", "2.0");

        Assert.False(session.AnyUnsaved);
        window.Close();
    });

    [Fact]
    public Task AKeystrokeInACardTheShotsHaveMovedOnFromDoesNothing() => Headless.Run(() =>
    {
        var (session, window) = OpenIntro(new CutsceneShot("camera", 0, 2), new CutsceneShot("camera", 2, 2));
        session.Save();
        var box = Headless.Find<TextBox>(window, "shot_0_start");
        Assert.True(box.Focus());
        box.Text = "1";
        Headless.Render();
        Assert.Equal("Move shot", session.History.UndoLabel);
        session.Undo();

        // The box is still held, so the card still shows the shot that the undo took back.
        box.Text = "1.5";
        Headless.Render();

        Assert.False(session.History.CanUndo);
        Assert.Equal(0, First(session).Start);
        window.Close();
    });

    [Fact]
    public Task TheCardsLetGoOfLookingWhenSomethingElseStopsIt() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.AddShot();
        Press(window, "shot_0_look");
        Assert.True(Headless.Until(() => Equals(Headless.Find<Button>(window, "shot_0_look").Content, "Stop looking")));

        session.LookThrough(null);

        Assert.True(Headless.Until(() => Equals(Headless.Find<Button>(window, "shot_0_look").Content, "Look through")));
        window.Close();
    });

    [Fact]
    public Task ARenamedCameraIsNamedByItsNewNameOnTheCard() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.AddShot();
        Assert.True(Headless.Until(() => Has(window, "shot_0_camera")));

        session.History.Run(new Rename(session.Workspace.Loaded!.Id, "camera", "Camera", "Lens"));

        Assert.True(Headless.Until(() => Assert.IsType<TextBlock>(Headless.Find<Button>(window, "shot_0_camera").Content).Text == "Lens"));
        window.Close();
    });

    [Fact]
    public Task ChangesElsewhereWhileLookingThroughDoNotFlyTheViewBackToTheCamera() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        session.AddShot();
        Press(window, "shot_0_look");
        Assert.True(Headless.Until(() => Equals(Headless.Find<Button>(window, "shot_0_look").Content, "Stop looking")));
        var state = Headless.All<SceneViewport>(window).First().State;
        state.PanZoomStart();
        state.PanZoomUpdate(1.4, Offset.Zero, false);
        state.PanZoomEnd();
        var was = state.Camera.Distance;

        session.SelectObject("cube");
        Headless.Render();

        Assert.Equal(was, state.Camera.Distance);
        window.Close();
    });

    [Fact]
    public Task TheInspectorKeysTheOpenCutsceneInCinematics() => Headless.Run(() =>
    {
        var (session, window) = OpenNew();
        // The inspector is a tab behind the shots, so it is not built until it is shown.
        session.Relayout(session.Layout.Show("inspector"));

        session.SelectObject("cube");
        Headless.Render();

        Assert.Contains(Headless.All<KeyButton>(window), one => one.IsVisible);
        window.Close();
    });

    [Fact]
    public Task MovingThePlayheadLeavesTheBoxBeingTypedInWhereItIs() => Headless.Run(() =>
    {
        WriteIntro(new CutsceneShot("camera", 0, 2), new CutsceneShot("camera", 2, 2));
        var (session, window) = Open();
        session.OpenCutscene(PathOf("cutscenes/intro.ocutscene"));
        Headless.Render();
        var box = Headless.Find<TextBox>(window, "shot_1_length");
        Assert.True(box.Focus());

        session.Cutscenes.At = 1;
        Headless.Render();

        Assert.Same(box, Headless.Find<TextBox>(window, "shot_1_length"));
        Assert.True(box.IsFocused);
        window.Close();
    });
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.Stage.Avalonia;

/// <summary>
/// The keys and the pointer over the game view reach the playing game, and the game's own control is
/// shown over the picture while it plays. Nothing here awaits, because a continuation back to the UI
/// thread is only reached while the test renders.
/// </summary>
[Collection(TinyGameBuild.Collection)]
public sealed class GameViewInputTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private const string Label = "game_interface_label";

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_game_input");

    public void Dispose() => _project.Delete(recursive: true);

    // A cube is added first so a selected mesh gives the G key something to do in the editor.
    private (EditorSession Session, EditorWindow Window) Open(string type)
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry(type), GameTestSupport.Copying(fixture.Build));
        session.AddShape(ShapeKind.Cube);
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private (EditorSession Session, EditorWindow Window) Play(string type)
    {
        var opened = Open(type);
        Headless.Click(opened.Window, "play_toggle");
        Assert.True(Headless.Until(() => opened.Session.Playing), "The game did not start.");
        Tick(opened.Session);
        return opened;
    }

    // What the playback clock does once a frame. Called by hand so a test knows how many frames passed.
    private static void Tick(EditorSession session)
    {
        session.AdvancePlayback(0.016);
        Headless.Render();
    }

    private static Border Picture(EditorWindow window) => Headless.Find<Border>(window, "game_view_picture");

    private static double CrateX(EditorSession session) => Crate(session).X;

    private static Orblit.VectorMath.Vector3 Crate(EditorSession session) =>
        session.Playback!.Workspace.Loaded!.Scene!["crate"]!.Position;

    private static void Press(EditorWindow window, PhysicalKey key) => window.KeyPressQwerty(key, RawInputModifiers.None);

    private static void Release(EditorWindow window, PhysicalKey key) => window.KeyReleaseQwerty(key, RawInputModifiers.None);

    [Fact]
    public Task PlayGivesTheKeysToTheGameView() => Headless.Run(() =>
    {
        var (_, window) = Play("TinyGame.KeyReader");

        Assert.True(Picture(window).IsFocused);
        window.Close();
    });

    [Fact]
    public Task AKeyHeldInTheViewReachesTheGameAsAControl() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.KeyReader");
        Assert.Equal(0, CrateX(session));

        Press(window, PhysicalKey.G);
        Tick(session);
        Assert.Equal(1, CrateX(session));

        Release(window, PhysicalKey.G);
        Tick(session);
        Assert.Equal(0, CrateX(session));
        window.Close();
    });

    [Fact]
    public Task AToolKeyPressedWhileAGamePlaysIsTheGamesAndNotTheEditors() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.KeyReader");
        var context = session.Context;
        var objects = session.Workspace.Loaded!.Scene!.Objects.Count;

        Press(window, PhysicalKey.G);
        Release(window, PhysicalKey.G);
        Press(window, PhysicalKey.Backspace);
        Release(window, PhysicalKey.Backspace);

        Assert.Equal(context, session.Context);
        Assert.Equal(objects, session.Workspace.Loaded!.Scene!.Objects.Count);
        window.Close();
    });

    // The palette is the way to Stop and everything else without the mouse, so a game must not eat it.
    [Fact]
    public Task AKeyWithTheCommandKeyHeldIsTheEditorsWhileAGamePlays() => Headless.Run(() =>
    {
        var (_, window) = Play("TinyGame.KeyReader");

        window.KeyPressQwerty(PhysicalKey.K, RawInputModifiers.Meta);
        Headless.Render();

        var palette = Assert.IsType<PaletteDialog>(Assert.Single(window.OwnedWindows));
        palette.Close();
        window.Close();
    });

    // Tab is how a keyboard user leaves the view. The game still sees the press.
    [Fact]
    public Task TabLeavesTheViewWhileAGamePlays() => Headless.Run(() =>
    {
        var (_, window) = Play("TinyGame.KeyReader");
        Assert.True(Picture(window).IsFocused);

        Press(window, PhysicalKey.Tab);
        Release(window, PhysicalKey.Tab);
        Headless.Render();

        Assert.False(Picture(window).IsFocused);
        window.Close();
    });

    [Fact]
    public Task PausedTheSameKeyIsTheEditorsAgain() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.KeyReader");
        Headless.Click(window, "play_toggle");
        Assert.False(session.Playing);
        // The click on the button may have moved the focus. The key goes to the view, as it did while playing.
        Picture(window).Focus();
        Assert.True(Picture(window).IsFocused);
        var context = session.Context;

        Press(window, PhysicalKey.G);
        Release(window, PhysicalKey.G);

        Assert.NotEqual(context, session.Context);
        window.Close();
    });

    [Fact]
    public Task AClickIsOneUpdateWithARayThroughThePointNamed() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.PointerReader");
        var picture = Picture(window);
        var camera = SceneCameras.GameCamera(session.Playback!.Workspace.Loaded!.Scene!)!;

        var left = Click(window, picture, 0.25);
        for (var frame = 0; frame < 3; frame++)
        {
            Tick(session);
        }

        var seenLeft = Crate(session);
        var expectedLeft = PointerRay.FromCamera(camera, left, picture.Bounds.Size).Direction;
        Assert.Equal(1, seenLeft.Y);
        Assert.Equal(expectedLeft.X, seenLeft.X, 6);
        Assert.Equal(expectedLeft.Z, seenLeft.Z, 6);

        var right = Click(window, picture, 0.75);
        Tick(session);
        Tick(session);

        var seenRight = Crate(session);
        var expectedRight = PointerRay.FromCamera(camera, right, picture.Bounds.Size).Direction;
        Assert.Equal(2, seenRight.Y);
        Assert.Equal(expectedRight.X, seenRight.X, 6);
        Assert.NotEqual(seenLeft.X, seenRight.X);
        window.Close();
    });

    [Fact]
    public Task AClickMadeBeforePlayIsNotCarriedIntoTheGame() => Headless.Run(() =>
    {
        var (session, window) = Open("TinyGame.PointerReader");
        session.OpenPanel(PanelKind.Game);
        Headless.Render();
        Click(window, Picture(window), 0.5);
        Assert.True(session.InputFeed!.Read().Pointer is { Pressed: true }, "The click was not seen before Play.");

        // Not the button: moving to it takes the pointer off the picture, which would hide the click on its own.
        _ = session.TogglePlayback();
        Assert.True(Headless.Until(() => session.Playing), "The game did not start.");
        Tick(session);
        Tick(session);

        Assert.Equal(0, Crate(session).Y);
        window.Close();
    });

    // A click in the picture, given as how far along its width. Returns where it landed in the picture's own coordinates.
    private static Point Click(EditorWindow window, Border picture, double along)
    {
        var inside = new Point(picture.Bounds.Width * along, picture.Bounds.Height / 2);
        var at = picture.TranslatePoint(inside, window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        return inside;
    }

    [Fact]
    public Task AnInterfaceGamesControlIsOverThePictureWhileItPlaysAndGoneAtStop() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced");

        var label = Headless.Find<TextBlock>(window, Label);
        Assert.Equal("game_view_interface", ((Control)label.Parent!).Name);
        Assert.NotNull(label.DataContext);

        Headless.Click(window, "play_stop");
        Headless.Render();

        Assert.Null(session.Playback);
        Assert.DoesNotContain(Headless.All<TextBlock>(window), one => one.Name == Label);
        Assert.Null(label.DataContext);
        window.Close();
    });

    [Fact]
    public Task TheGameIsAskedForItsControlOnceHoweverLongItPlays() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced");

        for (var frame = 0; frame < 5; frame++)
        {
            Tick(session);
        }

        var game = session.PlayingGame!;
        Assert.Equal(1, game.GetType().GetProperty("Made")!.GetValue(game));
        window.Close();
    });

    [Fact]
    public Task AClosedAndReopenedGameViewShowsTheSameControl() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced");
        var label = Headless.Find<TextBlock>(window, Label);

        session.Relayout(session.Layout.Close(session.Panels.TypeOf(PanelKind.Game)!.Panel.Id));
        Headless.Render();
        Assert.DoesNotContain(Headless.All<TextBlock>(window), one => one.Name == Label);

        session.OpenPanel(PanelKind.Game);
        Tick(session);

        Assert.Same(label, Headless.Find<TextBlock>(window, Label));
        window.Close();
    });

    [Fact]
    public Task ClosingTheWindowOfAnInterfaceGameLetsItsAssemblyUnload() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced");
        Assert.True(GameTestSupport.AnyGameContext());

        window.Close();

        Assert.Null(session.Playback);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
    });

    [Fact]
    public Task StoppingAnInterfaceGameLetsItsAssemblyUnload() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced");

        Headless.Click(window, "play_stop");
        Headless.Render();

        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
        window.Close();
    });

    [Fact]
    public Task AnInterfaceMadeFromXamlLoadsWithTheStyleOfItsOwnAssembly() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.XamlFaced");

        var label = Headless.Find<TextBlock>(window, "game_xaml_label");

        Assert.Equal("From XAML", label.Text);
        // 23 is set by Palette.axaml, which Faces.axaml includes by an avares address.
        Assert.Equal(23, label.FontSize);
        window.Close();
    });

    [Fact]
    public Task StoppingAnInterfaceMadeFromXamlLetsItsAssemblyUnload() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.XamlFaced");

        Headless.Click(window, "play_stop");
        Headless.Render();

        Assert.Null(session.Playback);
        Assert.True(GameLoaderTests.Until(() => !GameTestSupport.AnyGameContext()));
        window.Close();
    });

    [Fact]
    public Task AControlThatCannotBeMadeStopsPlayAndSaysWhy() => Headless.Run(() =>
    {
        var (session, window) = Open("TinyGame.BreaksMakingItsInterface");

        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => session.Last is { Level: SayLevel.Error }), "Play did not say it stopped.");

        Assert.Null(session.Playback);
        Assert.False(session.Playing);
        Assert.Contains("the label is missing", session.Log.Entries[^1].Detail);
        window.Close();
    });
}

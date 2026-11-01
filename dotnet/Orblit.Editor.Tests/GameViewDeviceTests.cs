using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The device bar in the game view: the shapes it offers, the frame it draws and what it keeps.</summary>
public sealed class GameViewDeviceTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_game_device");

    public void Dispose() => _project.Delete(recursive: true);

    private string SceneFile => Path.Combine(_project.FullName, "scenes", "main.oscene");

    private EditorSession NewSession()
    {
        var opened = ProjectWorkspace.Open(_project.FullName);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private (EditorSession Session, EditorWindow Window) Open()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SceneFile)!);
        if (!File.Exists(SceneFile))
        {
            File.WriteAllText(SceneFile, SceneDocument.Encode(EditorScene.Starter()));
        }

        var session = NewSession();
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        session.OpenPanel(PanelKind.Named("game")!);
        Headless.Render();
        return (session, window);
    }

    private static DeviceFrameHost Frame(Window window) => Headless.All<DeviceFrameHost>(window).Single();

    private static ToggleButton Toggle(Window window, string name) => Headless.Find<ToggleButton>(window, name);

    private static string Words(Window window, string name) => Headless.Find<TextBlock>(window, name).Text!;

    private static void Press(Window window, string name)
    {
        Headless.Click(window, name);
        Headless.Render();
    }

    private static double Shape(Window window)
    {
        var size = Frame(window).ScreenSize;
        return size.Width / size.Height;
    }

    [Fact]
    public Task ItStartsOnTheDesktopWithNoWayToTurnIt() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.True(Toggle(window, "game_view_device_desktop").IsChecked);
        Assert.False(Toggle(window, "game_view_device_mobile").IsChecked);
        Assert.False(Headless.Find<Button>(window, "game_view_turn").IsEnabled);
        Assert.False(Toggle(window, "game_view_hold_portrait").IsEnabled);
        Assert.True(Toggle(window, "game_view_hold_landscape").IsChecked);
        Assert.Equal("1920 × 1080", Words(window, "game_view_size"));
        window.Close();
    });

    [Fact]
    public Task APhoneIsDrawnAtThePhonesShape() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Press(window, "game_view_device_mobile");

        Assert.Equal(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait), session.Device.Frame);
        Assert.Equal(390.0 / 844, Shape(window), precision: 2);
        Assert.Equal("390 × 844", Words(window, "game_view_size"));
        Assert.True(Toggle(window, "game_view_device_mobile").IsChecked);
        Assert.True(Toggle(window, "game_view_hold_portrait").IsChecked);
        Assert.True(Headless.Find<Button>(window, "game_view_turn").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task ATurnDrawsThePhoneOnItsSide() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "game_view_device_mobile");

        Press(window, "game_view_turn");

        Assert.Equal(844.0 / 390, Shape(window), precision: 2);
        Assert.Equal("844 × 390", Words(window, "game_view_size"));
        Assert.True(Toggle(window, "game_view_hold_landscape").IsChecked);
        Assert.False(Toggle(window, "game_view_hold_portrait").IsChecked);
        window.Close();
    });

    [Fact]
    public Task PortraitAndLandscapeButtonsHoldThePhoneThatWay() => Headless.Run(() =>
    {
        var (session, window) = Open();
        Press(window, "game_view_device_tablet");

        Press(window, "game_view_hold_landscape");
        Assert.Equal(FrameOrientation.Landscape, session.Device.Frame.Orientation);

        Press(window, "game_view_hold_portrait");
        Assert.Equal(FrameOrientation.Portrait, session.Device.Frame.Orientation);
        window.Close();
    });

    [Fact]
    public Task PressingTheDeviceAlreadyShownLeavesItLit() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "game_view_device_tablet");

        Press(window, "game_view_device_tablet");

        Assert.True(Toggle(window, "game_view_device_tablet").IsChecked);
        window.Close();
    });

    [Theory]
    [InlineData(900, 600)]
    [InlineData(1440, 900)]
    [InlineData(1900, 1200)]
    public Task TheFrameKeepsItsShapeInAnyWindow(int width, int height) => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "game_view_device_mobile");

        window.Width = width;
        window.Height = height;
        Headless.Render();

        var host = Frame(window);
        Assert.Equal(390.0 / 844, Shape(window), precision: 2);
        Assert.True(host.ScreenSize.Width <= host.Bounds.Width);
        Assert.True(host.ScreenSize.Height <= host.Bounds.Height);
        window.Close();
    });

    [Fact]
    public Task ThePictureIsNeverDrawnLargerThanTheDevice() => Headless.Run(() =>
    {
        var (_, window) = Open();
        window.Width = 3000;
        window.Height = 2400;
        Headless.Render();

        Assert.InRange(Frame(window).Scale, 0.01, 1.0);
        window.Close();
    });

    [Fact]
    public Task TheZoomReadoutSaysHowMuchOfTheDeviceShows() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "game_view_device_mobile");

        var percent = Math.Round(Frame(window).Scale * 100).ToString(CultureInfo.InvariantCulture);

        Assert.Equal($"{percent}%", Words(window, "game_view_zoom"));
        window.Close();
    });

    [Fact]
    public Task TheChoiceComesBackWhenTheProjectIsOpenedAgain() => Headless.Run(() =>
    {
        var (_, window) = Open();
        Press(window, "game_view_device_tablet");
        Press(window, "game_view_turn");
        window.Close();

        var (session, again) = Open();

        Assert.Equal(new DeviceFrame(DeviceKind.Tablet, FrameOrientation.Landscape), session.Device.Frame);
        Assert.True(Toggle(again, "game_view_device_tablet").IsChecked);
        Assert.Equal("1112 × 834", Words(again, "game_view_size"));
        again.Close();
    });

    [Fact]
    public Task AChoiceNeverTouchesTheSceneFile() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var before = File.ReadAllBytes(SceneFile);

        Press(window, "game_view_device_mobile");
        Press(window, "game_view_turn");
        Press(window, "game_view_device_desktop");

        Assert.Equal(before, File.ReadAllBytes(SceneFile));
        Assert.False(session.AnyUnsaved);
        window.Close();
    });

    [Fact]
    public Task AChoiceWritesNothingToTheSceneDirectory() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Press(window, "game_view_device_mobile");

        Assert.Equal([SceneFile], Directory.GetFiles(Path.GetDirectoryName(SceneFile)!));
        Assert.True(File.Exists(Path.Combine(_project.FullName, ".orblit", "device.json")));
        window.Close();
    });

    [Fact]
    public void APortraitPhoneShadesItsNotchAndItsHomeBar()
    {
        var bands = UnsafeEdgesOverlay.Bands(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Portrait), new Size(195, 422));

        Assert.Equal([new Rect(0, 0, 195, 23.5), new Rect(0, 405, 195, 17)], bands);
    }

    [Fact]
    public void ALandscapePhoneShadesItsSidesBetweenTheTopAndTheBottomSoNoCornerIsShadedTwice()
    {
        var bands = UnsafeEdgesOverlay.Bands(new DeviceFrame(DeviceKind.Mobile, FrameOrientation.Landscape), new Size(422, 195));

        Assert.Equal(
            [new Rect(0, 184.5, 422, 10.5), new Rect(0, 0, 23.5, 184.5), new Rect(398.5, 0, 23.5, 184.5)],
            bands);
    }

    [Fact]
    public void ADesktopShadesNothing()
    {
        Assert.Empty(UnsafeEdgesOverlay.Bands(DeviceFrame.Desktop, new Size(960, 540)));
    }
}

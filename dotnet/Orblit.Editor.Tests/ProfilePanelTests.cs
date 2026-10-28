using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Profile;

// Dart: orblit-editor/test/profile_panel_test.dart. What the panel says about a profiler, with a clock
// the test winds by hand. The case that hands a shown panel another profiler is not ported, since the
// window builds the panel once over its own.
public sealed class ProfilePanelTests
{
    private long _micros;
    private long _bytes = 100 * 1048576;
    private readonly Profiler _profiler;

    public ProfilePanelTests() => _profiler = Profiler.Create(heap: () => _bytes, clock: () => _micros);

    // A 20 ms frame in which update took 4 ms.
    private void DrawFrame()
    {
        _micros += 16000;
        var started = _profiler.Begin();
        _micros += 4000;
        _profiler.End("update", started);
        _profiler.Frame();
        _bytes += 1048576;
    }

    private static Window Show(Profiler shown)
    {
        var window = new Window { Width = 400, Height = 600, Content = new ProfilePanel(shown) };
        window.Show();
        Headless.Render();
        return window;
    }

    private static List<string> Words(Control root) => Headless.All<TextBlock>(root).Select(block => block.Text ?? "").ToList();

    private static int Containing(Control root, string text) => Words(root).Count(word => word.Contains(text, StringComparison.Ordinal));

    [Fact]
    public Task ReadsFramesSpansGaugesCountsAndMemory() => Headless.Run(() =>
    {
        _profiler.Frame();
        for (var i = 0; i < 5; i++)
        {
            DrawFrame();
        }

        _profiler.Gauge("objects", 12);
        _profiler.Count("meshes", 3);

        var window = Show(_profiler);

        Assert.Equal(1, Containing(window, "50 fps · mean 20.0 ms"));
        Assert.Equal(1, Containing(window, "mean 4.0 ms · max 4.0 ms"));
        Assert.Contains("objects", Words(window));
        Assert.Contains("12", Words(window));
        Assert.Contains("meshes", Words(window));
        Assert.Contains("3", Words(window));
        Assert.Equal(1, Containing(window, "MB"));
    });

    [Fact]
    public Task ReadsAgainAfterTheInterval() => Headless.Run(() =>
    {
        _profiler.Frame();
        var window = Show(_profiler);
        Assert.Contains("none yet", Words(window));

        DrawFrame();

        Assert.True(Headless.Until(() => Containing(window, "50 fps") == 1));
        Assert.DoesNotContain("none yet", Words(window));
    });

    [Fact]
    public Task SaysAGrowingHeapGrowsInMegabytesAMinute() => Headless.Run(() =>
    {
        _profiler.Frame();
        for (var i = 0; i < 4; i++)
        {
            DrawFrame();
            _micros += 1000000;
            _profiler.Frame();
            _bytes += 1048576;
        }

        var window = Show(_profiler);

        Assert.Equal(1, Containing(window, "MB/min"));
        Assert.Equal(1, Containing(window, "+"));
    });

    [Fact]
    public Task SaysSoWhenProfilingIsOff() => Headless.Run(() =>
    {
        var window = Show(Profiler.Off);

        Assert.Equal(["Profiling is off."], Words(window));
    });

    // Added in C#. A closed panel that kept reading would cost what the panel is there to measure.
    [Fact]
    public Task StopsReadingOnceClosed() => Headless.Run(() =>
    {
        _profiler.Frame();
        var window = Show(_profiler);
        var panel = Assert.IsType<ProfilePanel>(window.Content);
        window.Content = null;
        Headless.Render();

        DrawFrame();
        Headless.Wait(700);

        Assert.Contains("none yet", Words(panel));
    });

    // Added in C#. The window's panel reads the frames its scene view draws.
    [Fact]
    public Task TheWindowsPanelTimesTheFramesTheSceneViewDraws() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();
        rig.Session.OpenPanel(PanelKind.Profile);
        Headless.Render();
        var panel = Headless.All<ProfilePanel>(rig.Window).Single();

        for (var i = 0; i < 3; i++)
        {
            rig.Window.MouseWheel(rig.Centre, new Vector(0, 1));
            Headless.Render();
        }

        Assert.True(Headless.Until(() => Containing(panel, " fps · ") == 1));
    });
}

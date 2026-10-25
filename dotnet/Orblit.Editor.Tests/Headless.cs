using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace Orblit.Editor.Tests;

/// <summary>
/// One headless Avalonia for every test in a process. The engine keeps the same two files in
/// <c>Orblit.HeadlessSupport</c>, which is not a package yet, so this is a second copy (F1).
/// </summary>
public static class Headless
{
    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(TestApp));

    /// <summary>
    /// Runs <paramref name="test"/> on the UI thread and finishes when it does. The session's own
    /// dispatch returns before an async body is done and drops what it throws, which would make
    /// every failed assertion after an await pass.
    /// </summary>
    public static Task Run(Func<Task> test)
    {
        var done = new TaskCompletionSource();
        _ = Session.Dispatch(
            async () =>
            {
                try
                {
                    await test();
                    done.SetResult();
                }
#pragma warning disable CA1031 // The failure is handed to the test that awaits it.
                catch (Exception error)
#pragma warning restore CA1031
                {
                    done.SetException(error);
                }
            },
            CancellationToken.None);
        return done.Task;
    }

    /// <summary>Runs <paramref name="test"/> on the UI thread, for a test with nothing to await.</summary>
    public static Task Run(Action test) =>
        Run(() =>
        {
            test();
            return Task.CompletedTask;
        });

    /// <summary>Lets the window draw what has been invalidated since it last did.</summary>
    public static void Render()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}

/// <summary>
/// An application with a theme, so that a window shows its content, and no screen to draw on. It
/// draws with Skia, because the stub that headless drawing gives a bitmap forgets what was copied in.
/// </summary>
public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

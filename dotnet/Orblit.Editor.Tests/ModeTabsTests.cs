using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The five workspaces as a row of tabs in the title bar, centred on the window.
public sealed class ModeTabsTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open()
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private static Button Tab(EditorWindow window, string mode) => Headless.Find<Button>(window, $"mode_{mode}");

    private static Border Shape(EditorWindow window, string mode) => Headless.Find<Border>(window, $"modeshape_{mode}");

    [Fact]
    public Task ClickingATabSwitchesTheMode() => Headless.Run(() =>
    {
        var (session, window) = Open();

        Tab(window, "modelling").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("modelling", session.Mode.Name);
        window.Close();
    });

    [Fact]
    public Task TheTabsFollowTheSessionMode() => Headless.Run(() =>
    {
        var (session, window) = Open();

        foreach (var mode in session.Modes.All)
        {
            session.EnterMode(mode);

            foreach (var other in session.Modes.All)
            {
                var on = other.Name == mode.Name;
                Assert.Same(on ? OrblitColors.Surface : Brushes.Transparent, Shape(window, other.Name).Background);
                Assert.Same(on ? OrblitColors.Ink : OrblitColors.InkDim, Tab(window, other.Name).Foreground);
                Assert.Equal(on ? "Selected" : string.Empty, AutomationProperties.GetItemStatus(Tab(window, other.Name)));
            }
        }

        window.Close();
    });

    [Fact]
    public Task TheRowIsReachableByKeyboard() => Headless.Run(() =>
    {
        var (session, window) = Open();
        var names = session.Modes.All.Select(mode => mode.Name).ToList();

        Assert.All(names, name =>
        {
            Assert.True(Tab(window, name).Focusable);
            Assert.True(Tab(window, name).IsTabStop);
            Assert.Equal(session.Modes.All.First(mode => mode.Name == name).Label, AutomationProperties.GetName(Tab(window, name)));
        });

        // An arrow on the window nudges what is selected, so a nudge is how to tell the window saw one.
        session.SelectObject("ground");
        var start = session.Workspace.Loaded!.Scene!["ground"]!.Position.Clone();

        Tab(window, names[0]).Focus();
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(Tab(window, names[1]).IsFocused);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.True(Tab(window, names[0]).IsFocused);

        // The first tab has nothing on its left, so the key leaves focus where it is.
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.True(Tab(window, names[0]).IsFocused);
        Assert.Equal(start.X, session.Workspace.Loaded!.Scene!["ground"]!.Position.X);
        window.Close();
    });

    [Fact]
    public Task TheTabsAreCentredOnTheWindow() => Headless.Run(() =>
    {
        var (_, window) = Open();
        var row = Headless.Find<Control>(window, "mode_tabs");

        var left = row.TranslatePoint(default, window)!.Value.X;

        Assert.Equal(window.Bounds.Width / 2, left + (row.Bounds.Width / 2), 1);
        window.Close();
    });

    [Fact]
    public Task TheRailIsGoneAndThePanelsStartAtTheEdge() => Headless.Run(() =>
    {
        var (_, window) = Open();

        Assert.DoesNotContain(window.GetVisualDescendants().OfType<Control>(), one => one.Name == "rail");
        var first = window.GetVisualDescendants().OfType<DockHost>().Single();
        Assert.True(first.TranslatePoint(default, window)!.Value.X < 8);
        window.Close();
    });
}

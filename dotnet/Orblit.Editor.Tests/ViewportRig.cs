using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// An editor window over a throwaway project, with its one scene view found. The window draws no
/// frame from the renderer, so the view keeps its plain grid.
/// </summary>
internal sealed class ViewportRig : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public ViewportRig(double width = 1440, double height = 900)
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        Session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        Window = new EditorWindow(Session, HostPlatform.MacOS) { Width = width, Height = height };
        Window.Show();
        Headless.Render();
        View = Window.GetLogicalDescendants().OfType<SceneViewport>().First();
    }

    public EditorSession Session { get; }

    public EditorWindow Window { get; }

    public SceneViewport View { get; }

    /// <summary>The middle of the view, in the window's own coordinates.</summary>
    public Point Centre => Where(new Point(View.Bounds.Width / 2, View.Bounds.Height / 2));

    /// <summary>Where the camera stands.</summary>
    public Vector3 Eye => View.State.Camera.ToRenderCamera().Position;

    public Point Where(Point inView) => View.TranslatePoint(inView, Window)!.Value;

    public Point Where(Control inside) => inside.TranslatePoint(new Point(inside.Bounds.Width / 2, inside.Bounds.Height / 2), Window)!.Value;

    public void Dispose()
    {
        Window.Close();
        _temp.Delete(recursive: true);
    }

    /// <summary>Holds the right button down over the view.</summary>
    public void Look() => Window.MouseDown(Centre, MouseButton.Right);

    public void LetGo() => Window.MouseUp(Centre, MouseButton.Right);

    /// <summary>A click in the view, which makes it the one the keyboard is talking to.</summary>
    public void Click(Point at)
    {
        Window.MouseDown(at, MouseButton.Left);
        Window.MouseUp(at, MouseButton.Left);
    }

    public void Press(PhysicalKey key) => Window.KeyPressQwerty(key, RawInputModifiers.None);

    public void Release(PhysicalKey key) => Window.KeyReleaseQwerty(key, RawInputModifiers.None);

    public void Tap(PhysicalKey key)
    {
        Press(key);
        Release(key);
    }

    /// <summary>The words on the view once it has drawn what changed.</summary>
    public IReadOnlyList<string> Words()
    {
        Headless.Render();
        return View.GetLogicalDescendants().OfType<TextBlock>().Select(one => one.Text ?? "").ToList();
    }

    public bool Says(string part) => Words().Any(one => one.Contains(part, StringComparison.Ordinal));

    public TextBlock Chip(string text)
    {
        Headless.Render();
        return View.GetLogicalDescendants().OfType<TextBlock>().First(one => one.Text == text);
    }
}

using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;
using Orblit.VectorMath;

/// <summary>A scene with one cube in it, being edited part by part, and the coordinates of its faces.</summary>
internal sealed class UvRig : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_uv");

    public UvRig()
    {
        var workspace = new Workspace(_project.FullName);
        var path = Path.Combine(_project.FullName, "scenes", "main.oscene");
        workspace.Add(new SceneEntry("a", "A", path, EditorScene.Starter(), neverWritten: true));
        Session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        Session.AddShape(ShapeKind.Cube);
        Session.SetContext(EditContext.Element);
    }

    public EditorSession Session { get; }

    public Mesh Mesh => Session.Workspace["a"]!.Scene!.Objects.Single(one => one.Kind == ObjectKind.Shape).CurrentMesh!;

    public void Dispose() => _project.Delete(recursive: true);

    public void Select(params int[] faces) => Session.PickElements(new ElementSelection(faces: [.. faces]), false);

    public List<Vector2> Corners(int face) => Mesh.UvsOf(Mesh.Faces[face]);

    /// <summary>A panel in a window, drawn once.</summary>
    public Window Show()
    {
        var window = new Window { Width = 360, Height = 900, Content = new UvPanel(Session) };
        window.Show();
        Headless.Render();
        return window;
    }
}

using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;

/// <summary>A scene with nothing in it, or one cube, and the modelling panel over it.</summary>
internal sealed class ModellingRig : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_modelling");

    public ModellingRig(bool cube = false)
    {
        var workspace = new Workspace(_project.FullName);
        var path = Path.Combine(_project.FullName, "scenes", "main.oscene");
        workspace.Add(new SceneEntry("a", "A", path, EditorScene.Starter(), neverWritten: true));
        Session = new EditorSession(workspace, new History(workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        if (cube)
        {
            Session.AddShape(ShapeKind.Cube);
        }
    }

    public EditorSession Session { get; }

    public Snapping Snapping { get; } = new();

    public Mesh Mesh => Session.ChosenShape!.CurrentMesh!;

    public void Dispose() => _project.Delete(recursive: true);

    public void Edit(params int[] faces)
    {
        Session.SetContext(EditContext.Element);
        Session.PickElements(new ElementSelection(faces: [.. faces]), false);
    }

    /// <summary>A panel in a window, drawn once.</summary>
    public Window Show()
    {
        var window = new Window { Width = 360, Height = 1400, Content = new ModellingPanel(Session, Snapping) };
        window.Show();
        Headless.Render();
        return window;
    }
}

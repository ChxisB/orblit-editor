namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Ui;

// Interfaces in their workspace: opened, made, put on the scene, and saved with everything else.
public sealed partial class EditorSession
{
    // Each file read once, since the scene view asks on every frame it draws. An interface that is
    // being changed is open on the bench, and the bench's copy is the one shown.
    private readonly Dictionary<string, UiDocument?> _sceneInterfaces = [];
    private bool _interfacesUnsaved;

    /// <summary>The interfaces open for laying out, and how the one shown is looked at.</summary>
    public InterfaceBench Interfaces { get; }

    /// <summary>
    /// Where the interface the loaded scene puts on screen is, relative to the project. Null when it
    /// puts none there.
    /// </summary>
    /// <remarks>
    /// The first visible canvas object, since a screen shows one interface at a time. Two canvases
    /// both visible is a scene saying two things, and the first is at least the one nearest the top
    /// of the tree.
    /// </remarks>
    public string? SceneInterfaceAsset => Workspace.Loaded?.Scene is { } scene
        ? scene.Objects.FirstOrDefault(item =>
            item.Kind == ObjectKind.Canvas && item.Visible && scene.IsShown(item.Id) && item.InterfaceAsset is not null)?.InterfaceAsset
        : null;

    /// <summary>
    /// The interface the loaded scene puts on screen. Null when it puts none there, or its file cannot
    /// be read.
    /// </summary>
    /// <remarks>
    /// The copy being laid out when there is one, so a change shows over the scene before it is saved.
    /// </remarks>
    public UiDocument? SceneInterface()
    {
        if (SceneInterfaceAsset is not { } relative)
        {
            return null;
        }

        var path = Path.Combine(Workspace.ProjectDirectory, relative);
        if (Interfaces[path] is { } open)
        {
            return open.Document;
        }

        if (!_sceneInterfaces.TryGetValue(relative, out var read))
        {
            read = ReadInterface(path);
            _sceneInterfaces[relative] = read;
        }

        return read;
    }

    /// <summary>
    /// Opens the canvas at <paramref name="path"/> in the Interface workspace.
    /// </summary>
    /// <remarks>
    /// A workspace rather than a panel beside the scene. A canvas is a design surface at a fixed size,
    /// and one squeezed in next to a scene view is too small to lay anything out in.
    /// </remarks>
    public void OpenInterface(string path)
    {
        if (Interfaces.OpenFile(path) is { } problem)
        {
            Say(problem, SayLevel.Error);
            return;
        }

        EnterMode("interface");
    }

    /// <summary>
    /// Makes an interface in the project's interfaces folder and opens it.
    /// </summary>
    /// <remarks>
    /// A scene that shows nothing yet gets it on a new canvas object, because an interface the game
    /// never shows is a step somebody has to know about.
    /// </remarks>
    public void NewInterface()
    {
        if (MakeIn("interfaces", NewAsset.Canvas) is not { } path)
        {
            return;
        }

        if (SceneInterfaceAsset is null && Working?.Scene is not null)
        {
            PutInterfaceOnScene(path);
        }

        OpenInterface(path);
    }

    /// <summary>
    /// Puts an interface on the scene being edited.
    /// </summary>
    /// <remarks>
    /// Onto the selected canvas if there is one, and onto a new canvas object if there is not. Being
    /// told to make an object first would be the editor knowing what somebody meant and refusing to
    /// do it.
    /// </remarks>
    public void PutInterfaceOnScene(string path)
    {
        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        var relative = Assets.Relative(path);
        if (Primary is { } primary && scene[primary] is { } selected && selected.Kind == ObjectKind.Canvas)
        {
            Run(new SetInterface(open.Id, selected.Id, selected.Name, relative));
            Say($"{selected.Name} now shows {Path.GetFileName(path)}.", SayLevel.Info);
            return;
        }

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, Path.GetFileNameWithoutExtension(path)),
            kind: ObjectKind.Canvas,
            interfaceAsset: relative);
        Run(new AddObject(added, open.Id));
        SelectObject(added.Id);
    }

    // Gives the Interface workspace something to show when it opens on nothing. Quiet when that file
    // cannot be read, since nobody asked for it, and the canvas then offers a new one instead.
    private void ShowSceneInterface()
    {
        if (Interfaces.Shown is null && SceneInterfaceAsset is { } path)
        {
            _ = Interfaces.OpenFile(Path.Combine(Workspace.ProjectDirectory, path));
        }
    }

    // Quiet, since nobody asked for it. A scene that names a missing file shows nothing over it.
    private static UiDocument? ReadInterface(string path)
    {
        try
        {
            return UiDocument.Read(IoFile.ReadAllText(path));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // Writes every interface with changes, and says which could not be written.
    private void SaveInterfaces()
    {
        foreach (var problem in Interfaces.SaveAll())
        {
            Say(problem, SayLevel.Error);
        }
    }

    // As with blends, only the window's mark of unsaved changes follows the bench.
    private void OnInterfacesChanged()
    {
        if (Interfaces.AnyUnsaved != _interfacesUnsaved)
        {
            _interfacesUnsaved = Interfaces.AnyUnsaved;
            Changed?.Invoke();
        }
    }
}

namespace Orblit.Editor.Core;

using Orblit.Motion;

// Blends in the graph: opened, made, and saved with the clips.
public sealed partial class EditorSession
{
    private bool _blendsUnsaved;

    /// <summary>The blends open in the graph. Editing one changes nothing the scene shows.</summary>
    public BlendBench Blends { get; }

    /// <summary>Opens the blend at <paramref name="path"/> in the graph, below the view in the animation mode.</summary>
    public void OpenBlend(string path)
    {
        EnterMode("animation");
        if (!ReadBlend(path))
        {
            return;
        }

        Layout = Layout.OpenBottom(new DockPanel(PanelKind.BlendGraph.Name, PanelKind.BlendGraph));
        Changed?.Invoke();
    }

    /// <summary>Makes a blend in the project's blends folder and opens it.</summary>
    public void NewBlend()
    {
        if (MakeIn("blends", NewAsset.Blend) is { } path)
        {
            OpenBlend(path);
        }
    }

    /// <summary>
    /// The names of the project's clips, which is what a state is offered to play. Whoever plays the
    /// blend decides what each is called, so these are suggestions and not a check.
    /// </summary>
    public IReadOnlyList<string> ClipNames() =>
        [.. Assets.Find(asset => asset.Kind == AssetKind.Clip).Select(Path.GetFileNameWithoutExtension).OfType<string>()];

    private bool ReadBlend(string path) =>
        ReadFile(path, "blend", Blends.OpenFile, error => error is BlendFormatException);

    // Writes every blend with changes, and says which could not be written.
    private void SaveBlends()
    {
        foreach (var problem in Blends.SaveAll())
        {
            Say(problem, SayLevel.Error);
        }
    }

    // An edit to a blend is a step on the undo stack, so only the window's mark of unsaved changes
    // follows the bench, and that moves when the answer does.
    private void OnBlendsChanged()
    {
        if (Blends.AnyUnsaved != _blendsUnsaved)
        {
            _blendsUnsaved = Blends.AnyUnsaved;
            Changed?.Invoke();
        }
    }
}

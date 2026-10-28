namespace Orblit.Editor.Core;

using Orblit.Ui;

/// <summary>An interface open in the Interface workspace.</summary>
public sealed class OpenInterface(string path, UiDocument document)
{
    public string Path { get; } = path;

    /// <summary>As it is now, edits and all. Only ever replaced whole, by an edit through the bench.</summary>
    public UiDocument Document { get; internal set; } = document;

    /// <summary>
    /// What the undo stack files this interface's changes under.
    /// <para>
    /// Prefixed so no scene or clip can ever be given the same one.
    /// </para>
    /// </summary>
    public string Id => IdFor(Path);

    public string Name => System.IO.Path.GetFileName(Path);

    /// <summary>The undo stack's stamp for this interface when it was last written.</summary>
    internal int SavedStamp { get; set; }

    internal static string IdFor(string path) => $"interface:{path}";
}

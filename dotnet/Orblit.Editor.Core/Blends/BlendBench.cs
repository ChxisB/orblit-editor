namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>
/// The blends being edited, and what is chosen in the one the graph shows.
/// </summary>
/// <remarks>
/// A model rather than state inside the panel, for the reason the clip bench is: the panel can be
/// closed and opened again, and the window asks whether anything is unsaved without knowing about
/// controls.
/// </remarks>
/// <param name="history">The undo stack every edit goes through.</param>
public sealed class BlendBench(History history)
{
    private readonly List<OpenBlend> _open = [];
    private string? _shown;
    private BlendPick _pick = BlendPick.Nothing;

    public event Action? Changed;

    public IReadOnlyList<OpenBlend> Open => [.. _open];

    public OpenBlend? this[string path] => _open.Find(one => one.Path == path);

    /// <summary>The blend the graph shows, or null.</summary>
    public OpenBlend? Shown => _shown is null ? null : this[_shown];

    public BlendDocument? Blend => Shown?.Blend;

    /// <summary>What is chosen in the shown blend.</summary>
    public BlendPick Pick
    {
        get => _pick;
        set
        {
            if (value != _pick)
            {
                _pick = value;
                Changed?.Invoke();
            }
        }
    }

    /// <summary>
    /// Shows the blend at <paramref name="path"/>, reading it first if it is not open, and says what
    /// could not be read.
    /// <para>
    /// Throws a <see cref="BlendFormatException"/> or an <see cref="IOException"/> for a file that
    /// cannot be opened at all.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> OpenFile(string path)
    {
        if (this[path] is not null)
        {
            Show(path);
            return [];
        }

        var load = BlendDocument.Decode(File.ReadAllText(path));
        OpenDocument(path, load.Blend);
        return load.Problems;
    }

    /// <summary>Shows <paramref name="blend"/> as the one at <paramref name="path"/>.</summary>
    public void OpenDocument(string path, BlendDocument blend)
    {
        if (this[path] is null)
        {
            _open.Add(new OpenBlend(path, blend));
        }

        Show(path);
    }

    public void Show(string path)
    {
        if (_shown == path || this[path] is null)
        {
            return;
        }

        _shown = path;
        _pick = BlendPick.Nothing;
        Changed?.Invoke();
    }

    /// <summary>Closes the blend at <paramref name="path"/>, and forgets its steps on the undo stack.</summary>
    public void Close(string path)
    {
        if (this[path] is not { } closing)
        {
            return;
        }

        _open.Remove(closing);
        history.Forget(closing.Id);
        if (_shown == path)
        {
            _shown = _open.Count == 0 ? null : _open[^1].Path;
            _pick = BlendPick.Nothing;
        }

        Changed?.Invoke();
    }

    public bool IsUnsaved(OpenBlend blend) => history.StampFor(blend.Id) != blend.SavedStamp;

    public bool AnyUnsaved => _open.Exists(IsUnsaved);

    /// <summary>Writes every blend with changes, and says which could not be written.</summary>
    public IReadOnlyList<string> SaveAll() => [.. _open.Where(IsUnsaved).ToList().Select(Save).OfType<string>()];

    /// <summary>Writes <paramref name="blend"/>, or says why it could not be.</summary>
    public string? Save(OpenBlend blend)
    {
        try
        {
            File.WriteAllText(blend.Path, blend.Blend.Encode());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"{blend.Blend.Name} could not be saved: {error.Message}";
        }

        blend.SavedStamp = history.StampFor(blend.Id);
        Changed?.Invoke();
        return null;
    }

    /// <summary>
    /// Changes the shown blend through the undo stack.
    /// </summary>
    /// <remarks>
    /// <see cref="BlendEditOptions.Pick"/> is what is chosen afterwards. A gesture ties a typed
    /// number's worth of these into one step. A change that answers the blend it was given changes
    /// nothing and is not a step.
    /// </remarks>
    public void Edit(string label, Func<BlendDocument, BlendDocument> change, BlendEditOptions options = default)
    {
        if (Shown is not { } shown)
        {
            return;
        }

        var to = change(shown.Blend);
        if (ReferenceEquals(to, shown.Blend))
        {
            return;
        }

        history.Run(new BlendEdit(this, shown.Path, label, (shown.Blend, _pick), to, options));
    }

    /// <summary>
    /// Where an edit puts a blend, forwards or back. Shows the blend it puts, so undoing a step in one
    /// somebody has moved away from is not a change they cannot see.
    /// </summary>
    internal void Put(string path, BlendDocument blend, BlendPick pick)
    {
        if (this[path] is not { } opened)
        {
            return;
        }

        opened.Blend = blend;
        _shown = path;
        _pick = StillThere(blend, pick);
        Changed?.Invoke();
    }

    private static BlendPick StillThere(BlendDocument blend, BlendPick pick) => pick switch
    {
        StatePicked state when !blend.WrittenStates.Any(one => one.Name == state.Name) => BlendPick.Nothing,
        ChangePicked change when change.Index < 0 || change.Index >= blend.WrittenChanges.Count => BlendPick.Nothing,
        _ => pick,
    };
}

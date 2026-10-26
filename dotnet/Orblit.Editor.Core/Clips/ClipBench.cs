namespace Orblit.Editor.Core;

using System.Collections.Frozen;
using Orblit.Motion;
using Doc = Orblit.Scene;

/// <summary>
/// The clips being edited, and where the timeline is in the one it shows.
/// </summary>
/// <remarks>
/// A model rather than state inside the timeline panel: the inspector's key buttons, the preview on the
/// scene and the panel all read it, and the panel can be closed and opened again without the playhead
/// going back to the start.
/// </remarks>
/// <param name="history">The undo stack every edit goes through.</param>
/// <param name="loadedScene">The scene a clip is played on, which is whichever one is loaded.</param>
/// <param name="playedOn">Whether this bench edits clips or cutscenes.</param>
public sealed class ClipBench(History history, Func<EditorScene?> loadedScene, PlayedOn playedOn = PlayedOn.Owner) : IKeying
{
    private readonly List<OpenClip> _open = [];
    private string? _shown;
    private double _at;
    private string? _owner;
    private bool _playing;
    private IReadOnlySet<KeyRef> _selection = FrozenSet<KeyRef>.Empty;
    private ChannelAddress? _curve;

    public event Action? Changed;

    /// <summary>Whether this bench edits clips or cutscenes, which is what the timeline calls things.</summary>
    public PlayedOn PlayedOn => playedOn;

    public IReadOnlyList<OpenClip> Open => [.. _open];

    public OpenClip? this[string path] => _open.Find(one => one.Path == path);

    /// <summary>The clip the timeline shows, or null.</summary>
    public OpenClip? Shown => _shown is null ? null : this[_shown];

    public ClipDocument? Clip => Shown?.Clip;

    /// <summary>Seconds into the shown clip.</summary>
    public double At
    {
        get => _at;
        set
        {
            var to = Clip is { } clip ? Math.Clamp(value, 0, clip.Duration) : 0;
            if (to == _at)
            {
                return;
            }

            _at = to;
            Notify();
        }
    }

    /// <summary>The playhead on the nearest frame, which is where a key goes.</summary>
    public double Frame => ClipEdits.Snapped(_at, Clip?.Rate ?? 0);

    /// <summary>
    /// The object the clip is played on, by id in the loaded scene.
    /// <para>
    /// Chosen rather than worked out: the same clip plays on every copy of a prefab, and which copy the
    /// timeline shows is somebody's choice.
    /// </para>
    /// </summary>
    public string? Owner
    {
        get => _owner;
        set
        {
            if (value != _owner)
            {
                _owner = value;
                Notify();
            }
        }
    }

    public bool Playing
    {
        get => _playing;
        set
        {
            if (value != _playing)
            {
                _playing = value && Clip is not null;
                Notify();
            }
        }
    }

    /// <summary>The keys selected on the timeline.</summary>
    public IReadOnlySet<KeyRef> Selection
    {
        get => _selection;
        set
        {
            if (!value.SetEquals(_selection))
            {
                _selection = value.ToFrozenSet();
                Notify();
            }
        }
    }

    /// <summary>The channel the curve view draws.</summary>
    public ChannelAddress? Curve
    {
        get => _curve;
        set
        {
            if (value != _curve)
            {
                _curve = value;
                Notify();
            }
        }
    }

    /// <summary>
    /// Shows the clip at <paramref name="path"/>, reading it first if it is not open, and says what
    /// could not be read.
    /// <para>
    /// Throws a <see cref="ClipFormatException"/>, a <see cref="CutsceneFormatException"/> or an
    /// <see cref="IOException"/> for a file that cannot be opened at all.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> OpenFile(string path)
    {
        if (this[path] is not null)
        {
            Show(path);
            return [];
        }

        var text = File.ReadAllText(path);
        if (playedOn == PlayedOn.Owner)
        {
            var clip = ClipDocument.Decode(text);
            OpenDocument(path, clip.Clip);
            return clip.Problems;
        }

        var cutscene = CutsceneDocument.Decode(text);
        OpenDocument(path, cutscene.Cutscene);
        return cutscene.Problems;
    }

    /// <summary>Shows <paramref name="clip"/> as the one at <paramref name="path"/>.</summary>
    public void OpenDocument(string path, ClipDocument clip) => Add(new OpenClip(path, clip));

    /// <summary>Shows <paramref name="cutscene"/> as the one at <paramref name="path"/>.</summary>
    public void OpenDocument(string path, CutsceneDocument cutscene) =>
        Add(new OpenClip(path, cutscene.Motion, cutscene.Shots, cutscene.Sounds));

    public void Show(string path)
    {
        if (_shown == path || this[path] is not { } next)
        {
            return;
        }

        _shown = path;
        ClearView();
        _at = Math.Clamp(_at, 0, next.Clip.Duration);
        Notify();
    }

    /// <summary>Clears the preview without discarding any open clip or its edits.</summary>
    public void Hide()
    {
        _shown = null;
        _owner = null;
        ClearView();
        Notify();
    }

    /// <summary>Closes the clip at <paramref name="path"/>, and forgets its steps on the undo stack.</summary>
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
            ClearView();
        }

        Notify();
    }

    public bool IsUnsaved(OpenClip clip) => history.StampFor(clip.Id) != clip.SavedStamp;

    public bool AnyUnsaved => _open.Exists(IsUnsaved);

    /// <summary>Writes every clip with changes, and says which could not be written.</summary>
    public IReadOnlyList<string> SaveAll() => [.. _open.Where(IsUnsaved).ToList().Select(Save).OfType<string>()];

    /// <summary>Writes <paramref name="clip"/>, or says why it could not be.</summary>
    public string? Save(OpenClip clip)
    {
        try
        {
            File.WriteAllText(clip.Path, clip.Cutscene?.Encode() ?? clip.Clip.Encode());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"{clip.Clip.Name} could not be saved: {error.Message}";
        }

        clip.SavedStamp = history.StampFor(clip.Id);
        Notify();
        return null;
    }

    /// <summary>
    /// Changes the shown clip through the undo stack.
    /// </summary>
    /// <remarks>
    /// A gesture in <paramref name="options"/> ties a drag's worth of these into one step. Say it only
    /// moves things when all it does is move keys or bend a curve, never when one comes or goes.
    /// </remarks>
    public void Edit(string label, Func<ClipDocument, ClipDocument> change, EditOptions options = default)
    {
        if (Shown is not { } shown)
        {
            return;
        }

        var to = change(shown.Clip);
        if (ReferenceEquals(to, shown.Clip) && options.Keys is null)
        {
            return;
        }

        history.Run(new ClipEdit(this, shown.Path, label, new ClipView(shown.Clip, _selection), to, options));
    }

    /// <summary>Ends a run of edits that were being merged, such as typing a length, so the next starts a new step.</summary>
    public void Seal() => history.Seal();

    /// <summary>
    /// Changes the shown cutscene's shots through the undo stack. A <paramref name="gesture"/> ties a
    /// typed number's worth of these into one step.
    /// </summary>
    public void EditShots(
        string label,
        Func<IReadOnlyList<CutsceneShot>, IEnumerable<CutsceneShot>> change,
        object? gesture = null)
    {
        if (Shown is not { Shots: { } shots } shown)
        {
            return;
        }

        IReadOnlyList<CutsceneShot> to = [.. change(shots).OrderBy(shot => shot.Start)];
        history.Run(new ShotEdit(this, shown.Path, label, shots, to, gesture));
    }

    public KeyMark MarkFor(SceneObject target, string fieldPath)
    {
        if (Clip is not { } clip || AddressFor(target, fieldPath) is not { } address)
        {
            return KeyMark.None;
        }

        var keyable = ClipEdits.ChannelAt(clip, address) is not null
            || ClipEdits.KindOfField(FieldOf(target, fieldPath)) is not null;
        return keyable ? ClipEdits.MarkOf(clip, address, Frame) : KeyMark.None;
    }

    public void Key(SceneObject target, string fieldPath)
    {
        if (Clip is not { } clip || AddressFor(target, fieldPath) is not { } address)
        {
            return;
        }

        var raw = FieldOf(target, fieldPath);
        var kind = ClipEdits.ChannelAt(clip, address)?.Kind ?? ClipEdits.KindOfField(raw);
        if (kind is null || ClipEdits.KeyValueOf(kind, fieldPath, raw) is not { } value)
        {
            return;
        }

        // Keying something with nothing chosen to play on makes it the one: the first key is where most
        // clips start, and asking first would be a question with one sensible answer. A cutscene plays
        // on the scene.
        if (playedOn == PlayedOn.Owner && OwnerIn(loadedScene()) is null)
        {
            _owner = target.Id;
        }

        var key = new NewKey(Frame, value);
        Edit($"Key {target.Name} {LabelOf(fieldPath)}", current => ClipEdits.Keyed(current, address, kind, key));
    }

    /// <summary>
    /// What <paramref name="target"/> names where the clip is played, or null when the loaded scene has
    /// nothing by that name, or nothing is chosen to play it on.
    /// </summary>
    public SceneObject? ObjectOf(string target)
    {
        if (loadedScene() is not { } scene)
        {
            return null;
        }

        return (playedOn, OwnerIn(scene)) switch
        {
            (PlayedOn.Scene, _) => scene[target],
            (_, null) => null,
            (_, { } owner) => scene[ScopeFor(owner).Resolve(target)],
        };
    }

    /// <summary>
    /// What the open clip's names point at in <paramref name="scene"/>, or null when the clip is not
    /// being played on something in that scene.
    /// <para>
    /// Null for any other scene than the loaded one, whose names mean nothing there, and for a
    /// cutscene, which moves what is in the scene and not the bones of a model.
    /// </para>
    /// </summary>
    public ClipScope? ScopeIn(EditorScene scene) =>
        playedOn == PlayedOn.Owner && Clip is not null && ReferenceEquals(scene, loadedScene()) && OwnerIn(scene) is { } owner
            ? ScopeFor(owner)
            : null;

    /// <summary>
    /// Where an edit puts a clip, forwards or back. Shows the clip it puts, so undoing a step in a clip
    /// somebody has moved away from is not a change they cannot see.
    /// </summary>
    internal void Put(string path, ClipDocument clip, IReadOnlySet<KeyRef> keys)
    {
        if (this[path] is not { } opened)
        {
            return;
        }

        opened.Clip = clip;
        Surface(path);
        _selection = keys.ToFrozenSet();
        _at = Math.Clamp(_at, 0, clip.Duration);
        if (_curve is { } curve && ClipEdits.ChannelAt(clip, curve) is null)
        {
            _curve = null;
        }

        Notify();
    }

    /// <summary>Where an edit puts a cutscene's shots, forwards or back.</summary>
    internal void PutShots(string path, IReadOnlyList<CutsceneShot> shots)
    {
        if (this[path] is not { } opened)
        {
            return;
        }

        opened.Shots = shots;
        Surface(path);
        Notify();
    }

    private static ClipScope ScopeFor(SceneObject owner) =>
        ClipScope.InScene(new Doc.SceneDocument(entities: [SceneDocument.EntityOf(owner)]), owner.Id);

    // The property as the scene writes it.
    private static object? FieldOf(SceneObject target, string property)
    {
        var dot = property.IndexOf('.', StringComparison.Ordinal);
        if (dot <= 0)
        {
            return null;
        }

        return SceneDocument.EntityOf(target)[property[..dot]]?.ToJson().GetValueOrDefault(property[(dot + 1)..]);
    }

    // The field without its component: "power" for "light.power". A property with no dot is its own label.
    private static string LabelOf(string property) =>
        property[(property.IndexOf('.', StringComparison.Ordinal) + 1)..];

    private void Add(OpenClip opened)
    {
        if (this[opened.Path] is null)
        {
            _open.Add(opened);
        }

        Show(opened.Path);
    }

    private void ClearView()
    {
        _selection = FrozenSet<KeyRef>.Empty;
        _curve = null;
        _playing = false;
    }

    private void Surface(string path)
    {
        if (_shown == path)
        {
            return;
        }

        _shown = path;
        _playing = false;
        _curve = null;
    }

    // The object the clip plays on, when the loaded scene has it.
    private SceneObject? OwnerIn(EditorScene? scene) => _owner is { } id ? scene?[id] : null;

    // The channel the property of the target would be keyed on, or null when the clip, played where it
    // is, has no way to name it.
    private ChannelAddress? AddressFor(SceneObject target, string property)
    {
        if (loadedScene() is not { } scene)
        {
            return null;
        }

        var name = (playedOn, OwnerIn(scene)) switch
        {
            (PlayedOn.Scene, _) => target.Id,
            (_, null) => string.Empty,
            (_, { } owner) => ScopeFor(owner).TargetOf(target.Id),
        };
        return name is null ? null : new ChannelAddress(name, null, property);
    }

    private void Notify() => Changed?.Invoke();
}

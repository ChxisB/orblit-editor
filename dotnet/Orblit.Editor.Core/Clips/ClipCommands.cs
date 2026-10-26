namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>A clip and the keys selected in it.</summary>
internal readonly record struct ClipView(ClipDocument Clip, IReadOnlySet<KeyRef> Keys);

/// <summary>
/// One change to a clip.
/// </summary>
/// <remarks>
/// Holds the clip from before and the clip from after, which are both whole and neither ever changed,
/// so undoing is putting one back.
/// </remarks>
internal sealed class ClipEdit : EditorCommand
{
    private readonly ClipBench _bench;
    private readonly string _path;
    private readonly ClipView _from;
    private readonly object? _gesture;

    // Not readonly: a merged drag rewrites where it ends up.
    private ClipDocument _to;
    private IReadOnlySet<KeyRef> _keysAfter;

    // F2: an edit is a clip before and after, where it lives, what it is called and what ties it to a drag.
    public ClipEdit(ClipBench bench, string path, string label, ClipView from, ClipDocument to, EditOptions options)
    {
        _bench = bench;
        _path = path;
        Label = label;
        _from = from;
        _to = to;
        _keysAfter = options.Keys ?? from.Keys;
        _gesture = options.Gesture;
        OnlyMoves = options.OnlyMoves;
    }

    public override string SceneId => OpenClip.IdFor(_path);

    public override string Label { get; }

    public override bool OnlyMoves { get; }

    public override object? MergeKey => _gesture is null ? null : (_path, _gesture);

    public override void Apply(ISceneHost host) => _bench.Put(_path, _to, _keysAfter);

    public override void Revert(ISceneHost host) => _bench.Put(_path, _from.Clip, _from.Keys);

    public override void Absorb(EditorCommand later)
    {
        if (later is ClipEdit next)
        {
            _to = next._to;
            _keysAfter = next._keysAfter;
        }
    }
}

/// <summary>
/// One change to a cutscene's shots, holding them whole from before and after the way a
/// <see cref="ClipEdit"/> holds a clip.
/// </summary>
internal sealed class ShotEdit : EditorCommand
{
    private readonly ClipBench _bench;
    private readonly string _path;
    private readonly IReadOnlyList<CutsceneShot> _from;
    private readonly object? _gesture;

    // Not readonly: a merged run of typing rewrites where it ends up.
    private IReadOnlyList<CutsceneShot> _to;

    // F2: an edit is the shots before and after, where they live, what it is called and what ties it to a run.
    public ShotEdit(
        ClipBench bench,
        string path,
        string label,
        IReadOnlyList<CutsceneShot> from,
        IReadOnlyList<CutsceneShot> to,
        object? gesture)
    {
        _bench = bench;
        _path = path;
        Label = label;
        _from = from;
        _to = to;
        _gesture = gesture;
    }

    public override string SceneId => OpenClip.IdFor(_path);

    public override string Label { get; }

    public override object? MergeKey => _gesture is null ? null : (_path, "shots", _gesture);

    public override void Apply(ISceneHost host) => _bench.PutShots(_path, _to);

    public override void Revert(ISceneHost host) => _bench.PutShots(_path, _from);

    public override void Absorb(EditorCommand later)
    {
        if (later is ShotEdit next)
        {
            _to = next._to;
        }
    }
}

namespace Orblit.Editor.Core;

using Orblit.Motion;

/// <summary>What one edit to a blend needs besides the blend before and after.</summary>
/// <param name="Pick">What is chosen afterwards. What was chosen before when not given.</param>
/// <param name="Gesture">Ties a typed number's worth of edits into one undo step. Null for an edit that stands alone.</param>
public readonly record struct BlendEditOptions(BlendPick? Pick = null, object? Gesture = null);

/// <summary>
/// One change to a blend.
/// </summary>
/// <remarks>
/// Holds the blend from before and the blend from after, which are both whole and neither ever
/// changed, so undoing is putting one back.
/// </remarks>
internal sealed class BlendEdit : EditorCommand
{
    private readonly BlendBench _bench;
    private readonly string _path;
    private readonly BlendDocument _from;
    private readonly BlendPick _pickBefore;
    private readonly object? _gesture;

    // Not readonly: a merged run of typing rewrites where it ends up.
    private BlendDocument _to;
    private BlendPick _pickAfter;

    // F2: an edit is a blend and a choice before and after, where it lives, what it is called and what ties it to a run.
    public BlendEdit(
        BlendBench bench,
        string path,
        string label,
        (BlendDocument Blend, BlendPick Pick) from,
        BlendDocument to,
        BlendEditOptions options)
    {
        _bench = bench;
        _path = path;
        Label = label;
        _from = from.Blend;
        _pickBefore = from.Pick;
        _to = to;
        _pickAfter = options.Pick ?? from.Pick;
        _gesture = options.Gesture;
    }

    public override string SceneId => OpenBlend.IdFor(_path);

    public override string Label { get; }

    public override object? MergeKey => _gesture is null ? null : (_path, "blend", _gesture);

    public override void Apply(ISceneHost host) => _bench.Put(_path, _to, _pickAfter);

    public override void Revert(ISceneHost host) => _bench.Put(_path, _from, _pickBefore);

    public override void Absorb(EditorCommand later)
    {
        if (later is BlendEdit next)
        {
            _to = next._to;
            _pickAfter = next._pickAfter;
        }
    }
}

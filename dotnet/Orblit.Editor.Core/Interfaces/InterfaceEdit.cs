namespace Orblit.Editor.Core;

using Orblit.Ui;

/// <summary>What one edit to an interface needs besides the document before and after.</summary>
/// <param name="Select">The selection afterwards. The one before when not given.</param>
/// <param name="Gesture">Ties a run of edits into one undo step. Null for an edit that stands alone.</param>
/// <param name="OnlyMoves">Whether all it does is move an element.</param>
public readonly record struct InterfaceEditOptions(IReadOnlyList<int>? Select = null, object? Gesture = null, bool OnlyMoves = false);

/// <summary>
/// One change to an interface.
/// </summary>
/// <remarks>
/// Holds the document from before and the document from after. Both are whole and neither ever
/// changes, so undoing is putting one back. An edit shares every subtree it did not touch, so a step
/// costs the spine of one edit rather than a copy of the interface.
/// </remarks>
internal sealed class InterfaceEdit : EditorCommand
{
    private readonly InterfaceBench _bench;
    private readonly string _path;
    private readonly UiDocument _from;
    private readonly IReadOnlyList<int>? _selectedBefore;
    private readonly object? _gesture;
    private readonly bool _onlyMoves;

    // Not readonly: a merged drag rewrites where it ends up.
    private UiDocument _to;
    private IReadOnlyList<int>? _selectedAfter;

    // F2: an edit is a document and a selection before and after, where it lives, what it is called and what ties it to a run.
    public InterfaceEdit(
        InterfaceBench bench,
        string path,
        string label,
        (UiDocument Document, IReadOnlyList<int>? Selected) from,
        UiDocument to,
        InterfaceEditOptions options)
    {
        _bench = bench;
        _path = path;
        Label = label;
        _from = from.Document;
        _selectedBefore = from.Selected;
        _to = to;
        _selectedAfter = options.Select ?? from.Selected;
        _gesture = options.Gesture;
        _onlyMoves = options.OnlyMoves;
    }

    public override string SceneId => OpenInterface.IdFor(_path);

    public override string Label { get; }

    public override object? MergeKey => _gesture is null ? null : (_path, _gesture);

    public override bool OnlyMoves => _onlyMoves;

    public override void Apply(ISceneHost host) => _bench.Put(_path, _to, _selectedAfter);

    public override void Revert(ISceneHost host) => _bench.Put(_path, _from, _selectedBefore);

    public override void Absorb(EditorCommand later)
    {
        if (later is InterfaceEdit next)
        {
            _to = next._to;
            _selectedAfter = next._selectedAfter;
        }
    }
}

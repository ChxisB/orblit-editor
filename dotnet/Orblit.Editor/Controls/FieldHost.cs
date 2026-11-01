using Avalonia.Threading;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// What the fields of one panel share: where their changes go, which sections are folded, and whether
/// one of them is being held.
/// </summary>
/// <remarks>
/// A drag runs a command a frame, and rebuilding every field under the pointer would drop the pointer.
/// So while a field is held the panel only refreshes the fields that registered, and it rebuilds once
/// the field lets go. A fold outlives a rebuild because it lives here and not in a control.
/// </remarks>
internal sealed class FieldHost(History history, Action rebuild)
{
    private readonly List<Action> _live = [];
    private readonly HashSet<string> _folded = [];
    private bool _holding;

    public History History { get; } = history;

    public bool Holding => _holding;

    public bool IsFolded(string title) => _folded.Contains(title);

    /// <summary>Opens a section that was folded and folds one that was open.</summary>
    public void Fold(string title)
    {
        if (!_folded.Remove(title))
        {
            _folded.Add(title);
        }

        // After the click that asked, which is on the heading a rebuild would remove.
        Dispatcher.UIThread.Post(rebuild);
    }

    /// <summary>Registers how a field shows what the scene says now. Called again at every change while one is held.</summary>
    public void Live(Action refresh) => _live.Add(refresh);

    /// <summary>Forgets every registered field, because a rebuild is about to make new ones.</summary>
    public void Reset() => _live.Clear();

    /// <summary>A field began changing something and will say when it has finished.</summary>
    public void Hold() => _holding = true;

    /// <summary>Runs one change that is a step by itself, such as a click on a choice.</summary>
    public void Apply(Action change)
    {
        Hold();
        change();
        Settle();
    }

    /// <summary>
    /// A field finished changing something: the run of changes becomes one undo step, and what the
    /// fields show is built again from the scene.
    /// </summary>
    public void Settle()
    {
        History.Seal();
        if (!_holding)
        {
            return;
        }

        _holding = false;
        // After the event that ended it, which may still be reading the control a rebuild would remove.
        Dispatcher.UIThread.Post(rebuild);
    }

    /// <summary>The scene or the history changed.</summary>
    public void Changed()
    {
        if (!_holding)
        {
            rebuild();
            return;
        }

        foreach (var refresh in _live.ToArray())
        {
            refresh();
        }
    }
}

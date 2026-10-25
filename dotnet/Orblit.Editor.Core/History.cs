namespace Orblit.Editor.Core;

/// <summary>
/// Where a command finds the scene it belongs to.
/// </summary>
/// <remarks>
/// Commands name a scene rather than holding one, so a scene can be closed and reopened without
/// every step on the undo stack pointing at a dead object.
/// </remarks>
public interface ISceneHost
{
    EditorScene? SceneFor(string sceneId);
}

/// <summary>
/// One undoable change to the scene.
/// </summary>
/// <remarks>
/// Every edit goes through one of these. A widget that mutated the scene directly would work
/// perfectly and be invisible to undo, which is the kind of gap nobody finds until they have lost
/// work to it.
/// </remarks>
public abstract class EditorCommand
{
    /// <summary>Which open scene this changes.</summary>
    public abstract string SceneId { get; }

    /// <summary>
    /// What the change is called, as it appears next to Undo. Written as the action, not the
    /// outcome: "Move Cube", so the menu reads "Undo Move Cube".
    /// </summary>
    public abstract string Label { get; }

    /// <summary>
    /// Every scene this changes, which is usually just <see cref="SceneId"/>.
    /// <para>
    /// A move between scenes changes two documents, and both of them have to read as unsaved
    /// afterwards. Stamping only the destination would leave the scene the object came out of looking
    /// clean while it is a whole object short of what is on disk.
    /// </para>
    /// </summary>
    public virtual IReadOnlySet<string> Touches => new HashSet<string> { SceneId };

    /// <summary>
    /// Identifies a run of changes that should collapse into one undo step.
    /// <para>
    /// Dragging a slider produces a command per frame. Without this, undo would step back through a
    /// hundred of them and feel broken. Null means the command always stands alone.
    /// </para>
    /// </summary>
    public virtual object? MergeKey => null;

    /// <summary>
    /// Whether this only moves things about, leaving what they are alone.
    /// <para>
    /// A drag runs one of these a frame, and most of the editor cannot be affected by one: a name is
    /// the same name at a different height, a file is the same file, a material is the same material.
    /// Saying so lets those parts be left alone, which is the difference between a drag that keeps up
    /// and one that does not.
    /// </para>
    /// <para>
    /// False by default, and deliberately: a command that forgets to say it moves things costs a
    /// rebuild nobody needed, and one that wrongly claims to costs a panel showing something that is
    /// no longer true.
    /// </para>
    /// </summary>
    public virtual bool OnlyMoves => false;

    /// <summary>
    /// Set by the history each time this command is run or absorbs another. What lets "has anything
    /// changed since the last save" be answered exactly. Depth alone cannot: a merged drag leaves the
    /// stack the same length while changing what it means.
    /// </summary>
    public int Stamp { get; set; }

    public abstract void Apply(ISceneHost host);

    public abstract void Revert(ISceneHost host);

    /// <summary>
    /// Folds a later command of the same <see cref="MergeKey"/> into this one. The earlier command
    /// keeps the value it started from and takes the later one's destination, so undoing the run
    /// returns to where the drag began.
    /// </summary>
    public virtual void Absorb(EditorCommand later)
    {
    }
}

/// <summary>
/// What has been done, and what can be undone. One stack across every open scene.
/// </summary>
/// <remarks>
/// Notifies rather than being polled, so the toolbar's Undo button and the viewport both learn about
/// a change the same way.
/// <para>
/// Global rather than one per scene: an editor where undo depends on which scene happens to be
/// selected undoes the wrong thing exactly when somebody is moving between two of them, which is the
/// moment they most need it to be predictable.
/// </para>
/// </remarks>
public sealed class History(ISceneHost host)
{
    /// <summary>
    /// How many steps are kept. Deep enough to cover a working session, bounded so a long one does
    /// not grow without limit.
    /// </summary>
    public const int Limit = 200;

    private readonly List<EditorCommand> _done = [];
    private readonly List<EditorCommand> _undone = [];

    // Stops the next command merging into the last one. Set when a gesture ends. A time window
    // would be the obvious alternative and is worse: it makes undo depend on how fast somebody
    // dragged.
    private bool _sealed = true;
    private int _stamps;

    public event Action? Changed;

    public ISceneHost Host { get; } = host;

    /// <summary>
    /// Whether the last thing that happened only moved something. Read by whoever is deciding how
    /// much to rebuild. Undo and redo are never move-only however move-only the command was:
    /// stepping back through history changes what is selected and what is dirty, and something has
    /// to notice.
    /// </summary>
    public bool LastOnlyMoved { get; private set; }

    public bool CanUndo => _done.Count > 0;

    public bool CanRedo => _undone.Count > 0;

    public string? UndoLabel => _done.Count == 0 ? null : _done[^1].Label;

    public string? RedoLabel => _undone.Count == 0 ? null : _undone[^1].Label;

    /// <summary>The scene the next undo would change, so the shell can select it.</summary>
    public string? UndoSceneId => _done.Count == 0 ? null : _done[^1].SceneId;

    public string? RedoSceneId => _undone.Count == 0 ? null : _undone[^1].SceneId;

    internal IReadOnlyList<string> Labels => [.. _done.Select(command => command.Label)];

    /// <summary>
    /// The stamp of the most recent change to one scene, or zero for none. What "has this scene
    /// changed since it was written" is answered with. A scene is unchanged when the last thing done
    /// to it is the same thing that was there when it was saved, whatever has happened to other
    /// scenes since.
    /// </summary>
    public int StampFor(string sceneId) =>
        _done.LastOrDefault(command => command.Touches.Contains(sceneId))?.Stamp ?? 0;

    /// <summary>Forgets every step belonging to a scene, for when one is closed.</summary>
    public void Forget(string sceneId)
    {
        _done.RemoveAll(command => command.Touches.Contains(sceneId));
        _undone.RemoveAll(command => command.Touches.Contains(sceneId));
        _sealed = true;
        Changed?.Invoke();
    }

    /// <summary>
    /// Applies a command and records it. Throws whatever the command throws, having recorded
    /// nothing: a refused edit must not leave a step on the stack that would undo something else.
    /// </summary>
    public void Run(EditorCommand command)
    {
        LastOnlyMoved = command.OnlyMoves;
        var key = command.MergeKey;
        if (!_sealed && key is not null && _done.Count > 0 && Equals(_done[^1].MergeKey, key))
        {
            command.Apply(Host);
            _done[^1].Absorb(command);
            _done[^1].Stamp = ++_stamps;
            Changed?.Invoke();
            return;
        }

        command.Apply(Host);

        // Cleared only once the command has applied without throwing. A redo stack discarded by an
        // edit that then failed would lose work for no reason.
        _undone.Clear();
        command.Stamp = ++_stamps;
        _done.Add(command);
        if (_done.Count > Limit)
        {
            _done.RemoveAt(0);
        }

        _sealed = key is null;
        Changed?.Invoke();
    }

    /// <summary>Ends a run of merging commands. Call when a drag or a typed edit finishes.</summary>
    public void Seal() => _sealed = true;

    public void Undo()
    {
        LastOnlyMoved = false;
        if (_done.Count == 0)
        {
            return;
        }

        var command = _done[^1];
        _done.RemoveAt(_done.Count - 1);
        command.Revert(Host);
        _undone.Add(command);
        _sealed = true;
        Changed?.Invoke();
    }

    public void Redo()
    {
        LastOnlyMoved = false;
        if (_undone.Count == 0)
        {
            return;
        }

        var command = _undone[^1];
        _undone.RemoveAt(_undone.Count - 1);
        command.Apply(Host);
        _done.Add(command);
        _sealed = true;
        Changed?.Invoke();
    }

    public void Clear()
    {
        _done.Clear();
        _undone.Clear();
        _sealed = true;
        Changed?.Invoke();
    }
}

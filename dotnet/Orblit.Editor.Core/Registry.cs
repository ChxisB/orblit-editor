namespace Orblit.Editor.Core;

/// <summary>Something the editor looks up by name.</summary>
public interface IRegistered
{
    /// <summary>
    /// Unique among its own sort: two panels cannot share a name, but a panel and a mode can.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// The things of one sort the editor can show, by name and in order.
/// </summary>
/// <remarks>
/// A list rather than a map underneath, because the order is part of what is registered: it is the
/// order the View menu lists panels in, and the order the inspector stacks its sections.
/// <para>
/// This is the seam that keeps the editor core from having to know every panel, section and gizmo
/// there is. Something new registers itself here, and the shell shows it without being told its name.
/// </para>
/// </remarks>
public sealed class Registry<T>
    where T : class, IRegistered
{
    private readonly List<T> _entries = [];

    /// <summary>Everything registered, in order.</summary>
    public IReadOnlyList<T> All => new List<T>(_entries).AsReadOnly();

    /// <summary>The one called <paramref name="name"/>, or null when nothing is.</summary>
    public T? this[string name] => _entries.Find(entry => entry.Name == name);

    /// <summary>
    /// Adds <paramref name="entry"/>, after the others or just ahead of the one called
    /// <paramref name="before"/>.
    /// <para>
    /// A name registered twice is a mistake in the code, not something a user did, so it throws
    /// rather than quietly keeping one of them.
    /// </para>
    /// </summary>
    public void Register(T entry, string? before = null)
    {
        if (this[entry.Name] is not null)
        {
            throw new InvalidOperationException($"Two things registered as \"{entry.Name}\".");
        }

        var at = before is null ? -1 : _entries.FindIndex(one => one.Name == before);
        if (at < 0)
        {
            _entries.Add(entry);
        }
        else
        {
            _entries.Insert(at, entry);
        }
    }
}

namespace Orblit.Editor.Core;

/// <summary>The data objects a project has, read once and kept.</summary>
/// <remarks>
/// Cached because a link is looked up on every rebuild of the inspector, and forty objects can point at
/// the same file. Reading it forty times a frame would make a panel that shows a number feel like a
/// panel that opens a file.
/// </remarks>
/// <param name="root">The project folder. Every path here is relative to it.</param>
public sealed class DataStore(string root)
{
    private readonly Dictionary<string, DataObject?> _held = [];

    /// <summary>Raised on a write and a forget, so a value edited in one place shows everywhere it is used.</summary>
    public event Action? Changed;

    /// <summary>
    /// The data object at a path, or null when it is missing or unreadable. The failure is kept too,
    /// or a link to a deleted file would go to the disk on every frame.
    /// </summary>
    public DataObject? this[string relative]
    {
        get
        {
            if (!_held.TryGetValue(relative, out var data))
            {
                data = Read(relative);
                _held[relative] = data;
            }

            return data;
        }
    }

    /// <summary>
    /// Writes one back and tells everything showing it. Returns what went wrong, or null. Not undoable,
    /// for the reason renaming a file is not: the undo stack covers the scene, and a step that quietly
    /// rewrote a file on disk would be a worse surprise.
    /// </summary>
    public string? Write(string relative, DataObject data)
    {
        try
        {
            var path = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, data.ToText());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }

        _held[relative] = data;
        Changed?.Invoke();
        return null;
    }

    /// <summary>Forgets what was read, all of it or one path, for when the folder changes underneath.</summary>
    public void Forget(string? relative = null)
    {
        if (relative is null)
        {
            _held.Clear();
        }
        else
        {
            _held.Remove(relative);
        }

        Changed?.Invoke();
    }

    private DataObject? Read(string relative)
    {
        try
        {
            var path = Path.Combine(root, relative);
            return File.Exists(path) ? DataObject.Read(File.ReadAllText(path)) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

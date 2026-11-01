namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Json;

/// <summary>Named arrangements for each workspace in one project.</summary>
public sealed class LayoutLibrary
{
    private readonly string _file;
    private readonly IReadOnlyList<PanelKind> _kinds;
    private readonly OrderedDictionary<string, object?> _saved = [];

    public LayoutLibrary(string file, IEnumerable<PanelKind> kinds)
    {
        _file = file;
        _kinds = [.. kinds];
        try
        {
            if (IoFile.Exists(file) && JsonText.Decode(IoFile.ReadAllText(file)) is IReadOnlyDictionary<string, object?> raw)
            {
                foreach (var (mode, layouts) in raw)
                {
                    _saved[mode] = layouts;
                }
            }
        }
        catch (Exception error) when (error is JsonFormatException or IOException or UnauthorizedAccessException)
        {
            // A damaged library must not stop the project opening, and a read-only project can still
            // use its built-in arrangements.
        }
    }

    public IReadOnlyList<string> Names(string mode) => [.. LayoutsFor(mode).Keys.Order(StringComparer.Ordinal)];

    public DockLayout? Find(string mode, string name) =>
        LayoutsFor(mode).GetValueOrDefault(name) is { } raw ? DockLayout.Read(JsonText.Encode(raw), _kinds) : null;

    public void Save(string mode, string name, DockLayout layout)
    {
        var layouts = new OrderedDictionary<string, object?>(LayoutsFor(mode)) { [name] = JsonText.Decode(layout.ToText()) };
        Write(mode, layouts);
    }

    public void Remove(string mode, string name)
    {
        var layouts = new OrderedDictionary<string, object?>(LayoutsFor(mode));
        layouts.Remove(name);
        Write(mode, layouts);
    }

    private IReadOnlyDictionary<string, object?> LayoutsFor(string mode) =>
        _saved.TryGetValue(mode, out var layouts) && layouts is IReadOnlyDictionary<string, object?> known
            ? known
            : new Dictionary<string, object?>();

    private void Write(string mode, OrderedDictionary<string, object?> layouts)
    {
        var next = new OrderedDictionary<string, object?>(_saved) { [mode] = layouts };

        // A full path to a file always has a directory.
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_file))!);
        var pending = _file + ".tmp";
        IoFile.WriteAllText(pending, JsonText.Encode(next));
        IoFile.Move(pending, _file, overwrite: true);
        _saved[mode] = layouts;
    }
}

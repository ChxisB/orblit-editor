namespace Orblit.Editor.Core;

using System.Globalization;
using Orblit.Json;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>What a copied object needs to land where it was.</summary>
public readonly record struct ClipboardContents(
    IReadOnlyList<SceneObject> Objects,
    IReadOnlyList<string> Roots,
    IReadOnlyDictionary<string, Matrix4> Worlds);

/// <summary>
/// Objects taken out of a scene, waiting to go into one.
/// </summary>
/// <remarks>
/// Detached copies rather than references. A scene can be unloaded between the copy and the paste,
/// which is the whole point of having this, so anything pointing back at the original document would
/// be pointing at nothing.
/// </remarks>
public sealed class SceneClipboard
{
    // What marks text as Orblit objects rather than any other JSON.
    private const string Marker = "orblit.objects";

    private readonly List<SceneObject> _objects = [];

    // The ids that were tops of what was copied, in order.
    private readonly List<string> _roots = [];

    // Where each top sat in the world when it was copied, so it can be put back there under a
    // different parent.
    private readonly OrderedDictionary<string, Matrix4> _worlds = [];

    public bool IsEmpty => _objects.Count == 0;

    /// <summary>What is on it, for a menu to say "Paste Crate" rather than just "Paste".</summary>
    public string Description
    {
        get
        {
            if (_objects.Count == 0)
            {
                return "";
            }

            return _roots.Count == 1
                ? _objects.First(one => one.Id == _roots[0]).Name
                : $"{_roots.Count} objects";
        }
    }

    /// <summary>
    /// Takes a copy of each object and everything under it.
    /// <para>
    /// Anything already inside something else being copied is left out. Copying a parent and its
    /// child together would otherwise paste the child twice: once as itself and once inside its
    /// parent.
    /// </para>
    /// </summary>
    public void Take(EditorScene scene, IEnumerable<string> ids)
    {
        Clear();

        var wanted = ids.Where(scene.Contains).ToList();
        var tops = wanted.Where(id => !wanted.Any(other => other != id && scene.IsAncestorOf(other, id)));

        foreach (var id in tops)
        {
            var target = scene[id]!; // wanted holds only ids the scene contains.
            _roots.Add(id);
            _worlds[id] = scene.WorldOf(id).Clone();

            var top = target.Copy();
            top.ParentId = null;
            _objects.Add(top);
            _objects.AddRange(scene.DescendantsOf(id).Select(child => child.Copy()));
        }
    }

    /// <summary>
    /// Objects ready to be added to a scene, with fresh ids.
    /// <para>
    /// New ids every time, so pasting twice gives two things rather than one thing that cannot decide
    /// which scene it is in. Parent links are remapped alongside, or a pasted child would point at the
    /// object it was copied from.
    /// </para>
    /// <para>
    /// A part of a prefab instance is named by its path into the instance, so it keeps that path
    /// under the instance's new id: a copied lamp post pastes as another instance of the lamp post,
    /// with its changes. A part copied without its instance has nothing to be a part of, and pastes
    /// as an ordinary object.
    /// </para>
    /// </summary>
    public ClipboardContents Contents(Func<string> nextId, string? parentId = null)
    {
        var copied = _objects.Select(one => one.Id).ToHashSet();
        var remap = new Dictionary<string, string>();

        string Mapped(string id)
        {
            if (remap.TryGetValue(id, out var known))
            {
                return known;
            }

            var outer = Doc.EntityPath.Enclosing(id).FirstOrDefault(copied.Contains);
            return remap[id] = outer is null ? nextId() : $"{Mapped(outer)}{id[outer.Length..]}";
        }

        foreach (var one in _objects)
        {
            Mapped(one.Id);
        }

        return new ClipboardContents(
            [
                .. _objects.Select(original => original.CopyAs(
                    remap[original.Id],
                    original.ParentId is null ? parentId : remap.GetValueOrDefault(original.ParentId))),
            ],
            [.. _roots.Select(root => remap[root])],
            _worlds.ToDictionary(entry => remap[entry.Key], entry => entry.Value.Clone()));
    }

    public void Clear()
    {
        _objects.Clear();
        _roots.Clear();
        _worlds.Clear();
    }

    /// <summary>
    /// The clipboard as text, for the system clipboard.
    /// <para>
    /// The same encoding a scene file uses, so what lands in a text editor is readable and could be
    /// pasted back, rather than an opaque blob that only this build understands.
    /// </para>
    /// </summary>
    public string ToText() => JsonText.Encode(
        new Dictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = (long)SceneDocument.FormatVersion,
            ["roots"] = _roots.Cast<object?>().ToList(),
            ["worlds"] = _worlds.ToDictionary(
                entry => entry.Key,
                entry => (object?)Enumerable.Range(0, 16).Select(i => (object?)entry.Value[i]).ToList()),
            ["objects"] = _objects.Select(one => (object?)SceneDocument.ObjectToJson(one)).ToList(),
        },
        indent: "  ");

    /// <summary>
    /// Reads objects out of text, or leaves the clipboard alone.
    /// <para>
    /// Returns whether it was Orblit objects. Anything else on the system clipboard, such as a path,
    /// a paragraph or JSON meaning something else, is not an error. It is simply not ours.
    /// </para>
    /// </summary>
    public bool TakeText(string? text)
    {
        if (text is null || !text.Contains(Marker, StringComparison.Ordinal))
        {
            return false;
        }

        if (Parse(text) is not { } parsed
            || parsed.GetValueOrDefault("kind") as string != Marker
            || parsed.GetValueOrDefault("objects") is not IReadOnlyList<object?> raw
            || parsed.GetValueOrDefault("roots") is not IReadOnlyList<object?> roots)
        {
            return false;
        }

        // Written by an older editor, it is read the way a scene from one is.
        var version = parsed.GetValueOrDefault("formatVersion") is long written ? written : SceneDocument.FormatVersion;
        var objects = raw
            .OfType<IReadOnlyDictionary<string, object?>>()
            .Select(entry => SceneDocument.ObjectFromJson(entry, version))
            .OfType<SceneObject>()
            .ToList();
        if (objects.Count == 0)
        {
            return false;
        }

        // A root naming an object that is not here would paste nothing.
        var kept = roots.OfType<string>().Where(id => objects.Any(one => one.Id == id)).ToList();
        if (kept.Count == 0)
        {
            return false;
        }

        // Read before anything is cleared, so a world that cannot be read never costs the copy that
        // is already held.
        Adopt(objects, kept, ReadWorlds(parsed.GetValueOrDefault("worlds"), objects));
        return true;
    }

    private void Adopt(List<SceneObject> objects, List<string> roots, Dictionary<string, Matrix4> worlds)
    {
        Clear();
        _objects.AddRange(objects);
        _roots.AddRange(roots);
        foreach (var (id, world) in worlds)
        {
            _worlds[id] = world;
        }
    }

    private static IReadOnlyDictionary<string, object?>? Parse(string text)
    {
        try
        {
            return JsonText.Decode(text) as IReadOnlyDictionary<string, object?>;
        }
        catch (JsonFormatException)
        {
            return null;
        }
    }

    // The system clipboard is anybody's to write, so a world is kept only when it is sixteen numbers for an
    // object that came with it. The others are not kept, so a paste cannot throw on them.
    private static Dictionary<string, Matrix4> ReadWorlds(object? worlds, List<SceneObject> objects)
    {
        var read = new Dictionary<string, Matrix4>();
        if (worlds is not IReadOnlyDictionary<string, object?> map)
        {
            return read;
        }

        foreach (var (id, values) in map)
        {
            if (values is IReadOnlyList<object?> { Count: 16 } list
                && list.All(value => value is long or double)
                && objects.Any(one => one.Id == id))
            {
                read[id] = Matrix4.FromList([.. list.Select(value => Convert.ToDouble(value, CultureInfo.InvariantCulture))]);
            }
        }

        return read;
    }
}

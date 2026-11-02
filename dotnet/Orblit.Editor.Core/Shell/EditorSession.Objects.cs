namespace Orblit.Editor.Core;

using Doc = Orblit.Scene;
using Orblit.Mesh;

/// <summary>Where a dragged object lands: a scene, a parent in it (null for the top) and a place among its children.</summary>
public sealed record Drop(string SceneId, string? ParentId, int Index);

// The objects inside a scene: added, deleted, reparented, copied and selected.
public sealed partial class EditorSession
{
    private readonly SceneClipboard _clipboard = new();
    private int _nextObject;

    /// <summary>Where a copy goes so another window can paste it. Memory only until a window gives the system's.</summary>
    public ITextClipboard TextClipboard { get; init; } = new InMemoryTextClipboard();

    /// <summary>Where new object ids get their time from.</summary>
    public TimeProvider Clock { get; init; } = TimeProvider.System;

    /// <summary>What a paste would put in, for a menu to name without reading the system clipboard.</summary>
    public string ClipboardLabel => _clipboard.Description;

    public string CutLabel => Selected.Count > 1 ? $"Cut {Selected.Count} objects" : "Cut";

    public string CopyLabel => Selected.Count > 1 ? $"Copy {Selected.Count} objects" : "Copy";

    public string PasteLabel => _clipboard.IsEmpty ? "Paste" : $"Paste {ClipboardLabel}";

    /// <summary>
    /// The scene an edit goes into, which is not always the loaded one.
    /// <para>
    /// The shared set is always there and never the loaded scene. Something selected in it is being
    /// edited in it, so every edit that reaches for "the scene" has to mean that one, or the shared set
    /// becomes a place things can only be built, never copied, pasted or dragged into.
    /// </para>
    /// </summary>
    public SceneEntry? Working =>
        (Primary is { } primary ? Workspace.SceneHolding(primary) : null) ?? Workspace.Loaded;

    public void Add(ObjectKind kind)
    {
        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, kind.ToString()),
            kind: kind,
            colour: new Colour(kind == ObjectKind.Light ? 0xFFFFF3E0 : 0xFFD9634F));

        // Inside whatever is selected when that can hold things, which is what somebody building a
        // hierarchy means by add most of the time.
        var selected = Primary is { } primary ? scene[primary] : null;
        var parent = selected is null ? null : selected.Kind == ObjectKind.Group ? selected.Id : selected.ParentId;
        if (Run(new AddObject(added, open.Id, parent)))
        {
            SelectObject(added.Id);
        }
    }

    /// <summary>
    /// Puts a shape in the scene, parametric to begin with: a width, a height and a depth until
    /// somebody pulls a face off it.
    /// </summary>
    /// <remarks>The geometry file is written when the viewport next builds the scene, so nothing is refreshed here.</remarks>
    public void AddShape(ShapeKind kind)
    {
        if (Working is not { Scene: { } scene } open)
        {
            Say("There is no scene loaded to add to.", SayLevel.Warning);
            return;
        }

        var added = new SceneObject(
            id: NextObjectId(),
            name: UniqueName(scene, kind.Label),
            kind: ObjectKind.Shape,
            shape: new Shape { Kind = kind },
            colour: new Colour(0xFF8E99A8));

        var selected = Primary is { } primary ? scene[primary] : null;
        if (Run(new AddObject(added, open.Id, selected?.Kind == ObjectKind.Group ? selected.Id : null)))
        {
            SelectObject(added.Id);
        }
    }

    /// <summary>Deletes one object, whatever is selected.</summary>
    public void Delete(string id)
    {
        if (Workspace.SceneHolding(id) is not { Scene: { } scene } open || scene[id] is not { } one)
        {
            return;
        }

        if (!Run(new DeleteObjects(open.Id, [id], one.Name)))
        {
            return;
        }

        var rest = Selected.Where(selected => selected != id).ToList();
        Choose(rest, Primary == id ? LastOf(rest) : Primary);
    }

    /// <summary>Deletes everything selected, as one step.</summary>
    public void DeleteSelection()
    {
        if (Working is not { Scene: { } scene } open || Selected.Count == 0)
        {
            return;
        }

        var ids = TreeOrder(scene).Where(Selected.Contains).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var what = ids.Count == 1 ? scene[ids[0]]?.Name ?? "object" : $"{ids.Count} objects";
        if (Run(new DeleteObjects(open.Id, ids, what)))
        {
            ClearSelection();
        }
    }

    public void ClearSelection() => Choose([], null);

    /// <summary>
    /// Selects an object. <paramref name="additive"/> toggles it in or out. <paramref name="range"/>
    /// takes everything between the anchor and it, in the order the tree is drawn, which is what
    /// somebody shift-clicking means and not the order the document holds.
    /// </summary>
    public void SelectObject(string id, bool additive = false, bool range = false)
    {
        if (Workspace.Loaded?.Scene is not { } scene)
        {
            return;
        }

        if (range && Primary is { } anchor && Between(TreeOrder(scene), anchor, id) is { } between)
        {
            Choose([.. Selected.Union(between)], id);
        }
        else if (additive && Selected.Contains(id))
        {
            var rest = Selected.Where(selected => selected != id).ToList();
            Choose(rest, Primary == id ? LastOf(rest) : Primary);
        }
        else if (additive)
        {
            Choose([.. Selected, id], id);
        }
        else
        {
            Choose([id], id);
        }
    }

    /// <summary>Moves an object in the tree, by reparenting, reordering or both.</summary>
    public void Move(string id, Drop drop)
    {
        if (Workspace.SceneHolding(id) is not { Scene: { } scene } open || scene[id] is not { } one)
        {
            return;
        }

        // A part is the prefab's, and can be rearranged only inside the instance it belongs to.
        // Anywhere else it would stop being a part, and that is what unpacking is for.
        if (Doc.EntityPath.InstanceOf(id) is { } instance && !StaysInside(instance, open.Id, drop))
        {
            Say($"{one.Name} is part of {scene[instance]?.Name ?? instance}. Unpack that to move its parts out of it.", SayLevel.Info);
            return;
        }

        // Onto a different scene's row, the shared set most often. One scene loses the subtree and
        // another gains it, so it is its own operation and not a refusal.
        if (drop.SceneId != open.Id)
        {
            Run(new MoveBetweenScenes(open.Id, drop.SceneId, id, one.Name, drop.ParentId, drop.Index));
            return;
        }

        MoveWithin(open.Id, scene, one, drop);
    }

    private void MoveWithin(string sceneId, EditorScene scene, SceneObject one, Drop drop)
    {
        var fromIndex = scene.IndexOf(one.Id);
        var toIndex = drop.Index;
        var sameParent = one.ParentId == drop.ParentId;

        // Taking it out first moves everything after it up by one, so an index read from the tree as
        // drawn is one too many when moving down.
        if (sameParent && fromIndex < toIndex)
        {
            toIndex -= 1;
        }

        if (!sameParent || fromIndex != toIndex)
        {
            Run(new MoveObject(sceneId, one.Id, one.Name, one.ParentId, drop.ParentId, fromIndex, toIndex));
        }
    }

    /// <summary>Copy, for a menu or a key, which cannot wait on a clipboard.</summary>
    public void Copy() => Start(CopyAsync);

    public void Cut() => Start(CutAsync);

    public void Paste() => Start(PasteAsync);

    /// <summary>Puts the selection on the clipboard, and on the system's as text.</summary>
    public async Task CopyAsync()
    {
        if (Working?.Scene is not { } scene || Selected.Count == 0)
        {
            return;
        }

        AtRest(() => _clipboard.Take(scene, Selected));
        Changed?.Invoke();
        await TextClipboard.SetTextAsync(_clipboard.ToText());
        Say($"Copied {_clipboard.Description}.", SayLevel.Info);
    }

    /// <summary>Copies the selection and then removes it.</summary>
    public async Task CutAsync()
    {
        if (Working?.Scene is not { } scene || Selected.Count == 0)
        {
            return;
        }

        // Copied before it is deleted, since the delete is what makes it unreachable.
        AtRest(() => _clipboard.Take(scene, Selected));
        await TextClipboard.SetTextAsync(_clipboard.ToText());
        DeleteSelection();
    }

    /// <summary>
    /// Puts the clipboard into the scene being worked in, beside whatever is selected and not inside
    /// it. Pasting into the thing somebody was looking at buries it one level down.
    /// </summary>
    public async Task PasteAsync()
    {
        if (Working is not { Scene: { } scene } open)
        {
            return;
        }

        // The system clipboard first, so a copy from another window wins over what this one did last.
        _clipboard.TakeText(await TextClipboard.GetTextAsync());
        if (_clipboard.IsEmpty)
        {
            Say("There is nothing on the clipboard to paste.", SayLevel.Warning);
            return;
        }

        var beside = Primary is { } primary ? scene[primary] : null;
        Paste(open, _clipboard, beside?.ParentId);
    }

    /// <summary>Copies the selection and pastes it straight back.</summary>
    public void Duplicate()
    {
        if (Working is not { Scene: { } scene } open || Selected.Count == 0)
        {
            return;
        }

        // On its own clipboard, so duplicating does not throw away what somebody copied earlier.
        var taken = AtRest(() =>
        {
            var held = new SceneClipboard();
            held.Take(scene, Selected);
            return held;
        });
        Paste(open, taken, Primary is { } primary ? scene[primary]?.ParentId : null);
    }

    private void Paste(SceneEntry open, SceneClipboard taken, string? parentId)
    {
        var content = taken.Contents(NextObjectId, parentId);
        if (Run(new PasteObjects(open.Id, content.Objects, content.Roots, taken.Description, content.Worlds)))
        {
            Choose(content.Roots, LastOf(content.Roots));
        }
    }

    // Nothing awaits a menu click, so a clipboard that fails is said here and not left unobserved.
    private async void Start(Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or UnauthorizedAccessException or System.Runtime.InteropServices.ExternalException)
        {
            Say($"The clipboard could not be used: {error.Message}", SayLevel.Error);
        }
    }

    // Answers whether the command ran, so what follows an add or a delete is not done to a scene that
    // never changed.
    private bool Run(EditorCommand command)
    {
        var refused = History.Refuses(command);
        try
        {
            History.Run(command);
        }
        catch (SceneException error)
        {
            Say(error.Message, SayLevel.Error);
            return false;
        }

        return !refused;
    }

    private static string? LastOf(IReadOnlyList<string> ids) => ids.Count == 0 ? null : ids[^1];

    // The time in microseconds and a count, so two objects made in one tick still differ.
    private string NextObjectId() => $"o{Clock.GetUtcNow().Ticks / 10}_{_nextObject++}";

    private static string UniqueName(EditorScene scene, string baseName)
    {
        var taken = scene.Objects.Select(one => one.Name).ToHashSet();
        return Enumerable.Range(1, int.MaxValue)
            .Select(at => at == 1 ? baseName : $"{baseName} {at}")
            .First(name => !taken.Contains(name));
    }

    // Object ids in the order the tree draws them.
    private static List<string> TreeOrder(EditorScene scene)
    {
        var order = new List<string>();
        Walk(scene.Roots);
        return order;

        void Walk(IReadOnlyList<SceneObject> objects)
        {
            foreach (var one in objects)
            {
                order.Add(one.Id);
                Walk(scene.ChildrenOf(one.Id));
            }
        }
    }

    // Everything from one id to the other, either way round. Null when either is not in the order.
    private static List<string>? Between(List<string> order, string anchor, string id)
    {
        var from = order.IndexOf(anchor);
        var to = order.IndexOf(id);
        return from < 0 || to < 0 ? null : order.GetRange(Math.Min(from, to), Math.Abs(to - from) + 1);
    }

    private static bool StaysInside(string instance, string sceneId, Drop drop) =>
        drop.SceneId == sceneId && drop.ParentId is { } parent && Doc.EntityPath.Within(instance, parent);
}

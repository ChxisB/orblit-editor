namespace Orblit.Editor.Core;

using Orblit.Ui;

/// <summary>
/// The interfaces being laid out, and how the one shown is being looked at.
/// </summary>
/// <remarks>
/// A model rather than state inside a panel: the canvas, the element tree and the design panel all
/// read it, a panel can be closed and opened again without losing the selection, and each edit is a
/// step on the one undo stack the rest of the editor uses.
/// </remarks>
public sealed class InterfaceBench
{
    private readonly History _history;
    private readonly string? _stringsFolder;
    private readonly List<OpenInterface> _open = [];
    private string? _shown;
    private IReadOnlyList<int>? _selected = [];
    private IReadOnlyList<int>? _hovered;
    private bool _previewing;
    private bool _outlines = true;
    private bool _guides = true;
    private bool _columns;
    private (double Width, double Height)? _device;
    private bool _landscape;

    // The drag in progress: what ties its frames into one step, and what it is moving.
    private (object Gesture, IReadOnlyList<int> Path)? _drag;

    /// <param name="history">The undo stack every edit goes through.</param>
    /// <param name="stringsFolder">
    /// Where the project keeps its language files, one <c>&lt;locale&gt;.json</c> each, or null when
    /// there is no project to read them from.
    /// </param>
    public InterfaceBench(History history, string? stringsFolder = null)
    {
        _history = history;
        _stringsFolder = stringsFolder;
        Strings.Changed += () => Changed?.Invoke();
    }

    public event Action? Changed;

    public History History => _history;

    /// <summary>
    /// The words of every language the project has, and the one shown. A view like the device: the
    /// language being looked at is not saved in the file.
    /// </summary>
    public UiStrings Strings { get; } = new();

    /// <summary>The language files that could not be read, and why.</summary>
    public IReadOnlyList<string> StringProblems { get; private set; } = [];

    /// <summary>The keys each language has no text for, counting those the shown interface asks for.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Missing => Strings.Gaps(Document?.Root.TextKeys);

    public IReadOnlyList<OpenInterface> Open => [.. _open];

    public OpenInterface? this[string path]
    {
        get
        {
            var key = Path.GetFullPath(path);
            return _open.Find(one => one.Path == key);
        }
    }

    /// <summary>The interface the workspace shows, or null.</summary>
    public OpenInterface? Shown => _shown is null ? null : this[_shown];

    public UiDocument? Document => Shown?.Document;

    /// <summary>The selected element's path from the root, or null for none.</summary>
    public IReadOnlyList<int>? Selected
    {
        get => _selected;
        set => SetPath(ref _selected, value);
    }

    public IReadOnlyList<int>? Hovered
    {
        get => _hovered;
        set => SetPath(ref _hovered, value);
    }

    public UiNode? Element => _selected is null ? null : Document?.Root.At(_selected);

    /// <summary>Whether the canvas shows what the game shows, with nothing drawn over it and nothing to select.</summary>
    public bool Previewing
    {
        get => _previewing;
        set => Set(ref _previewing, value);
    }

    public bool Outlines
    {
        get => _outlines;
        set => Set(ref _outlines, value);
    }

    public bool Guides
    {
        get => _guides;
        set => Set(ref _guides, value);
    }

    /// <summary>Whether the column grid is drawn.</summary>
    public bool Columns
    {
        get => _columns;
        set => Set(ref _columns, value);
    }

    /// <summary>
    /// The device being previewed, upright, or null for the canvas's own size.
    /// </summary>
    /// <remarks>
    /// Not part of the document. Which device somebody is looking at while they work is a view, the
    /// same way the outlines are. Saving it would mean two people opening the same file and
    /// disagreeing about what it is.
    /// </remarks>
    public (double Width, double Height)? Device
    {
        get => _device;
        set => Set(ref _device, value);
    }

    /// <summary>
    /// Whether it is being held sideways.
    /// </summary>
    /// <remarks>
    /// Kept apart from the device rather than folded into it, so turning a phone on its side and then
    /// picking a tablet gives a tablet on its side. A single flipped size would forget which way up
    /// somebody was working.
    /// </remarks>
    public bool Landscape
    {
        get => _landscape;
        set => Set(ref _landscape, value);
    }

    /// <summary>The screen to lay out on: the device the way it is being held, or null for the canvas's own size.</summary>
    public (double Width, double Height)? Preview
    {
        get
        {
            if (Document is not { } document || (_device is null && !_landscape))
            {
                return null;
            }

            var (width, height) = _device ?? (document.Canvas.Width, document.Canvas.Height);
            return _landscape ? (height, width) : (width, height);
        }
    }

    /// <summary>
    /// The size the shown interface is laid out at, or null with nothing shown.
    /// </summary>
    /// <remarks>
    /// The screen's only when the canvas follows the screen. A fixed canvas is one layout at its own
    /// size, and the fit then magnifies it, so a phone preview of it answers to no breakpoint.
    /// </remarks>
    public (double Width, double Height)? LaidOut => Document?.Canvas is not { } canvas ? null
        : canvas.Fit == CanvasFit.Responsive ? Preview ?? (canvas.Width, canvas.Height) : (canvas.Width, canvas.Height);

    /// <summary>Whether two element paths name the same element. Both null counts as the same.</summary>
    public static bool SamePath(IReadOnlyList<int>? a, IReadOnlyList<int>? b) =>
        a is null || b is null ? a is null && b is null : a.SequenceEqual(b);

    /// <summary>
    /// Reads the language files again, in place of the tables held.
    /// </summary>
    /// <remarks>
    /// The tables are emptied first, so a key somebody took out of a file is gone from the canvas
    /// too. The one object stays, because the scene view and the game view hold it. The language
    /// being shown carries over, or is the first one the project has when it has none of that name.
    /// </remarks>
    public void LoadStrings()
    {
        var (sources, problems) = ReadLanguages(_stringsFolder);
        Strings.Clear();
        foreach (var (locale, source) in sources)
        {
            try
            {
                Strings.AddJson(locale, source);
            }
            catch (FormatException)
            {
                problems.Add($"{locale}.json is not one object of key to text.");
            }
        }

        StringProblems = problems;
        var has = Strings.Locales;
        if (has.Count > 0 && !has.Contains(Strings.Locale))
        {
            Strings.Use(has[0]);
        }

        Changed?.Invoke();
    }

    /// <summary>Shows the interface at <paramref name="path"/>, reading it first if it is not open, and says why it could not be.</summary>
    public string? OpenFile(string path)
    {
        LoadStrings();
        if (this[path] is not null)
        {
            Show(path);
            return null;
        }

        var name = Path.GetFileName(path);
        UiDocument? document;
        try
        {
            document = UiDocument.Read(File.ReadAllText(path));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return $"{name} is not in the project any more.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"{name} could not be read: {error.Message}";
        }

        if (document is null)
        {
            return $"{name} is not a readable interface.";
        }

        _open.Add(new OpenInterface(Path.GetFullPath(path), document));
        Show(path);
        return null;
    }

    public void Show(string path)
    {
        var key = Path.GetFullPath(path);
        if (_shown == key || this[key] is null)
        {
            return;
        }

        _shown = key;
        _selected = [];
        _hovered = null;
        Changed?.Invoke();
    }

    public bool IsUnsaved(OpenInterface open) => _history.StampFor(open.Id) != open.SavedStamp;

    public bool AnyUnsaved => _open.Exists(IsUnsaved);

    /// <summary>Writes every interface with changes, and says which could not be written.</summary>
    public IReadOnlyList<string> SaveAll() => [.. _open.Where(IsUnsaved).ToList().Select(Save).OfType<string>()];

    /// <summary>Writes <paramref name="open"/>, or says why it could not be.</summary>
    public string? Save(OpenInterface open)
    {
        try
        {
            File.WriteAllText(open.Path, open.Document.ToText());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return $"Could not save {open.Name}: {error.Message}";
        }

        open.SavedStamp = _history.StampFor(open.Id);
        Changed?.Invoke();
        return null;
    }

    /// <summary>Ends a run of edits that merge, such as the words typed into one field or one drag of a slider.</summary>
    public void Settle() => _history.Seal();

    /// <summary>
    /// Changes the shown interface through the undo stack.
    /// </summary>
    /// <remarks>
    /// A change that answers the document it was given changes nothing and is not a step.
    /// </remarks>
    public void Edit(string label, Func<UiDocument, UiDocument> change, InterfaceEditOptions options = default)
    {
        if (Shown is not { } shown)
        {
            return;
        }

        var to = change(shown.Document);
        if (ReferenceEquals(to, shown.Document))
        {
            return;
        }

        _history.Run(new InterfaceEdit(this, shown.Path, label, (shown.Document, _selected), to, options));
    }

    /// <summary>
    /// Changes the selected element.
    /// </summary>
    /// <remarks>
    /// A gesture ties the edits into one step while they are made to the same element, so the words
    /// typed into a field are undone together rather than a letter at a time.
    /// </remarks>
    public void EditElement(string label, Func<UiNode, UiNode> change, object? gesture = null)
    {
        if (_selected is not { } path || Element is not { } element)
        {
            return;
        }

        Edit(
            $"{label} on {UiElements.NameOf(element)}",
            document => document with { Root = document.Root.ReplaceAt(path, change(element)) },
            new InterfaceEditOptions(Gesture: gesture is null ? null : (gesture, string.Join('.', path))));
    }

    public void SetCanvas(string label, UiCanvas canvas, object? gesture = null) =>
        Edit(label, document => document with { Canvas = canvas }, new InterfaceEditOptions(Gesture: gesture));

    /// <summary>
    /// Adds <paramref name="what"/> inside the selection, which is what "add" means when something is
    /// selected and there is nowhere else it could sensibly go.
    /// </summary>
    public void Add(UiElement what)
    {
        var into = _selected ?? [];
        if (Document?.Root.At(into) is not { } parent)
        {
            return;
        }

        var made = what.Make();
        // Placed rather than dropped at the origin when its parent does not lay it out: everything
        // added to a stack landing in the same corner and on top of the last one is a pile.
        if (parent.Type == "stack")
        {
            var at = 48.0 + (parent.Children.Count * 24);
            made = made.PlaceAt(at, at);
        }

        // A container is a decision about where things line up, and the grid is what they line up
        // against. Being shown nothing to put it against sends somebody looking for the setting.
        if (what == UiElement.Column || what == UiElement.Row)
        {
            _columns = true;
        }

        var count = parent.Children.Count;
        Edit(
            $"Add {what.Label}",
            document => document with { Root = document.Root.InsertAt(into, count, made) },
            new InterfaceEditOptions(Select: [.. into, count]));
    }

    /// <summary>
    /// Turns the selected container into a row of equal columns.
    /// </summary>
    /// <remarks>
    /// The one operation a grid is for. Whatever was inside goes into the first column rather than
    /// being thrown away or spread out. Somebody splitting a screen in two has content on it already,
    /// and a split that emptied it would be a split nobody could use twice.
    /// </remarks>
    public void Split(int count)
    {
        if (_selected is not { } path || Element is not { } element)
        {
            return;
        }

        // Stripped of their places on the way in. A child that keeps left and top is laid out as
        // placed, and a placed child that is no longer in a stack takes the canvas with it.
        IReadOnlyList<UiNode> kept = [.. element.Children.Select(static child => child.Unplaced)];
        var split = element with
        {
            Type = "row",
            // Stacked until there is room to sit side by side. Four columns across a phone are four
            // columns nobody can read.
            Classes = $"{UiElements.Flowing(element.Classes)} col md:row".Trim(),
            // Equal widths only where they are widths. Stacked, flex-1 would divide the height instead.
            Children = [.. Enumerable.Range(0, count).Select(i => new UiNode
            {
                Type = "column",
                Classes = "md:flex-1 gap-2",
                Children = i == 0 ? kept : [],
            })],
        };

        _columns = true;
        Edit(
            $"Split {UiElements.NameOf(element)} into {count} columns",
            document => document with { Root = document.Root.ReplaceAt(path, Spanning(split)) });
    }

    /// <summary>
    /// Moves an element while it is being dragged.
    /// </summary>
    /// <remarks>
    /// The whole drag is one step. Without that an undo would walk back through every frame of the
    /// movement. The fraction is kept while the pointer is down: rounding every frame throws away a
    /// fraction of a pixel each time, and a slow drag ends up behind the pointer.
    /// </remarks>
    public void Move(IReadOnlyList<int> path, (double Left, double Top) to)
    {
        if (Document?.Root.At(path) is not { } element)
        {
            return;
        }

        _drag ??= (new object(), path);
        Edit(
            $"Move {UiElements.NameOf(element)}",
            document => document with { Root = document.Root.ReplaceAt(path, element.PlaceAt(to.Left, to.Top, round: false)) },
            new InterfaceEditOptions(Gesture: _drag.Value.Gesture, OnlyMoves: true));
    }

    /// <summary>
    /// Lands the dragged element on a whole pixel, as the end of the drag's one step.
    /// </summary>
    /// <remarks>
    /// A file full of positions to fourteen decimal places is a file whose diff is unreadable, and
    /// the difference is not visible on any screen.
    /// </remarks>
    public void MoveDone()
    {
        var drag = _drag;
        _drag = null;
        if (drag is { } held && Document?.Root.At(held.Path) is { Placed: { } at } element)
        {
            Edit(
                $"Move {UiElements.NameOf(element)}",
                document => document with { Root = document.Root.ReplaceAt(held.Path, element.PlaceAt(at.Left, at.Top)) },
                new InterfaceEditOptions(Gesture: held.Gesture, OnlyMoves: true));
        }

        _history.Seal();
    }

    public void Remove(IReadOnlyList<int> path)
    {
        if (path.Count == 0 || Document?.Root.At(path) is not { } element)
        {
            return;
        }

        Edit(
            $"Remove {UiElements.NameOf(element)}",
            document => document with { Root = document.Root.RemoveAt(path) },
            new InterfaceEditOptions(Select: [.. path.Take(path.Count - 1)]));
    }

    public void RemoveSelected()
    {
        if (_selected is { } path)
        {
            Remove(path);
        }
    }

    /// <summary>
    /// Where an edit puts an interface, forwards or back. Shows the interface it puts, so undoing a
    /// step in one somebody has moved away from is not a change they cannot see.
    /// </summary>
    internal void Put(string path, UiDocument document, IReadOnlyList<int>? selected)
    {
        if (this[path] is not { } open)
        {
            return;
        }

        open.Document = document;
        if (_shown != path)
        {
            _shown = path;
            _hovered = null;
        }

        // The selection can be left pointing at something that has gone.
        _selected = selected is not null && document.Root.At(selected) is null ? [] : selected;
        if (_hovered is { } hovered && document.Root.At(hovered) is null)
        {
            _hovered = null;
        }

        Changed?.Invoke();
    }

    /// <summary>
    /// A placed element given a width to divide.
    /// </summary>
    /// <remarks>
    /// Something dropped on a stack has a left and nothing else, which leaves its width unbounded.
    /// Equal columns of an unbounded width are the reason the canvas went blank. Reaching the far edge
    /// is the honest reading of "split this into columns". Anything that already says how wide it is
    /// keeps what it says.
    /// </remarks>
    private static UiNode Spanning(UiNode node)
    {
        if (node.Placed is null)
        {
            return node;
        }

        var style = new UiUtilities().Parse(node.Classes).Merge(new UiCss().Parse(node.Css));
        if (style.Width is not null || style.Right is not null)
        {
            return node;
        }

        var rest = node.Css.Split(';').Select(static one => one.Trim()).Where(static one => one.Length > 0);
        return node with { Css = string.Join("; ", [.. rest, "right: 0"]) };
    }

    /// <summary>The text of each <c>.json</c> file in <paramref name="folder"/>, by the language it is named for, and which could not be read.</summary>
    private static (List<(string Locale, string Source)> Sources, List<string> Problems) ReadLanguages(string? folder)
    {
        var sources = new List<(string, string)>();
        var problems = new List<string>();
        if (folder is null || !Directory.Exists(folder))
        {
            return (sources, problems);
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.json");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            problems.Add($"strings/ could not be listed: {error.Message}");
            return (sources, problems);
        }

        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            try
            {
                sources.Add((Path.GetFileNameWithoutExtension(file), File.ReadAllText(file)));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                problems.Add($"{Path.GetFileName(file)} could not be read: {error.Message}");
            }
        }

        return (sources, problems);
    }

    private void Set<T>(ref T slot, T value)
    {
        if (EqualityComparer<T>.Default.Equals(slot, value))
        {
            return;
        }

        slot = value;
        Changed?.Invoke();
    }

    private void SetPath(ref IReadOnlyList<int>? slot, IReadOnlyList<int>? value)
    {
        if (SamePath(slot, value))
        {
            return;
        }

        slot = value;
        Changed?.Invoke();
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>
/// Asks which model a clip was made for, which one it should move and how their bones pair, and answers the
/// clip made from that.
/// </summary>
public sealed class RetargetDialog : Window
{
    private const string Ask = "Choose the model the clip was made for and the one it should move.";

    private readonly ClipDocument _clip;
    private readonly IRetargetFiles _files;
    private readonly IReadOnlyList<string> _models;
    private readonly IReadOnlyList<string> _maps;
    private readonly Dictionary<string, RestSkeleton?> _skeletons = [];

    private readonly StackPanel _top = new() { Spacing = Space.Sm };
    private readonly StackPanel _list = new();
    private readonly TextBlock _hint = Caption(OrblitColors.InkDim);
    private readonly TextBlock _summary = Caption(OrblitColors.InkDim);
    private readonly TextBlock _problems = Caption(OrblitColors.Warn);
    private readonly TextBlock _noteText = Caption(OrblitColors.InkMid);
    private readonly Grid _pairs = new() { RowDefinitions = new RowDefinitions("Auto,*") };
    private readonly Button _make = new() { Name = "make", Content = "Make clip" };

    private RetargetDraft _draft;
    private PairTable? _table;
    private string? _note;

    /// <param name="clip">The clip to move onto another model.</param>
    /// <param name="files">The models and bone maps there are, and where a map is kept.</param>
    /// <param name="from">The model the clip is most likely for, or null.</param>
    public RetargetDialog(ClipDocument clip, IRetargetFiles files, string? from = null)
    {
        _clip = clip;
        _files = files;
        _models = files.Models;
        _maps = files.Maps;
        _draft = new RetargetDraft(from, null, new BoneMap());
        _table = TableFor(_draft);

        Title = $"Retarget {clip.Name}";
        Width = 680;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        Content = Layout();
        Render();
    }

    /// <summary>Asks, and returns the clip made, or null when it was cancelled.</summary>
    public static Task<RetargetMade?> Choose(Window owner, ClipDocument clip, IRetargetFiles files, string? from = null) =>
        new RetargetDialog(clip, files, from).ShowDialog<RetargetMade?>(owner);

    private static TextBlock Caption(Avalonia.Media.IBrush colour) => new()
    {
        FontSize = OrblitText.Caption,
        Foreground = colour,
        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
    };

    private Grid Layout()
    {
        _make.Click += (_, _) => Make();
        var cancel = new Button { Name = "cancel", Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(_make);

        _hint.TextAlignment = Avalonia.Media.TextAlignment.Center;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _pairs.Children.Add(_summary);
        var scroll = new ScrollViewer { Content = _list, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1);
        _pairs.Children.Add(scroll);
        var body = new Grid { Children = { _hint, _pairs } };

        var rows = new Grid
        {
            Margin = new Thickness(Space.Lg),
            RowSpacing = Space.Sm,
            RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"),
        };
        Place(rows, _top, 0);
        Place(rows, body, 1);
        Place(rows, _problems, 2);
        Place(rows, _noteText, 3);
        Place(rows, buttons, 4);
        return rows;
    }

    private static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private RestSkeleton? Skeleton(string? model)
    {
        if (model is null)
        {
            return null;
        }

        if (!_skeletons.TryGetValue(model, out var known))
        {
            known = _files.SkeletonOf(model);
            _skeletons[model] = known;
        }

        return known;
    }

    private PairTable? TableFor(RetargetDraft draft) =>
        Skeleton(draft.From) is { } from && Skeleton(draft.To) is { } to ? PairTable.Of(from, to, draft.Map) : null;

    private string? Missing(string? model) =>
        model is not null && Skeleton(model) is null ? $"{Path.GetFileName(model)} has no skeleton." : null;

    private void Update(RetargetDraft draft, string? note = null)
    {
        _draft = draft;
        _table = TableFor(draft);
        _note = note;
        Render();
    }

    private void Change(BoneMap map) => Update(_draft with { Map = map });

    private void Load(string path)
    {
        BoneMapLoad loaded;
        try
        {
            loaded = _files.ReadMap(path);
        }
        catch (BoneMapFormatException error)
        {
            _note = $"{Path.GetFileName(path)}: {error.Message}";
            Render();
            return;
        }

        var map = loaded.Map;
        var file = Path.GetFileName(path);
        Update(
            new RetargetDraft(
                _models.Contains(map.FromAsset) ? map.FromAsset : _draft.From,
                _models.Contains(map.ToAsset) ? map.ToAsset : _draft.To,
                map),
            loaded.Problems.Count == 0
                ? $"Loaded {file}."
                : $"{loaded.Problems.Count} things in {file} could not be read. First: {loaded.Problems[0]}");
    }

    private void Save()
    {
        if (_draft is not { From: { } from, To: { } to })
        {
            return;
        }

        var name = $"{Path.GetFileNameWithoutExtension(from)}-to-{Path.GetFileNameWithoutExtension(to)}";
        var made = _files.WriteMap(name, _draft.Map.CopyWith(fromAsset: from, toAsset: to));
        _note = made.Path is null ? $"Could not save the map: {made.Problem}" : $"Saved {made.Path}.";
        Render();
    }

    private void Make()
    {
        if (_table is not { } table || Skeleton(_draft.From) is not { } from || Skeleton(_draft.To) is not { } to || _draft.To is not { } model)
        {
            return;
        }

        var made = table.Map.Apply(_clip, from, to);
        Close(new RetargetMade(made.Clip.CopyWith(name: RetargetWords.NameFor(_clip.Name, model)), model, made.Problems));
    }

    private void Render()
    {
        _top.Children.Clear();
        _top.Children.Add(Models());
        _top.Children.Add(Switches());
        _top.Children.Add(MapBar());

        _make.IsEnabled = _table is not null;
        _hint.Text = Missing(_draft.From) ?? Missing(_draft.To) ?? Ask;
        _hint.IsVisible = _table is null;
        _pairs.IsVisible = _table is not null;
        _noteText.Text = _note;
        _noteText.IsVisible = _note is not null;

        // The list keeps its scroll while its rows are replaced, so a choice low in a long skeleton stays in view.
        _list.Children.Clear();
        var problems = "";
        if (_table is { } table && Skeleton(_draft.From) is { } from)
        {
            _summary.Text = $"{table.Driven} of {table.Rows.Count} bones are moved by something.";
            _list.Children.AddRange(RetargetPairs.Rows(table, from.Names, Change));
            problems = RetargetPairs.Problems(table);
        }

        _problems.Text = problems;
        _problems.IsVisible = problems.Length > 0;
    }

    private Grid Models()
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        var arrow = new TextBlock
        {
            Text = "→",
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.Sm, 0),
        };
        Grid.SetColumn(arrow, 1);
        var to = Labelled("Move", ModelPick("models_to", _draft.To, model => _draft with { To = model }));
        Grid.SetColumn(to, 2);
        row.Children.Add(Labelled("Made for", ModelPick("models_from", _draft.From, model => _draft with { From = model })));
        row.Children.Add(arrow);
        row.Children.Add(to);
        return row;
    }

    private Button ModelPick(string name, string? current, Func<string, RetargetDraft> chosen) =>
        Dropdown.Of(name, current ?? "Choose a model", () => _models.Select(model => (model, (Action)(() => Update(chosen(model))))));

    private static Grid Labelled(string label, Control child)
    {
        var text = new TextBlock
        {
            Text = label,
            FontSize = OrblitText.Caption,
            Foreground = OrblitColors.InkMid,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(child, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("70,*"), Children = { text, child } };
    }

    private StackPanel Switches()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm };
        row.Children.Add(new OrblitSwitch("Pair by the parts of a person", _draft.Map.Humanoid, on => Change(_draft.Map.CopyWith(humanoid: on))) { Name = "switch_humanoid" });
        row.Children.Add(Word("Pair by body", Space.Lg));
        row.Children.Add(new OrblitSwitch("Turn each bone to point the way its driver does at rest", _draft.Map.FixRest, on => Change(_draft.Map.CopyWith(fixRest: on))) { Name = "switch_fix_rest" });
        row.Children.Add(Word("Fix rest pose", 0));
        return row;
    }

    private static TextBlock Word(string text, double after) => new()
    {
        Text = text,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkMid,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(0, 0, after, 0),
    };

    private Grid MapBar()
    {
        var pick = Dropdown.Of(
            "bone_map",
            _maps.Count == 0 ? "None saved yet" : "Load a saved map",
            () => _maps.Select(map => (map, (Action)(() => Load(map)))));
        var save = new Button { Name = "save_map", Content = "Save map", IsEnabled = _table is not null, Margin = new Thickness(Space.Sm, 0, 0, 0) };
        save.Click += (_, _) => Save();
        Grid.SetColumn(save, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Labelled("Bone map", pick), save } };
    }
}

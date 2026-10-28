using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>The kits of one store: words and filters above, a grid of cards below.</summary>
/// <remarks>The search box lives as long as the dialog, so the words typed are kept while a kit is open.</remarks>
internal sealed class StoreBrowse : Grid
{
    private const int Columns = 4;
    private const string BoxIcon = "M4 7l8-4 8 4v10l-8 4-8-4zM4 7l8 4 8-4M12 11v10";

    private readonly StoreSession _session;
    private readonly WrapPanel _filters = new() { ItemSpacing = Space.Md, LineSpacing = Space.Xs };
    private readonly TextBlock _engineNote = Words(
        "This project does not say which engine it uses, so kits for any version are listed.", OrblitColors.InkDim);

    private readonly ContentControl _results = new();

    public StoreBrowse(StoreSession session)
    {
        _session = session;
        RowSpacing = Space.Sm;
        RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*");
        var search = new TextBox { Name = "search", PlaceholderText = "Search kits", Text = session.Query.Text };
        search.TextChanged += (_, _) => _session.SetQuery(_session.Query with { Text = search.Text ?? "" });
        Place(search, 0);
        Place(_filters, 1);
        Place(_engineNote, 2);
        Place(_results, 3);
    }

    public void Render()
    {
        _filters.Children.Clear();
        _filters.Children.AddRange(Filters());
        _engineNote.IsVisible = !_session.EngineKnown;
        _results.Content = Results();
    }

    private static TextBlock Words(string text, IBrush ink) => new()
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = ink,
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private void Place(Control child, int row)
    {
        SetRow(child, row);
        Children.Add(child);
    }

    private IEnumerable<Control> Filters()
    {
        var query = _session.Query;
        string[] licences = ["", .. StoreQuery.Licences(_session.Rows)];
        string[] tags = ["", .. StoreQuery.Tags(_session.Rows)];
        yield return Pick("sort", "Sort", query.Sort == StoreSort.Name ? "Name" : "Newest", [
            ("Newest", () => _session.SetQuery(query with { Sort = StoreSort.Updated })),
            ("Name", () => _session.SetQuery(query with { Sort = StoreSort.Name })),
        ]);
        yield return Pick("licence", "Licence", AnyOr(query.Licence, licences), [.. licences.Select(x => (AnyOr(x, licences), (Action)(() => _session.SetQuery(query with { Licence = x }))))]);
        yield return Pick("tag", "Tag", AnyOr(query.Tag, tags), [.. tags.Select(x => (AnyOr(x, tags), (Action)(() => _session.SetQuery(query with { Tag = x }))))]);
        yield return Toggle("hide_scripts", "Hide kits with scripts", query.HideScripts, x => _session.SetQuery(query with { HideScripts = x }));
        yield return Toggle("show_incompatible", "Show kits for other engines", _session.Settings.ShowIncompatible, _session.SetShowIncompatible);
    }

    // A licence or tag that left the list when the store changed reads as any.
    private static string AnyOr(string value, string[] options) => value.Length == 0 || !options.Contains(value) ? "Any" : value;

    private static StackPanel Pick(string name, string label, string shown, (string Text, Action Choose)[] options)
    {
        var pick = Dropdown.Of(name, shown, () => options);
        pick.MinWidth = 90;
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, Children = { Words(label, OrblitColors.InkMid), pick } };
    }

    private static StackPanel Toggle(string name, string label, bool value, Action<bool> onChanged) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = Space.Sm,
        Children = { new OrblitSwitch(label, value, onChanged) { Name = name }, Words(label, OrblitColors.InkMid) },
    };

    private Control Results()
    {
        if (_session.Loading)
        {
            return Message("Reading the store…");
        }

        if (_session.Problem is { } problem)
        {
            var again = new Button { Name = "try_again", Content = "Try again", HorizontalAlignment = HorizontalAlignment.Center };
            again.Click += async (_, _) => await _session.GoOnlineAsync();
            return new StackPanel { Spacing = Space.Md, VerticalAlignment = VerticalAlignment.Center, Children = { Message(problem), again } };
        }

        var shown = _session.Shown;
        if (shown.Count == 0)
        {
            return Message(_session.Rows.Count == 0 ? "This store lists no kits."
                : _session.Settings.ShowIncompatible ? "No kit matches."
                : "No kit matches. Kits for other engine versions are hidden.");
        }

        // A store can list hundreds of kits, so only the rows in view are made and fetch their pictures.
        return new ScrollViewer
        {
            Content = new ItemsControl
            {
                ItemsSource = shown.Select((row, index) => (row, index)).Chunk(Columns),
                ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
                ItemTemplate = new FuncDataTemplate<(KitRow Row, int Index)[]>((cards, _) => CardRow(cards)),
            },
        };
    }

    private static TextBlock Message(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkDim,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(Space.Xl),
    };

    private Grid CardRow((KitRow Row, int Index)[] cards)
    {
        var line = new Grid { Margin = new Thickness(0, 0, 0, Space.Md), ColumnSpacing = Space.Md, ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", Columns))) };
        foreach (var (row, index) in cards)
        {
            var card = Card(row, index);
            SetColumn(card, index % Columns);
            line.Children.Add(card);
        }

        return line;
    }

    private Button Card(KitRow row, int index)
    {
        var manifest = row.Manifest;
        var picture = new Border { Background = OrblitColors.LineSoft, ClipToBounds = true, Child = new Glyph(BoxIcon, 24, OrblitColors.InkDim) };
        StorePictures.Show(picture, _session.ThumbnailAsync(row), 400);
        var flags = new[] { manifest.ContainsScripts ? "Scripts" : null, _session.Fits(row) ? null : "Other engine" }.OfType<string>();
        var about = new StackPanel
        {
            Margin = new Thickness(Space.Sm),
            Children =
            {
                new TextBlock { Text = manifest.Name, FontSize = OrblitText.Label, Foreground = OrblitColors.Ink, TextTrimming = TextTrimming.CharacterEllipsis },
                Words($"{manifest.Publisher} · {manifest.Version}", OrblitColors.InkDim),
                Words(string.Join(" · ", flags), OrblitColors.Warn),
            },
        };
        SetRow(about, 1);
        var card = new Button
        {
            Name = $"kit_{index}",
            Height = 190,
            Padding = default,
            Background = OrblitColors.Raised,
            CornerRadius = Radii.Card,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Content = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { picture, about } },
        };
        card.Click += async (_, _) => await _session.SelectAsync(row);
        return card;
    }
}

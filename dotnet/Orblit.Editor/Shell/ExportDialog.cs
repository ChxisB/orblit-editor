using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>
/// Cooks the project into a pack for each chosen target, or a patch on a pack already exported, and shows
/// what the exports folder holds and what a pack in it serves.
/// </summary>
public sealed class ExportDialog : Window
{
    private const string PackedIcon = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM8 12l3 3 5-6";
    private const string UnchangedIcon = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM8 12h8";
    private const string FailedIcon = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM12 7v6M12 16.5v.01";

    private readonly ExportSession _session;
    private readonly StackPanel _body = new() { Spacing = Space.Sm };
    private readonly Button _exportView = new() { Name = "view_export", Content = "Export" };
    private readonly Button _contentsView = new() { Name = "view_contents", Content = "Contents" };
    private readonly Button _close = new() { Name = "close", Content = "Close", IsCancel = true };
    private bool _showingContents;

    /// <param name="session">What the dialog shows. The dialog disposes of it once closed.</param>
    /// <param name="onReveal">Shows a folder in the system's file browser.</param>
    public ExportDialog(ExportSession session, Action<string> onReveal)
    {
        _session = session;
        Title = "Export";
        Width = 720;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        Content = Layout(onReveal);
        session.Changed += Render;
        // The cook carries on with the dialog gone, and then nobody hears how it went. Quitting the editor
        // is still allowed, because then nobody is left to hear it either.
        Closing += (_, closing) => closing.Cancel = _session.Running && closing.CloseReason == WindowCloseReason.WindowClosing;
        Closed += (_, _) => _session.Dispose();
        Opened += async (_, _) => await _session.LoadAsync();
        Render();
    }

    /// <summary>Shows the dialog over <paramref name="owner"/> until it is closed.</summary>
    public static Task Open(Window owner, ExportSession session, Action<string> onReveal) =>
        new ExportDialog(session, onReveal).ShowDialog(owner);

    private static string Plural(int count, string noun) => $"{count} {noun}{(count == 1 ? "" : "s")}";

    private static TextBlock Caption(string text, IBrush ink) => new()
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = ink,
        TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Mono(string text, IBrush ink) => new()
    {
        Text = text,
        FontFamily = OrblitText.Mono,
        FontSize = OrblitText.Caption,
        Foreground = ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkMid,
        Margin = new Thickness(0, Space.Sm, 0, 0),
    };

    private static TextBlock Message(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkDim,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        Margin = new Thickness(Space.Xl),
    };

    private static void Place(Grid grid, Control child, int row)
    {
        Grid.SetRow(child, row);
        grid.Children.Add(child);
    }

    private Grid Layout(Action<string> onReveal)
    {
        _exportView.Click += (_, _) => Switch(contents: false);
        _contentsView.Click += (_, _) => Switch(contents: true);
        _close.Click += (_, _) => Close();
        var reveal = new Button { Name = "reveal", Content = "Show exports folder" };
        reveal.Click += (_, _) => onReveal(ExportFolder.PathOf(_session.ProjectDirectory));

        var title = new TextBlock { Text = "Export", FontSize = OrblitText.Body, Foreground = OrblitColors.Ink, VerticalAlignment = VerticalAlignment.Center };
        var views = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs, Children = { _exportView, _contentsView } };
        Grid.SetColumn(views, 1);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right, Children = { reveal, _close } };
        var rows = new Grid { Margin = new Thickness(Space.Lg), RowSpacing = Space.Md, RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Place(rows, new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { title, views } }, 0);
        Place(rows, new ScrollViewer { Content = _body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }, 1);
        Place(rows, actions, 2);
        return rows;
    }

    private void Switch(bool contents)
    {
        _showingContents = contents;
        Render();
    }

    private async void ShowContents(ExportedPack pack)
    {
        _showingContents = true;
        await _session.ShowAsync(pack);
    }

    private void Render()
    {
        _close.IsEnabled = !_session.Running;
        Mark(_exportView, !_showingContents);
        Mark(_contentsView, _showingContents);
        _body.Children.Clear();
        _body.Children.AddRange(_showingContents ? ContentsView() : ExportView());
    }

    // The view on show reads as a button, and the other as a quiet word beside it.
    private static void Mark(Button button, bool current)
    {
        button.Background = current ? OrblitColors.Raised : Brushes.Transparent;
        button.Foreground = current ? OrblitColors.Ink : OrblitColors.InkDim;
    }

    private List<Control> ExportView()
    {
        var patchOf = _session.PatchOf;
        var export = new Button { Name = "export", Content = patchOf is null ? "Export" : "Export patch", IsEnabled = _session.CanExport };
        export.Click += async (_, _) => await _session.StartAsync();
        var progress = Caption($"Cooking and packing {_session.Cooking}…", OrblitColors.InkMid);
        progress.IsVisible = _session.Cooking is not null;
        List<Control> view =
        [
            Section("Targets"),
            Targets(open: patchOf is null && !_session.Running),
            Section("Make"),
            BasePick(),
            Caption(Hint(patchOf), OrblitColors.InkDim),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Md, Margin = new Thickness(0, Space.Sm), Children = { export, progress } },
        ];
        view.AddRange(_session.Results.Count > 0 ? _session.Results.Select(ResultTile) : Folder());
        return view;
    }

    private WrapPanel Targets(bool open)
    {
        var chips = new WrapPanel { ItemSpacing = Space.Sm, LineSpacing = Space.Sm };
        var chosen = _session.Targets;
        foreach (var name in CookTargets.Names)
        {
            var chip = new ToggleButton { Name = $"target_{name}", Content = name, IsChecked = chosen.Contains(name), IsEnabled = open };
            chip.Click += (_, _) => _session.ToggleTarget(name);
            chips.Children.Add(chip);
        }

        return chips;
    }

    private Button BasePick()
    {
        var pick = Dropdown.Of(
            "patch_of",
            _session.PatchOf is { } patchOf ? $"A patch of {patchOf.FileName}" : "A whole pack",
            () => [("A whole pack", () => _session.ChoosePatchOf(null)), .. _session.Listing.Packs.Select(pack => ($"A patch of {pack.FileName}", (Action)(() => _session.ChoosePatchOf(pack))))]);
        pick.IsEnabled = !_session.Running;
        return pick;
    }

    private static string Hint(ExportedPack? patchOf) => patchOf is null
        ? "Everything in the project, as the one file an app ships."
        : $"Only what differs from {patchOf.FileName}, for {patchOf.Target}. Pick the newest patch when players already have the ones before.";

    private StackPanel ResultTile(TargetExport result, int index)
    {
        var (icon, ink) = result.Outcome switch
        {
            ExportOutcome.Packed => (PackedIcon, OrblitColors.Good),
            ExportOutcome.Unchanged => (UnchangedIcon, OrblitColors.InkDim),
            _ => (FailedIcon, OrblitColors.Bad),
        };
        var target = new TextBlock { Text = result.Target, FontSize = OrblitText.Body, Foreground = OrblitColors.Ink, VerticalAlignment = VerticalAlignment.Center };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, Children = { new Glyph(icon, 15, ink), target, Caption(Headline(result), OrblitColors.InkMid) } };
        if (result.Outcome == ExportOutcome.Packed)
        {
            var contents = new Button { Name = $"contents_{index}", Content = "Contents", Background = Brushes.Transparent };
            contents.Click += (_, _) =>
            {
                if (_session.Listing.Packs.FirstOrDefault(pack => pack.Path == result.Path) is { } pack)
                {
                    ShowContents(pack);
                }
            };
            head.Children.Add(contents);
        }

        var tile = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 0, Space.Md), Children = { head, Indented(Caption(Detail(result), OrblitColors.InkDim)) } };
        foreach (var failure in result.Failures)
        {
            tile.Children.Add(Indented(new SelectableTextBlock
            {
                Text = $"{failure.Id}: {failure.Reason}",
                FontFamily = OrblitText.Mono,
                FontSize = OrblitText.Caption,
                Foreground = OrblitColors.Bad,
                TextWrapping = TextWrapping.Wrap,
            }));
        }

        return tile;
    }

    // Under the words beside the mark, not under the mark.
    private static Control Indented(Control child)
    {
        child.Margin = new Thickness(23, 0, 0, 0);
        return child;
    }

    private static string Headline(TargetExport result)
    {
        if (result.Summary is not { } summary)
        {
            return result.Message;
        }

        var name = result.Path is { } path ? $"{Path.GetFileName(path)} · " : "";
        return $"{name}{Plural(summary.Assets, "asset")} · {SizeLabel.Of(summary.Bytes)}";
    }

    private static string Detail(TargetExport result)
    {
        if (result.Summary is not { } summary)
        {
            return result.Cooked.Length == 0 ? "" : $"Cook: {result.Cooked}.";
        }

        var patch = result.PatchOf is { } patchOf
            ? $"Patch of {patchOf}: {summary.Added} new, {summary.Replaced} replaced, {summary.Removed} removed. "
            : "";
        return $"{patch}{Plural(summary.Blobs, "blob")}, {SizeLabel.Of(summary.SharedBytes)} saved by sharing. Cook: {result.Cooked}.";
    }

    private List<Control> Folder()
    {
        var listing = _session.Listing;
        if (listing.Packs.Count == 0 && listing.Unreadable.Count == 0)
        {
            return [Message("Nothing exported yet. A whole pack goes first. Patches are built against an export that is already here.")];
        }

        List<Control> rows = [Section("In the exports folder")];
        rows.AddRange(listing.Packs.Select(PackRow));
        rows.AddRange(listing.Unreadable.Select(line => Caption(line, OrblitColors.Warn)));
        return rows;
    }

    private Button PackRow(ExportedPack pack, int index)
    {
        var about = Caption($"{(pack.IsPatch ? "patch" : "whole")} · {Plural(pack.Assets, "asset")} · {SizeLabel.Of(pack.Bytes)}", OrblitColors.InkDim);
        Grid.SetColumn(about, 1);
        var row = new Button
        {
            Name = $"pack_{index}",
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.Sm, Children = { Mono(pack.FileName, OrblitColors.Ink), about } },
        };
        row.Click += (_, _) => ShowContents(pack);
        return row;
    }

    private List<Control> ContentsView()
    {
        if (_session.Listing.Packs.Count == 0)
        {
            return [Message("There is no export to look inside yet.")];
        }

        var pick = Dropdown.Of(
            "shown",
            _session.Shown is { } shown ? Entry(shown) : "Pick an export",
            () => _session.Listing.Packs.Select(pack => (Entry(pack), (Action)(() => ShowContents(pack)))));
        return [pick, .. Table()];
    }

    private static string Entry(ExportedPack pack) => $"{pack.FileName} · {pack.Target}";

    private List<Control> Table()
    {
        if (_session.ContentsProblem is { } problem)
        {
            return [Message(problem)];
        }

        if (_session.Contents is not { } contents)
        {
            return [Message(_session.Shown is null ? "Pick an export above." : "Reading…")];
        }

        var layered = contents.Layered ? $" · {Plural(contents.Packs.Count, "pack")}" : "";
        var removed = contents.Removed.Count == 0 ? "" : $" · {contents.Removed.Count} taken away";
        var heading = Caption($"{Plural(contents.Rows.Count, "asset")} · {SizeLabel.Of(contents.Bytes)}{layered}{removed}", OrblitColors.InkMid);
        // A pack can serve tens of thousands of assets, so only the rows in view are made.
        var rows = new ItemsControl
        {
            ItemsSource = contents.Rows,
            ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
            ItemTemplate = new FuncDataTemplate<ContentsRow>((row, _) => ContentsLine(row, contents.Layered)),
        };
        return [heading, rows];
    }

    private static Grid ContentsLine(ContentsRow row, bool layered)
    {
        var id = Mono(row.Id, OrblitColors.Ink);
        Grid.SetColumn(id, 1);
        var line = new Grid { Height = 22, ColumnDefinitions = new ColumnDefinitions("72,*,Auto"), ColumnSpacing = Space.Sm, Children = { Mono(SizeLabel.Of(row.Bytes), OrblitColors.InkMid), id } };
        if (layered)
        {
            var pack = Caption(row.Pack, OrblitColors.InkDim);
            Grid.SetColumn(pack, 2);
            line.Children.Add(pack);
        }

        return line;
    }
}

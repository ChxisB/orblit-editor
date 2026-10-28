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
/// One kit of the store: what it is, who made it, what it needs, what is in it, and a button to install the
/// release shown.
/// </summary>
internal sealed class StoreDetail : Grid
{
    private const string BackIcon = "M19 12H5M11 6l-6 6 6 6";

    private readonly StoreSession _session;
    private readonly Button _install = new() { Name = "install", Content = "Install…", Background = OrblitColors.Ember, Foreground = OrblitColors.Ground, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly StackPanel _body = new() { Spacing = 2 };

    public StoreDetail(StoreSession session, Action<KitRow, KitRelease> onInstall)
    {
        _session = session;
        RowSpacing = Space.Sm;
        RowDefinitions = new RowDefinitions("Auto,*");
        var back = new Button
        {
            Name = "all_kits",
            Background = Brushes.Transparent,
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs, Children = { new Glyph(BackIcon, 14, OrblitColors.InkMid), new TextBlock { Text = "All kits" } } },
        };
        back.Click += async (_, _) => await _session.SelectAsync(null);
        _install.Click += (_, _) =>
        {
            if (_session.Selected is { } row && _session.Chosen is { } chosen)
            {
                onInstall(row, chosen.Release);
            }
        };
        Children.Add(new Grid { Children = { back, _install } });
        var scroll = new ScrollViewer { Name = "detail_scroll", Content = _body, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        SetRow(scroll, 1);
        Children.Add(scroll);
    }

    public void Render()
    {
        _install.IsEnabled = _session.Chosen is not null;
        _body.Children.Clear();
        if (_session.Selected is { } row)
        {
            _body.Children.AddRange(Body(row));
        }
    }

    private static TextBlock Words(string text, IBrush? ink = null, double size = OrblitText.Caption) => new()
    {
        Text = text,
        FontSize = size,
        Foreground = ink ?? OrblitColors.InkMid,
        TextWrapping = TextWrapping.Wrap,
    };

    private static TextBlock Section(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 9.5,
        LetterSpacing = 0.6,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(0, Space.Sm, 0, 3),
    };

    private List<Control> Body(KitRow row)
    {
        var manifest = row.Manifest;
        List<Control> body =
        [
            Words(manifest.Name, OrblitColors.Ink, OrblitText.Body),
            Words($"{manifest.Publisher} · {manifest.Version}", OrblitColors.InkDim),
            Words(manifest.Summary, OrblitColors.Ink, OrblitText.Label),
        ];
        if (_session.Problem is { } problem)
        {
            body.Add(Words(problem, OrblitColors.Bad));
        }
        else if (_session.Releases is not { } releases)
        {
            body.Add(Words("Reading the kit…", OrblitColors.InkDim));
        }
        else
        {
            if (releases.Previews.Count > 0)
            {
                body.Add(Previews(releases.Previews));
            }

            body.AddRange(Facts(row));
            if (_session.Chosen is { } chosen)
            {
                body.AddRange(ReleaseChoice(releases, chosen));
            }

            body.AddRange(FileList());
        }

        return body;
    }

    private ScrollViewer Previews(IReadOnlyList<string> names)
    {
        var strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm };
        foreach (var name in names)
        {
            var frame = new Border { Width = 213, Height = 120, CornerRadius = Radii.Control, ClipToBounds = true, Background = OrblitColors.LineSoft };
            StorePictures.Show(frame, _session.PreviewAsync(name), 426);
            strip.Children.Add(frame);
        }

        return new ScrollViewer { Content = strip, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0, Space.Md, 0, 0) };
    }

    // The licence, the engine range and the tags.
    private List<Control> Facts(KitRow row)
    {
        var manifest = row.Manifest;
        List<Control> facts =
        [
            new SelectableTextBlock { Text = $"Licence: {manifest.Licence.Spdx}, {manifest.Licence.Url}", FontSize = OrblitText.Caption, Foreground = OrblitColors.InkMid, Margin = new Thickness(0, Space.Md, 0, 0) },
            Words($"For engine {manifest.Engine.From} to {manifest.Engine.To}"),
        ];
        if (!_session.Fits(row))
        {
            facts.Add(Words($"This project uses engine {_session.Engine}, which is outside that range. The kit may not work.", OrblitColors.Warn));
        }

        if (manifest.Tags.Count > 0)
        {
            facts.Add(Words($"Tags: {string.Join(", ", manifest.Tags)}"));
        }

        return facts;
    }

    private List<Control> ReleaseChoice(KitReleases releases, PublishedRelease chosen)
    {
        var pick = Dropdown.Of(
            "release",
            Entry(chosen.Release),
            () => releases.Releases.Select(item => (Entry(item.Release), (Action)(() => _session.ChooseRelease(item)))));
        pick.HorizontalAlignment = HorizontalAlignment.Left;
        List<Control> choice = [Section("Release"), pick, Words($"{SizeLabel.Of(chosen.Release.Bytes)}. {chosen.Release.Notes}")];
        if (chosen.ContainsScripts)
        {
            choice.Add(Words("Contains scripts. You see them before anything is installed.", OrblitColors.Warn));
        }

        return choice;
    }

    private static string Entry(KitRelease release) => $"{release.Version} · {release.Released}";

    private List<Control> FileList()
    {
        switch (_session.FilesState)
        {
            case FileListState.Unread:
                var show = new Button { Name = "show_files", Content = "Show files", Background = Brushes.Transparent };
                show.Click += async (_, _) => await _session.LoadFilesAsync();
                return [new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, Margin = new Thickness(0, Space.Sm, 0, 0), Children = { show, Words("Asks the host of the kit file for its list of files.", OrblitColors.InkDim) } }];
            case FileListState.Reading:
                return [Words("Reading the file list…", OrblitColors.InkDim)];
            case FileListState.Unavailable:
                return [Words("The file list is not available from this store. You will see the files before anything is installed.", OrblitColors.InkDim)];
            default:
                // Read, so the session holds the list.
                var files = _session.Files!;
                // A kit can hold thousands of files, so only the rows in view are made.
                return
                [
                    Section($"Files ({files.Count})"),
                    new ItemsControl
                    {
                        ItemsSource = files,
                        ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()),
                        ItemTemplate = new FuncDataTemplate<KeyValuePair<AssetId, long>>((file, _) => FileRow(file)),
                    },
                ];
        }
    }

    private static Grid FileRow(KeyValuePair<AssetId, long> file)
    {
        var size = Words(SizeLabel.Of(file.Value), OrblitColors.InkDim);
        SetColumn(size, 1);
        var id = new TextBlock { Text = file.Key.ToString(), FontFamily = OrblitText.Mono, FontSize = OrblitText.Caption, Foreground = OrblitColors.Ink, TextTrimming = TextTrimming.CharacterEllipsis };
        return new Grid { Height = 20, ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.Sm, Children = { id, size } };
    }
}

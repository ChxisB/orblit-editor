using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Everything the editor has said, newest first. Each level can be hidden, and an entry with more to
/// say opens on a click.
/// </summary>
internal sealed class ConsolePanel : UserControl
{
    private const string InfoIcon = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM12 11v6M12 7.5v.01";
    private const string WarningIcon = "M12 4L2 20h20zM12 10v5M12 17.5v.01";
    private const string ErrorIcon = "M12 3a9 9 0 1 0 0 18a9 9 0 1 0 0-18zM12 7v6M12 16.5v.01";
    private const string CopyIcon = "M9 9h11v11H9zM5 15H4V4h11v1";
    private const string OpenIcon = "M10 7l5 5-5 5";

    private static readonly SayLevel[] Levels = [SayLevel.Info, SayLevel.Warning, SayLevel.Error];

    private readonly EditorLog _log;
    private readonly ITextClipboard _clipboard;
    private readonly HashSet<SayLevel> _showing = [.. Levels];
    private readonly StackPanel _filters = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel _rows = new() { Name = "console_rows" };

    // The entry and not its row, because a new entry at the top would move the row onto another one.
    private LogEntry? _opened;

    public ConsolePanel(EditorLog log, ITextClipboard clipboard)
    {
        _log = log;
        _clipboard = clipboard;
        Background = OrblitColors.Surface;

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Height = 28 };
        bar.Children.Add(_filters);
        bar.Children.Add(new ToolbarAction("console_copy", CopyIcon, "Copy everything shown", Copy) { [Grid.ColumnProperty] = 1 });
        bar.Children.Add(new ToolbarAction("console_clear", TimelineIcons.Delete, "Clear", _log.Clear) { [Grid.ColumnProperty] = 2 });

        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        body.Children.Add(new Border { BorderBrush = OrblitColors.LineSoft, BorderThickness = new Thickness(0, 0, 0, 1), Child = bar });
        body.Children.Add(new ScrollViewer { Name = "console_scroll", Content = _rows, [Grid.RowProperty] = 1 });
        Content = body;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _log.Changed += Rebuild;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _log.Changed -= Rebuild;
        base.OnDetachedFromVisualTree(e);
    }

    private static (string Label, string Name, string Icon, IBrush Ink) Look(SayLevel level) => level switch
    {
        SayLevel.Warning => ("Warnings", "warning", WarningIcon, OrblitColors.Warn),
        SayLevel.Error => ("Errors", "error", ErrorIcon, OrblitColors.Bad),
        _ => ("Info", "info", InfoIcon, OrblitColors.InkMid),
    };

    private static bool HasDetail(LogEntry entry) => !string.IsNullOrWhiteSpace(entry.Detail);

    private IEnumerable<LogEntry> Shown() => _log.Entries.Where(entry => _showing.Contains(entry.Level));

    private bool IsOpen(LogEntry entry) => _opened is { } opened && opened.At == entry.At && opened.SameAs(entry);

    private void Rebuild()
    {
        _filters.Children.Clear();
        foreach (var level in Levels)
        {
            _filters.Children.Add(Filter(level));
        }

        // shortcut: every row is rebuilt on each entry. Virtualize if 500 rows lag.
        _rows.Children.Clear();
        var shown = Shown().Reverse().ToList();
        if (shown.Count == 0)
        {
            _rows.Children.Add(Empty());
        }

        for (var i = 0; i < shown.Count; i++)
        {
            _rows.Children.Add(Row(shown[i], i));
        }
    }

    private void Toggle(SayLevel level)
    {
        if (!_showing.Remove(level))
        {
            _showing.Add(level);
        }

        Rebuild();
    }

    private void Open(LogEntry entry)
    {
        _opened = IsOpen(entry) ? null : entry;
        Rebuild();
    }

    private void Copy()
    {
        var lines = Shown().Select(entry => $"[{Look(entry.Level).Name}] {entry.Message}" + (HasDetail(entry) ? $"\n{entry.Detail}" : ""));
        _ = _clipboard.SetTextAsync(string.Join('\n', lines));
    }

    private Button Filter(SayLevel level)
    {
        var (label, name, icon, ink) = Look(level);
        var on = _showing.Contains(level);
        var colour = on ? ink : OrblitColors.InkDim;
        var filter = new Button
        {
            Name = $"console_filter_{name}",
            Height = 24,
            Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = on ? OrblitColors.Raised : Brushes.Transparent,
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Space.Xs,
                Children =
                {
                    new Glyph(icon, 13, colour),
                    new TextBlock { Text = $"{label} {_log.CountOf(level)}", FontSize = OrblitText.Label, Foreground = colour },
                },
            },
        };
        filter.Click += (_, _) => Toggle(level);
        return filter;
    }

    private TextBlock Empty() => new()
    {
        Name = "console_empty",
        Text = _log.Entries.Count == 0
            ? "No messages yet. Problems and progress appear here."
            : "No matching messages. Turn on a message type above.",
        FontSize = 12,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(Space.Md),
    };

    private StackPanel Row(LogEntry entry, int index)
    {
        var (_, _, icon, ink) = Look(entry.Level);
        var open = IsOpen(entry);
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = Space.Sm };
        line.Children.Add(HasDetail(entry) ? new Glyph(open ? TimelineIcons.Caret : OpenIcon, 13, OrblitColors.InkDim) : new Glyph(icon, 13, ink));
        line.Children.Add(new TextBlock
        {
            Text = entry.Message,
            FontSize = 12,
            Foreground = entry.Level == SayLevel.Info ? OrblitColors.InkMid : ink,
            TextTrimming = TextTrimming.CharacterEllipsis,
            [Grid.ColumnProperty] = 1,
        });
        if (entry.Repeats > 1)
        {
            line.Children.Add(Repeats(entry.Repeats));
        }

        line.Children.Add(new TextBlock
        {
            Text = entry.At.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
            FontFamily = OrblitText.Mono,
            FontSize = 10,
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
            [Grid.ColumnProperty] = 3,
        });

        var button = new Button
        {
            Name = $"console_row_{index}",
            Content = line,
            Padding = new Thickness(Space.Md, 3),
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        if (HasDetail(entry))
        {
            button.Click += (_, _) => Open(entry);
        }

        var row = new StackPanel { Children = { button } };
        if (open)
        {
            row.Children.Add(Detail(entry.Detail));
        }

        return row;
    }

    private static Border Repeats(int count) => new()
    {
        Background = OrblitColors.Raised,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(6, 1),
        VerticalAlignment = VerticalAlignment.Center,
        [Grid.ColumnProperty] = 2,
        Child = new TextBlock { Text = $"×{count}", FontSize = 10, Foreground = OrblitColors.InkMid },
    };

    private static SelectableTextBlock Detail(string detail) => new()
    {
        Text = detail,
        FontFamily = OrblitText.Mono,
        FontSize = 11,
        Foreground = OrblitColors.InkMid,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(21 + Space.Md, Space.Xs, Space.Md, Space.Sm),
    };
}

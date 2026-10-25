using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Finds a command or an object by typing. Gives back what was chosen, so the caller runs it once the
/// dialog is gone and not from under it.
/// </summary>
/// <remarks>
/// shortcut: icons are not drawn, because no icon set is bundled yet (decision 84), and the keycap
/// beside a row is plain text.
/// </remarks>
public sealed class PaletteDialog : Window
{
    private readonly IReadOnlyList<PaletteEntry> _entries;
    private readonly TextBox _field = new() { Name = "field", PlaceholderText = "Search commands and objects", BorderThickness = new Thickness(0), Background = Brushes.Transparent };
    private readonly StackPanel _rows = new() { Name = "rows", Margin = new Thickness(Space.Sm, 6, Space.Sm, 10) };
    private readonly TextBlock _count = Widgets.Mono("", "count");
    private IReadOnlyList<PaletteMatch> _results = [];
    private int _index;

    public PaletteDialog(IReadOnlyList<PaletteEntry> entries, string initialQuery = "")
    {
        _entries = entries;
        Title = "Command palette";
        Width = 640;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var column = new StackPanel();
        column.Children.Add(new Border { Padding = new Thickness(18, 14), Child = _field });
        column.Children.Add(new Border { Height = 1, Background = OrblitColors.LineSoft });
        column.Children.Add(new ScrollViewer { MaxHeight = 420, Content = _rows });
        column.Children.Add(new Border { Height = 1, Background = OrblitColors.LineSoft });
        column.Children.Add(new Border { Padding = new Thickness(Space.Lg, Space.Sm), Child = _count });
        Content = column;

        _field.Text = initialQuery;
        _field.TextChanged += (_, _) => Search(_field.Text ?? "");
        // On the window, not the field, so the arrows work wherever focus has strayed to.
        AddHandler(KeyDownEvent, OnKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Opened += (_, _) => _field.Focus();
        Search(initialQuery);
    }

    /// <summary>Opens the palette over <paramref name="owner"/>. Null when it was closed with nothing chosen.</summary>
    public static Task<PaletteEntry?> Choose(Window owner, IReadOnlyList<PaletteEntry> entries, string initialQuery = "") =>
        new PaletteDialog(entries, initialQuery).ShowDialog<PaletteEntry?>(owner);

    private void Search(string text)
    {
        _results = CommandPalette.Search(_entries, text);
        _index = 0;
        Draw();
    }

    private void Move(int by)
    {
        if (_results.Count > 0)
        {
            _index = (_index + by) % _results.Count;
            Draw();
        }
    }

    private void Run()
    {
        if (_results.Count > 0)
        {
            Close(_results[_index].Entry);
        }
    }

    private void OnKey(object? sender, KeyEventArgs press)
    {
        switch (press.Key)
        {
            case Key.Down:
                Move(1);
                break;
            case Key.Up:
                Move(_results.Count - 1);
                break;
            case Key.Enter:
                Run();
                break;
            case Key.Escape:
                Close(null);
                break;
            default:
                return;
        }

        press.Handled = true;
    }

    private void Draw()
    {
        _rows.Children.Clear();
        PaletteGroup? heading = null;
        for (var at = 0; at < _results.Count; at++)
        {
            var match = _results[at];
            if (match.Entry.Group != heading)
            {
                heading = match.Entry.Group;
                _rows.Children.Add(new TextBlock { Text = heading.Value.Title(), FontSize = OrblitText.Caption, FontWeight = FontWeight.SemiBold, Foreground = OrblitColors.InkDim, Margin = new Thickness(10, 12, 10, 4) });
            }

            _rows.Children.Add(Row(match, at));
        }

        if (_results.Count == 0)
        {
            _rows.Children.Add(new TextBlock { Name = "nothing", Text = "Nothing matches.", Foreground = OrblitColors.InkDim, Margin = new Thickness(Space.Lg) });
        }

        _count.Text = _results.Count == 1 ? "1 result" : $"{_results.Count} results";
    }

    private Border Row(PaletteMatch match, int at)
    {
        var entry = match.Entry;
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Space.Md };
        line.Children.Add(Label(match));
        if (entry.Meta.Length > 0)
        {
            var meta = Widgets.Mono(entry.Meta, size: OrblitText.Caption);
            Grid.SetColumn(meta, 1);
            line.Children.Add(meta);
        }

        if (entry.Keys is { } keys)
        {
            var cap = Widgets.Mono(keys, size: OrblitText.Caption);
            Grid.SetColumn(cap, 2);
            line.Children.Add(cap);
        }

        var row = new Border
        {
            Name = $"row_{at}",
            Height = 36,
            Margin = new Thickness(0, 0, 0, 2),
            Padding = new Thickness(10, 0),
            CornerRadius = Radii.Card,
            Background = at == _index ? OrblitColors.EmberWash : Brushes.Transparent,
            Child = line,
        };
        row.Tapped += (_, _) => Close(entry);
        return row;
    }

    // The label with the part that matched lit in the accent.
    private static TextBlock Label(PaletteMatch match)
    {
        var label = match.Entry.Label;
        var text = new TextBlock { FontSize = OrblitText.Body, Foreground = OrblitColors.Ink, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        text.Inlines!.Add(new Run(label[..match.HitStart]));
        text.Inlines.Add(new Run(label[match.HitStart..match.HitEnd]) { Foreground = OrblitColors.Ember, FontWeight = FontWeight.SemiBold });
        text.Inlines.Add(new Run(label[match.HitEnd..]));
        return text;
    }
}

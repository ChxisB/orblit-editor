using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>A picture button on the timeline's bar. Grey while it cannot be used, ember while it is on.</summary>
internal sealed class ToolbarAction : Button
{
    private string _icon;
    private bool _on;

    public ToolbarAction(string name, string icon, string tip, Action tap)
    {
        Name = name;
        _icon = icon;
        Width = 28;
        Height = 28;
        Padding = new Thickness(0);
        Margin = new Thickness(Space.Xs / 2, 0);
        Background = Brushes.Transparent;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        ToolTip.SetTip(this, tip);
        Click += (_, _) => tap();
        Draw();
    }

    /// <summary>Changes what the button looks like and says, for one that is play at one moment and pause at the next.</summary>
    public void Show(string icon, string tip, bool on)
    {
        ToolTip.SetTip(this, tip);
        if (icon != _icon || on != _on)
        {
            _icon = icon;
            _on = on;
            Draw();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Draw();
        }
    }

    private void Draw() => Content = new Glyph(_icon, 15, !IsEnabled ? OrblitColors.Line : _on ? OrblitColors.Ember : OrblitColors.InkMid);
}

/// <summary>A small label that is either on or off.</summary>
internal sealed class ToolbarChip : Button
{
    private readonly TextBlock _text = new() { FontSize = 11 };

    public ToolbarChip(string name, string label, string tip, Action tap)
    {
        Name = name;
        Padding = new Thickness(Space.Sm, 3);
        MinHeight = 0;
        CornerRadius = Radii.Control;
        BorderThickness = new Thickness(1);
        _text.Text = label;
        Content = _text;
        ToolTip.SetTip(this, tip);
        Click += (_, _) => tap();
        On = false;
    }

    public bool On
    {
        set
        {
            Background = value ? OrblitColors.Raised : Brushes.Transparent;
            BorderBrush = value ? OrblitColors.Line : Brushes.Transparent;
            _text.Foreground = value ? OrblitColors.Ink : OrblitColors.InkDim;
        }
    }
}

/// <summary>
/// A label that opens a menu, the way every choice on the timeline's bar is made. The menu is built when
/// it opens, so it offers what is true then.
/// </summary>
internal sealed class ToolbarMenu : Button
{
    private readonly Func<IEnumerable<Control>> _items;
    private readonly TextBlock _text = new() { FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Glyph _caret = new(TimelineIcons.Caret, 14);
    private readonly Glyph? _icon;
    private bool _warn;

    public ToolbarMenu(string name, string? tip, Func<IEnumerable<Control>> items, string? icon = null)
    {
        Name = name;
        _items = items;
        Padding = new Thickness(Space.Sm, 3);
        MinHeight = 0;
        CornerRadius = Radii.Control;
        BorderThickness = new Thickness(1);
        BorderBrush = OrblitColors.LineSoft;
        Background = Brushes.Transparent;

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        if (icon is not null)
        {
            _icon = new Glyph(icon, 13);
            row.Children.Add(_icon);
        }

        row.Children.Add(_text);
        row.Children.Add(_caret);
        Content = row;
        if (tip is not null)
        {
            ToolTip.SetTip(this, tip);
        }

        Click += (_, _) => Open();
        Recolour();
    }

    public string Label
    {
        set => _text.Text = value;
    }

    /// <summary>One line of a menu, greyed out and doing nothing when <paramref name="act"/> is null.</summary>
    public static MenuItem Item(string name, string header, Action? act)
    {
        var item = new MenuItem { Name = name, Header = header, IsEnabled = act is not null };
        item.Click += (_, _) => act?.Invoke();
        return item;
    }

    /// <summary>Draws the picture in the warning colour, for a choice that points at nothing.</summary>
    public bool Warn
    {
        set
        {
            _warn = value;
            Recolour();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Recolour();
        }
    }

    private void Open()
    {
        var menu = new ContextMenu();
        foreach (var item in _items())
        {
            menu.Items.Add(item);
        }

        ContextMenu = menu;
        menu.Open(this);
    }

    private void Recolour()
    {
        var ink = !IsEnabled ? OrblitColors.Line : _warn ? OrblitColors.Warn : OrblitColors.InkMid;
        _text.Foreground = IsEnabled ? OrblitColors.Ink : OrblitColors.InkDim;
        _caret.Ink = ink;
        if (_icon is not null)
        {
            _icon.Ink = ink;
        }
    }
}

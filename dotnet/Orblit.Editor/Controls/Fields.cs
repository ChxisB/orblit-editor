using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

/// <summary>What a drag row adds to its label and the numbers it reads.</summary>
internal sealed record DragOptions
{
    /// <summary>Units per logical pixel dragged.</summary>
    public double Step { get; init; } = 0.01;

    public int Decimals { get; init; } = 2;

    /// <summary>A floor for each number, so a scale cannot be dragged through zero.</summary>
    public double? Minimum { get; init; }

    /// <summary>Something small after the field, such as the button that keys it.</summary>
    public Control? Trailing { get; init; }
}

/// <summary>What a slider row adds to its label and value.</summary>
internal sealed record SliderOptions
{
    public required double Min { get; init; }

    public required double Max { get; init; }

    public int Decimals { get; init; }

    public string Unit { get; init; } = "";

    /// <summary>Something small after the value, such as the button that keys it.</summary>
    public Control? Trailing { get; init; }
}

/// <summary>
/// The controls an inspector's fields are made of: a labelled row for each kind of value, laid out on
/// one column so every field lines up.
/// </summary>
/// <remarks>
/// A field says what it did and leaves the value to the scene. Changes run through <see cref="FieldHost"/>
/// so that a drag is one undo step and the panel is not rebuilt under the pointer.
/// </remarks>
internal sealed class Fields(FieldHost host)
{
    public const double Height = 28;
    private const double LabelWidth = 80;

    /// <summary>Where this set of fields sends its changes, for a control that is not a field but acts like one.</summary>
    public FieldHost Host => host;

    private static readonly Colour[] StandardSwatches =
    [
        new(0xFFFFF3E0), new(0xFFD9634F), new(0xFF7FB069), new(0xFF5B8DD9), new(0xFFE5B84F), new(0xFF3B424C),
    ];

    /// <summary>Fields stacked one under the other.</summary>
    public static StackPanel Column(IEnumerable<Control> children)
    {
        var column = new StackPanel();
        column.Children.AddRange(children);
        return column;
    }

    /// <summary>A label made safe to put in a control's name.</summary>
    public static string Key(string label) =>
        string.Concat(label.Select(one => char.IsLetterOrDigit(one) ? char.ToLowerInvariant(one) : '_'));

    /// <summary>A labelled row, so every field lines up on the same column.</summary>
    /// <param name="label">What the row is for.</param>
    /// <param name="child">The field.</param>
    /// <param name="trailing">Something small after the field.</param>
    /// <param name="name">The row's name, for a list of rows that repeat a label, because a window cannot hold two controls of one name.</param>
    public static Grid Row(string label, Control child, Control? trailing = null, string? name = null)
    {
        var row = new Grid
        {
            Name = name ?? $"row_{Key(label)}",
            ColumnDefinitions = new ColumnDefinitions($"{LabelWidth},*,Auto"),
            MinHeight = Height,
            Margin = new Thickness(0, 6),
        };
        row.Children.Add(Caption(label));
        Grid.SetColumn(child, 1);
        row.Children.Add(child);
        if (trailing is not null)
        {
            Grid.SetColumn(trailing, 2);
            trailing.Margin = new Thickness(Space.Xs, 0, 0, 0);
            row.Children.Add(trailing);
        }

        return row;
    }

    /// <summary>A button that spans its column and asks for no attention until it is wanted.</summary>
    public static Button Button(string label, string tooltip, Action onPressed)
    {
        var button = new Button
        {
            Name = $"button_{Key(label)}",
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            FontSize = OrblitText.Label,
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => onPressed();
        return button;
    }

    /// <summary>A sentence under a field, quiet unless it is a problem.</summary>
    public static TextBlock Note(string text, IBrush? colour = null) => new()
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = colour ?? OrblitColors.InkDim,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 6),
    };

    /// <summary>A value shown and not edited.</summary>
    public static Grid Text(string label, string value) => Row(label, new Border
    {
        Height = Height,
        Padding = new Thickness(10, 0),
        Background = OrblitColors.Raised,
        CornerRadius = Radii.Control,
        Child = new TextBlock
        {
            Text = value,
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        },
    });

    /// <summary>A section a person can fold away. A fold outlives a rebuild because the host holds it.</summary>
    public Control Section(string title, Control child)
    {
        var folded = host.IsFolded(title);
        var heading = new Border
        {
            Name = $"section_{Key(title)}",
            Height = 36,
            Padding = new Thickness(Space.Lg, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = (folded ? "▸  " : "▾  ") + title,
                FontSize = OrblitText.Label,
                FontWeight = FontWeight.SemiBold,
                Foreground = OrblitColors.Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
        heading.PointerPressed += (_, _) => host.Fold(title);

        child.Margin = new Thickness(Space.Lg, Space.Xs, Space.Lg, Space.Lg);
        child.IsVisible = !folded;
        var section = new StackPanel { Children = { heading, child } };
        return new Border
        {
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = section,
        };
    }

    /// <summary>One number or a few that belong together, each dragged sideways to change.</summary>
    /// <param name="label">What the numbers are.</param>
    /// <param name="read">The numbers as they are now. Asked again at every step of a drag, because the drag is what is changing them.</param>
    /// <param name="onChanged">Called with every number, one of them moved.</param>
    /// <param name="options">How far a pixel moves a number and where it stops.</param>
    public Control Drag(string label, Func<IReadOnlyList<double>> read, Action<double[]> onChanged, DragOptions? options = null)
    {
        options ??= new DragOptions();
        var values = read();
        var axes = values.Count == 3;
        var fields = new Grid();
        for (var index = 0; index < values.Count; index++)
        {
            var at = index;
            fields.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var number = Number(
                () => read()[at],
                axes ? AxisOf(index) : null,
                options.Decimals,
                pixels => onChanged(InspectorModel.Dragged(read(), at, pixels * options.Step, options.Minimum)));
            number.Name = $"drag_{Key(label)}_{index}";
            Grid.SetColumn(number, index);
            number.Margin = new Thickness(index == 0 ? 0 : 3, 0, index == values.Count - 1 ? 0 : 3, 0);
            fields.Children.Add(number);
        }

        // Three numbers are a point in space and want the whole width to themselves, under their name.
        if (!axes)
        {
            return Row(label, fields, options.Trailing);
        }

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
        head.Children.Add(Caption(label));
        if (options.Trailing is { } trailing)
        {
            Grid.SetColumn(trailing, 1);
            head.Children.Add(trailing);
        }

        return new StackPanel { Name = $"row_{Key(label)}", Margin = new Thickness(0, 6), Children = { head, fields } };
    }

    /// <summary>A value with a slider, in real units.</summary>
    public Control Slider(string label, double value, Action<double> onChanged, SliderOptions options)
    {
        var shown = new TextBlock
        {
            Width = 56,
            TextAlignment = TextAlignment.Right,
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
            VerticalAlignment = VerticalAlignment.Center,
        };
        string Format(double at) => at.ToString($"F{options.Decimals}", CultureInfo.InvariantCulture) + options.Unit;
        var slider = new Slider
        {
            Name = $"slider_{Key(label)}",
            Minimum = options.Min,
            Maximum = options.Max,
            Value = Math.Clamp(value, options.Min, options.Max),
            VerticalAlignment = VerticalAlignment.Center,
        };
        shown.Text = Format(slider.Value);
        Grid.SetColumn(shown, 1);
        var pressed = false;
        slider.AddHandler(InputElement.PointerPressedEvent, (_, _) => pressed = true, RoutingStrategies.Tunnel, handledEventsToo: true);
        slider.AddHandler(
            InputElement.PointerReleasedEvent,
            (_, _) =>
            {
                pressed = false;
                host.Settle();
            },
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        slider.PointerCaptureLost += (_, _) =>
        {
            if (pressed)
            {
                pressed = false;
                host.Settle();
            }
        };
        slider.PropertyChanged += (_, change) =>
        {
            if (change.Property != RangeBase.ValueProperty)
            {
                return;
            }

            shown.Text = Format(slider.Value);
            host.Hold();
            onChanged(slider.Value);
            if (!pressed)
            {
                // shortcut: a key step settles at once and the rebuild drops focus, upgrade when keyboard use matters.
                host.Settle();
            }
        };
        var both = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        both.Children.Add(slider);
        both.Children.Add(shown);
        return Row(label, both, options.Trailing);
    }

    /// <summary>A flag, as a switch.</summary>
    public Control Toggle(string label, bool value, Action<bool> onChanged) => Row(label, new OrblitSwitch(
        label,
        value,
        on => host.Apply(() => onChanged(on)))
    {
        Name = $"toggle_{Key(label)}",
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Center,
    });

    /// <summary>A short list of options, shown rather than hidden behind a menu.</summary>
    /// <param name="label">What is chosen.</param>
    /// <param name="options">Every choice, in the order they sit.</param>
    /// <param name="selected">The one that is on.</param>
    /// <param name="onSelect">Null where there is nothing to choose: a single option is a statement of fact.</param>
    public Control Choice(string label, IReadOnlyList<string> options, string selected, Action<string>? onSelect = null)
    {
        var track = new Grid { Height = Height };
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            track.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var cell = Cell(option, option == selected, onSelect is null ? null : () => host.Apply(() => onSelect(option)));
            cell.Name = $"choice_{Key(label)}_{Key(option)}";
            Grid.SetColumn(cell, index);
            track.Children.Add(cell);
        }

        return Row(label, Tracked(track));
    }

    /// <summary>Several flags in one row, each a cell that lights while it is on.</summary>
    /// <param name="label">What the flags hold.</param>
    /// <param name="cells">Each cell's text and whether it is on.</param>
    /// <param name="onToggle">Called with the cell's index.</param>
    public Control Flags(string label, IReadOnlyList<(string Name, bool On)> cells, Action<int> onToggle)
    {
        var track = new Grid { Height = Height };
        for (var index = 0; index < cells.Count; index++)
        {
            var at = index;
            track.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            var cell = Cell(cells[at].Name, cells[at].On, () => host.Apply(() => onToggle(at)));
            cell.Name = $"toggle_{Key(label)}_{Key(cells[at].Name)}";
            Grid.SetColumn(cell, at);
            track.Children.Add(cell);
        }

        return Row(label, Tracked(track));
    }

    /// <summary>A choice from a menu, for a list too long to lay side by side in a row as narrow as the inspector.</summary>
    /// <param name="label">What is chosen.</param>
    /// <param name="shown">What the row reads while closed. Not taken from the options, since the value it stands for may not be one of them.</param>
    /// <param name="options">Each value the menu offers, by the name it goes under.</param>
    /// <param name="onSelect">Called with the value picked.</param>
    public Control Pick<T>(string label, string shown, IEnumerable<KeyValuePair<T, string>> options, Action<T> onSelect)
    {
        var menu = new MenuFlyout();
        foreach (var (value, name) in options)
        {
            var item = new MenuItem { Name = $"pick_{Key(label)}_{Key(name)}", Header = name };
            item.Click += (_, _) => host.Apply(() => onSelect(value));
            menu.Items.Add(item);
        }

        var button = new Button
        {
            Name = $"pick_{Key(label)}",
            Height = Height,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(10, 0, Space.Sm, 0),
            Background = OrblitColors.Raised,
            CornerRadius = Radii.Control,
            Flyout = menu,
            Content = new TextBlock
            {
                Text = shown,
                FontSize = OrblitText.Label,
                Foreground = OrblitColors.Ink,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
        return Row(label, button);
    }

    /// <summary>A short palette rather than a full picker: enough to see a colour change reach the renderer.</summary>
    /// <param name="label">What is coloured.</param>
    /// <param name="value">The colour it is now.</param>
    /// <param name="onChanged">Called with the swatch picked.</param>
    /// <param name="swatches">The colours offered. A ground tint wants earth, not a light's white and primaries.</param>
    public Control Palette(string label, Colour value, Action<Colour> onChanged, IReadOnlyList<Colour>? swatches = null)
    {
        // Wraps rather than runs on, because a narrow window leaves less room than six swatches take.
        var wrap = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var index = 0;
        foreach (var swatch in swatches ?? StandardSwatches)
        {
            var picked = swatch;
            var cell = new Border
            {
                Name = $"swatch_{Key(label)}_{index++}",
                Width = 20,
                Height = 20,
                Margin = new Thickness(0, 0, 3, 3),
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Avalonia.Media.Color.FromUInt32(picked.Argb)),
                BorderBrush = picked == value ? OrblitColors.Ember : OrblitColors.Line,
                BorderThickness = new Thickness(picked == value ? 2 : 1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            cell.PointerPressed += (_, _) => host.Apply(() => onChanged(picked));
            wrap.Children.Add(cell);
        }

        wrap.Children.Add(new TextBlock
        {
            Text = value.ToHex(),
            FontSize = 10.5,
            FontFamily = OrblitText.Mono,
            Margin = new Thickness(Space.Xs, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        });
        return Row(label, wrap);
    }

    /// <summary>A line of text that changes the scene as it is typed and settles when it loses focus.</summary>
    /// <param name="label">What is named.</param>
    /// <param name="value">The text as the scene has it.</param>
    /// <param name="onChanged">Called with the text at every edit.</param>
    /// <param name="hint">Greyed text shown while the field is empty.</param>
    public Control Entry(string label, string value, Action<string> onChanged, string? hint = null) =>
        Row(label, Box($"entry_{Key(label)}", value, onChanged, hint));

    /// <summary>The box <see cref="Entry"/> is made of, for a row that has something of its own beside it.</summary>
    /// <param name="name">The control's name.</param>
    /// <param name="value">The text as the scene has it.</param>
    /// <param name="onChanged">Called with the text at every edit.</param>
    /// <param name="hint">Greyed text shown while the box is empty.</param>
    public TextBox Box(string name, string value, Action<string> onChanged, string? hint = null)
    {
        var box = new TextBox
        {
            Name = name,
            Text = value,
            PlaceholderText = hint,
            Height = Height,
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
        };
        // A box that has just been built reports its starting text once it is shown. That is not an edit.
        var last = value;
        box.TextChanged += (_, _) =>
        {
            var now = box.Text ?? "";
            if (now == last)
            {
                return;
            }

            last = now;
            host.Hold();
            onChanged(now);
        };
        // Rebuilding the row under a caret would drop it, so the panel waits until the box lets go.
        box.LostFocus += (_, _) => host.Settle();
        return box;
    }

    /// <summary>
    /// One switch for each physics layer, in rows of eight. A layer reads as its number, and as its
    /// name when the scene gives it one, in the tooltip: a name would not fit in a cell this small.
    /// </summary>
    /// <param name="label">What the layers are for.</param>
    /// <param name="bits">The layers that are on, one bit each, the first layer the lowest.</param>
    /// <param name="names">What the scene calls each layer, from the first.</param>
    /// <param name="onToggle">Called with the layer's index.</param>
    public Control Layers(string label, int bits, IReadOnlyList<string> names, Action<int> onToggle)
    {
        const int across = 8;
        var column = new StackPanel();
        for (var top = 0; top < SceneSettings.LayerCount; top += across)
        {
            var line = new Grid { Height = 22 };
            for (var index = top; index < top + across; index++)
            {
                var at = index;
                line.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                var name = at < names.Count ? names[at] : "";
                var cell = Cell($"{at + 1}", (bits & (1 << at)) != 0, () => host.Apply(() => onToggle(at)));
                cell.Name = $"layer_{Key(label)}_{at}";
                ToolTip.SetTip(cell, name.Length == 0 ? $"Layer {at + 1}" : $"{at + 1}: {name}");
                Grid.SetColumn(cell, at - top);
                line.Children.Add(cell);
            }

            column.Children.Add(line);
        }

        return Row(label, new Border
        {
            Padding = new Thickness(2),
            Background = OrblitColors.Raised,
            CornerRadius = new CornerRadius(4),
            Child = column,
        });
    }

    private static TextBlock Caption(string label) => new()
    {
        Text = label,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkMid,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private static Border Tracked(Control cells) => new()
    {
        Padding = new Thickness(2),
        Background = OrblitColors.Raised,
        CornerRadius = Radii.Control,
        Child = cells,
    };

    /// <summary>One switch in a row of them, lit while it is on. With no action it reads as fact.</summary>
    private static Border Cell(string label, bool on, Action? onTap)
    {
        var cell = new Border
        {
            Background = on ? OrblitColors.EmberWash : Brushes.Transparent,
            CornerRadius = new CornerRadius(Radii.Control.TopLeft - 2),
            Cursor = onTap is null ? null : new Cursor(StandardCursorType.Hand),
            Child = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                Foreground = on ? OrblitColors.Ember : OrblitColors.InkDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        if (onTap is not null)
        {
            cell.PointerPressed += (_, press) =>
            {
                press.Handled = true;
                onTap();
            };
        }

        return cell;
    }

    private static (string Name, IBrush Colour) AxisOf(int index) => index switch
    {
        0 => ("X", OrblitColors.AxisX),
        1 => ("Y", OrblitColors.AxisY),
        _ => ("Z", OrblitColors.AxisZ),
    };

    /// <summary>One draggable number, with the letter of its axis in front where it has one.</summary>
    private Border Number(Func<double> read, (string Name, IBrush Colour)? axis, int decimals, Action<double> onDrag)
    {
        var text = new TextBlock
        {
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Show() => text.Text = read().ToString($"F{decimals}", CultureInfo.InvariantCulture);
        Show();
        // A drag reads the number again at every step, and this is what the rows show of it.
        host.Live(Show);
        var inside = new Grid { ColumnDefinitions = new ColumnDefinitions(axis is null ? "*" : "Auto,*") };
        if (axis is { } letter)
        {
            inside.Children.Add(new TextBlock
            {
                Text = letter.Name,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                Foreground = letter.Colour,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        Grid.SetColumn(text, axis is null ? 0 : 1);
        inside.Children.Add(text);
        var number = new Border
        {
            Height = Height,
            Padding = new Thickness(Space.Sm, 0),
            Background = OrblitColors.Raised,
            CornerRadius = Radii.Control,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Child = inside,
        };
        Drags(number, onDrag);
        number.PointerEntered += (_, _) => number.Background = OrblitColors.Hover;
        number.PointerExited += (_, _) => number.Background = OrblitColors.Raised;
        return number;
    }

    /// <summary>Calls <paramref name="onDrag"/> with the pixels the pointer moved, and settles when it lets go.</summary>
    private void Drags(Control target, Action<double> onDrag)
    {
        double? last = null;
        target.PointerPressed += (_, press) =>
        {
            if (!press.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
            {
                return;
            }

            last = press.GetPosition(target).X;
            press.Pointer.Capture(target);
            press.Handled = true;
        };
        target.PointerMoved += (_, move) =>
        {
            if (last is not { } from)
            {
                return;
            }

            var now = move.GetPosition(target).X;
            last = now;
            if (now != from)
            {
                host.Hold();
                onDrag(now - from);
            }
        };
        void Release()
        {
            if (last is null)
            {
                return;
            }

            last = null;
            host.Settle();
        }

        target.PointerReleased += (_, release) =>
        {
            release.Pointer.Capture(null);
            Release();
        };
        target.PointerCaptureLost += (_, _) => Release();
    }
}

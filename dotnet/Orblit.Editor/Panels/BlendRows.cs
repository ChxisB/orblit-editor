using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>
/// The rows the blend inspector is made of that a scene's inspector has no use for: a number that is
/// typed, a name with a menu of the names it could be, and a box that adds one. Every edit it makes goes
/// through the bench, so each is a step that can be undone.
/// </summary>
/// <param name="fields">Where the rows send their changes, and what folds.</param>
/// <param name="bench">What is edited.</param>
internal sealed class BlendRows(Fields fields, BlendBench bench)
{
    public Fields Fields => fields;

    public BlendBench Bench => bench;

    /// <summary>One edit that is a step by itself, such as a click.</summary>
    public void Run(string label, Func<BlendDocument, BlendDocument> change, BlendPick? pick = null) =>
        bench.Edit(label, change, new BlendEditOptions(pick));

    /// <summary>An edit that is one of a run of keystrokes, so that the run is one step.</summary>
    public void Typed(string label, Func<BlendDocument, BlendDocument> change, BlendPick? pick = null) =>
        bench.Edit(label, change, new BlendEditOptions(pick, label));

    /// <summary>Something small after a number, such as what it is counted in.</summary>
    public static Control Unit(string text) => new TextBlock
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.InkDim,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A button that is one step when pressed, and is left out of reach when it would do nothing.</summary>
    public Button Act(string label, string tooltip, Action onPressed, bool enabled = true)
    {
        var button = Fields.Button(label, tooltip, () => fields.Host.Apply(onPressed));
        button.IsEnabled = enabled;
        return button;
    }

    /// <summary>A small cross that takes something out.</summary>
    public Button Remove(string name, string tooltip, Action onPressed)
    {
        var button = new Button
        {
            Name = name,
            Width = Fields.Height,
            Height = Fields.Height,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Glyph(TimelineIcons.Close, 14, OrblitColors.InkDim),
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => fields.Host.Apply(onPressed);
        return button;
    }

    /// <summary>A number typed in a box. Half a number, such as a lone minus sign, is not a value yet and says nothing.</summary>
    public Control Number(string label, double value, Action<double> onChanged, Control? trailing = null)
    {
        var box = fields.Box($"number_{Fields.Key(label)}", BlendConditionModel.NumberText(value), text =>
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number))
            {
                onChanged(number);
            }
        });
        return Fields.Row(label, box, trailing);
    }

    /// <summary>A name typed in a box, with a menu of the names it could be when there are any.</summary>
    public Control Name(string label, string value, IReadOnlyList<string> choices, Action<string> onChanged)
    {
        var key = Fields.Key(label);
        var box = fields.Box($"name_{key}", value, onChanged);
        var both = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        both.Children.Add(box);
        if (choices.Count > 0)
        {
            var menu = Menu("name", key, choices, choice => fields.Host.Apply(() => onChanged(choice)));
            Grid.SetColumn(menu, 1);
            both.Children.Add(menu);
        }

        return Fields.Row(label, both);
    }

    /// <summary>
    /// A box and a button that adds what was typed. A menu of the names it could be fills the box and
    /// adds nothing until the button is pressed.
    /// </summary>
    public Control Add(string label, string hint, IReadOnlyList<string> choices, Action<string> onAdd)
    {
        var key = Fields.Key(label);
        var box = new TextBox
        {
            Name = $"add_{key}",
            PlaceholderText = hint,
            Height = Fields.Height,
            FontSize = OrblitText.Label,
            FontFamily = OrblitText.Mono,
        };
        var add = Act("Add", $"Add what is typed as a {label.ToLowerInvariant()}.", () => onAdd((box.Text ?? "").Trim()));
        add.Name = $"add_{key}_button";
        add.Margin = new Thickness(Space.Xs, 0, 0, 0);
        var all = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        all.Children.Add(box);
        if (choices.Count > 0)
        {
            var menu = Menu("add", key, choices, choice => box.Text = choice);
            Grid.SetColumn(menu, 1);
            all.Children.Add(menu);
        }

        Grid.SetColumn(add, 2);
        all.Children.Add(add);
        return Fields.Row(label, all);
    }

    private static Button Menu(string kind, string key, IReadOnlyList<string> choices, Action<string> onPicked)
    {
        var flyout = new MenuFlyout();
        foreach (var choice in choices)
        {
            var item = new MenuItem { Name = $"option_{key}_{Fields.Key(choice)}", Header = choice };
            item.Click += (_, _) => onPicked(choice);
            flyout.Items.Add(item);
        }

        return new Button
        {
            Name = $"{kind}_{key}_choices",
            Width = Fields.Height,
            Height = Fields.Height,
            Margin = new Thickness(Space.Xs, 0, 0, 0),
            Padding = new Thickness(0),
            Background = OrblitColors.Raised,
            CornerRadius = Radii.Control,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Flyout = flyout,
            Content = new Glyph(TimelineIcons.Caret, 14),
        };
    }
}

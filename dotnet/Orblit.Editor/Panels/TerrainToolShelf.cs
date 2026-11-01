using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Terrain;

/// <summary>
/// The brush's tools in a row over the panels, with the two settings most worth reaching for while
/// painting: how big and how strong.
/// </summary>
/// <remarks>
/// It changes what it shows in place and is never rebuilt, because a slider rebuilt under the pointer
/// loses the drag.
///
/// shortcut: a tool is its name, because no icon set is bundled yet (decision 84).
/// </remarks>
internal sealed class TerrainToolShelf : Border
{
    // After the shaping tools, and after the paints, so the tools read in three groups.
    private static readonly HashSet<BrushTool> EndsAGroup = [BrushTool.Slope, BrushTool.Roughness];

    private readonly TerrainBench _bench;
    private readonly Dictionary<BrushTool, Button> _buttons = [];
    private readonly Slider _size = new() { Name = "shelf_size" };
    private readonly Slider _strength = new() { Name = "shelf_strength" };
    private bool _syncing;

    public TerrainToolShelf(TerrainBench bench)
    {
        _bench = bench;
        Name = "terrain_shelf";
        Background = OrblitColors.Surface;
        Padding = new Thickness(Space.Md, Space.Xs);
        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = Row(),
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Sync;
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Sync;
        base.OnDetachedFromVisualTree(e);
    }

    private StackPanel Row()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs, VerticalAlignment = VerticalAlignment.Center };
        foreach (var tool in Enum.GetValues<BrushTool>())
        {
            row.Children.Add(ToolButton(tool));
            if (EndsAGroup.Contains(tool))
            {
                row.Children.Add(Divider());
            }
        }

        row.Children.Add(Divider());
        row.Children.Add(Setting("Size", _size, new SliderOptions { Min = 1, Max = 128, Unit = " m" }, value => _bench.Brush = _bench.Brush.CopyWith(size: value)));
        row.Children.Add(Setting("Strength", _strength, new SliderOptions { Min = 0, Max = 1, Decimals = 2 }, value => _bench.Brush = _bench.Brush.CopyWith(strength: value)));
        return row;
    }

    private Button ToolButton(BrushTool tool)
    {
        var words = BrushWords.Of(tool);
        var button = new Button
        {
            Name = $"tool_{tool}",
            Content = words.Label,
            FontSize = OrblitText.Caption,
            Padding = new Thickness(Space.Sm, Space.Xs),
        };
        ToolTip.SetTip(button, $"{words.Label}\n{words.Hint}");
        button.Click += (_, _) => _bench.Tool = tool;
        _buttons[tool] = button;
        return button;
    }

    private static Border Divider() => new()
    {
        Width = 1,
        Height = 18,
        Margin = new Thickness(Space.Xs, 0),
        Background = OrblitColors.Line,
    };

    private StackPanel Setting(string label, Slider slider, SliderOptions options, Action<double> write)
    {
        slider.Minimum = options.Min;
        slider.Maximum = options.Max;
        slider.Width = 110;
        slider.VerticalAlignment = VerticalAlignment.Center;
        var shown = new TextBlock
        {
            Width = 48,
            TextAlignment = TextAlignment.Right,
            FontSize = OrblitText.Caption,
            FontFamily = OrblitText.Mono,
            Foreground = OrblitColors.InkMid,
            VerticalAlignment = VerticalAlignment.Center,
        };
        void Show() => shown.Text = slider.Value.ToString($"F{options.Decimals}", CultureInfo.InvariantCulture) + options.Unit;
        Show();
        slider.PropertyChanged += (_, change) =>
        {
            if (change.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty)
            {
                return;
            }

            Show();
            if (!_syncing)
            {
                write(slider.Value);
            }
        };
        var caption = new TextBlock { Text = label, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, VerticalAlignment = VerticalAlignment.Center };
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs, Children = { caption, slider, shown } };
    }

    // Shows what the bench holds. The flag keeps a value set from here from being written back as if somebody had dragged it.
    private void Sync()
    {
        foreach (var (tool, button) in _buttons)
        {
            var active = tool == _bench.Tool;
            button.Background = active ? OrblitColors.EmberWash : Brushes.Transparent;
            button.Foreground = active ? OrblitColors.Ember : OrblitColors.InkMid;
        }

        _syncing = true;
        _size.Value = _bench.Brush.Size;
        _strength.Value = _bench.Brush.Strength;
        _syncing = false;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Panels;

/// <summary>What the bar's controls do.</summary>
/// <param name="Up">Goes up one folder.</param>
/// <param name="Add">Opens the menu of what can be made, from the button it is given.</param>
/// <param name="Refresh">Reads the folder again.</param>
/// <param name="Filtered">Told what is typed in the filter.</param>
/// <param name="Preview">Shows or hides the preview.</param>
internal sealed record AssetBrowserHeaderLinks(Action Up, Action<Control> Add, Action Refresh, Action<string> Filtered, Action Preview);

/// <summary>
/// The bar across the top of the browser: where you are, what is left to cook, and what you can do from here.
/// </summary>
/// <remarks>
/// There is no title. The tab above says what the panel is, and a heading that repeats it is a line of
/// pixels saying nothing.
/// </remarks>
internal sealed class AssetBrowserHeader : Border
{
    // Below this the bar has no room for a field worth typing in, so it has none. What was typed is
    // still filtering the files.
    private const double FilterRoom = 340;

    private readonly Button _up = IconButton("browser_up", "Up one folder", AssetIcons.Up);
    private readonly Button _add = IconButton("browser_add", "New folder or file", AssetIcons.Add);
    private readonly Button _preview = IconButton("browser_preview", "Hide the preview", AssetIcons.Eye);
    private readonly Button _refresh = IconButton("browser_refresh", "Read the folder again", AssetIcons.Refresh);
    private readonly TextBlock _crumb = new() { Name = "browser_crumb", FontFamily = OrblitText.Mono, FontSize = OrblitText.Label, Foreground = OrblitColors.InkMid, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _cook = new() { Name = "browser_cook", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Space.Xs, 0) };
    private readonly TextBlock _count = new() { Name = "browser_count", FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(Space.Xs, 0) };
    private readonly TextBox _filter = new() { Name = "browser_filter", PlaceholderText = "Filter assets", Width = 184, Margin = new Thickness(Space.Xs, 0) };

    /// <param name="links">What the controls do.</param>
    public AssetBrowserHeader(AssetBrowserHeaderLinks links)
    {
        Height = 36;
        Padding = new Thickness(Space.Xs, 0);
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(0, 0, 0, 1);

        _up.Click += (_, _) => links.Up();
        // The same menu the right click opens. Here as well, because a menu nobody knows to right click
        // for is a menu nobody has.
        _add.Click += (_, _) => links.Add(_add);
        _preview.Click += (_, _) => links.Preview();
        _refresh.Click += (_, _) => links.Refresh();
        _filter.TextChanged += (_, _) => links.Filtered(_filter.Text ?? "");

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        row.Children.Add(_cook);
        row.Children.Add(_count);
        row.Children.Add(_filter);
        row.Children.Add(_add);
        row.Children.Add(_preview);
        row.Children.Add(_refresh);
        var bar = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
        DockPanel.SetDock(_up, Dock.Left);
        DockPanel.SetDock(row, Dock.Right);
        bar.Children.Add(_up);
        bar.Children.Add(row);
        bar.Children.Add(_crumb);
        Child = bar;

        SizeChanged += (_, e) =>
        {
            _filter.IsVisible = e.NewSize.Width >= FilterRoom;
            _filter.Width = Math.Clamp(e.NewSize.Width * 0.34, 120, 184);
        };
    }

    /// <summary>Whether the preview is showing, or null where there is no room for one and so no switch for it.</summary>
    public bool? Previewing
    {
        set
        {
            _preview.IsVisible = value is not null;
            _preview.Content = new Glyph(value == false ? AssetIcons.EyeOff : AssetIcons.Eye, 15);
            ToolTip.SetTip(_preview, value == false ? "Show the preview" : "Hide the preview");
        }
    }

    public void Show(AssetBrowserModel model, int count, CookSummary? cook)
    {
        _up.IsEnabled = model.CanGoUp;
        _crumb.Text = model.Crumb.Length == 0 ? "/" : model.Crumb;
        _count.Text = AssetBrowserModel.CountOf(count);
        _cook.IsVisible = cook is not null;
        _cook.Text = cook?.Text;
        _cook.Foreground = cook?.Tone is { } tone ? OrblitColors.For(tone) : OrblitColors.InkDim;
        ToolTip.SetTip(_cook, cook?.Tip);
    }

    private static Button IconButton(string name, string tip, string icon)
    {
        var button = new Button
        {
            Name = name,
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            Margin = new Thickness(Space.Xs / 2, 0),
            Background = Brushes.Transparent,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Glyph(icon, 15),
        };
        ToolTip.SetTip(button, tip);
        return button;
    }
}

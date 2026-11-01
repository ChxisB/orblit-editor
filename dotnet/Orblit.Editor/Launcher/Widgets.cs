using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>The few text and button shapes the launcher repeats, so a page does not choose its own sizes.</summary>
internal static class Widgets
{
    public static TextBlock Display(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Display,
        FontWeight = FontWeight.SemiBold,
        Foreground = OrblitColors.Ink,
    };

    public static TextBlock Body(string text, string? name = null) => new()
    {
        Name = name,
        Text = text,
        FontSize = OrblitText.Body,
        Foreground = OrblitColors.InkMid,
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Label(string text, string? name = null) => new()
    {
        Name = name,
        Text = text,
        FontSize = OrblitText.Label,
        Foreground = OrblitColors.InkMid,
        TextWrapping = TextWrapping.Wrap,
    };

    /// <summary>A name: one line, cut short rather than wrapped.</summary>
    public static TextBlock Title(string text, string? name = null, IBrush? tone = null) => new()
    {
        Name = name,
        Text = text,
        FontSize = OrblitText.Body,
        FontWeight = FontWeight.SemiBold,
        Foreground = tone ?? OrblitColors.Ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    public static TextBlock Mono(string text, string? name = null, double size = OrblitText.Caption) => new()
    {
        Name = name,
        Text = text,
        FontFamily = OrblitText.Mono,
        FontSize = size,
        Foreground = OrblitColors.InkDim,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    public static TextBlock Section(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 10.5,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = 0.9,
        Foreground = OrblitColors.InkDim,
    };

    public static Button Action(string name, string text, bool primary = false)
    {
        var button = new Button { Name = name, Content = text, VerticalAlignment = VerticalAlignment.Center };
        if (primary)
        {
            button.Classes.Add("accent");
        }

        return button;
    }

    /// <summary>A column no wider than a page should be, centred when the window is wider.</summary>
    public static StackPanel Column(double maxWidth, double spacing) => new()
    {
        MaxWidth = maxWidth,
        Spacing = spacing,
        Margin = new Thickness(Space.Xxl, Space.Xl),
        HorizontalAlignment = HorizontalAlignment.Stretch,
    };
}

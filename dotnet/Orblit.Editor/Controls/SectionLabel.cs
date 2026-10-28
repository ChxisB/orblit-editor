using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

/// <summary>The small capitals over a group of rows in a panel or dialog.</summary>
internal static class SectionLabel
{
    public static TextBlock Of(string text) => new()
    {
        Text = text.ToUpperInvariant(),
        FontSize = 9.5,
        LetterSpacing = 0.6,
        Foreground = OrblitColors.InkDim,
        Margin = new Thickness(0, Space.Sm, 0, 3),
    };
}

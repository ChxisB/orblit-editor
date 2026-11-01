using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>Why there is nothing to look at, said and not left blank.</summary>
/// <remarks>
/// The panel lives in a dock that can be dragged down to a couple of rows. The icon is the part worth
/// losing there, and the words were carrying the meaning anyway.
/// </remarks>
internal sealed class PreviewNote : Border
{
    // Below this the icon over two lines of words does not fit.
    private const double RoomForIcon = 84;

    private readonly Glyph? _icon;

    /// <param name="saying">What to say.</param>
    /// <param name="icon">Path data for a picture above the words, or null for none.</param>
    public PreviewNote(string saying, string? icon = null)
    {
        Padding = new Thickness(Space.Sm);
        var column = new StackPanel { Spacing = Space.Sm, VerticalAlignment = VerticalAlignment.Center };
        if (icon is not null)
        {
            _icon = new Glyph(icon, 26, OrblitColors.InkDim) { HorizontalAlignment = HorizontalAlignment.Center };
            column.Children.Add(_icon);
        }

        column.Children.Add(new TextBlock
        {
            Name = "preview_saying",
            Text = saying,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 3,
            FontSize = 11,
            Foreground = OrblitColors.InkDim,
        });
        Child = column;
        SizeChanged += (_, e) =>
        {
            if (_icon is not null)
            {
                _icon.IsVisible = e.NewSize.Height >= RoomForIcon;
            }
        };
    }
}

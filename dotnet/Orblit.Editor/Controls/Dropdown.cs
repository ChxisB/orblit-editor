using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Orblit.Editor;

/// <summary>A button that reads what is chosen and opens a menu of the other choices, for a dialog that has no history to apply them through.</summary>
internal static class Dropdown
{
    /// <summary>A button called <paramref name="name"/> that reads <paramref name="shown"/>.</summary>
    /// <param name="name">The button's name. The items have none, because a list of bone names can hold two that differ only in punctuation and a window cannot hold two controls of one name.</param>
    /// <param name="shown">What the button reads while the menu is closed.</param>
    /// <param name="items">The choices, asked for each time the menu opens.</param>
    /// <param name="ink">The colour of what the button reads, or null for the usual one.</param>
    public static Button Of(string name, string shown, Func<IEnumerable<(string Text, Action Choose)>> items, IBrush? ink = null)
    {
        var menu = new MenuFlyout();
        // Built when opened. A row for every bone of a skeleton would otherwise make a menu item for every
        // driver at every change.
        menu.Opening += (_, _) =>
        {
            menu.Items.Clear();
            foreach (var (text, choose) in items())
            {
                var item = new MenuItem { Header = text };
                // After the click, which is on a menu that a rebuilt dialog would take away.
                item.Click += (_, _) => Dispatcher.UIThread.Post(choose);
                menu.Items.Add(item);
            }
        };

        return new Button
        {
            Name = name,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = OrblitColors.Raised,
            CornerRadius = Radii.Control,
            Flyout = menu,
            Content = new TextBlock
            {
                Text = shown,
                FontSize = OrblitText.Label,
                Foreground = ink ?? OrblitColors.Ink,
                TextTrimming = TextTrimming.CharacterEllipsis,
            },
        };
    }
}

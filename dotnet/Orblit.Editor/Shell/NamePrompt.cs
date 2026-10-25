using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Orblit.Editor;

/// <summary>A small window that asks for one word, such as the name to save a layout under.</summary>
public sealed class NamePrompt : Window
{
    private readonly TextBox _name = new() { Name = "name", MinWidth = 240 };

    public NamePrompt(string title, string label, string initial = "")
    {
        _name.Text = initial;
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var ok = new Button { Name = "ok", Content = "OK", IsDefault = true };
        ok.Click += (_, _) => Close(_name.Text?.Trim());
        var cancel = new Button { Name = "cancel", Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);

        var column = new StackPanel { Margin = new Thickness(Space.Lg), Spacing = Space.Sm };
        column.Children.Add(new TextBlock { Text = label, Foreground = OrblitColors.InkMid, FontSize = OrblitText.Label });
        column.Children.Add(_name);
        column.Children.Add(buttons);
        Content = column;
    }

    /// <summary>Asks, and returns what was typed, or null when it was cancelled.</summary>
    public static Task<string?> Ask(Window owner, string title, string label, string initial = "") =>
        new NamePrompt(title, label, initial).ShowDialog<string?>(owner);
}

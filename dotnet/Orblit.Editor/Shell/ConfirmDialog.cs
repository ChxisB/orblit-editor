using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>A small window that asks whether to go ahead with something that cannot be undone.</summary>
public sealed class ConfirmDialog : Window
{
    public ConfirmDialog(string title, string text, string action)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Answer("cancel", "Cancel", false, isCancel: true));
        buttons.Children.Add(Answer("ok", action, true));

        var column = new StackPanel { Margin = new Thickness(Space.Lg), Spacing = Space.Md, MaxWidth = 360 };
        column.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = OrblitColors.InkMid,
            FontSize = OrblitText.Label,
            TextWrapping = TextWrapping.Wrap,
        });
        column.Children.Add(buttons);
        Content = column;
    }

    /// <summary>Asks, and returns whether to go ahead. Closing the window any other way means no.</summary>
    public static Task<bool> Ask(Window owner, string title, string text, string action) =>
        new ConfirmDialog(title, text, action).ShowDialog<bool>(owner);

    private Button Answer(string name, string text, bool answer, bool isCancel = false)
    {
        var button = new Button { Name = name, Content = text, IsCancel = isCancel };
        button.Click += (_, _) => Close(answer);
        return button;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>
/// Asks whether a project may run its own code on this computer. Cancel is the default, so a press of
/// Enter that nobody meant starts nothing.
/// </summary>
public sealed class TrustDialog : Window
{
    public TrustDialog(string folder)
    {
        Title = "Trust this project?";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Answer("cancel", "Cancel", false, isDefault: true, isCancel: true));
        buttons.Children.Add(Answer("trust", "Trust and Play", true));

        var column = new StackPanel { Margin = new Thickness(Space.Lg), Spacing = Space.Md, MaxWidth = 420 };
        column.Children.Add(new TextBlock
        {
            Name = "folder",
            Text = folder,
            Foreground = OrblitColors.Ink,
            FontFamily = OrblitText.Mono,
            FontSize = OrblitText.Label,
            TextWrapping = TextWrapping.Wrap,
        });
        column.Children.Add(Words("Playing this project builds its game and runs it. That code runs on this computer and can reach your files. Trust it only if it came from someone you trust.", OrblitColors.InkMid));
        column.Children.Add(Words("The trust is for this folder and not for one version of it. Code that changes later, for example after a pull, stays trusted. To take it back, use Stop trusting this project in the File menu.", OrblitColors.InkDim));
        column.Children.Add(buttons);
        Content = column;
    }

    /// <summary>Asks, and returns whether to go ahead. Closing the window any other way means no.</summary>
    public static Task<bool> Ask(Window owner, string folder) =>
        new TrustDialog(folder).ShowDialog<bool>(owner);

    private static TextBlock Words(string text, IBrush colour) => new()
    {
        Text = text,
        Foreground = colour,
        FontSize = OrblitText.Label,
        TextWrapping = TextWrapping.Wrap,
    };

    private Button Answer(string name, string text, bool answer, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button { Name = name, Content = text, IsDefault = isDefault, IsCancel = isCancel };
        button.Click += (_, _) => Close(answer);
        return button;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>What the compiler said about a script that did not build, or built with warnings.</summary>
internal sealed class BuildDialog : Window
{
    public BuildDialog(string title, string output)
    {
        Title = title;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var close = new Button { Name = "close", Content = "Close", IsCancel = true, IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(Space.Lg),
            Spacing = Space.Md,
            Children =
            {
                new TextBlock { Text = title, FontSize = OrblitText.Body, Foreground = OrblitColors.Ink },
                new ScrollViewer
                {
                    Width = 640,
                    Height = 320,
                    Content = new SelectableTextBlock
                    {
                        Name = "build_output",
                        Text = output.Length == 0 ? "The compiler said nothing." : output,
                        FontFamily = OrblitText.Mono,
                        FontSize = OrblitText.Caption,
                        Foreground = OrblitColors.Ink,
                        TextWrapping = TextWrapping.Wrap,
                    },
                },
                close,
            },
        };
    }

    /// <summary>Shows what was said over <paramref name="owner"/> until it is closed.</summary>
    public static Task Tell(Window owner, string title, string output) => new BuildDialog(title, output).ShowDialog(owner);
}

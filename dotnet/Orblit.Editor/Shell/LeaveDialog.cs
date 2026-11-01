using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>Asks what to do with a scene whose changes are not on disk, before they are lost.</summary>
public sealed class LeaveDialog : Window
{
    public LeaveDialog(SceneEntry entry)
    {
        Title = $"Save {entry.Title} first?";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(Answer("cancel", "Cancel", Leaving.Stop, isCancel: true));
        buttons.Children.Add(Answer("discard", "Discard", Leaving.Discard));
        buttons.Children.Add(Answer("save", "Save", Leaving.Save, isDefault: true));

        var column = new StackPanel { Margin = new Thickness(Space.Lg), Spacing = Space.Md, MaxWidth = 360 };
        column.Children.Add(new TextBlock
        {
            Text = entry.Path is null
                ? "It has never been written to disk. Closing it loses it."
                : "It has changes that have not been written to disk.",
            Foreground = OrblitColors.InkMid,
            FontSize = OrblitText.Label,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });
        column.Children.Add(buttons);
        Content = column;
    }

    /// <summary>Asks, and returns the answer. Closing the window any other way means stop.</summary>
    public static Task<Leaving> Ask(Window owner, SceneEntry entry) =>
        new LeaveDialog(entry).ShowDialog<Leaving>(owner);

    private Button Answer(string name, string text, Leaving answer, bool isDefault = false, bool isCancel = false)
    {
        var button = new Button { Name = name, Content = text, IsDefault = isDefault, IsCancel = isCancel };
        button.Click += (_, _) => Close(answer);
        return button;
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Editor;

/// <summary>Says that something could not be done, and why, with one way to dismiss it.</summary>
public sealed class Complaint : Window
{
    public Complaint(string title, string detail)
    {
        Title = title;
        MaxWidth = 380;
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;

        var close = Widgets.Action("close", "Close", primary: true);
        close.IsDefault = true;
        close.IsCancel = true;
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();

        var column = new StackPanel { Margin = new Thickness(Space.Xl), Spacing = Space.Md };
        column.Children.Add(Widgets.Title(title, "complaint_title"));
        column.Children.Add(Widgets.Body(detail, "complaint_detail"));
        column.Children.Add(close);
        Content = column;
    }
}

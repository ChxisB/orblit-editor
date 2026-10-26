using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

/// <summary>A switch that reads as on or off at a glance. It says what it did and leaves the value to its owner.</summary>
internal sealed class OrblitSwitch : Border
{
    public OrblitSwitch(string label, bool value, Action<bool> onChanged)
    {
        Width = 30;
        Height = 16;
        CornerRadius = new CornerRadius(8);
        Background = value ? OrblitColors.Ember : OrblitColors.Line;
        Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        Child = new Border
        {
            Width = 12,
            Height = 12,
            CornerRadius = new CornerRadius(6),
            Background = OrblitColors.Ink,
            Margin = new Thickness(2),
            HorizontalAlignment = value ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
        IsOn = value;
        ToolTip.SetTip(this, label);
        PointerPressed += (_, press) =>
        {
            press.Handled = true;
            onChanged(!IsOn);
        };
    }

    public bool IsOn { get; }
}

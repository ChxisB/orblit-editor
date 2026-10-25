using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;

namespace Orblit.Editor;

/// <summary>
/// A raised panel that can be chosen by a click, or by Enter or Space when it has focus, and that
/// lights when the pointer is over it.
/// </summary>
public sealed class Card : Border
{
    private Control? _revealed;

    public Card(string name, string label, Control content, bool enabled = true)
    {
        Name = name;
        Child = content;
        Focusable = true;
        Padding = new Thickness(8, 8, 8, 14);
        CornerRadius = Radii.Window;
        BorderThickness = new Thickness(1);
        BorderBrush = OrblitColors.LineSoft;
        Background = OrblitColors.Surface;
        Cursor = new Cursor(enabled ? StandardCursorType.Hand : StandardCursorType.Arrow);
        AutomationProperties.SetName(this, label);
    }

    public event Action? Clicked;

    /// <summary>
    /// A part that shows only while the pointer or the keyboard is on the card. Keyboard focus counts,
    /// or a control that appears on hover could never be reached without a mouse.
    /// </summary>
    public Control? Revealed
    {
        set
        {
            _revealed = value;
            ShowRevealed();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            Background = IsPointerOver ? OrblitColors.Raised : OrblitColors.Surface;
            ShowRevealed();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Clicked?.Invoke();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Source == this && e.Key is Key.Enter or Key.Space)
        {
            e.Handled = true;
            Clicked?.Invoke();
        }
    }

    private void ShowRevealed()
    {
        if (_revealed is not null)
        {
            _revealed.IsVisible = IsPointerOver || IsKeyboardFocusWithin;
        }
    }
}

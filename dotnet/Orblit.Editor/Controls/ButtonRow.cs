using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

/// <summary>
/// Buttons side by side in equal shares, or one above another when a narrow panel would cut a word
/// off one of them. Stacked rather than squeezed, because the words are what tell somebody new what
/// each one does.
/// </summary>
internal sealed class ButtonRow : Panel
{
    private bool _stacked;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0)
        {
            return default;
        }

        var gaps = Space.Xs * (Children.Count - 1);
        var share = (availableSize.Width - gaps) / Children.Count;
        foreach (var child in Children)
        {
            child.Measure(Size.Infinity);
        }

        // A word on a button does not wrap, so its size with no limit is the size it needs in either shape.
        _stacked = Children.Any(child => child.DesiredSize.Width > share);
        return _stacked
            ? new Size(Children.Max(child => child.DesiredSize.Width), Children.Sum(child => child.DesiredSize.Height) + gaps)
            : new Size(Children.Sum(child => child.DesiredSize.Width) + gaps, Children.Max(child => child.DesiredSize.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var share = (finalSize.Width - (Space.Xs * (Children.Count - 1))) / Children.Count;
        var at = 0.0;
        foreach (var child in Children)
        {
            var place = _stacked
                ? new Rect(0, at, finalSize.Width, child.DesiredSize.Height)
                : new Rect(at, 0, share, finalSize.Height);
            child.Arrange(place);
            at += (_stacked ? place.Height : place.Width) + Space.Xs;
        }

        return finalSize;
    }
}

using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

/// <summary>
/// Cards side by side, as many to a row as the width holds at a readable size. Every grid on the
/// launcher uses the same column width, so the template cards sit over the project cards in straight
/// lines, and each row is as tall as its tallest card.
/// </summary>
public sealed class CardGrid : Panel
{
    private const double Gap = Space.Lg;
    private const double Wanted = 280;
    private const int Most = 3;

    internal static int ColumnsFor(double width) => Math.Clamp((int)((width + Gap) / (Wanted + Gap)), 1, Most);

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? Wanted * Most + Gap * (Most - 1) : availableSize.Width;
        var columns = ColumnsFor(width);
        var cell = new Size((width - Gap * (columns - 1)) / columns, double.PositiveInfinity);
        foreach (var child in Children)
        {
            child.Measure(cell);
        }

        var rows = RowHeights(columns);
        return new Size(width, rows.Sum() + Gap * Math.Max(0, rows.Count - 1));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var columns = ColumnsFor(finalSize.Width);
        var cell = (finalSize.Width - Gap * (columns - 1)) / columns;
        var rows = RowHeights(columns);
        var top = 0.0;
        for (var row = 0; row < rows.Count; row++)
        {
            for (var at = row * columns; at < Math.Min(Children.Count, (row + 1) * columns); at++)
            {
                Children[at].Arrange(new Rect((at % columns) * (cell + Gap), top, cell, rows[row]));
            }

            top += rows[row] + Gap;
        }

        return finalSize;
    }

    private List<double> RowHeights(int columns) =>
        [.. Children.Chunk(columns).Select(row => row.Max(child => child.DesiredSize.Height))];
}

namespace Orblit.Editor.Panels;

/// <summary>
/// Which rows of a long list are built: the ones in view and a few on each side, so a short scroll
/// finds its rows already there. Rows are one height, so the answer is arithmetic and not a walk.
/// </summary>
public readonly record struct RowWindow(int First, int Count)
{
    /// <summary>Rows built past each edge of the view.</summary>
    public const int Spare = 8;

    /// <param name="count">How many rows the list has.</param>
    /// <param name="offset">How far the list is scrolled, from its top.</param>
    /// <param name="view">How tall the view is.</param>
    /// <param name="rowHeight">The height of every row.</param>
    public static RowWindow Of(int count, double offset, double view, double rowHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowHeight);

        var end = Math.Clamp((int)Math.Ceiling((Math.Max(offset, 0) + Math.Max(view, 0)) / rowHeight) + Spare, 0, count);
        var first = Math.Clamp((int)Math.Floor(Math.Max(offset, 0) / rowHeight) - Spare, 0, end);
        return new RowWindow(first, end - first);
    }
}

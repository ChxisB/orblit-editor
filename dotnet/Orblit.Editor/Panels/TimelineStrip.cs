using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// One thin row between the ruler and the sheet: a name on the left, and on the right a lane whose pieces
/// stand at the times they are for, so they line up with the ruler above.
/// </summary>
/// <remarks>
/// The pieces are made again only when the clip, its shots or the width change. A playing clip changes
/// the bench every frame, and a piece rebuilt for each one would close the menu it opened.
/// </remarks>
internal sealed class TimelineStrip : Grid
{
    private readonly ClipBench _bench;
    private readonly Func<TimeAxis, IEnumerable<Control>> _pieces;
    private readonly Canvas _lane = new() { ClipToBounds = true };
    private readonly StackPanel _actions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

    // What the pieces were last made from.
    private object? _clip;
    private object? _shots;
    private double _width = -1;

    public TimelineStrip(string name, string label, ClipBench bench, Func<TimeAxis, IEnumerable<Control>> pieces)
    {
        _bench = bench;
        _pieces = pieces;
        Name = name;
        Height = TimelineTracks.RowHeight;
        ColumnDefinitions = new ColumnDefinitions($"{TimelineTracks.NamesWidth},*");
        var title = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        title.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = OrblitText.Caption,
            Foreground = OrblitColors.InkDim,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(_actions, 1);
        title.Children.Add(_actions);

        var names = new Border
        {
            Padding = new Thickness(Space.Sm, 0, Space.Xs, 0),
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = title,
        };
        var lane = new Border
        {
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = _lane,
        };
        Grid.SetColumn(lane, 1);
        Children.Add(names);
        Children.Add(lane);
        _lane.SizeChanged += (_, _) => Refresh();
    }

    /// <summary>Puts a button at the right end of the strip's name.</summary>
    public void AddToName(Control control) => _actions.Children.Add(control);

    public void Refresh()
    {
        if (_bench.Clip is not { } clip)
        {
            return;
        }

        var shots = _bench.Shown?.Shots;
        var width = _lane.Bounds.Width;
        if (ReferenceEquals(clip, _clip) && ReferenceEquals(shots, _shots) && width == _width)
        {
            return;
        }

        _clip = clip;
        _shots = shots;
        _width = width;
        _lane.Children.Clear();
        foreach (var piece in _pieces(new TimeAxis(clip, width)))
        {
            _lane.Children.Add(piece);
        }
    }
}

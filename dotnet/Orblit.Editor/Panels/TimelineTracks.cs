using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>
/// What the clip moves, one row for each thing and one for each channel of it, in step with the rows of
/// the sheet beside it.
/// </summary>
internal sealed class TimelineTracks : StackPanel
{
    /// <summary>The height of every row here and in the sheet, so a channel and its keys line up.</summary>
    public const double RowHeight = 22;

    /// <summary>The width of the names column, which the ruler, the strips and the sheet start after.</summary>
    public const double NamesWidth = 200;

    private readonly ClipBench _bench;
    private readonly TimelineModel _model;

    // What the rows were last built from. A playing clip changes the bench every frame and none of it
    // changes the rows, which are rebuilt only when one of these does. The names are the headings' own,
    // which a rename or an undo changes with the clip left as it was.
    private ClipDocument? _clip;
    private ChannelAddress? _curve;
    private string? _owner;
    private string _names = string.Empty;

    public TimelineTracks(ClipBench bench, TimelineModel model)
    {
        _bench = bench;
        _model = model;
        Name = "timeline_tracks";
    }

    /// <summary>Lists <paramref name="clip"/>'s rows, unless the list already is that.</summary>
    public void Show(ClipDocument clip)
    {
        var names = NamesOf(clip);
        if (ReferenceEquals(clip, _clip) && _bench.Curve == _curve && _bench.Owner == _owner && names == _names)
        {
            return;
        }

        _clip = clip;
        _curve = _bench.Curve;
        _owner = _bench.Owner;
        _names = names;
        Children.Clear();
        var index = 0;
        foreach (var row in TimelineRow.Of(clip))
        {
            Children.Add(row.Channel is { } channel ? ChannelRow(index, channel) : Heading(index, row.Target));
            index++;
        }
    }

    private string NamesOf(ClipDocument clip) =>
        string.Join('\n', clip.Channels.Select(channel => _bench.ObjectOf(channel.Target)?.Name));

    private Border Heading(int index, string target)
    {
        var thing = _bench.ObjectOf(target);
        var missing = thing is null && target.Length > 0;
        var name = thing?.Name ?? (target.Length == 0 ? "Whatever plays it" : target);
        var row = new Border
        {
            Name = $"timeline_heading_{index}",
            Height = RowHeight,
            Background = OrblitColors.Raised,
            Padding = new Thickness(Space.Sm, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = Space.Xs,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Glyph(missing ? TimelineIcons.Unlinked : AssetIcons.Of(AssetKind.Mesh), 12, missing ? OrblitColors.Warn : OrblitColors.InkDim),
                    new TextBlock
                    {
                        Text = name,
                        FontSize = OrblitText.Label,
                        Foreground = missing ? OrblitColors.Warn : OrblitColors.Ink,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };
        if (missing)
        {
            ToolTip.SetTip(row, "Nothing is called this where the clip is played.");
        }

        return row;
    }

    private Border ChannelRow(int index, ClipChannel channel)
    {
        var address = ClipEdits.AddressOf(channel);
        var chosen = _bench.Curve == address;
        var close = new Border
        {
            Name = $"timeline_close_{index}",
            Width = 18,
            Height = 18,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            IsVisible = false,
            Child = new Glyph(TimelineIcons.Close, 11),
        };
        ToolTip.SetTip(close, "Remove this channel and its keys");
        close.PointerPressed += (_, press) =>
        {
            press.Handled = true;
            _model.RemoveChannel(address);
        };

        var row = new Border
        {
            Name = $"timeline_channel_{index}",
            Height = RowHeight,
            Padding = new Thickness(Space.Xl, 0, Space.Xs, 0),
            Background = chosen ? OrblitColors.EmberWash : Brushes.Transparent,
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new TextBlock
                    {
                        Text = TimelineWords.Channel(address),
                        FontSize = OrblitText.Label,
                        Foreground = chosen ? OrblitColors.Ink : OrblitColors.InkMid,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                },
            },
        };
        Grid.SetColumn(close, 1);
        ((Grid)row.Child).Children.Add(close);
        row.PointerPressed += (_, _) => _bench.Curve = address;
        row.PointerEntered += (_, _) =>
        {
            close.IsVisible = true;
            row.Background = chosen ? OrblitColors.EmberWash : OrblitColors.Hover;
        };
        row.PointerExited += (_, _) =>
        {
            close.IsVisible = false;
            row.Background = chosen ? OrblitColors.EmberWash : Brushes.Transparent;
        };
        return row;
    }
}

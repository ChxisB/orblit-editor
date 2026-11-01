using Avalonia;
using Avalonia.Controls;
using IconPath = Avalonia.Controls.Shapes.Path;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Orblit.Editor;

using Orblit.Editor.Viewport;

/// <summary>The strip of tools down the left of the view: one tile for each handle mode.</summary>
internal sealed class ToolPalette : Border
{
    private const double Tile = 32;

    // Path data of the two icons, open_with and threesixty, 24 units square.
    private static readonly Dictionary<GizmoMode, string> Icons = new()
    {
        [GizmoMode.Move] = "M10 9h4V6h3l-5-5-5 5h3v3zm-1 1H6V7l-5 5 5 5v-3h3v-4zm14 2l-5-5v3h-3v4h3v3l5-5zm-9 3h-4v3H7l5 5 5-5h-3v-3z",
        [GizmoMode.Rotate] = "M12 7C6.48 7 2 9.24 2 12c0 2.24 2.94 4.13 7 4.77V20l4-4-4-4v2.73c-3.15-.56-5-1.9-5-2.73 0-1.06 3.04-3 8-3s8 1.94 8 3c0 .73-1.46 1.89-4 2.53v2.05c3.53-.77 6-2.53 6-4.58 0-2.76-4.48-5-10-5z",
    };

    private readonly SceneViewportState _state;
    private readonly Dictionary<GizmoMode, Border> _tiles = [];

    public ToolPalette(SceneViewportState state)
    {
        _state = state;
        Background = new ImmutableSolidColorBrush(Color.FromUInt32(0xCC161A21));
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(1);
        CornerRadius = Radii.Control;
        Padding = new Thickness(3);
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, 0, Space.Sm, 0);

        var column = new StackPanel { Spacing = Space.Xs };
        foreach (var mode in Enum.GetValues<GizmoMode>())
        {
            var tile = TileFor(mode);
            _tiles[mode] = tile;
            column.Children.Add(tile);
        }

        Child = column;
        Sync();
    }

    /// <summary>Lights the tile of the mode the view is in.</summary>
    public void Sync()
    {
        foreach (var (mode, tile) in _tiles)
        {
            var lit = mode == _state.Mode;
            tile.Background = lit ? OrblitColors.EmberWash : Brushes.Transparent;
            ((IconPath)tile.Child!).Fill = lit ? OrblitColors.Ember : OrblitColors.InkMid;
        }
    }

    private Border TileFor(GizmoMode mode)
    {
        var tile = new Border
        {
            Name = $"tool_{mode.ToString().ToLowerInvariant()}",
            Width = Tile,
            Height = Tile,
            CornerRadius = Radii.Control,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new IconPath
            {
                Data = Geometry.Parse(Icons[mode]),
                Stretch = Stretch.Uniform,
                Width = 18,
                Height = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        ToolTip.SetTip(tile, mode.Label());
        tile.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _state.SelectMode(mode);
        };
        return tile;
    }
}

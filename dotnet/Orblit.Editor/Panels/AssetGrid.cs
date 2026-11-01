using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>What the grid and its tiles tell the browser.</summary>
/// <param name="Select">A file or folder was picked.</param>
/// <param name="Open">A file or folder was double clicked.</param>
/// <param name="Menu">A menu was asked for, on a file or on the empty space (null), from a control.</param>
/// <param name="DropObject">An object was dropped on the files, or null where nothing takes one.</param>
internal sealed record AssetGridLinks(Action<Asset> Select, Action<Asset> Open, Action<Asset?, Control> Menu, Action<string>? DropObject);

/// <summary>The files in the open folder.</summary>
internal sealed class AssetGrid : UserControl
{
    private const double Gap = 8;

    // The room around the tiles. Each tile carries half the gap itself, so these are less by that much.
    private const double SideRoom = 6 + 12;

    private readonly AssetGridLinks _links;
    private readonly ContentControl _host = new();
    private readonly UniformGrid _tiles = new() { Margin = new Thickness(2, 2, 8, 8) };
    private readonly ScrollViewer _scroll = new();
    private readonly List<AssetTile> _all = [];

    public AssetGrid(AssetGridLinks links)
    {
        _links = links;
        _scroll.Content = _tiles;
        _host.Content = _scroll;

        var drop = new Border
        {
            IsVisible = false,
            IsHitTestVisible = false,
            Background = OrblitColors.EmberWash,
            BorderBrush = OrblitColors.Ember,
            BorderThickness = new Thickness(1),
            CornerRadius = Radii.Control,
            Child = new TextBlock
            {
                Text = "Drop to make a prefab",
                FontSize = OrblitText.Label,
                Foreground = OrblitColors.Ink,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        // Opaque, so a right click lands on the gaps between tiles and on an empty folder, which is
        // exactly where somebody reaches for "new".
        var frame = new Grid { Background = Brushes.Transparent };
        frame.Children.Add(_host);
        frame.Children.Add(drop);
        Content = frame;

        ContextRequested += (_, e) =>
        {
            links.Menu(null, this);
            e.Handled = true;
        };
        if (links.DropObject is { } dropped)
        {
            ObjectDrops.Accept(frame, dropped, over => drop.IsVisible = over);
        }

        SizeChanged += (_, e) => _tiles.Columns = ColumnsFor(e.NewSize.Width);
    }

    public IReadOnlyList<AssetTile> Tiles => _all;

    /// <summary>As many as fit without any being narrower than the minimum, so a wide panel gets more tiles and not fatter ones.</summary>
    internal static int ColumnsFor(double width) =>
        Math.Clamp((int)Math.Floor((width - SideRoom + Gap) / (AssetTile.Narrowest + Gap)), 1, 64);

    public void Show(IReadOnlyList<Asset> entries, bool filtering, CookStatusIndex? cook, string? selected)
    {
        _all.Clear();
        _tiles.Children.Clear();
        foreach (var asset in entries)
        {
            var tile = new AssetTile(asset, asset.IsFolder ? null : cook?[asset.Path], _links) { Selected = asset.Path == selected };
            _all.Add(tile);
            _tiles.Children.Add(tile);
        }

        _host.Content = entries.Count > 0 ? _scroll : filtering ? NothingMatches() : Empty();
    }

    /// <summary>Marks the one tile that is picked, without drawing the rest again.</summary>
    public void Mark(string? selected)
    {
        foreach (var tile in _all)
        {
            tile.Selected = tile.Asset.Path == selected;
        }
    }

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.InkDim,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextAlignment = TextAlignment.Center,
    };

    private static TextBlock NothingMatches()
    {
        var note = Note("Nothing here has that name.");
        note.VerticalAlignment = VerticalAlignment.Center;
        return note;
    }

    private StackPanel Empty()
    {
        var make = new Button { Name = "browser_new", Content = "New folder or file", HorizontalAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(make, "Choose what to make in this folder.");
        make.Click += (_, _) => _links.Menu(null, make);
        return new StackPanel
        {
            Spacing = Space.Xs,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { Note("This folder is empty."), Note("Create a file or copy assets into this folder."), make },
        };
    }
}

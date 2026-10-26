using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>One file or folder in the grid: a picture, a name and, beside it, where the cook has it.</summary>
internal sealed class AssetTile : Border
{
    /// <summary>The narrowest a tile is drawn, which is what the grid counts columns by.</summary>
    public const double Narrowest = 112;

    /// <summary>What <see cref="Tag"/> of the cook's dot says, so a test finds the dot among the tiles.</summary>
    public const string CookDot = "cook_dot";

    // Padding, thumbnail, the gap, one line of name, padding.
    private const double TileHeight = 92;

    // Half the gap between tiles, which the grid does not add itself.
    private const double Gap = 4;

    private readonly Glyph _glyph;
    private readonly TextBlock _name;
    private bool _selected;
    private bool _hovering;

    public AssetTile(Asset asset, CookState? state, AssetGridLinks links)
    {
        Asset = asset;
        Height = TileHeight;
        Margin = new Thickness(Gap);
        Padding = new Thickness(6);
        CornerRadius = Radii.Card;
        Cursor = new Cursor(StandardCursorType.Hand);
        ToolTip.SetTip(this, AssetBrowserModel.TipOf(asset, state));
        ToolTip.SetShowDelay(this, 600);

        _glyph = new Glyph(AssetIcons.Of(asset.Kind), 24);
        _name = new TextBlock
        {
            Text = asset.Name,
            FontFamily = OrblitText.Mono,
            FontSize = OrblitText.Caption,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 6, 0, 0),
        };
        _name[Grid.RowProperty] = 1;
        Child = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Children = { Thumbnail(asset, state), _name } };

        PointerEntered += (_, _) => Lit(hovering: true);
        PointerExited += (_, _) => Lit(hovering: false);
        PointerPressed += (_, press) =>
        {
            if (press.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                links.Select(asset);
            }
        };
        DoubleTapped += (_, _) => links.Open(asset);
        ContextRequested += (_, e) =>
        {
            links.Menu(asset, this);
            e.Handled = true;
        };
        Paint();
    }

    public Asset Asset { get; }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Paint();
        }
    }

    private Grid Thumbnail(Asset asset, CookState? state)
    {
        var showsPicture = AssetBrowserModel.ShowsPicture(asset);
        var picture = new Border
        {
            Tag = "thumb",
            Height = 56,
            ClipToBounds = true,
            CornerRadius = Radii.Control,
            // Behind a picture it is the ground, so a texture with transparency reads as transparent
            // and not as a hole.
            Background = showsPicture ? OrblitColors.Ground : OrblitColors.Raised,
            Child = _glyph,
        };
        _glyph.HorizontalAlignment = HorizontalAlignment.Center;
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        if (showsPicture)
        {
            ShowPicture(picture, asset.Path);
        }

        var frame = new Grid { Children = { picture } };
        if (state?.Mark() is { } mark)
        {
            frame.Children.Add(Dot(mark));
        }

        return frame;
    }

    // A dot and not a badge or a strip of text. There are four hundred of these in a folder of
    // textures, and anything larger would compete with the picture it sits on. Ringed in the panel's
    // own colour so it reads on a dark thumbnail and on a bright one alike.
    private static Border Dot(CookMark mark) => new()
    {
        Tag = CookDot,
        Width = 8,
        Height = 8,
        CornerRadius = new CornerRadius(4),
        Background = OrblitColors.For(mark),
        BorderBrush = OrblitColors.Surface,
        BorderThickness = new Thickness(1.5),
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Top,
        IsHitTestVisible = false,
    };

    private static async void ShowPicture(Border picture, string path)
    {
        if (await AssetThumbnails.LoadAsync(path) is { } bitmap)
        {
            picture.Child = new Image { Source = bitmap, Stretch = Stretch.UniformToFill, IsHitTestVisible = false };
        }
    }

    private void Lit(bool hovering)
    {
        _hovering = hovering;
        Paint();
    }

    private void Paint()
    {
        Background = _selected ? OrblitColors.EmberWash : _hovering ? OrblitColors.Hover : Brushes.Transparent;
        _glyph.Ink = Asset.IsFolder ? OrblitColors.InkMid : _selected ? OrblitColors.Ember : OrblitColors.InkDim;
        _name.Foreground = _selected ? OrblitColors.Ink : OrblitColors.InkMid;
    }
}

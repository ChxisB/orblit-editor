using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using IoPath = System.IO.Path;

/// <summary>The folders of the project, down the left of the browser.</summary>
internal sealed class AssetFolderTree : Border
{
    private const double TreeWidth = 176;

    private readonly Action<string> _open;
    private readonly Action<string, string>? _dropObject;
    private readonly StackPanel _rows = new() { Margin = new Thickness(6, 6, 6, 8) };

    /// <param name="open">Told the path of the folder that was clicked.</param>
    /// <param name="dropObject">Told the object and the folder when an object is dropped on a row, or null where nothing takes one.</param>
    public AssetFolderTree(Action<string> open, Action<string, string>? dropObject)
    {
        _open = open;
        _dropObject = dropObject;
        Child = new ScrollViewer { Content = _rows };
        Stacked = false;
    }

    /// <summary>
    /// Above the files and not beside them, in a panel too narrow for both. It then runs the panel's
    /// width, with a line under it.
    /// </summary>
    public bool Stacked
    {
        set
        {
            Width = value ? double.NaN : TreeWidth;
            BorderBrush = OrblitColors.LineSoft;
            BorderThickness = value ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 0);
        }
    }

    public void Show(string root, IReadOnlyList<FolderAt> folders, string current)
    {
        _rows.Children.Clear();
        _rows.Children.Add(Row(new FolderRowLook("Project", 0, AssetIcons.Home, Same(current, root)), root));
        foreach (var folder in folders)
        {
            var look = new FolderRowLook(IoPath.GetFileName(folder.Path), folder.Depth + 1, AssetIcons.Of(AssetKind.Folder), Same(current, folder.Path));
            _rows.Children.Add(Row(look, folder.Path));
        }
    }

    private static bool Same(string one, string other) =>
        IoPath.TrimEndingDirectorySeparator(one) == IoPath.TrimEndingDirectorySeparator(other);

    private FolderRow Row(FolderRowLook look, string path) =>
        new(look, () => _open(path), _dropObject is null ? null : id => _dropObject(id, path));
}

/// <summary>How one row of the folder tree looks.</summary>
internal sealed record FolderRowLook(string Name, int Depth, string Icon, bool Selected);

/// <summary>One folder in the tree. Dropping an object on it makes a prefab there without opening it.</summary>
internal sealed class FolderRow : Border
{
    private readonly bool _selected;
    private readonly Glyph _glyph;
    private readonly TextBlock _name;
    private bool _hovering;
    private bool _catching;

    public FolderRow(FolderRowLook look, Action open, Action<string>? dropObject)
    {
        _selected = look.Selected;
        Height = 26;
        Margin = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(Space.Sm + (look.Depth * 12), 0, Space.Sm, 0);
        CornerRadius = Radii.Control;
        BorderThickness = new Thickness(1);
        Cursor = new Cursor(StandardCursorType.Hand);
        Tag = look.Name;

        _glyph = new Glyph(look.Icon, 14);
        _name = new TextBlock
        {
            Text = look.Name,
            FontSize = 12.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.Sm, 0, 0, 0),
        };
        _name[Grid.ColumnProperty] = 1;
        Child = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { _glyph, _name } };

        PointerEntered += (_, _) => Lit(hovering: true);
        PointerExited += (_, _) => Lit(hovering: false);
        PointerPressed += (_, press) =>
        {
            if (press.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                open();
            }
        };
        if (dropObject is not null)
        {
            ObjectDrops.Accept(this, dropObject, over => Catch(over));
        }

        Paint();
    }

    private void Lit(bool hovering)
    {
        _hovering = hovering;
        Paint();
    }

    // Worth showing, because the rows are 26 pixels apart and dropping on the wrong one is easy.
    private void Catch(bool over)
    {
        _catching = over;
        Paint();
    }

    private void Paint()
    {
        Background = _catching ? OrblitColors.EmberWash : _selected ? OrblitColors.Raised : _hovering ? OrblitColors.Hover : Brushes.Transparent;
        BorderBrush = _catching ? OrblitColors.Ember : Brushes.Transparent;
        _glyph.Ink = _selected || _catching ? OrblitColors.Ember : OrblitColors.InkDim;
        _name.Foreground = _selected || _hovering || _catching ? OrblitColors.Ink : OrblitColors.InkMid;
    }
}

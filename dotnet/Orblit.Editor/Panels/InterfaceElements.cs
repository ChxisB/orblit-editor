using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Ui;

/// <summary>
/// The elements of the interface being laid out, as a tree. Press one to choose it, and point at one
/// to mark it on the canvas as well.
/// </summary>
internal sealed class InterfaceElements : UserControl
{
    private const double Indent = 13;

    private readonly InterfaceBench _bench;
    private readonly StackPanel _rows = new() { Name = "interface_element_rows", Margin = new Thickness(6, Space.Sm) };
    private readonly TextBlock _nothing = Fields.Note(InterfaceSideModel.NoElements);
    private UiNode? _built;

    public InterfaceElements(InterfaceBench bench)
    {
        _bench = bench;
        Name = "interface_elements";
        Background = OrblitColors.Surface;
        _nothing.Name = "interface_elements_none";
        _nothing.Margin = new Thickness(Space.Lg);
        Content = new ScrollViewer { Content = new StackPanel { Children = { _nothing, _rows } } };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Follow;
        Follow();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Follow;
        base.OnDetachedFromVisualTree(e);
    }

    // Built again only when the tree changed. Pointing at a row changes the bench too, and a row built
    // again under the pointer is a new control the pointer never entered, so it could not be left.
    private void Follow()
    {
        var root = _bench.Document?.Root;
        _nothing.IsVisible = root is null;
        if (!ReferenceEquals(root, _built))
        {
            _built = root;
            _rows.Children.Clear();
            foreach (var (node, path) in root?.Walk() ?? [])
            {
                _rows.Children.Add(new Row(_bench, node, path));
            }
        }

        foreach (var row in _rows.Children.OfType<Row>())
        {
            row.Mark(InterfaceBench.SamePath(_bench.Selected, row.Path), InterfaceBench.SamePath(_bench.Hovered, row.Path));
        }
    }

    // One element, indented by how deep it sits. The root has no remove, since an interface with
    // nothing in it at all is not one that can be added to.
    private sealed class Row : Border
    {
        private readonly TextBlock _label;
        private readonly Button? _remove;

        public Row(InterfaceBench bench, UiNode node, IReadOnlyList<int> path)
        {
            Path = path;
            var name = UiElements.NameOf(node);
            Name = $"element_{string.Join('_', path)}";
            Height = 24;
            Padding = new Thickness(Space.Sm + (path.Count * Indent), 0, Space.Xs, 0);
            CornerRadius = Radii.Control;
            Cursor = new Cursor(StandardCursorType.Hand);
            _label = new TextBlock
            {
                Text = name,
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };
            var line = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { _label } };
            if (path.Count > 0)
            {
                _remove = new Button
                {
                    Name = $"element_remove_{string.Join('_', path)}",
                    Content = new Glyph(TimelineIcons.Close, 12, OrblitColors.InkDim),
                    Padding = new Thickness(Space.Xs / 2),
                    Background = Brushes.Transparent,
                    IsVisible = false,
                };
                ToolTip.SetTip(_remove, $"Remove {name}");
                _remove.Click += (_, _) => bench.Remove(path);
                _remove[Grid.ColumnProperty] = 1;
                line.Children.Add(_remove);
            }

            Child = line;
            PointerEntered += (_, _) => bench.Hovered = path;
            PointerExited += (_, _) => bench.Hovered = null;
            PointerPressed += (_, press) =>
            {
                if (press.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    bench.Selected = path;
                }
            };
        }

        public IReadOnlyList<int> Path { get; }

        public void Mark(bool chosen, bool pointed)
        {
            Background = chosen ? OrblitColors.EmberWash : pointed ? OrblitColors.Raised : Brushes.Transparent;
            _label.Foreground = chosen ? OrblitColors.Ember : pointed ? OrblitColors.Ink : OrblitColors.InkMid;
            if (_remove is not null)
            {
                _remove.IsVisible = pointed;
            }
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Ui;
using Orblit.Ui.Avalonia;

/// <summary>
/// The interface being laid out, drawn on the screen it is previewed at. Press an element to choose
/// it, drag one that sits in a stack to move it, and point at one to see which it is.
/// </summary>
/// <remarks>
/// The screen is drawn at its own size and then scaled into the panel. An interface laid out into
/// the panel would reflow to fit it, and the editor would be showing a different interface from the
/// game.
/// </remarks>
internal sealed class InterfaceCanvas : UserControl
{
    private const double Slop = 4;
    private const double MostZoom = 4;
    private const double LeastScale = 0.05;
    private const double MostScale = 8;

    // Darker than the canvas ground, so the bars a fit leaves read as space the game will not draw in.
    private static readonly IBrush Bars = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF07090C));
    // Under the interface, so an element with no background shows against something.
    private static readonly IBrush Paper = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF11141A));
    private static readonly IBrush Bezel = new ImmutableSolidColorBrush(OrblitColors.InkDimColor, 0.6);
    private static readonly IBrush Pointed = new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.5);
    private static readonly IBrush Faint = new ImmutableSolidColorBrush(OrblitColors.EmberColor, 0.14);

    private readonly InterfaceBench _bench;
    private readonly UiSurface _surface = new() { Name = "interface_surface" };
    private readonly InterfaceGuides _guides = new() { Name = "interface_guides" };
    private readonly Panel _page = new() { Name = "interface_page" };
    private readonly Viewbox _fit;
    private readonly Panel _screen;
    private readonly Border _edge = new() { Name = "interface_device_edge", BorderBrush = Bezel, BorderThickness = new Thickness(2), IsHitTestVisible = false };
    private readonly Viewbox _zoom;
    private Drag? _drag;

    /// <param name="bench">The interface shown, how it is looked at, and what a press or a drag changes.</param>
    public InterfaceCanvas(InterfaceBench bench)
    {
        _bench = bench;
        Name = "interface_canvas";
        Background = OrblitColors.Ground;
        _page.Children.Add(_surface);
        _page.Children.Add(_guides);
        _fit = new Viewbox { Stretch = Stretch.Fill, Child = _page };
        // Clipped to the screen. A canvas bigger than the device is exactly what somebody needs to see,
        // and letting it spill past the edge would hide it.
        _screen = new Panel { Name = "interface_screen", Background = Bars, ClipToBounds = true, Children = { _fit, _edge } };
        _zoom = new Viewbox { Margin = new Thickness(Space.Xl), Child = _screen };
        Content = _zoom;
        // Tunnelling, so a press chooses an element before a button in the interface takes it.
        AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        AddHandler(PointerReleasedEvent, (_, _) => EndDrag(), RoutingStrategies.Tunnel);
        PointerCaptureLost += (_, _) => EndDrag();
        PointerExited += (_, _) => Leave();
        Follow();
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

    // Sets what changed and keeps every control, so the pointer a drag captured stays with the canvas
    // while the interface under it is built again.
    private void Follow()
    {
        _zoom.IsVisible = _bench.Document is not null;
        if (_bench.Document is not { } document)
        {
            return;
        }

        var canvas = document.Canvas;
        var screen = _bench.Preview ?? (canvas.Width, canvas.Height);
        // The width the layout answers to is the screen's only when the canvas follows the screen. A
        // fixed canvas is one layout, and the fit magnifies it.
        var laidOut = canvas.Fit == CanvasFit.Responsive ? screen : (canvas.Width, canvas.Height);
        var inScreen = Math.Clamp(canvas.ScaleFor(screen.Width, screen.Height), LeastScale, MostScale);
        (_screen.Width, _screen.Height) = screen;
        (_zoom.MaxWidth, _zoom.MaxHeight) = (screen.Width * MostZoom, screen.Height * MostZoom);
        (_fit.Width, _fit.Height) = (laidOut.Width * inScreen, laidOut.Height * inScreen);
        (_page.Width, _page.Height) = laidOut;
        _surface.Description = document.Root;
        _surface.Canvas = canvas;
        _surface.Strings = _bench.Strings;
        _surface.KnownWidth = laidOut.Width;
        Dress(canvas, screen != laidOut || inScreen != 1);
    }

    // The editor's own marks over the interface. Previewing takes them away, along with the wrappers
    // that make an element pressable, so what is left is what the game builds.
    private void Dress(UiCanvas canvas, bool scaled)
    {
        var designing = !_bench.Previewing;
        _page.Background = designing ? Paper : null;
        _surface.Decorate = designing ? new UiDecorator(Decorate) : null;
        _guides.Show(canvas, edges: designing && _bench.Guides, columns: designing && _bench.Columns);
        _edge.IsVisible = designing && _bench.Guides && scaled;
        foreach (var element in _surface.GetVisualDescendants().OfType<Outlined>())
        {
            Outline(element);
        }
    }

    private Outlined Decorate(UiNode node, IReadOnlyList<int> path, Control built)
    {
        var element = new Outlined(path, built, IsFree(path));
        Outline(element);
        return element;
    }

    // An element in a stack is placed by its own left and top, so it is the one kind a drag can move.
    private bool IsFree(IReadOnlyList<int> path) =>
        path.Count > 0 && _bench.Document?.Root.At([.. path.Take(path.Count - 1)])?.Type == "stack";

    private void Outline(Outlined element)
    {
        var chosen = InterfaceBench.SamePath(_bench.Selected, element.Path);
        var pointed = InterfaceBench.SamePath(_bench.Hovered, element.Path);
        element.Outline.BorderThickness = new Thickness(chosen ? 1.5 : 1);
        element.Outline.BorderBrush = chosen ? OrblitColors.Ember : pointed ? Pointed : _bench.Outlines ? Faint : null;
    }

    private static Outlined? ElementUnder(RoutedEventArgs e) => (e.Source as Visual)?.FindAncestorOfType<Outlined>(includeSelf: true);

    // The innermost element under the pointer is the one chosen, since that is what pressing on a box
    // inside a box means. The press is taken, so a button being laid out is not also pressed.
    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || ElementUnder(e) is not { } element)
        {
            return;
        }

        e.Handled = true;
        _bench.Selected = element.Path;
        if (element.Free && _bench.Document?.Root.At(element.Path) is { } node)
        {
            _drag = new Drag(element.Path, e.GetPosition(this), e.GetPosition(_page), node.Placed ?? (0, 0));
            e.Pointer.Capture(this);
        }
    }

    // Measured from the press rather than added up a move at a time, so the element stays under the
    // pointer and a snap onto a column edge never holds it there once the pointer has gone on.
    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_drag is not { } drag)
        {
            _bench.Hovered = ElementUnder(e)?.Path;
            return;
        }

        if (!drag.Moving && Point.Distance(e.GetPosition(this), drag.Pressed) < Slop)
        {
            return;
        }

        _drag = drag with { Moving = true };
        var by = e.GetPosition(_page) - drag.From;
        var left = drag.Start.Left + by.X;
        if (_bench.Columns && _bench.Document is { } document)
        {
            left = document.Canvas.SnapAcross(left, _page.Width) ?? left;
        }

        _bench.Move(drag.Path, (left, drag.Start.Top + by.Y));
    }

    private void EndDrag()
    {
        if (_drag is null)
        {
            return;
        }

        _drag = null;
        _bench.MoveDone();
    }

    private void Leave()
    {
        if (_drag is null)
        {
            _bench.Hovered = null;
        }
    }

    /// <summary>
    /// What wraps each element while it is being designed: the element, an outline over it, and where
    /// it is in the tree.
    /// </summary>
    internal sealed class Outlined : Panel
    {
        public Outlined(IReadOnlyList<int> path, Control built, bool free)
        {
            Path = path;
            Free = free;
            // Pressable across its whole box, not only where it draws something.
            Background = Brushes.Transparent;
            Cursor = new Cursor(free ? StandardCursorType.SizeAll : StandardCursorType.Hand);
            Children.Add(built);
            Children.Add(Outline);
        }

        public IReadOnlyList<int> Path { get; }

        /// <summary>Whether it sits in a stack, and so moves when dragged.</summary>
        public bool Free { get; }

        // Over the element rather than around it. A border in the layout would move everything by a
        // pixel and the canvas would stop being what the game shows.
        public Border Outline { get; } = new() { IsHitTestVisible = false };
    }

    // A press on an element that can move: where the pointer went down, in the canvas and in the page,
    // and where the element was. Moving once it has gone further than a shaky click.
    private readonly record struct Drag(IReadOnlyList<int> Path, Point Pressed, Point From, (double Left, double Top) Start)
    {
        public bool Moving { get; init; }
    }
}

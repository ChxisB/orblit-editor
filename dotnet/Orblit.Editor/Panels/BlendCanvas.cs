using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>
/// A blend as boxes and arrows. Press a state or an arrow's label to choose it, press the space between
/// to choose nothing, and drag from the handle on a state onto another to make a change between them.
/// </summary>
internal sealed class BlendCanvas : UserControl
{
    private const double Slop = 4;
    private const double LabelWidth = 190;
    private const double HandleSize = 28;

    private readonly BlendBench _bench;
    // Sized to the sketch, and pinned to the corner because a sized child is otherwise centred in the viewport.
    private readonly Canvas _page = new() { Name = "blend_page", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly Dictionary<string, Border> _boxes = [];
    private BlendSketch? _sketch;
    private BlendArrows? _arrows;
    private Link? _link;

    /// <param name="bench">The blend shown, what is chosen in it, and what a drag changes.</param>
    public BlendCanvas(BlendBench bench)
    {
        _bench = bench;
        Name = "blend_canvas";
        Background = OrblitColors.Ground;
        Content = new ScrollViewer
        {
            Name = "blend_canvas_scroll",
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            Content = _page,
        };
        // Whatever is pressed on the page marks its press handled, so this is only the space between.
        PointerPressed += (_, _) => _bench.Pick = BlendPick.Nothing;
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Rebuild;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Rebuild;
        base.OnDetachedFromVisualTree(e);
    }

    // Children only, so the scroll offset stays where it was.
    private void Rebuild()
    {
        _page.Children.Clear();
        _boxes.Clear();
        _link = null;
        if (_bench.Blend is not { } blend)
        {
            (_sketch, _arrows) = (null, null);
            return;
        }

        var sketch = BlendSketch.Of(blend);
        var picked = _bench.Pick is ChangePicked change ? change.Index : -1;
        (_page.Width, _page.Height) = (sketch.Width, sketch.Height);
        (_sketch, _arrows) = (sketch, new BlendArrows(sketch, picked));
        _page.Children.Add(_arrows);
        var start = BlendGraphModel.StateOwning(blend, blend.WrittenStart);
        foreach (var state in blend.WrittenStates)
        {
            _page.Children.Add(StateBox(state, sketch.States[state.Name], state.Name == start));
        }

        if (sketch.Anywhere is { } any)
        {
            _page.Children.Add(AnyBox(any));
        }

        foreach (var arrow in sketch.Arrows)
        {
            _page.Children.Add(Label(blend, arrow, picked));
        }

        Paint();
    }

    private Border StateBox(BlendState state, SketchBox at, bool isStart)
    {
        var key = Fields.Key(state.Name);
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                Line($"state_name_{key}", state.Name, OrblitText.Label, OrblitColors.Ink, FontWeight.SemiBold),
                Line($"state_plays_{key}", BlendSourceModel.Describe(state.Plays), OrblitText.Caption, OrblitColors.InkDim),
            },
        };
        var box = Frame($"state_{key}", at, Contents(text, Handle($"link_{key}", state.Name), isStart ? Flag(key) : null));
        box.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _bench.Pick = new StatePicked(state.Name);
        };
        _boxes[state.Name] = box;
        return box;
    }

    private Border AnyBox(SketchBox at) => Frame(
        "anywhere",
        at,
        Contents(Line("anywhere_text", "Any state", OrblitText.Label, OrblitColors.InkMid, FontWeight.SemiBold), Handle("link_anywhere", null), null));

    private static TextBlock Line(string name, string text, double size, IBrush ink, FontWeight weight = FontWeight.Normal) => new()
    {
        Name = name,
        Text = text,
        FontSize = size,
        FontWeight = weight,
        Foreground = ink,
        TextTrimming = TextTrimming.CharacterEllipsis,
        MaxLines = 1,
    };

    private static Glyph Flag(string key) => new(TimelineIcons.Flag, 12, OrblitColors.Ember)
    {
        Name = $"start_{key}",
        Margin = new Thickness(0, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static Grid Contents(Control text, Border handle, Glyph? flag)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(handle, 2);
        text.VerticalAlignment = VerticalAlignment.Center;
        if (flag is not null)
        {
            row.Children.Add(flag);
        }

        row.Children.Add(text);
        row.Children.Add(handle);
        return row;
    }

    private static Border Frame(string name, SketchBox at, Control content)
    {
        var box = new Border
        {
            Name = name,
            Width = at.Width,
            Height = at.Height,
            Padding = new Thickness(10, 0, 4, 0),
            CornerRadius = Radii.Card,
            Background = OrblitColors.Surface,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(1),
            Child = content,
        };
        Canvas.SetLeft(box, at.Left);
        Canvas.SetTop(box, at.Top);
        return box;
    }

    private Border Label(BlendDocument blend, SketchArrow arrow, int picked)
    {
        var first = arrow.Changes[0];
        var more = arrow.Changes.Count > 1 ? $" +{arrow.Changes.Count - 1}" : "";
        var isPicked = arrow.Changes.Contains(picked);
        var label = new Border
        {
            Name = $"change_{first}",
            MaxWidth = LabelWidth,
            Padding = new Thickness(8, 4),
            CornerRadius = Radii.Control,
            Background = isPicked ? OrblitColors.EmberWash : OrblitColors.Raised,
            BorderBrush = isPicked ? OrblitColors.Ember : OrblitColors.Line,
            BorderThickness = new Thickness(1),
            Child = Line($"change_text_{first}", BlendConditionModel.Describe(blend.WrittenChanges[first].When) + more, OrblitText.Caption, OrblitColors.Ink),
        };
        label.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            _bench.Pick = new ChangePicked(BlendSketch.ChangeAfter(arrow.Changes, picked));
        };
        // Measured now and not left to layout, so it can be centred on where the arrow is.
        label.Measure(Size.Infinity);
        Canvas.SetLeft(label, arrow.Anchor.X - (arrow.IsLoop ? 0 : label.DesiredSize.Width / 2));
        Canvas.SetTop(label, arrow.Anchor.Y - (label.DesiredSize.Height / 2));
        return label;
    }

    private void Paint()
    {
        var picked = (_bench.Pick as StatePicked)?.Name;
        foreach (var (name, box) in _boxes)
        {
            var over = _link?.Over == name;
            box.Background = name == picked ? OrblitColors.EmberWash : OrblitColors.Surface;
            box.BorderBrush = over || name == picked ? OrblitColors.Ember : OrblitColors.Line;
            box.BorderThickness = new Thickness(over ? 2 : 1);
        }
    }

    private Border Handle(string name, string? from)
    {
        var handle = new Border
        {
            Name = name,
            Width = HandleSize,
            Height = HandleSize,
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Glyph(TimelineIcons.Link, 14, OrblitColors.InkDim),
        };
        ToolTip.SetTip(handle, "Drag onto a state to make a change to it");
        handle.PointerPressed += (_, e) => Press(handle, from, e);
        handle.PointerMoved += (_, e) => Move(e);
        handle.PointerReleased += (_, e) => Release(e);
        handle.PointerCaptureLost += (_, _) => Cancel();
        return handle;
    }

    private void Press(Control handle, string? from, PointerPressedEventArgs e)
    {
        e.Handled = true;
        if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
        {
            return;
        }

        e.Pointer.Capture(handle);
        _link = new Link(from, SpotOf(e));
    }

    private void Move(PointerEventArgs e)
    {
        if (_link is not { } link || _sketch is not { } sketch || _arrows is not { } arrows)
        {
            return;
        }

        var at = SpotOf(e);
        link.Linking |= link.Press.Distance(at) > Slop;
        if (!link.Linking)
        {
            return;
        }

        link.Over = sketch.StateAt(at);
        arrows.Follow(at);
        Paint();
    }

    private void Release(PointerReleasedEventArgs e)
    {
        if (_link is not { } link || _sketch is not { } sketch)
        {
            return;
        }

        var to = link.Linking ? sketch.StateAt(SpotOf(e)) : null;
        Cancel();
        if (to is not null && _bench.Blend is { } blend)
        {
            var added = new BlendEditOptions(new ChangePicked(blend.WrittenChanges.Count));
            _bench.Edit("Add change", one => one.WithChangeAdded(link.From, to), added);
        }
    }

    private Spot SpotOf(PointerEventArgs e)
    {
        var at = e.GetPosition(_page);
        return new Spot(at.X, at.Y);
    }

    private void Cancel()
    {
        _link = null;
        _arrows?.Follow(null);
        Paint();
    }

    /// <summary>A press on a handle, and how far it has come.</summary>
    /// <param name="from">The state the handle is on, or null for the box that stands for any state.</param>
    /// <param name="press">Where it was pressed on the page.</param>
    private sealed class Link(string? from, Spot press)
    {
        public string? From { get; } = from;

        public Spot Press { get; } = press;

        /// <summary>True once it is past the slop, so a press that is let go is not a link.</summary>
        public bool Linking { get; set; }

        public string? Over { get; set; }
    }
}

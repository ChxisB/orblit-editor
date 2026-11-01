using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Editing a blend: the graph of its states and changes, with the fields of whatever is chosen beside it.
/// </summary>
/// <remarks>
/// Follows the bench itself, because an edit here changes nothing the rest of the editor shows. The
/// graph and the fields are made once and follow it too, so a tab changes what they show and does not
/// make new ones. The tabs follow the undo stack as well: an edit applies before the stack records it, so
/// the bench's own change still reads the blend as saved.
/// </remarks>
internal sealed class BlendGraphPanel : UserControl
{
    private const string NoBlend =
        "No blend is open. Open a .oblend file from the project, or make one. A blend turns what a game says, "
        + "such as how fast something moves, into which clip plays.";

    /// <summary>Below this width the fields go under the graph rather than beside it.</summary>
    private const double SideBySide = 560;

    private const double InspectorWidth = 288;
    private const double TabsHeight = 30;

    private readonly BlendBench _bench;
    private readonly History _history;
    private readonly StackPanel _tabs = new() { Name = "blend_tabs", Orientation = Orientation.Horizontal };
    private readonly Grid _body = new() { Name = "blend_body" };
    private readonly Control _empty;
    private readonly Control _shown;
    private readonly Control _graph;
    private readonly Control _divider = new Border { Background = OrblitColors.Line };
    private readonly Control _fields;
    private bool _wide = true;

    /// <param name="bench">The blends open, and what is chosen in the one shown.</param>
    /// <param name="history">What the fields settle into one step.</param>
    /// <param name="clips">The clips a state could play, read when the fields are built.</param>
    /// <param name="onNew">Makes a blend in the project and opens it here.</param>
    public BlendGraphPanel(BlendBench bench, History history, Func<IReadOnlyList<string>> clips, Action onNew)
    {
        _bench = bench;
        _history = history;
        _graph = new BlendCanvas(bench);
        _fields = new BlendInspector(bench, history, clips);
        _empty = Empty(onNew);
        _shown = Shown(onNew);
        _body.Children.Add(_graph);
        _body.Children.Add(_divider);
        _body.Children.Add(_fields);
        _body.SizeChanged += (_, size) => Fit(size.NewSize.Width);
        Place();
        Name = "blend_panel";
        Background = OrblitColors.Surface;
        Content = new Grid { Children = { _empty, _shown } };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += Rebuild;
        _history.Changed += Rebuild;
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= Rebuild;
        _history.Changed -= Rebuild;
        base.OnDetachedFromVisualTree(e);
    }

    private static ScrollViewer Empty(Action onNew)
    {
        var make = Fields.Button("New blend", "Make a blend in the project's blends folder and open it.", onNew);
        make.HorizontalAlignment = HorizontalAlignment.Center;
        make.Padding = new Thickness(Space.Lg, Space.Xs);
        var about = Fields.Note(NoBlend);
        about.Name = "blend_none";
        about.TextAlignment = TextAlignment.Center;
        about.MaxWidth = 420;
        var column = new StackPanel
        {
            Spacing = Space.Xs,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.Lg),
            Children = { about, make },
        };

        // Scrolls, because the panel can be dragged shorter than this.
        return new ScrollViewer { Name = "blend_empty", VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = column };
    }

    private Grid Shown(Action onNew)
    {
        var add = new Border
        {
            Name = "blend_new",
            Padding = new Thickness(Space.Sm, 0),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Glyph(TimelineIcons.Plus, 16),
        };
        ToolTip.SetTip(add, "New blend");
        add.PointerPressed += (_, press) =>
        {
            press.Handled = true;
            onNew();
        };
        var strip = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        strip.Children.Add(new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _tabs,
        });
        Grid.SetColumn(add, 1);
        strip.Children.Add(add);
        var tabs = new Border
        {
            Height = TabsHeight,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = strip,
        };
        Grid.SetRow(_body, 1);
        return new Grid
        {
            Name = "blend_shown",
            RowDefinitions = new RowDefinitions("Auto,*"),
            Children = { tabs, _body },
        };
    }

    // Side by side with the divider between, or the graph over the fields, three parts to two.
    private void Place()
    {
        _body.ColumnDefinitions = new ColumnDefinitions(_wide ? $"*,1,{InspectorWidth}" : "*");
        _body.RowDefinitions = new RowDefinitions(_wide ? "*" : "3*,1,2*");
        Put(_graph, 0);
        Put(_divider, 1);
        Put(_fields, 2);
    }

    private void Put(Control part, int at)
    {
        Grid.SetColumn(part, _wide ? at : 0);
        Grid.SetRow(part, _wide ? 0 : at);
    }

    private void Fit(double width)
    {
        if ((width >= SideBySide) != _wide)
        {
            _wide = !_wide;
            Place();
        }
    }

    private void Rebuild()
    {
        var shown = _bench.Shown;
        _empty.IsVisible = shown is null;
        _shown.IsVisible = shown is not null;
        _tabs.Children.Clear();
        foreach (var open in _bench.Open)
        {
            _tabs.Children.Add(Tab(open, ReferenceEquals(open, shown)));
        }
    }

    private Border Tab(OpenBlend open, bool current)
    {
        var name = Path.GetFileNameWithoutExtension(open.Path);
        var key = Fields.Key(name);
        var label = new TextBlock
        {
            Name = $"blend_tab_text_{key}",
            Text = _bench.IsUnsaved(open) ? $"{name} •" : name,
            FontSize = current ? OrblitText.Label : OrblitText.Caption,
            Foreground = current ? OrblitColors.Ink : OrblitColors.InkMid,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var close = new Border
        {
            Name = $"blend_close_{key}",
            Padding = new Thickness(5),
            Background = Brushes.Transparent,
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new Glyph(TimelineIcons.Close, 12),
        };
        ToolTip.SetTip(close, $"Close {name}");
        close.PointerPressed += (_, press) =>
        {
            press.Handled = true;
            _bench.Close(open.Path);
        };
        var tab = new Border
        {
            Name = $"blend_tab_{key}",
            Padding = new Thickness(Space.Sm, 0, 4, 0),
            Background = Brushes.Transparent,
            BorderBrush = current ? OrblitColors.Ember : Brushes.Transparent,
            BorderThickness = new Thickness(0, 0, 0, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { label, close } },
        };
        tab.PointerPressed += (_, _) => _bench.Show(open.Path);
        return tab;
    }
}

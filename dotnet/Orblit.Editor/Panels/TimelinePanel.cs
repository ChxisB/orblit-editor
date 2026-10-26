using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// The timeline: which clip is open, where the playhead is and what the clip moves. It draws a
/// <see cref="ClipBench"/> and asks a <see cref="TimelineModel"/> for every change.
/// </summary>
/// <remarks>
/// Keys act only while the panel itself has focus, because a length being typed is typing and not a
/// command. A press anywhere on the panel gives it focus, apart from the length box, which takes its own.
/// </remarks>
internal sealed class TimelinePanel : UserControl
{
    private const string NothingKeyed =
        "Nothing keyed yet. Select something, then press the diamond beside a value in the inspector to key it here.";

    private readonly ClipBench _bench;
    private readonly TimelineModel _model;
    private readonly TimelineToolbar _toolbar;
    private readonly TimelineRuler _ruler;
    private readonly TimelineTracks _tracks;
    private readonly TimelineTracks _curveTracks;
    private readonly DopeSheet _keysView;
    private readonly CurveView _curveView;
    private readonly TextBlock _where;
    private readonly TimelineStrip _marks;
    private readonly TimelineStrip _shots;
    private readonly ScrollViewer _none;
    private readonly TextBlock _nothingKeyed;
    private readonly ScrollViewer _sheet;
    private readonly Grid _curves;
    private readonly ContentControl _body = new();
    private readonly Grid _main;
    private readonly Dictionary<Key, Action> _keys;
    private readonly HashSet<Key> _down = [];

    // Bumped on detach, so a frame asked for before it does nothing after.
    private int _loop;
    private bool _requested;

    public TimelinePanel(ClipBench bench, TimelineLinks links, SkeletonLook? look = null)
    {
        _bench = bench;
        _model = new TimelineModel(bench);
        _toolbar = new TimelineToolbar(bench, _model, links, look);
        _ruler = new TimelineRuler(bench);
        _tracks = new TimelineTracks(bench, _model);
        _curveTracks = new TimelineTracks(bench, _model);
        _keysView = new DopeSheet(bench);
        _curveView = new CurveView(bench);
        _marks = MarkStrip.Create(bench, new MarkModel(bench));
        _shots = ShotStrip.Create(bench);
        _where = new TextBlock
        {
            Name = "timeline_where",
            FontSize = OrblitText.Caption,
            FontFamily = OrblitText.Mono,
            Foreground = OrblitColors.InkMid,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _none = NoClip(bench, links);
        _nothingKeyed = Centred(Fields.Note(NothingKeyed));
        _sheet = SheetView();
        _curves = CurvesView();
        _main = MainView();
        _keys = new Dictionary<Key, Action>
        {
            [Key.Space] = _model.Play,
            [Key.Left] = () => _model.Step(-1),
            [Key.Right] = () => _model.Step(1),
            [Key.Home] = _model.Home,
            [Key.End] = _model.End,
            [Key.Delete] = _model.DeleteKeys,
            [Key.Back] = _model.DeleteKeys,
            [Key.Escape] = () => bench.Selection = new HashSet<KeyRef>(),
        };

        Focusable = true;
        Background = OrblitColors.Surface;
        AddHandler(PointerPressedEvent, TakeFocus, RoutingStrategies.Tunnel);
        // A key let go after focus has moved on is never seen to come up.
        LostFocus += (_, _) => _down.Clear();
        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _bench.Changed += OnChanged;
        _model.ViewChanged += Refresh;
        OnChanged();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _bench.Changed -= OnChanged;
        _model.ViewChanged -= Refresh;
        _loop++;
        _requested = false;
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!ReferenceEquals(e.Source, this) || !_keys.TryGetValue(e.Key, out var act))
        {
            return;
        }

        // Only the arrows go on while held, so a held key walks the playhead. The rest act once.
        var repeat = !_down.Add(e.Key);
        if (!repeat || e.Key is Key.Left or Key.Right)
        {
            act();
        }

        e.Handled = true;
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _down.Remove(e.Key);
    }

    private static TextBlock Centred(TextBlock note)
    {
        note.HorizontalAlignment = HorizontalAlignment.Center;
        note.VerticalAlignment = VerticalAlignment.Center;
        note.TextAlignment = TextAlignment.Center;
        note.Margin = new Thickness(Space.Lg);
        return note;
    }

    // What the panel shows with nothing open: what a clip is, and the one button that makes one.
    private static ScrollViewer NoClip(ClipBench bench, TimelineLinks links)
    {
        var words = TimelineWords.For(bench.PlayedOn);
        var make = Fields.Button(words.Make, words.MakeTip, links.OnNew);
        make.HorizontalAlignment = HorizontalAlignment.Center;
        make.Padding = new Thickness(Space.Lg, Space.Xs);
        var about = Fields.Note(words.About);
        about.TextAlignment = TextAlignment.Center;
        about.MaxWidth = 420;
        var column = new StackPanel
        {
            Spacing = Space.Xs,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Space.Lg),
            Children =
            {
                new Glyph(TimelineIcons.Of(bench.PlayedOn), 32, OrblitColors.InkDim) { HorizontalAlignment = HorizontalAlignment.Center },
                new TextBlock
                {
                    Name = "timeline_none",
                    Text = words.None,
                    FontSize = OrblitText.Body,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = OrblitColors.Ink,
                    TextAlignment = TextAlignment.Center,
                },
                about,
                make,
            },
        };

        // Scrolls, because the panel can be dragged shorter than this.
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = column };
    }

    private Grid MainView()
    {
        var names = new Border
        {
            Padding = new Thickness(Space.Sm, 0),
            BorderBrush = OrblitColors.LineSoft,
            BorderThickness = new Thickness(0, 0, 1, 1),
            Child = _where,
        };
        var ruler = new Grid { Height = 24, ColumnDefinitions = new ColumnDefinitions($"{TimelineTracks.NamesWidth},*") };
        Grid.SetColumn(_ruler, 1);
        ruler.Children.Add(names);
        ruler.Children.Add(_ruler);

        var main = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*") };
        Grid.SetRow(ruler, 1);
        Grid.SetRow(_marks, 2);
        Grid.SetRow(_shots, 3);
        Grid.SetRow(_body, 4);
        main.Children.Add(_toolbar);
        main.Children.Add(ruler);
        main.Children.Add(_marks);
        main.Children.Add(_shots);
        main.Children.Add(_body);
        return main;
    }

    private static Border Names(TimelineTracks tracks) => new()
    {
        BorderBrush = OrblitColors.LineSoft,
        BorderThickness = new Thickness(0, 0, 1, 0),
        Child = tracks,
    };

    // The names and, beside them, the room the sheet of keys takes.
    private ScrollViewer SheetView()
    {
        var names = Names(_tracks);
        var both = new Grid { ColumnDefinitions = new ColumnDefinitions($"{TimelineTracks.NamesWidth},*") };
        Grid.SetColumn(_keysView, 1);
        both.Children.Add(names);
        both.Children.Add(_keysView);
        return new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = both };
    }

    // The names keep to a scroll of their own, and the curve takes the whole height beside them.
    private Grid CurvesView()
    {
        var both = new Grid { ColumnDefinitions = new ColumnDefinitions($"{TimelineTracks.NamesWidth},*") };
        Grid.SetColumn(_curveView, 1);
        both.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = Names(_curveTracks) });
        both.Children.Add(_curveView);
        return both;
    }

    private void TakeFocus(object? sender, PointerPressedEventArgs e)
    {
        var inLength = e.Source is Visual source && source.FindAncestorOfType<TextBox>(includeSelf: true) is not null;
        if (!inLength && !IsFocused)
        {
            Focus();
        }
    }

    private void OnChanged()
    {
        Refresh();
        RequestFrame();
    }

    private void Refresh()
    {
        var clip = _bench.Clip;
        Control view = clip is null ? _none : _main;
        if (!ReferenceEquals(Content, view))
        {
            Content = view;
        }

        if (clip is null)
        {
            return;
        }

        _toolbar.Refresh();
        _ruler.InvalidateVisual();
        _keysView.InvalidateVisual();
        _curveView.InvalidateVisual();
        _where.Text = TimelineWords.Where(_bench.At, clip.Rate);
        (_model.View == TimelineView.Curves ? _curveTracks : _tracks).Show(clip);
        _marks.Refresh();
        _shots.IsVisible = _bench.Shown?.Shots is not null;
        _shots.Refresh();
        Control body = clip.Channels.Count == 0 ? _nothingKeyed : _model.View == TimelineView.Curves ? _curves : _sheet;
        if (!ReferenceEquals(_body.Content, body))
        {
            _body.Content = body;
        }
    }

    private void RequestFrame()
    {
        if (_requested || !_bench.Playing || TopLevel.GetTopLevel(this) is not { } top)
        {
            return;
        }

        _requested = true;
        var loop = _loop;
        top.RequestAnimationFrame(now =>
        {
            if (loop == _loop)
            {
                OnFrame(now);
            }
        });
    }

    private void OnFrame(TimeSpan now)
    {
        _requested = false;
        _model.Tick(now);

        // The first frame of a play only reads the clock and changes nothing, so it asks for the next itself.
        RequestFrame();
    }
}

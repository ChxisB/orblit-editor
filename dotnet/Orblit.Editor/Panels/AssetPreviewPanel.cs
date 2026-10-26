using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Orblit.Filament.Avalonia;

namespace Orblit.Editor;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;

/// <summary>
/// What the picked file looks like.
/// </summary>
/// <remarks>
/// A project is a list of file names until something shows what is in them. <c>barrel_02.gltf</c> and
/// <c>barrel_03.gltf</c> are the same word to a reader and two barrels to whoever made them. A model is
/// drawn by the renderer from the file on disk, and a texture is shown as the picture it is.
/// Beside the browser's files and not in the inspector: the inspector answers what is in this scene,
/// and picking a file out of the project is a different question.
/// </remarks>
internal sealed class AssetPreviewPanel : Border
{
    private const double PanelWidth = 236;

    // Twice the width it is drawn at, for a sharp screen.
    private const int PictureWidth = 480;

    // A frame after a stall should not spin the model by the length of the stall.
    private const double LongestStep = 0.25;

    private readonly AssetPreviewModel _model;
    private readonly TextBlock _heading = new() { Name = "preview_heading", FontSize = OrblitText.Label, Foreground = OrblitColors.InkMid, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _turn = new() { Name = "preview_turn", Width = 24, Height = 24, Padding = new Thickness(0), Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
    private readonly Border _stage = new() { Name = "preview_stage" };
    private readonly StackPanel _facts = new() { Name = "preview_facts" };
    private readonly Border _factsFrame;
    private RendererViewport? _renderer;
    private Point? _dragFrom;
    private TimeSpan? _last;
    private int _loop;
    private bool _requested;

    /// <param name="renderer">Whether this machine runs the renderer. Without one a model is named and not drawn.</param>
    public AssetPreviewPanel(bool renderer = true)
    {
        _model = new AssetPreviewModel(renderer);
        Width = PanelWidth;
        Background = OrblitColors.Surface;
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(1, 0, 0, 0);

        _turn.Click += (_, _) =>
        {
            _model.Turn(!_model.Turning);
            Turned();
        };
        var title = new Grid { Height = 30, Margin = new Thickness(Space.Sm, 0, Space.Xs, 0), ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        title.Children.Add(_heading);
        Grid.SetColumn(_turn, 1);
        title.Children.Add(_turn);
        var titleFrame = new Border { BorderBrush = OrblitColors.LineSoft, BorderThickness = new Thickness(0, 0, 0, 1), Child = title };

        _factsFrame = new Border { BorderBrush = OrblitColors.LineSoft, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(Space.Sm, Space.Xs), Child = _facts };
        var column = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        Grid.SetRow(_stage, 1);
        Grid.SetRow(_factsFrame, 2);
        column.Children.Add(titleFrame);
        column.Children.Add(_stage);
        column.Children.Add(_factsFrame);
        Child = column;
        Sync();
    }

    /// <summary>What is picked, or null when nothing is.</summary>
    public Asset? Asset => _model.Asset;

    /// <summary>Whether the view is turning by itself.</summary>
    public bool Turning => _model.Turning;

    /// <summary>The renderer's control, or null while the stage is not a model.</summary>
    public RendererViewport? Renderer => _renderer;

    /// <summary>Picks a file to show, or nothing.</summary>
    public void Show(Asset? asset)
    {
        var from = _model.Stage;
        _model.Show(asset);
        if (from != _model.Stage || _model.Stage != PreviewStage.Model)
        {
            SetStage();
        }

        Sync();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // A frame already asked for belongs to the loop that ended here.
        _loop++;
        _requested = false;
        _last = null;
        base.OnDetachedFromVisualTree(e);
    }

    private static Grid Row(string label, string value)
    {
        var line = new Grid { ColumnDefinitions = new ColumnDefinitions("54,*"), Margin = new Thickness(0, 1) };
        line.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = OrblitColors.InkDim });
        var text = new TextBlock { Text = value, FontSize = 11, Foreground = OrblitColors.InkMid, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(text, 1);
        line.Children.Add(text);
        return line;
    }

    // The parts that follow the model's state and never the other way round.
    private void Sync()
    {
        _heading.Text = _model.Heading;
        _turn.IsVisible = _model.OffersTurn;
        _facts.Children.Clear();
        foreach (var (label, value) in _model.Facts())
        {
            _facts.Children.Add(Row(label, value));
        }

        _factsFrame.IsVisible = _facts.Children.Count > 0;
        Turned();
    }

    // The turn button shows what pressing it would do, and a frame is owed whenever that changes.
    private void Turned()
    {
        _turn.Content = new Glyph(_model.Turning ? AssetIcons.Pause : AssetIcons.Play, 15, OrblitColors.InkDim);
        ToolTip.SetTip(_turn, _model.Turning ? "Stop turning" : "Turn");
        RequestFrame();
    }

    // Swaps what the stage holds for what the model now says it is.
    private void SetStage()
    {
        _renderer = null;
        _stage.Child = _model.Stage switch
        {
            PreviewStage.Picture => PictureStage(_model.Asset!.Path),
            PreviewStage.Model => ModelStage(),
            PreviewStage.Unavailable => new PreviewNote(_model.Saying, AssetIcons.Of(AssetKind.Mesh)),
            PreviewStage.Kind => new PreviewNote(_model.Saying, AssetIcons.Of(_model.Asset!.Kind)),
            _ => new PreviewNote(_model.Saying),
        };
    }

    private Border PictureStage(string path)
    {
        var holder = new Border { Name = "preview_picture", Background = OrblitColors.Ground, Padding = new Thickness(Space.Sm) };
        LoadPicture(holder, path);
        return holder;
    }

    private async void LoadPicture(Border holder, string path)
    {
        var bitmap = await AssetThumbnails.LoadAsync(path, PictureWidth);
        // Another file was picked while this one decoded.
        if (_model.Asset?.Path != path)
        {
            return;
        }

        holder.Child = bitmap is null ? new PreviewNote("This file could not be read as an image") : Picture(bitmap);
    }

    private static Panel Picture(Bitmap bitmap) => new()
    {
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Children = { new Chequer(), new Image { Source = bitmap, Stretch = Stretch.Uniform } },
    };

    private Border ModelStage()
    {
        _renderer = new RendererViewport();
        var host = new Border { Background = Brushes.Transparent, Child = _renderer, ClipToBounds = true };
        host.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(host).Properties.IsLeftButtonPressed)
            {
                _dragFrom = e.GetPosition(host);
                e.Pointer.Capture(host);
            }
        };
        host.PointerMoved += (_, e) => Drag(e.GetPosition(host));
        host.PointerReleased += (_, e) =>
        {
            _dragFrom = null;
            e.Pointer.Capture(null);
        };
        // A wheel notch is one unit and a scroll in pixels is about a hundred of them.
        host.PointerWheelChanged += (_, e) =>
        {
            _model.Scroll(-e.Delta.Y * 100);
            RequestFrame();
        };
        return host;
    }

    private void Drag(Point to)
    {
        if (_dragFrom is not { } from)
        {
            return;
        }

        _model.Orbit(to.X - from.X, to.Y - from.Y);
        _dragFrom = to;
        Turned();
    }

    private void RequestFrame()
    {
        if (_requested || _renderer is null || TopLevel.GetTopLevel(this) is not { } top)
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
        var step = _last is { } last ? Math.Min((now - last).TotalSeconds, LongestStep) : 0;
        _last = _model.Turning ? now : null;
        _model.Tick(step);
        if (_renderer is null)
        {
            return;
        }

        // The renderer picks how it will show frames after it is attached. Until it has, ask again.
        if (_renderer.Path != ViewportPath.Pending && _model.Scene() is { } scene)
        {
            _renderer.Frame(scene, now.TotalSeconds);
        }

        if (_model.Turning || _renderer.Path == ViewportPath.Pending)
        {
            RequestFrame();
        }
    }
}

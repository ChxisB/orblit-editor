using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Orblit.Editor;

/// <summary>
/// The mark over the content for a moment as the app opens. Any key or click ends it early, and once
/// it has faded it stops covering the content, so it cannot take a click meant for what is under it.
/// </summary>
/// <remarks>
/// shortcut: the mark and a wordmark are drawn in code. The Dart editor shows a 622 KB logo picture.
/// Bundling a binary asset is a choice for Chris, so it waits (decision 84 covers the font beside it).
/// </remarks>
public sealed class Splash : Grid
{
    internal static readonly TimeSpan Fade = TimeSpan.FromMilliseconds(340);

    private readonly TaskCompletionSource _skipped = new();
    private TopLevel? _top;
    private readonly Grid _cover = new() { Name = "splash", Background = OrblitColors.Ground };

    public Splash(Control content, TimeSpan hold, string version)
    {
        var mark = new OrblitMark { Width = 96, Height = 96 };
        var centre = new StackPanel { Spacing = Space.Lg, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        centre.Children.Add(mark);
        centre.Children.Add(Widgets.Display("ORBLIT"));
        _cover.Children.Add(centre);
        _cover.Children.Add(new TextBlock
        {
            Text = version,
            FontFamily = OrblitText.Mono,
            FontSize = OrblitText.Caption,
            Foreground = OrblitColors.InkDim,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, Space.Xl),
        });
        _cover.Transitions = [new DoubleTransition { Property = OpacityProperty, Duration = Fade }];

        Children.Add(content);
        Children.Add(_cover);
        AddHandler(PointerPressedEvent, (_, _) => Skip(), RoutingStrategies.Tunnel);
        Done = Run(hold);
    }

    /// <summary>Completes when the splash has faded and no longer covers anything.</summary>
    public Task Done { get; }

    public bool IsDone { get; private set; }

    public void Skip() => _skipped.TrySetResult();

    // On the window rather than on this control, because nothing has focus as the app opens and a key
    // with no focus never travels through the content.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _top = TopLevel.GetTopLevel(this);
        _top?.AddHandler(KeyDownEvent, SkipOnKey, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _top?.RemoveHandler(KeyDownEvent, SkipOnKey);
        _top = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void SkipOnKey(object? sender, KeyEventArgs e) => Skip();

    private async Task Run(TimeSpan hold)
    {
        await Task.WhenAny(Task.Delay(hold), _skipped.Task);
        _cover.Opacity = 0;
        await Task.Delay(Fade);
        _cover.IsVisible = false;
        IsDone = true;
    }
}

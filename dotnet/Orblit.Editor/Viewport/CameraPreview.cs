using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Filament.Avalonia;

namespace Orblit.Editor;

using Orblit.Filament;

/// <summary>A card in the corner of the scene view that shows what the selected camera sees.</summary>
/// <remarks>
/// Bottom-left because that is the corner the view leaves empty. Under the tools it would cover what
/// someone is about to press. Nothing in it takes the pointer, because it is a picture of the shot and a
/// click on it should still select what is behind it.
/// </remarks>
internal sealed class CameraPreview : Border
{
    private readonly RendererViewport? _renderer;

    /// <param name="renderer">Whether to draw with the renderer. Off, the card stays plain, which is what a test wants.</param>
    public CameraPreview(bool renderer)
    {
        Name = "camera_preview";
        Width = 240;
        Height = 135;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Bottom;
        Margin = new Thickness(Space.Md, 0, 0, 52);
        Background = OrblitColors.Ground;
        BorderBrush = OrblitColors.Line;
        BorderThickness = new Thickness(1);
        CornerRadius = Radii.Control;
        ClipToBounds = true;
        IsHitTestVisible = false;
        IsVisible = false;
        var label = SectionLabel.Of("Camera");
        label.Margin = new Thickness(Space.Sm, 0);
        label.VerticalAlignment = VerticalAlignment.Center;
        var stack = new Grid { RowDefinitions = new RowDefinitions("20,*") };
        stack.Children.Add(label);
        if (renderer)
        {
            _renderer = new RendererViewport();
            Grid.SetRow(_renderer, 1);
            stack.Children.Add(_renderer);
        }

        Child = stack;
    }

    /// <summary>Whether the renderer has yet to pick how it shows frames, so a frame now would be lost.</summary>
    public bool Waiting => _renderer?.Path == ViewportPath.Pending;

    public void Frame(RenderScene scene, double seconds) => _renderer?.Frame(scene, seconds);
}

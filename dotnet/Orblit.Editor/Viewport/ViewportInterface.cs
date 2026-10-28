using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Ui;
using Orblit.Ui.Avalonia;

/// <summary>
/// The interface the scene puts on screen, drawn over the scene view with none of the editor's marks
/// on it. The canvas is laid out at its own size and made to fit the view.
/// </summary>
/// <remarks>
/// No ground and no bars, because they would hide the scene this is drawn over. It ignores the
/// pointer, because a handle under a heads-up display is a handle nobody can reach. This is a preview
/// of the interface, not the interface.
/// </remarks>
internal sealed class ViewportInterface : Viewbox
{
    private const double MostZoom = 4;

    private readonly UiSurface _surface = new() { Name = "viewport_interface_surface" };
    private readonly Panel _page;

    public ViewportInterface()
    {
        Name = "viewport_interface";
        IsHitTestVisible = false;
        Stretch = Stretch.Uniform;
        Margin = new Thickness(Space.Xl);
        _page = new Panel { ClipToBounds = true, Children = { _surface } };
        Child = _page;
    }

    /// <summary>Draws <paramref name="document"/>, or nothing when it is null.</summary>
    public void Show(UiDocument? document, UiStrings strings)
    {
        IsVisible = document is not null;
        if (document is null)
        {
            return;
        }

        var canvas = document.Canvas;
        (_page.Width, _page.Height) = (canvas.Width, canvas.Height);
        (MaxWidth, MaxHeight) = (canvas.Width * MostZoom, canvas.Height * MostZoom);
        _surface.Description = document.Root;
        _surface.Canvas = canvas;
        _surface.Strings = strings;
        _surface.KnownWidth = canvas.Width;
    }
}

using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The row along the top of the game view: which device the scene is shown on, which way it is held,
/// how big that is, and how much of that size the picture has room for.
/// </summary>
internal sealed class GameViewBar : Border
{
    private readonly DevicePreference _device;
    private readonly Dictionary<DeviceKind, ToggleButton> _kinds = [];
    private readonly Dictionary<FrameOrientation, ToggleButton> _holds = [];
    private readonly Button _turn;
    private readonly TextBlock _size = Readout("game_view_size");
    private readonly TextBlock _zoom = Readout("game_view_zoom");

    public GameViewBar(DevicePreference device)
    {
        _device = device;
        Name = "game_view_bar";
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(Space.Sm, Space.Xs);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Xs };
        foreach (var kind in Enum.GetValues<DeviceKind>())
        {
            row.Children.Add(_kinds[kind] = Choice($"game_view_device_{Lower(kind)}", kind.ToString(), () => _device.Choose(kind)));
        }

        row.Children.Add(Gap());
        foreach (var held in Enum.GetValues<FrameOrientation>())
        {
            row.Children.Add(_holds[held] = Choice($"game_view_hold_{Lower(held)}", held.ToString(), () => _device.Hold(held)));
        }

        _turn = new Button { Name = "game_view_turn", Content = "Turn", FontSize = OrblitText.Caption, Padding = Roomy(), VerticalAlignment = VerticalAlignment.Center };
        _turn.Click += (_, _) =>
        {
            _device.Turn();
            Refresh();
        };
        row.Children.Add(_turn);
        row.Children.Add(Gap());
        row.Children.Add(_size);
        row.Children.Add(_zoom);
        Child = row;
        Refresh();
    }

    /// <summary>Shows what the picture is drawn at, such as <c>74%</c>.</summary>
    public void Zoom(double scale) => _zoom.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Round(scale * 100)}%");

    /// <summary>Lights the buttons that match the device and turns off the ones it cannot use.</summary>
    public void Refresh()
    {
        var frame = _device.Frame;
        foreach (var (kind, button) in _kinds)
        {
            button.IsChecked = kind == frame.Kind;
        }

        foreach (var (held, button) in _holds)
        {
            button.IsChecked = held == frame.Orientation;
            button.IsEnabled = frame.CanTurn;
        }

        _turn.IsEnabled = frame.CanTurn;
        _size.Text = frame.Size;
    }

    private static string Lower(Enum value) => value.ToString().ToLowerInvariant();

    private static Thickness Roomy() => new(Space.Sm, Space.Xs);

    private static TextBlock Readout(string name) => new()
    {
        Name = name,
        FontSize = OrblitText.Caption,
        FontFamily = OrblitText.Mono,
        Foreground = OrblitColors.InkMid,
        VerticalAlignment = VerticalAlignment.Center,
        Margin = new Thickness(Space.Xs, 0),
    };

    private static Border Gap() => new() { Width = 1, Margin = new Thickness(Space.Xs, 2), Background = OrblitColors.LineSoft };

    // A press that changes nothing, such as the device already shown, would still flip the button, so the
    // row is drawn again from the preference after every press.
    private ToggleButton Choice(string name, string text, Action press)
    {
        var button = new ToggleButton { Name = name, Content = text, FontSize = OrblitText.Caption, Padding = Roomy(), VerticalAlignment = VerticalAlignment.Center };
        button.Click += (_, _) =>
        {
            press();
            Refresh();
        };
        return button;
    }
}

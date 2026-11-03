using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The row under the game view's bar that picks which player's view is shown. It is there while more
/// than one player runs, with a button for each.
/// </summary>
internal sealed class PlayerBar : Border
{
    private const string HostTip = "The host. Its game runs the server end.";
    private const string GuestTip = "A guest. Its game runs a client end, joined to the host over a loopback link.";

    private readonly EditorSession _session;
    private readonly Action _afterChoosing;
    private readonly StackPanel _row = new() { Orientation = Orientation.Horizontal, Spacing = Space.Xs };
    private readonly List<ToggleButton> _buttons = [];

    /// <param name="session">What is playing, and which player is shown.</param>
    /// <param name="afterChoosing">Run after a button is pressed, so the view can take the keys back.</param>
    public PlayerBar(EditorSession session, Action afterChoosing)
    {
        _session = session;
        _afterChoosing = afterChoosing;
        Name = "game_view_players";
        BorderBrush = OrblitColors.LineSoft;
        BorderThickness = new Thickness(0, 0, 0, 1);
        Padding = new Thickness(Space.Sm, Space.Xs);
        Child = _row;
        Sync();
    }

    /// <summary>Draws the row from the session: a button for each player running, and the shown one lit.</summary>
    public void Sync()
    {
        var running = _session.PlayersRunning;
        IsVisible = running > 1;
        if (_buttons.Count != running)
        {
            Rebuild(running);
        }

        for (var index = 0; index < _buttons.Count; index++)
        {
            _buttons[index].IsChecked = index == _session.ShownPlayer;
        }
    }

    private void Rebuild(int running)
    {
        _row.Children.Clear();
        _buttons.Clear();
        for (var index = 0; index < running && running > 1; index++)
        {
            var button = Choice(index);
            _buttons.Add(button);
            _row.Children.Add(button);
        }
    }

    // A press that changes nothing, such as the player already shown, would still flip the button, so the
    // row is drawn again from the session after every press.
    private ToggleButton Choice(int index)
    {
        var button = new ToggleButton
        {
            Name = $"game_view_player_{index + 1}",
            Content = $"Player {index + 1}",
            FontSize = OrblitText.Caption,
            Padding = new Thickness(Space.Sm, Space.Xs),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ToolTip.SetTip(button, index == 0 ? HostTip : GuestTip);
        button.Click += (_, _) =>
        {
            _session.ShowPlayer(index);
            Sync();
            _afterChoosing();
        };
        return button;
    }
}

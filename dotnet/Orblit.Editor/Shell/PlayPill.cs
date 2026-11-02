using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Play or pause the scene, and stop it. Play waits for a scene, and stop for something playing or a
/// game being built. While the game builds, Play is off and says so.
/// </summary>
internal sealed class PlayPill : Border
{
    private readonly EditorSession _session;
    private readonly ToolbarAction _play;
    private readonly ToolbarAction _stop;
    private readonly ToolbarMenu _players;

    public PlayPill(EditorSession session)
    {
        _session = session;
        Name = "play_pill";
        Padding = new Thickness(Space.Xs, 0);
        VerticalAlignment = VerticalAlignment.Center;
        CornerRadius = Radii.Control;
        Background = OrblitColors.Raised;
        _play = new ToolbarAction("play_toggle", AssetIcons.Play, "Play", () => _ = session.TogglePlayback());
        _stop = new ToolbarAction("play_stop", AssetIcons.Stop, "Stop", session.StopPlayback);
        _players = new ToolbarMenu("play_players", "How many players Play starts. Each is a copy of the game, joined to the host.", PlayerItems)
        {
            Margin = new Thickness(Space.Xs, 0, 0, 0),
        };
        Child = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { _play, _stop, _players } };
        Sync();
    }

    private static string Count(int players) => players == 1 ? "1 player" : $"{players} players";

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _session.PlaybackChanged += Sync;
        _session.Workspace.Changed += Sync;
        _session.Changed += Sync;
        Sync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _session.PlaybackChanged -= Sync;
        _session.Workspace.Changed -= Sync;
        _session.Changed -= Sync;
        base.OnDetachedFromVisualTree(e);
    }

    private void Sync()
    {
        var building = _session.Building;
        var label = building ? "Building the game" : _session.Playing ? "Pause" : "Play";
        _play.IsEnabled = !building && (_session.Playing || _session.Workspace.Loaded?.Scene is not null);
        _play.Show(_session.Playing ? AssetIcons.Pause : AssetIcons.Play, label, _session.Playing || building);
        _stop.IsEnabled = _session.Playback is not null || building;
        SyncPlayers(building);
    }

    // While a game runs the button says how many run, and the count cannot change until it stops.
    private void SyncPlayers(bool building)
    {
        var idle = _session.Playback is null;
        _players.Label = Count(idle ? _session.PlayerCount : Math.Max(_session.PlayersRunning, 1));
        _players.IsEnabled = idle && !building;
    }

    // The count in force is greyed out, so the menu shows what Play will do and offers only a change.
    private IEnumerable<Control> PlayerItems() =>
        Enumerable.Range(1, EditorSession.MaxPlayers).Select(count => ToolbarMenu.Item(
            $"play_players_{count}",
            Count(count),
            count == _session.PlayerCount ? null : () => _session.PlayerCount = count));
}

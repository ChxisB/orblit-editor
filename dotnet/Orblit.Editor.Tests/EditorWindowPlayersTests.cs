using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>
/// Choosing how many players Play starts, and moving between their views once it has. Nothing here
/// awaits, because a continuation back to the UI thread is only reached while the test renders.
/// </summary>
[Collection(TinyGameBuild.Collection)]
public sealed class EditorWindowPlayersTests(TinyGameBuild fixture) : IClassFixture<TinyGameBuild>, IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_window_players");

    public void Dispose() => _project.Delete(recursive: true);

    private (EditorSession Session, EditorWindow Window) Open(string type, int players = 1, IBuildCommand? build = null)
    {
        var session = GameTestSupport.Open(_project, GameTestSupport.Entry(type), build ?? GameTestSupport.Copying(fixture.Build));
        session.PlayerCount = players;
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        Headless.Render();
        return (session, window);
    }

    private (EditorSession Session, EditorWindow Window) Play(string type, int players)
    {
        var opened = Open(type, players);
        Headless.Click(opened.Window, "play_toggle");
        Assert.True(Headless.Until(() => opened.Session.Playing), "The game did not start.");
        Headless.Render();
        return opened;
    }

    private static List<MenuItem> MenuOf(EditorWindow window, string name)
    {
        Headless.Click(window, name);
        var menu = Headless.Find<Button>(window, name).ContextMenu;
        Assert.NotNull(menu);
        return [.. menu.Items.OfType<MenuItem>()];
    }

    private static void Pick(MenuItem item) => item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

    private static bool Says(Control root, string text) => Headless.All<TextBlock>(root).Any(one => one.Text == text);

    private static IReadOnlyList<ToggleButton> PlayerButtons(EditorWindow window) =>
        [.. Headless.All<ToggleButton>(window).Where(one => one.Name?.StartsWith("game_view_player_", StringComparison.Ordinal) == true)];

    private static Border Row(EditorWindow window) => Headless.Find<Border>(window, "game_view_players");

    [Fact]
    public Task PlayOffersOneToFourPlayersAndGreysOutTheCountThatIsChosen() => Headless.Run(() =>
    {
        var (_, window) = Open("TinyGame.Tiny");

        var items = MenuOf(window, "play_players");

        Assert.Equal(["play_players_1", "play_players_2", "play_players_3", "play_players_4"], items.Select(one => one.Name));
        Assert.Equal(["1 player", "2 players", "3 players", "4 players"], items.Select(one => one.Header?.ToString()));
        Assert.False(items[0].IsEnabled);
        Assert.All(items.Skip(1), one => Assert.True(one.IsEnabled));
        window.Close();
    });

    [Fact]
    public Task ChoosingACountSetsWhatPlayStartsAndTheButtonSaysIt() => Headless.Run(() =>
    {
        var (session, window) = Open("TinyGame.Tiny");
        Assert.True(Says(Headless.Find<Button>(window, "play_players"), "1 player"));

        Pick(MenuOf(window, "play_players").Single(one => one.Name == "play_players_3"));
        Headless.Render();

        Assert.Equal(3, session.PlayerCount);
        Assert.True(Says(Headless.Find<Button>(window, "play_players"), "3 players"));
        window.Close();
    });

    [Fact]
    public Task TheCountCannotBeChangedWhileAGameRunsAndCanAfterStop() => Headless.Run(() =>
    {
        var (_, window) = Play("TinyGame.Tiny", 2);

        Assert.False(Headless.Find<Button>(window, "play_players").IsEnabled);
        Assert.True(Says(Headless.Find<Button>(window, "play_players"), "2 players"));

        Headless.Click(window, "play_stop");
        Headless.Render();

        Assert.True(Headless.Find<Button>(window, "play_players").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task TheCountCannotBeChangedWhileTheGameBuilds() => Headless.Run(() =>
    {
        var started = new TaskCompletionSource();
        var (session, window) = Open("TinyGame.Tiny", build: GameTestSupport.Blocked(started));

        Headless.Click(window, "play_toggle");
        Assert.True(Headless.Until(() => started.Task.IsCompleted));
        Assert.True(session.Building);

        Assert.False(Headless.Find<Button>(window, "play_players").IsEnabled);

        Headless.Click(window, "play_stop");
        Assert.True(Headless.Until(() => !session.Building));
        Assert.True(Headless.Find<Button>(window, "play_players").IsEnabled);
        window.Close();
    });

    [Fact]
    public Task AGameThatRunsKeepsSayingHowManyPlayersItStartedWith() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Tiny", 2);

        session.PlayerCount = 4;
        Headless.Render();

        Assert.True(Says(Headless.Find<Button>(window, "play_players"), "2 players"));
        window.Close();
    });

    [Fact]
    public Task PressingThePlayerAlreadyShownLeavesItLitWhilePlayIsPaused() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Tiny", 2);
        // A paused Play takes no more frames, so nothing but the press redraws the row.
        Headless.Click(window, "play_toggle");
        Assert.False(session.Playing);

        Headless.Click(window, "game_view_player_1");
        Headless.Render();

        Assert.Equal(0, session.ShownPlayer);
        Assert.Equal([true, false], PlayerButtons(window).Select(one => one.IsChecked == true));
        window.Close();
    });

    [Fact]
    public Task OnePlayerHasNoRowOfPlayersAndTwoHaveAButtonEachWithTheHostLit() => Headless.Run(() =>
    {
        var one = Play("TinyGame.Tiny", 1);
        Assert.False(Row(one.Window).IsVisible);
        one.Window.Close();

        var (_, window) = Play("TinyGame.Tiny", 2);

        Assert.True(Row(window).IsVisible);
        var buttons = PlayerButtons(window);
        Assert.Equal(["game_view_player_1", "game_view_player_2"], buttons.Select(one => one.Name));
        Assert.Equal([true, false], buttons.Select(one => one.IsChecked == true));
        window.Close();
    });

    [Fact]
    public Task PressingAPlayerShowsItsViewAndItsGamesOwnControlAndKeepsTheKeys() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Faced", 2);
        var host = session.PlayingGame;
        Assert.Same(host, Headless.Find<TextBlock>(window, "game_interface_label").DataContext);

        Headless.Click(window, "game_view_player_2");
        Headless.Render();

        Assert.Equal(1, session.ShownPlayer);
        Assert.NotSame(host, session.PlayingGame);
        Assert.Same(session.PlayingGame, Headless.Find<TextBlock>(window, "game_interface_label").DataContext);
        Assert.Equal([false, true], PlayerButtons(window).Select(one => one.IsChecked == true));
        Assert.Same(session.ShownPlayback!.Workspace, Headless.All<GameViewPanel>(window).Single().Subject);
        Assert.True(Headless.Find<Border>(window, "game_view_picture").IsFocused);
        window.Close();
    });

    [Fact]
    public Task AChangeOfPlayerFromTheSessionLightsItsButtonToo() => Headless.Run(() =>
    {
        var (session, window) = Play("TinyGame.Tiny", 3);

        session.ShowPlayer(2);
        Headless.Render();

        Assert.Equal([false, false, true], PlayerButtons(window).Select(one => one.IsChecked == true));
        window.Close();
    });

    [Fact]
    public Task StopTakesTheRowOfPlayersAway() => Headless.Run(() =>
    {
        var (_, window) = Play("TinyGame.Tiny", 2);

        Headless.Click(window, "play_stop");
        Headless.Render();

        Assert.False(Row(window).IsVisible);
        Assert.Empty(PlayerButtons(window));
        window.Close();
    });
}

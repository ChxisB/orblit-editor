using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>File › Kit store: find a kit in a store, read about it and install it.</summary>
public sealed class StoreDialog : Window
{
    private readonly StoreSession _session;
    private readonly StackPanel _picker = new() { Orientation = Orientation.Horizontal, Spacing = Space.Xs };
    private readonly ContentControl _body = new();
    private readonly StoreBrowse _browse;
    private readonly StoreDetail _detail;

    /// <param name="session">What the dialog shows. The dialog disposes of it once closed.</param>
    /// <param name="installFor">Makes the install of the kit and the release the person chose.</param>
    public StoreDialog(StoreSession session, Func<KitRow, KitRelease, InstallSession> installFor)
    {
        _session = session;
        _browse = new StoreBrowse(session);
        _detail = new StoreDetail(session, (row, release) => _ = InstallDialog.Open(this, installFor(row, release)));
        Title = "Kit store";
        Width = 720;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        Content = Layout();
        session.Changed += Render;
        Closed += (_, _) => _session.Dispose();
        Render();
    }

    /// <summary>Shows the dialog over <paramref name="owner"/> until it is closed.</summary>
    public static Task Open(Window owner, StoreSession session, Func<KitRow, KitRelease, InstallSession> installFor) =>
        new StoreDialog(session, installFor).ShowDialog(owner);

    private Grid Layout()
    {
        var close = new Button { Name = "close", Content = "Close", IsCancel = true, HorizontalAlignment = HorizontalAlignment.Right };
        close.Click += (_, _) => Close();
        var title = new TextBlock { Text = "Kit store", FontSize = OrblitText.Body, Foreground = OrblitColors.Ink, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(_picker, 1);
        var rows = new Grid { Margin = new Thickness(Space.Lg), RowSpacing = Space.Md, RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        rows.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { title, _picker } });
        Grid.SetRow(_body, 1);
        rows.Children.Add(_body);
        Grid.SetRow(close, 2);
        rows.Children.Add(close);
        return rows;
    }

    private void Render()
    {
        _picker.Children.Clear();
        _picker.Children.AddRange(RepositoryPicker.Of(this, _session));
        if (!_session.Online)
        {
            _body.Content = Offline();
        }
        else if (_session.Selected is null)
        {
            _browse.Render();
            _body.Content = _browse;
        }
        else
        {
            _detail.Render();
            _body.Content = _detail;
        }
    }

    // Nothing has been sent until the person asks.
    private StackPanel Offline()
    {
        var goOnline = new Button { Name = "go_online", Content = "Go online", Background = OrblitColors.Ember, Foreground = OrblitColors.Ground, HorizontalAlignment = HorizontalAlignment.Center };
        goOnline.Click += async (_, _) => await _session.GoOnlineAsync();
        var words = new TextBlock
        {
            Text = $"The store is at {_session.Repository}. Nothing has been sent to it. Going online reads its list of kits "
                + "and their pictures. The files of a kit are listed only when you ask, and a kit is downloaded only when you install it.",
            FontSize = OrblitText.Body,
            Foreground = OrblitColors.Ink,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
        };
        return new StackPanel
        {
            MaxWidth = 420,
            Spacing = Space.Lg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { words, goOnline },
        };
    }
}

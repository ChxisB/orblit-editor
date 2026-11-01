using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>Download a kit, choose its files and folder, and write them.</summary>
/// <remarks>The dialog cannot be closed while files are written, so a write under way is never hidden.</remarks>
public sealed class InstallDialog : Window
{
    private readonly InstallSession _session;
    private readonly ContentControl _body = new();
    private readonly Button _close = new() { Name = "close", HorizontalAlignment = HorizontalAlignment.Right };

    // Kept across renders, so the folder typed and the ticks outlast a failed write.
    private KitChoose? _choose;

    /// <param name="session">What the dialog shows. The dialog disposes of it once closed.</param>
    public InstallDialog(InstallSession session)
    {
        _session = session;
        Title = $"Install {session.Row.Manifest.Name} {session.Release.Version}";
        Width = 680;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        _close.Click += (_, _) => Close();
        var title = new TextBlock { Text = Title, FontSize = OrblitText.Body, Foreground = OrblitColors.Ink };
        Grid.SetRow(_body, 1);
        Grid.SetRow(_close, 2);
        Content = new Grid
        {
            Margin = new Thickness(Space.Lg),
            RowSpacing = Space.Md,
            RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            Children = { title, _body, _close },
        };
        // Changed is raised on a pool thread while the kit downloads.
        session.Changed += () => Dispatcher.UIThread.Post(Render);
        // Quitting the editor is still allowed, because then nobody is left to hear how the write went.
        Closing += (_, e) => e.Cancel = _session.Stage == InstallStage.Installing && e.CloseReason == WindowCloseReason.WindowClosing;
        Closed += (_, _) => _session.Dispose();
        Render();
    }

    /// <summary>Starts the download and shows the dialog over <paramref name="owner"/> until it is closed.</summary>
    public static Task Open(Window owner, InstallSession session)
    {
        var dialog = new InstallDialog(session);
        _ = session.StartAsync();
        return dialog.ShowDialog(owner);
    }

    private void Render()
    {
        _close.Content = _session.Stage == InstallStage.Done ? "Close" : "Cancel";
        _close.IsEnabled = _session.Stage != InstallStage.Installing;
        _body.Content = _session.Failed ? Failed() : _session.Stage switch
        {
            InstallStage.Downloading => Downloading(),
            InstallStage.Done => Done(),
            _ => Choose(),
        };
    }

    private StackPanel Downloading()
    {
        var fraction = _session.Progress?.Fraction;
        return new StackPanel
        {
            Spacing = Space.Md,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = fraction is { } part ? $"Downloading… {part * 100:0}%" : "Downloading…", FontSize = OrblitText.Body, Foreground = OrblitColors.Ink, HorizontalAlignment = HorizontalAlignment.Center },
                new ProgressBar { Width = 320, Maximum = 1, Value = fraction ?? 0, IsIndeterminate = fraction is null, Foreground = OrblitColors.Ember, Background = OrblitColors.Line },
            },
        };
    }

    private StackPanel Failed()
    {
        var again = new Button { Name = "try_again", Content = "Try again", HorizontalAlignment = HorizontalAlignment.Center };
        again.Click += async (_, _) => await _session.StartAsync();
        var problem = new TextBlock { Text = _session.Problem, FontSize = OrblitText.Caption, Foreground = OrblitColors.Bad, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center };
        return new StackPanel { Spacing = Space.Md, VerticalAlignment = VerticalAlignment.Center, Children = { problem, again } };
    }

    private KitChoose Choose()
    {
        // Choosing and installing come only once the kit is downloaded and checked, so it is there.
        _choose ??= new KitChoose(_session, _session.Kit!);
        _choose.Render();
        return _choose;
    }

    private TextBlock Done()
    {
        var locked = _session.Locked;
        var count = locked?.Files.Count ?? 0;
        var where = locked is null || locked.Into.Length == 0 ? "in the project folder" : $"under {locked.Into}";
        return new TextBlock
        {
            Text = $"Installed {count} {(count == 1 ? "file" : "files")} {where}. kits.lock lists them.",
            FontSize = OrblitText.Body,
            Foreground = OrblitColors.Ink,
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}

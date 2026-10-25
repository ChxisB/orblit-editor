using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The window the editor opens in when it is not told which project to open: a rail, and the page
/// the session says to show. It draws what a <see cref="LauncherSession"/> holds and calls it.
/// </summary>
/// <remarks>
/// shortcut: the rail has only Projects. The Dart editor's Examples page waits for the gallery (task 17).
/// </remarks>
public sealed class LauncherWindow : Window
{
    internal const string Version = "Engine 0.1.0 · pre-alpha";

    private readonly LauncherSession _session;
    private readonly Func<string, Task<string?>> _pickFolder;
    private readonly ProjectsPage _projects;
    private readonly ContentControl _page = new() { Name = "page" };

    /// <param name="session">What the launcher is showing.</param>
    /// <param name="pickFolder">Asks for a folder with the given title. Defaults to the system dialog, and a test passes its own.</param>
    /// <param name="splash">How long the mark covers the launcher as it opens. Null shows no mark.</param>
    public LauncherWindow(LauncherSession session, Func<string, Task<string?>>? pickFolder = null, TimeSpan? splash = null)
    {
        _session = session;
        _pickFolder = pickFolder ?? PickWithDialog;
        _projects = new ProjectsPage(session, OpenFolder);
        Title = "Orblit";
        Width = 1180;
        Height = 760;
        MinWidth = 720;
        MinHeight = 480;
        Background = OrblitColors.Ground;

        var frame = new Grid { ColumnDefinitions = new ColumnDefinitions("232,*") };
        Grid.SetColumn(_page, 1);
        frame.Children.Add(Rail());
        frame.Children.Add(_page);
        Content = splash is { } hold ? new Splash(frame, hold, Version) : frame;

        _session.Changed += Refresh;
        _session.Complained += Complain;
        Closed += (_, _) =>
        {
            _session.Changed -= Refresh;
            _session.Complained -= Complain;
        };
        Refresh();
    }

    private Border Rail()
    {
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, Margin = new Thickness(Space.Sm, 0, 0, Space.Lg) };
        title.Children.Add(new OrblitMark { Width = 26, Height = 26 });
        var word = Widgets.Title("Orblit");
        word.VerticalAlignment = VerticalAlignment.Center;
        title.Children.Add(word);

        var projects = new Button
        {
            Name = "rail_projects",
            Content = "Projects",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Background = OrblitColors.EmberWash,
        };
        projects.Click += (_, _) => _session.Show(LauncherView.Projects);

        var version = Widgets.Mono(Version, size: 11);
        var column = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(Space.Md, Space.Lg) };
        Grid.SetRow(projects, 1);
        Grid.SetRow(version, 3);
        column.Children.Add(title);
        column.Children.Add(projects);
        column.Children.Add(version);
        return new Border
        {
            Background = OrblitColors.Surface,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = column,
        };
    }

    private void Refresh()
    {
        if (_session.View != LauncherView.Create)
        {
            _page.Content = _projects;
            _projects.Refresh();
        }
        else if (_page.Content is CreatePage create)
        {
            create.Refresh();
        }
        else
        {
            _page.Content = new CreatePage(_session, () => _pickFolder("Choose where the project goes"));
        }
    }

    private async Task OpenFolder()
    {
        if (await _pickFolder("Open a project folder") is { } folder)
        {
            _session.OpenFolder(folder);
        }
    }

    private void Complain(string title, string detail) => _ = new Complaint(title, detail).ShowDialog(this);

    private async Task<string?> PickWithDialog(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}

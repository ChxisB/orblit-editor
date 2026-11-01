using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// The first page: ways to start, and what was opened lately. The search box is built once, because
/// rebuilding it as somebody types would take the cursor away.
/// </summary>
/// <remarks>
/// shortcut: a project whose folder has gone shows the word moved and no icon, because no icon set is
/// bundled yet (decision 84).
/// </remarks>
public sealed class ProjectsPage : ScrollViewer
{
    private readonly LauncherSession _session;
    private readonly Border _recent = new();

    public ProjectsPage(LauncherSession session, Func<Task> openFolder)
    {
        _session = session;

        var search = new TextBox { Name = "search", PlaceholderText = "Search projects", Width = 220, VerticalAlignment = VerticalAlignment.Center };
        search.TextChanged += (_, _) => _session.Search(search.Text ?? "");
        var open = Widgets.Action("open_folder", "Open a folder");
        open.Click += async (_, _) => await openFolder();
        var create = Widgets.Action("new_project", "New project", primary: true);
        create.Click += (_, _) => _session.Show(LauncherView.Create);

        var titles = new StackPanel { Spacing = Space.Xs };
        titles.Children.Add(Widgets.Display("Projects"));
        titles.Children.Add(Widgets.Body("Pick up where you left off, or start something new."));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, VerticalAlignment = VerticalAlignment.Bottom };
        actions.Children.Add(search);
        actions.Children.Add(open);
        actions.Children.Add(create);
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        Grid.SetColumn(actions, 1);
        header.Children.Add(titles);
        header.Children.Add(actions);

        var templates = new CardGrid();
        foreach (var template in Enum.GetValues<ProjectTemplate>())
        {
            templates.Children.Add(TemplateCard(template));
        }

        var column = Widgets.Column(1120, Space.Xl);
        column.Children.Add(header);
        column.Children.Add(Section("Start from a template", templates));
        column.Children.Add(Section("Recent", _recent));
        Content = column;
        Refresh();
    }

    /// <summary>Redraws the list of projects. Nothing else on the page changes with the session.</summary>
    public void Refresh() => _recent.Child = _session.Matching.Count > 0 ? RecentGrid() : Empty();

    private static StackPanel Section(string title, Control body)
    {
        var section = new StackPanel { Spacing = Space.Md };
        section.Children.Add(Widgets.Section(title));
        section.Children.Add(body);
        return section;
    }

    private Card TemplateCard(ProjectTemplate template)
    {
        var tag = template switch
        {
            ProjectTemplate.Empty => "Blank",
            ProjectTemplate.Scene or ProjectTemplate.ThirdPerson => "3D",
            _ => throw new System.Diagnostics.UnreachableException(),
        };
        var badge = new Border
        {
            Margin = new Thickness(Space.Sm),
            Padding = new Thickness(Space.Sm, 2),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromArgb(0xB8, 0x0E, 0x11, 0x16)),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = Widgets.Mono(tag, size: 11),
        };
        var card = new Card($"template_{template}", $"Start a project from {template.Label()}", Stacked(SceneArt.ForTemplate(template).Picture(96, badge), template.Label(), template.Description()));
        card.Clicked += () => _session.StartFrom(template);
        return card;
    }

    private CardGrid RecentGrid()
    {
        var grid = new CardGrid();
        var matching = _session.Matching;
        for (var at = 0; at < matching.Count; at++)
        {
            grid.Children.Add(ProjectCard(at, matching[at]));
        }

        return grid;
    }

    // A project whose folder has gone is shown rather than hidden, so someone who moved a directory
    // sees why it will not open instead of wondering where their work went.
    private Card ProjectCard(int index, Project project)
    {
        var missing = !project.Exists;
        var forget = new Button
        {
            Name = "forget",
            Content = "×",
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(Space.Sm),
            CornerRadius = Radii.Control,
        };
        Avalonia.Automation.AutomationProperties.SetName(forget, $"Remove {project.Name} from this list");
        ToolTip.SetTip(forget, "Remove from this list");
        forget.Click += (_, e) =>
        {
            e.Handled = true;
            _session.Forget(project);
        };

        var name = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        name.Children.Add(Widgets.Title(project.Name, tone: missing ? OrblitColors.InkDim : null));
        if (missing)
        {
            var moved = new Border
            {
                Margin = new Thickness(Space.Sm, 0, 0, 0),
                Padding = new Thickness(6, 1),
                CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xE0, 0xB2, 0x52)),
                Background = new SolidColorBrush(Color.FromArgb(0x1F, 0xE0, 0xB2, 0x52)),
                Child = new TextBlock { Name = "moved", Text = "moved", FontSize = 10, Foreground = OrblitColors.Warn },
            };
            Grid.SetColumn(moved, 1);
            name.Children.Add(moved);
        }

        var details = new StackPanel { Spacing = 2, Margin = new Thickness(6, 0) };
        details.Children.Add(name);
        details.Children.Add(Widgets.Mono(_session.PathOf(project)));
        details.Children.Add(Widgets.Label($"Opened {_session.OpenedAgo(project)}"));

        var art = missing ? SceneArt.Missing : SceneArt.ForName(project.Name);
        var column = new StackPanel { Spacing = Space.Md };
        column.Children.Add(art.Picture(124, forget));
        column.Children.Add(details);

        var card = new Card($"project_{index}", $"Open {project.Name}", column, enabled: !missing)
        {
            Revealed = forget,
        };
        card.Clicked += () => _session.Open(project);
        return card;
    }

    private static StackPanel Stacked(Control picture, string title, string description)
    {
        var words = new StackPanel { Spacing = 2, Margin = new Thickness(6, 0) };
        words.Children.Add(Widgets.Title(title));
        words.Children.Add(Widgets.Label(description));
        var column = new StackPanel { Spacing = Space.Md };
        column.Children.Add(picture);
        column.Children.Add(words);
        return column;
    }

    private Border Empty()
    {
        var searching = _session.Recents.Count > 0;
        var words = new StackPanel { Name = "note", Spacing = Space.Xs };
        words.Children.Add(Widgets.Title(searching ? "Nothing matches" : "No projects yet"));
        words.Children.Add(Widgets.Body(searching
            ? "No project has that in its name."
            : $"Make one from a template, or open a folder that already has an {Project.FileName} in it."));
        return new Border
        {
            Padding = new Thickness(Space.Xl),
            CornerRadius = Radii.Window,
            BorderThickness = new Thickness(1),
            BorderBrush = OrblitColors.LineSoft,
            Child = words,
        };
    }
}

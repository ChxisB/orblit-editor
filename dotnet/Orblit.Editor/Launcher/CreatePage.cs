using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>
/// Asks for what a new project needs: a name, somewhere to put it and what to start from. It shows
/// where the folder will land before it is made, so nobody is surprised by where their project went.
/// </summary>
/// <remarks>
/// A new page is made each time this one is opened, so the fields start from their defaults.
/// </remarks>
public sealed class CreatePage : ScrollViewer
{
    private readonly LauncherSession _session;
    private readonly Dictionary<ProjectTemplate, RadioButton> _radios = [];

    public CreatePage(LauncherSession session, Func<Task<string?>> browse)
    {
        _session = session;

        var name = new TextBox { Name = "name", Text = "Untitled Project" };
        var location = new TextBox { Name = "location", Text = session.DefaultLocation, FontFamily = OrblitText.Mono, HorizontalAlignment = HorizontalAlignment.Stretch };
        var destination = Widgets.Mono("", "destination");
        void ShowDestination() => destination.Text = ProjectStore.DestinationFor(name.Text ?? "", location.Text ?? "");
        name.TextChanged += (_, _) => ShowDestination();
        location.TextChanged += (_, _) => ShowDestination();
        ShowDestination();

        var browseButton = Widgets.Action("browse", "Browse");
        browseButton.Click += async (_, _) =>
        {
            if (await browse() is { } folder)
            {
                location.Text = folder;
            }
        };
        var folder = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.Sm };
        Grid.SetColumn(browseButton, 1);
        folder.Children.Add(location);
        folder.Children.Add(browseButton);

        var cancel = Widgets.Action("cancel", "Cancel");
        cancel.Click += (_, _) => session.Show(LauncherView.Projects);
        var create = Widgets.Action("create", "Create project", primary: true);
        void Create() => session.Create(name.Text ?? "", location.Text ?? "");
        create.Click += (_, _) => Create();
        name.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Create();
            }
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(cancel);
        buttons.Children.Add(create);

        var column = Widgets.Column(640, Space.Sm);
        column.Children.Add(Widgets.Display("New project"));
        column.Children.Add(Widgets.Body("A folder, a scene, and somewhere to put assets."));
        column.Children.Add(Field("Name", name, Space.Xl));
        column.Children.Add(Field("Location", folder, Space.Md));
        column.Children.Add(destination);
        column.Children.Add(Widgets.Section("Start from"));
        foreach (var template in Enum.GetValues<ProjectTemplate>())
        {
            column.Children.Add(Radio(template));
        }

        column.Children.Add(buttons);
        Content = column;
    }

    /// <summary>Follows the session, which the cards on the first page and the radios here both change.</summary>
    public void Refresh()
    {
        foreach (var (template, radio) in _radios.Where(one => (one.Value.IsChecked == true) != (one.Key == _session.Template)))
        {
            radio.IsChecked = template == _session.Template;
        }
    }

    private static StackPanel Field(string label, Control field, double above)
    {
        var column = new StackPanel { Spacing = Space.Xs, Margin = new Thickness(0, above, 0, 0) };
        column.Children.Add(Widgets.Label(label));
        column.Children.Add(field);
        return column;
    }

    private RadioButton Radio(ProjectTemplate template)
    {
        var words = new StackPanel { Spacing = 2 };
        words.Children.Add(Widgets.Title(template.Label()));
        words.Children.Add(Widgets.Label(template.Description()));
        var radio = new RadioButton
        {
            Name = $"radio_{template}",
            GroupName = "template",
            IsChecked = template == _session.Template,
            Content = words,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        radio.IsCheckedChanged += (_, _) =>
        {
            if (radio.IsChecked == true)
            {
                _session.StartFrom(template);
            }
        };
        _radios[template] = radio;
        return radio;
    }
}

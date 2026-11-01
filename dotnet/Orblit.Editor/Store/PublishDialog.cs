using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>File › Publish kit: pack a folder of the project as a kit and show the entry that lists it in a store.</summary>
/// <remarks>The dialog cannot be closed while it packs, so a pack under way is never hidden.</remarks>
public sealed class PublishDialog : Window
{
    private readonly PublishSession _session;
    private readonly ITextClipboard _clipboard;
    private readonly TextBox _folder = new() { Name = "folder", PlaceholderText = "assets/kits/crates", FontFamily = OrblitText.Mono };
    private readonly TextBox _url = new() { Name = "url", PlaceholderText = "https://example.com/crates-1.0.0.opack", FontFamily = OrblitText.Mono };
    private readonly TextBox _notes = new() { Name = "notes", PlaceholderText = "What changed." };
    private readonly TextBlock _problem = new() { Name = "problem", FontSize = OrblitText.Caption, Foreground = OrblitColors.Bad, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, Space.Sm, 0, 0) };
    private readonly Button _publish = new() { Name = "publish", Background = OrblitColors.Ember, Foreground = OrblitColors.Ground, HorizontalAlignment = HorizontalAlignment.Right };
    private readonly ContentControl _body = new();
    private readonly Button _close = new() { Name = "close", HorizontalAlignment = HorizontalAlignment.Right };

    // Kept across renders, so what was typed outlasts a failed run.
    private readonly Grid _form;

    /// <param name="session">What the dialog shows. The dialog disposes of it once closed.</param>
    /// <param name="clipboard">Where the entry goes when the person copies it.</param>
    public PublishDialog(PublishSession session, ITextClipboard clipboard)
    {
        _session = session;
        _clipboard = clipboard;
        Title = "Publish kit";
        Width = 680;
        Height = 560;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        _publish.Click += async (_, _) => await _session.StartAsync(_folder.Text ?? "", _url.Text ?? "", _notes.Text ?? "");
        _close.Click += (_, _) => Close();
        _form = Form();
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
        session.Changed += Render;
        // Quitting the editor is still allowed, because then nobody is left to hear how the pack went.
        Closing += (_, e) => e.Cancel = _session.Stage == PublishStage.Working && e.CloseReason == WindowCloseReason.WindowClosing;
        Closed += (_, _) => _session.Dispose();
        Render();
    }

    /// <summary>Shows the dialog over <paramref name="owner"/> until it is closed.</summary>
    public static Task Open(Window owner, PublishSession session, ITextClipboard clipboard) =>
        new PublishDialog(session, clipboard).ShowDialog(owner);

    private static TextBlock Caption(string text) => new()
    {
        Text = text,
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.InkMid,
        TextWrapping = TextWrapping.Wrap,
    };

    private static SelectableTextBlock Mono(string text) => new()
    {
        Text = text,
        FontFamily = OrblitText.Mono,
        FontSize = OrblitText.Caption,
        Foreground = OrblitColors.Ink,
        TextWrapping = TextWrapping.Wrap,
    };

    private void Render()
    {
        var working = _session.Stage == PublishStage.Working;
        var done = _session.Stage == PublishStage.Done;
        _close.Content = done ? "Close" : "Cancel";
        _close.IsEnabled = !working;
        _folder.IsReadOnly = _url.IsReadOnly = _notes.IsReadOnly = working;
        _problem.Text = _session.Problem;
        _problem.IsVisible = _session.Problem is not null;
        _publish.Content = working ? "Packing…" : "Publish";
        _publish.IsEnabled = !working;
        _body.Content = done ? Done() : _form;
    }

    private Grid Form()
    {
        // A kit with many problems gives a long message, so the form scrolls and the button stays in view.
        var scroll = new ScrollViewer
        {
            Content = new StackPanel
            {
                Children =
                {
                    Caption("Packs a folder of this project as a kit and shows the entry that lists it in a store. The folder "
                        + "holds a kit.json and the files to give a project. Nothing is uploaded."),
                    SectionLabel.Of("Folder in the project"),
                    _folder,
                    SectionLabel.Of("Address the file will be hosted at"),
                    _url,
                    SectionLabel.Of("Notes for this release"),
                    _notes,
                    _problem,
                },
            },
        };
        Grid.SetRow(_publish, 1);
        return new Grid { RowSpacing = Space.Sm, RowDefinitions = new RowDefinitions("*,Auto"), Children = { scroll, _publish } };
    }

    private ScrollViewer Done()
    {
        // Shown only once the run is done, when the session holds what it made and where.
        var published = _session.Published!;
        var kitFile = _session.KitFile!;
        var entry = published.Entry;
        return new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = Space.Xs,
                Children =
                {
                    new TextBlock { Text = $"Packed {Path.GetFileName(kitFile)}: {SizeLabel.Of(published.Summary.Bytes)}.", FontSize = OrblitText.Body, Foreground = OrblitColors.Ink },
                    Mono(kitFile),
                    Mono(published.Summary.Hash.Hex),
                    SectionLabel.Of($"Entry for {entry.Publisher}/{entry.Slug}"),
                    EntryBox(_session.EntryText),
                    Caption($"Upload the file so that it is served at {published.Release.Url}. Then save the entry as "
                        + $"kits/{entry.Publisher}/{entry.Slug}.json in a clone of the orblit-kits repository, or add the "
                        + "release to that file when it lists releases already. Check it with kit index, and open a pull request."),
                },
            },
        };
    }

    private Border EntryBox(string text)
    {
        var copy = new Button { Name = "copy", Content = "Copy", HorizontalAlignment = HorizontalAlignment.Right };
        copy.Click += async (_, _) =>
        {
            await _clipboard.SetTextAsync(text);
            // Only once the copy went through, so nobody pastes what was not copied.
            copy.Content = "Copied";
        };
        return new Border
        {
            Margin = new Thickness(0, 0, 0, Space.Md),
            Padding = new Thickness(Space.Sm),
            Background = OrblitColors.Raised,
            BorderBrush = OrblitColors.Line,
            BorderThickness = new Thickness(1),
            CornerRadius = Radii.Control,
            Child = new StackPanel { Spacing = Space.Xs, Children = { Mono(text), copy } },
        };
    }
}

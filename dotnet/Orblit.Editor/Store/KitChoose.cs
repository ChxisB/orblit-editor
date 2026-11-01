using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>A downloaded kit: its scripts, if any, the folder it goes into, and a tick for each file.</summary>
/// <remarks>
/// A file the project already has with other bytes is unticked and marked, and is replaced only when the
/// person ticks it. The choices stay on screen while the files are written, so a failure leaves them as they
/// were.
/// </remarks>
internal sealed class KitChoose : Grid
{
    // A read looks at every file of the kit in the project, so it waits for a pause in the typing.
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(300);

    private readonly InstallSession _session;

    // Each read of the kit's files builds a dictionary, so it is read once.
    private readonly IReadOnlyDictionary<AssetId, long> _files;
    private readonly TextBox _folder;
    private readonly TextBlock _problem = new() { Name = "folder_problem", FontSize = OrblitText.Caption, Foreground = OrblitColors.Bad, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, Space.Xs, 0, 0) };
    private readonly ContentControl _count = new();
    private readonly ItemsControl _list = new() { ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    private readonly Button _install = new() { Name = "install", Background = OrblitColors.Ember, Foreground = OrblitColors.Ground, HorizontalAlignment = HorizontalAlignment.Right };

    // Files the person unticked.
    private readonly HashSet<AssetId> _left = [];

    // Files the project has that the person ticked to replace.
    private readonly HashSet<AssetId> _replace = [];

    // Files the project has with other bytes, or null while they are read or when the folder is not one a
    // kit may go into.
    private HashSet<AssetId>? _conflicts;
    private string? _readProblem;
    private int _typed;

    public KitChoose(InstallSession session, Kit kit)
    {
        _session = session;
        _files = kit.Files;
        RowSpacing = Space.Sm;
        RowDefinitions = new RowDefinitions("Auto,*,Auto");
        _folder = new TextBox { Name = "folder", Text = $"assets/kits/{session.Row.Manifest.Slug}", FontFamily = OrblitText.Mono };
        _folder.TextChanged += async (_, _) => await FolderChangedAsync();
        // A row is handed no item while the list swaps its source at each render.
        _list.ItemTemplate = new FuncDataTemplate<FileTick?>((tick, _) => tick is null ? null : TickRow(tick));
        _install.Click += async (_, _) => await _session.InstallAsync(Into, Picks(), _replace.ToHashSet());
        var top = new StackPanel();
        if (kit.Scripts.Count > 0)
        {
            top.Children.Add(ScriptWarning(kit.Scripts));
        }

        top.Children.AddRange([SectionLabel.Of("Folder in the project"), _folder, _problem, _count]);
        var scroll = new ScrollViewer { Content = _list };
        SetRow(scroll, 1);
        SetRow(_install, 2);
        Children.AddRange([top, scroll, _install]);
        _ = ReadConflictsAsync();
    }

    private string Into => (_folder.Text ?? "").Trim();

    public void Render()
    {
        var picks = Picks();
        var problem = KitRules.PlaceProblem(Into, picks) ?? _readProblem ?? _session.Problem;
        var writing = _session.Stage == InstallStage.Installing;
        _problem.Text = problem;
        _problem.IsVisible = problem is not null;
        _folder.IsReadOnly = writing;
        _count.Content = SectionLabel.Of($"Files ({picks.Count} of {_files.Count})");
        _list.IsEnabled = !writing;
        _list.ItemsSource = _files.Select((file, index) => new FileTick(index, file.Key, file.Value, Picked(file.Key), IsConflict(file.Key))).ToArray();
        _install.Content = writing ? "Writing files…" : "Install";
        // A folder a kit may not go into leaves the conflicts unread, so that alone keeps Install off.
        _install.IsEnabled = picks.Count > 0 && _conflicts is not null && !writing;
    }

    private static Border ScriptWarning(IReadOnlyList<AssetId> scripts) => new()
    {
        Margin = new Thickness(0, 0, 0, Space.Md),
        Padding = new Thickness(Space.Sm),
        Background = OrblitColors.Raised,
        BorderBrush = OrblitColors.Warn,
        BorderThickness = new Thickness(1),
        CornerRadius = Radii.Control,
        Child = new StackPanel
        {
            Spacing = Space.Xs,
            Children =
            {
                new TextBlock
                {
                    Text = $"This kit holds {scripts.Count} {(scripts.Count == 1 ? "script" : "scripts")}. Orblit never runs them "
                        + "when it installs a kit. Read them before you use them in your project.",
                    FontSize = OrblitText.Caption,
                    Foreground = OrblitColors.Warn,
                    TextWrapping = TextWrapping.Wrap,
                },
                new ScrollViewer
                {
                    MaxHeight = 64,
                    Content = new TextBlock { Text = string.Join('\n', scripts), FontFamily = OrblitText.Mono, FontSize = OrblitText.Caption, Foreground = OrblitColors.Ink },
                },
            },
        },
    };

    private HashSet<AssetId> Picks() => [.. _files.Keys.Where(Picked)];

    private bool IsConflict(AssetId id) => _conflicts?.Contains(id) ?? false;

    private bool Picked(AssetId id) => IsConflict(id) ? _replace.Contains(id) : !_left.Contains(id);

    private void Toggle(AssetId id)
    {
        var set = IsConflict(id) ? _replace : _left;
        if (!set.Add(id))
        {
            set.Remove(id);
        }

        Render();
    }

    private async Task FolderChangedAsync()
    {
        _conflicts = null;
        _readProblem = null;
        Render();
        // Only the wait that began at the last key reads, so the read starts once the typing stops.
        var typed = ++_typed;
        await Task.Delay(Pause);
        if (typed == _typed)
        {
            await ReadConflictsAsync();
        }
    }

    private async Task ReadConflictsAsync()
    {
        var into = Into;
        if (KitRules.PlaceProblem(into, _files.Keys) is not null)
        {
            _conflicts = null;
            Render();
            return;
        }

        IReadOnlyList<AssetId>? found = null;
        string? problem = null;
        try
        {
            found = await _session.ConflictsAsync(into);
        }
#pragma warning disable CA1031 // A project folder that cannot be read is said under the folder, not thrown.
        catch (Exception error)
#pragma warning restore CA1031
        {
            problem = error.Message;
        }

        if (into != Into)
        {
            return;
        }

        _conflicts = found?.ToHashSet();
        _readProblem = problem;
        _replace.RemoveWhere(id => found is null || !found.Contains(id));
        Render();
    }

    private Grid TickRow(FileTick tick)
    {
        var box = new CheckBox
        {
            Name = $"tick_{tick.Index}",
            IsChecked = tick.Picked,
            Content = new TextBlock { Text = tick.Id.ToString(), FontFamily = OrblitText.Mono, FontSize = OrblitText.Caption, Foreground = OrblitColors.Ink, TextTrimming = TextTrimming.CharacterEllipsis },
        };
        box.Click += (_, _) => Toggle(tick.Id);
        var replaces = new TextBlock { Text = "replaces your file", FontSize = OrblitText.Caption, Foreground = OrblitColors.Warn, IsVisible = tick.Replaces, VerticalAlignment = VerticalAlignment.Center };
        var size = new TextBlock { Text = SizeLabel.Of(tick.Size), FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, VerticalAlignment = VerticalAlignment.Center };
        SetColumn(replaces, 1);
        SetColumn(size, 2);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = Space.Sm, Children = { box, replaces, size } };
    }

    private sealed record FileTick(int Index, AssetId Id, long Size, bool Picked, bool Replaces);
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using IoPath = System.IO.Path;

/// <summary>
/// The project's files. It draws what an <see cref="AssetBrowserModel"/> lists and asks the tree for
/// every change to a file.
/// </summary>
/// <remarks>
/// A panel of its own and not a part of the outliner, because the two answer different questions: what
/// is in this scene, and what this project has.
/// </remarks>
internal sealed class AssetBrowserPanel : UserControl
{
    // Narrower than this and the folders go over the files.
    private const double StackedBelow = 360;

    // Narrower than this and the preview is left out.
    private const double PreviewFrom = 560;

    private readonly AssetBrowserModel _model;
    private readonly CookStatusIndex? _cook;
    private readonly AssetBrowserLinks _links;
    private readonly AssetBrowserHeader _header;
    private readonly AssetFolderTree _folders;
    private readonly AssetGrid _grid;
    private readonly AssetPreviewPanel _preview;
    private readonly Grid _body = new();
    private bool? _stacked;
    private bool _roomForPreview;

    // A panel along the bottom has no width to spare, so somebody working in a narrow window can shut it.
    private bool _previewing = true;

    /// <param name="tree">The project's files.</param>
    /// <param name="cook">Where each asset stands with the cook, for the mark beside each file. Without one the panel draws no marks.</param>
    /// <param name="links">What the rest of the editor does for the browser.</param>
    /// <param name="renderer">Whether this machine runs the renderer, which the preview draws a model with.</param>
    public AssetBrowserPanel(AssetTree tree, CookStatusIndex? cook = null, AssetBrowserLinks? links = null, bool renderer = true)
    {
        _model = new AssetBrowserModel(tree);
        _cook = cook;
        _links = links ?? new AssetBrowserLinks();
        _header = new AssetBrowserHeader(new AssetBrowserHeaderLinks(GoUp, ShowMenu, Rebuild, Filtered, TogglePreview));
        _preview = new AssetPreviewPanel(renderer);
        _folders = new AssetFolderTree(GoTo, _links.OnMakePrefab);
        _grid = new AssetGrid(new AssetGridLinks(Select, OpenAsset, ShowMenuFor, _links.OnMakePrefab is null ? null : id => _links.OnMakePrefab(id, _model.Directory)));
        Background = OrblitColors.Surface;

        _body.Children.Add(_folders);
        _body.Children.Add(_grid);
        var frame = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        _body[Grid.RowProperty] = 1;
        frame.Children.Add(_header);
        frame.Children.Add(_body);
        Content = frame;

        SizeChanged += (_, e) =>
        {
            _roomForPreview = e.NewSize.Width >= PreviewFrom;
            Fit(e.NewSize.Width < StackedBelow);
            ShowPreview();
        };
        Fit(stacked: false);
        ShowPreview();
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model.Tree.Changed += OnFilesChanged;
        if (_cook is not null)
        {
            _cook.Changed += OnCookChanged;
            // Not awaited: the panel is drawable before the first answer, and on a large project the
            // first answer is a second or two away.
            _ = _cook.RefreshAsync();
        }

        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _model.Tree.Changed -= OnFilesChanged;
        if (_cook is not null)
        {
            _cook.Changed -= OnCookChanged;
        }

        base.OnDetachedFromVisualTree(e);
    }

    /// <summary>Makes a folder or file in the open folder, and opens a file straight away: nobody asks for a new script to look at its icon.</summary>
    internal void MakeNew(NewAsset what, string name)
    {
        var made = _model.Tree.Create(_model.Directory, what, name);
        if (made.Problem is not null)
        {
            _links.OnProblem?.Invoke($"Could not create it: {made.Problem}");
            return;
        }

        _model.Select(made.Path);
        Rebuild();
        if (!what.IsFolder && made.Path is not null)
        {
            _links.OnOpenAsset?.Invoke(AssetTree.Describe(made.Path));
        }
    }

    internal void Remove(Asset asset)
    {
        if (AssetTree.Delete(asset.Path) is { } problem)
        {
            _links.OnProblem?.Invoke($"Could not delete {asset.Name}: {problem}");
            return;
        }

        Forget(asset);
        Rebuild();
    }

    internal void Rename(Asset asset, string name)
    {
        if (AssetTree.Rename(asset.Path, name) is { } problem)
        {
            _links.OnProblem?.Invoke($"Could not rename {asset.Name}: {problem}");
            return;
        }

        Forget(asset);
        Rebuild();
    }

    // A file that is gone, or has another path now, is not the one picked any more.
    private void Forget(Asset asset)
    {
        if (_model.Selected == asset.Path)
        {
            _model.Select(null);
            _preview.Show(null);
        }
    }

    // The tree raises on a thread of its own. Anything that changed a file may have changed what the
    // cook would do with it, and the index coalesces the asks, so a save that touches forty files is
    // one answer.
    private void OnFilesChanged() => Dispatcher.UIThread.Post(() =>
    {
        Rebuild();
        _ = _cook?.RefreshAsync();
    });

    private void OnCookChanged() => Dispatcher.UIThread.Post(Rebuild);

    private void Rebuild()
    {
        var entries = _model.Entries();
        _header.Show(_model, entries.Count, _cook is null ? null : CookSummary.Of(_cook));
        _folders.Show(_model.Tree.Root, _model.Tree.Folders(), _model.Directory);
        _grid.Show(entries, _model.IsFiltering, _cook, _model.Selected);
    }

    private void Filtered(string text)
    {
        _model.Filter = text;
        Rebuild();
    }

    private void GoTo(string directory)
    {
        _model.Open(directory);
        Arrived();
    }

    private void GoUp()
    {
        _model.Up();
        Arrived();
    }

    private void Arrived()
    {
        Rebuild();
        _preview.Show(null);
        _links.OnSelectAsset?.Invoke(null);
    }

    private void Select(Asset asset)
    {
        _model.Select(asset.Path);
        _grid.Mark(asset.Path);
        _preview.Show(asset);
        _links.OnSelectAsset?.Invoke(asset);
    }

    private void OpenAsset(Asset asset)
    {
        if (asset.IsFolder)
        {
            GoTo(asset.Path);
            return;
        }

        _links.OnOpenAsset?.Invoke(asset);
    }

    private void ShowMenu(Control at) => ShowMenuFor(null, at);

    private void ShowMenuFor(Asset? asset, Control at)
    {
        var acting = asset is null
            ? null
            : new AssetActions(() => OpenAsset(asset), () => AskRename(asset), () => AskDelete(asset), asset.CanBuild && _links.OnBuild is { } build ? () => build(asset) : null);
        var menu = AssetMenu.Build(AskCreate, acting);
        menu.Placement = asset is null && at is Button ? PlacementMode.BottomEdgeAlignedLeft : PlacementMode.Pointer;
        menu.Open(at);
    }

    private async void AskCreate(NewAsset what)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var prompt = new NamePrompt($"New {what.Label.ToLowerInvariant()}", what.IsFolder ? "Name" : $"Adds {what.Extension} for you", what.Suggested) { AcceptText = "Create" };
        if (await prompt.ShowDialog<string?>(owner) is { } name)
        {
            MakeNew(what, name);
        }
    }

    private async void AskRename(Asset asset)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var prompt = new NamePrompt("Rename", "Name", asset.Name) { AcceptText = "Rename" };
        if (await prompt.ShowDialog<string?>(owner) is { } name)
        {
            Rename(asset, name);
        }
    }

    private async void AskDelete(Asset asset)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var text = asset.IsFolder
            ? "This deletes the folder and everything in it. It does not go to the Trash, and undo does not cover files."
            : "This does not go to the Trash, and undo does not cover files.";
        if (await ConfirmDialog.Ask(owner, $"Delete {asset.Name}?", text, "Delete"))
        {
            Remove(asset);
        }
    }

    private void TogglePreview()
    {
        _previewing = !_previewing;
        ShowPreview();
    }

    // A preview where there is room for a grid worth having between it and the folders. Under that the
    // folders go over the files, and the preview has no column to be in.
    private void ShowPreview()
    {
        var shown = _roomForPreview && _stacked != true && _previewing;
        _header.Previewing = _roomForPreview ? _previewing : null;
        // Taken out of the body and not hidden, so the renderer behind it is freed and stops drawing.
        if (shown && !_body.Children.Contains(_preview))
        {
            _body.Children.Add(_preview);
        }
        else if (!shown)
        {
            _body.Children.Remove(_preview);
        }
    }

    // Folders beside the files where there is room for both, and over them below that: the panel is as
    // likely to be a tall strip down the side of the window as a wide one under the view.
    private void Fit(bool stacked)
    {
        if (_stacked == stacked)
        {
            return;
        }

        _stacked = stacked;
        _folders.Stacked = stacked;
        _body.RowDefinitions.Clear();
        _body.ColumnDefinitions.Clear();
        if (stacked)
        {
            _body.RowDefinitions.AddRange([new RowDefinition(2, GridUnitType.Star), new RowDefinition(3, GridUnitType.Star)]);
            Grid.SetRow(_grid, 1);
            Grid.SetColumn(_grid, 0);
            return;
        }

        _body.ColumnDefinitions.AddRange([new ColumnDefinition(GridLength.Auto), new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(GridLength.Auto)]);
        Grid.SetColumn(_preview, 2);
        Grid.SetRow(_grid, 0);
        Grid.SetColumn(_grid, 1);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Editor;

using Orblit.Asset;
using Orblit.Editor.Core;

/// <summary>
/// The store being read, the stores a person can switch to, and buttons to add one and to forget one that
/// was added.
/// </summary>
internal static class RepositoryPicker
{
    private const string AddIcon = "M12 5v14M5 12h14";
    private const string ForgetIcon = "M5 12h14";

    public static IEnumerable<Control> Of(Window owner, StoreSession session)
    {
        var pick = Dropdown.Of(
            "store",
            Shown(session.Repository),
            () => session.Settings.Repositories.Select(address => (Shown(address), (Action)(async () => await session.UseRepositoryAsync(address)))));
        pick.Width = 240;
        yield return pick;
        yield return Icon("add_store", AddIcon, "Add a store", async () =>
        {
            if (await AddStorePrompt.Ask(owner, session.Settings) is { } text)
            {
                await session.AddRepositoryAsync(text);
            }
        });
        if (!session.Repository.Equals(StoreSettings.DefaultStoreAddress))
        {
            yield return Icon("forget_store", ForgetIcon, "Forget this store", async () => await session.RemoveRepositoryAsync(session.Repository));
        }
    }

    private static string Shown(WebUrl address) => $"{address.Host}{address.Path}";

    private static Button Icon(string name, string icon, string tooltip, Action onPressed)
    {
        var button = new Button { Name = name, Background = Brushes.Transparent, Content = new Glyph(icon, 16, OrblitColors.InkMid) };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => onPressed();
        return button;
    }
}

/// <summary>Asks for the address of a store and says at once why one cannot be used.</summary>
internal sealed class AddStorePrompt : Window
{
    private const string Hint = "The address of a folder that holds index.json.";

    private readonly StoreSettings _settings;
    private readonly TextBox _address = new() { Name = "address", PlaceholderText = "https://example.com/store/", FontFamily = OrblitText.Mono, MinWidth = 420 };
    private readonly TextBlock _note = new() { Name = "note", Text = Hint, FontSize = OrblitText.Caption, Foreground = OrblitColors.InkDim, TextWrapping = TextWrapping.Wrap };
    private readonly Button _add = new() { Name = "add", Content = "Add", IsDefault = true, IsEnabled = false };

    public AddStorePrompt(StoreSettings settings)
    {
        _settings = settings;
        Title = "Add a store";
        SizeToContent = SizeToContent.WidthAndHeight;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = OrblitColors.Surface;
        _address.TextChanged += (_, _) => Check();
        _add.Click += (_, _) => Close(_address.Text?.Trim());
        var cancel = new Button { Name = "cancel", Content = "Cancel", IsCancel = true };
        cancel.Click += (_, _) => Close(null);
        Content = new StackPanel
        {
            Margin = new Thickness(Space.Lg),
            Spacing = Space.Sm,
            Children =
            {
                _address,
                _note,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.Sm, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, _add } },
            },
        };
    }

    /// <summary>Asks, and returns the address typed, or null when it was cancelled.</summary>
    public static Task<string?> Ask(Window owner, StoreSettings settings) => new AddStorePrompt(settings).ShowDialog<string?>(owner);

    private void Check()
    {
        var text = _address.Text ?? "";
        var problem = text.Trim().Length == 0 ? null : _settings.RepositoryProblem(text);
        _note.Text = problem ?? Hint;
        _note.Foreground = problem is null ? OrblitColors.InkDim : OrblitColors.Bad;
        _add.IsEnabled = text.Trim().Length > 0 && problem is null;
    }
}

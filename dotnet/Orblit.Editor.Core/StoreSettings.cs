namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Asset;
using Orblit.Json;

/// <summary>
/// The stores the editor reads and whether it lists kits made for another engine version, kept in
/// one file beside the editor's other settings.
/// </summary>
public sealed class StoreSettings
{
    private readonly string _file;
    private readonly List<WebUrl> _added = [];

    public StoreSettings(string file)
    {
        _file = file;
        try
        {
            if (!IoFile.Exists(file)
                || JsonText.Decode(IoFile.ReadAllText(file)) is not IReadOnlyDictionary<string, object?> raw)
            {
                return;
            }

            ShowIncompatible = raw.GetValueOrDefault("showIncompatible") is true;
            if (raw.GetValueOrDefault("repositories") is IReadOnlyList<object?> added)
            {
                foreach (var item in added.OfType<string>().Where(item => RepositoryProblem(item) is null))
                {
                    Add(Folder(WebUrl.Parse(item.Trim())));
                }
            }
        }
        catch (Exception error) when (error is JsonFormatException or IOException or UnauthorizedAccessException)
        {
            // A damaged file must not stop the store opening, and one that cannot be read leaves the
            // default store.
        }
    }

    /// <summary>The store the editor reads until a person adds another.</summary>
    public static WebUrl DefaultStoreAddress { get; } = WebUrl.Parse("https://orblitengine.com/store/");

    /// <summary>The default store first, then the ones a person added, in that order.</summary>
    public IReadOnlyList<WebUrl> Repositories => [DefaultStoreAddress, .. _added];

    public bool ShowIncompatible { get; private set; }

    /// <summary>Why <paramref name="text"/> cannot be added as a store, or null.</summary>
    public string? RepositoryProblem(string text)
    {
        if (!WebUrl.TryParse(text.Trim(), out var address) || address.Scheme.Length == 0)
        {
            return "That is not an address.";
        }

        if (KitStoreClient.AddressProblem(address) is { } problem)
        {
            return problem;
        }

        return Repositories.Contains(Folder(address)) ? "That store is already in the list." : null;
    }

    /// <summary>Throws an <see cref="ArgumentException"/> when <see cref="RepositoryProblem"/> has something to say.</summary>
    public void AddRepository(string text)
    {
        if (RepositoryProblem(text) is { } problem)
        {
            throw new ArgumentException(problem, nameof(text));
        }

        Add(Folder(WebUrl.Parse(text.Trim())));
        Write();
    }

    public void RemoveRepository(WebUrl address)
    {
        if (_added.Remove(address))
        {
            Write();
        }
    }

    public void SetShowIncompatible(bool value)
    {
        ShowIncompatible = value;
        Write();
    }

    // An address with no trailing slash names a folder, so both spellings are one store.
    private static WebUrl Folder(WebUrl address) =>
        address.Path.EndsWith('/') ? address : WebUrl.Parse($"{address}/");

    private void Add(WebUrl address)
    {
        if (!Repositories.Contains(address))
        {
            _added.Add(address);
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!); // A file path always has a folder.
            var pending = _file + ".tmp";
            IoFile.WriteAllText(
                pending,
                JsonText.Encode(
                    new OrderedDictionary<string, object?>
                    {
                        ["repositories"] = _added.Select(address => (object?)address.ToString()).ToList(),
                        ["showIncompatible"] = ShowIncompatible,
                    }));
            IoFile.Move(pending, _file, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // shortcut: a settings file that cannot be written is kept for this run only. Tell the
            // person when it matters.
        }
    }
}

namespace Orblit.Editor.Core;

using Orblit.Asset;

public enum StoreSort
{
    Updated,
    Name,
}

/// <summary>What a person has typed and picked to narrow the list of kits.</summary>
public sealed record StoreQuery
{
    /// <summary>Words that must each appear in a kit's name, summary, publisher, slug or tags.</summary>
    public string Text { get; init; } = "";

    public StoreSort Sort { get; init; }

    /// <summary>An SPDX name, or empty for any licence.</summary>
    public string Licence { get; init; } = "";

    /// <summary>A tag, or empty for any.</summary>
    public string Tag { get; init; } = "";

    public bool HideScripts { get; init; }

    /// <summary>The licences <paramref name="rows"/> use, sorted.</summary>
    public static IReadOnlyList<string> Licences(IEnumerable<KitRow> rows) =>
        [.. rows.Select(row => row.Manifest.Licence.Spdx).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>The tags <paramref name="rows"/> use, sorted.</summary>
    public static IReadOnlyList<string> Tags(IEnumerable<KitRow> rows) =>
        [.. rows.SelectMany(row => row.Manifest.Tags).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>
    /// The rows that pass, in the order <see cref="Sort"/> gives. A kit made for other engine versions than
    /// <paramref name="engine"/> is left out unless <paramref name="showIncompatible"/>.
    /// </summary>
    public IReadOnlyList<KitRow> Apply(IEnumerable<KitRow> rows, string engine, bool showIncompatible)
    {
        var words = Text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var kept = rows.Where(row => Passes(row, words) && (showIncompatible || row.Manifest.Engine.Fits(engine)));
        // The date is year, month and day in that order, so text order is date order.
        var sorted = Sort == StoreSort.Name
            ? kept.OrderBy(NameKey, StringComparer.Ordinal)
            : kept.OrderByDescending(row => row.Updated, StringComparer.Ordinal).ThenBy(NameKey, StringComparer.Ordinal);
        return [.. sorted];
    }

    private static string NameKey(KitRow row) => row.Manifest.Name.ToLowerInvariant();

    private bool Passes(KitRow row, string[] words)
    {
        var manifest = row.Manifest;
        if ((Licence.Length > 0 && manifest.Licence.Spdx != Licence)
            || (Tag.Length > 0 && !manifest.Tags.Contains(Tag))
            || (HideScripts && manifest.ContainsScripts))
        {
            return false;
        }

        var haystack = string.Join('\n', [manifest.Name, manifest.Summary, manifest.Publisher, manifest.Slug, .. manifest.Tags])
            .ToLowerInvariant();
        return words.All(word => haystack.Contains(word, StringComparison.Ordinal));
    }
}

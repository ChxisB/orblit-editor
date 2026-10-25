namespace Orblit.Editor.Core;

using System.Diagnostics;

/// <summary>The heading an entry is listed under, in the order the headings run.</summary>
public enum PaletteGroup
{
    Commands,
    Objects,
}

public static class PaletteGroups
{
    public static string Title(this PaletteGroup group) => group switch
    {
        PaletteGroup.Commands => "Commands",
        PaletteGroup.Objects => "Objects",
        _ => throw new UnreachableException(),
    };
}

/// <summary>One thing the palette can find and run.</summary>
public sealed record PaletteEntry
{
    public required PaletteGroup Group { get; init; }

    public required string Label { get; init; }

    /// <summary>The name of the icon the shell draws beside the label.</summary>
    public required string Icon { get; init; }

    public required Action OnRun { get; init; }

    /// <summary>Where it lives or what it works on, in small type after the label.</summary>
    public string Meta { get; init; } = "";

    /// <summary>The shortcut that does the same thing, if there is one.</summary>
    public string? Keys { get; init; }
}

/// <summary>An entry that matched, and which part of its label did.</summary>
/// <param name="Entry">The entry that matched.</param>
/// <param name="HitStart">Where the span of the label to light up begins.</param>
/// <param name="HitEnd">Where it ends. Equal to <paramref name="HitStart"/> when nothing was typed.</param>
public sealed record PaletteMatch(PaletteEntry Entry, int HitStart, int HitEnd);

public static class CommandPalette
{
    /// <summary>
    /// The entries whose label has every word of <paramref name="query"/> in it, a group at a time
    /// and the earliest hit first.
    /// <para>
    /// Few are kept for each group while nothing is typed, because a scene with a thousand objects
    /// is a list nobody reads, and more once there is something to narrow it by.
    /// </para>
    /// </summary>
    public static IReadOnlyList<PaletteMatch> Search(IReadOnlyList<PaletteEntry> entries, string query)
    {
        var words = query.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var limit = words.Length == 0 ? 6 : 12;

        // OrderBy is stable, so a tie keeps the order the entries came in.
        var found = entries
            .Select(entry => Match(entry, words))
            .OfType<PaletteMatch>()
            .OrderBy(match => match.HitStart)
            .ToList();

        return
        [
            .. Enum.GetValues<PaletteGroup>()
                .SelectMany(group => found.Where(match => match.Entry.Group == group).Take(limit)),
        ];
    }

    // shortcut: lower-cases one character at a time, so a label holding a capital dotted I lights up
    // a different span from Dart. Only the highlight moves, never which entries match.
    private static PaletteMatch? Match(PaletteEntry entry, string[] words)
    {
        if (words.Length == 0)
        {
            return new PaletteMatch(entry, 0, 0);
        }

        var label = entry.Label.ToLowerInvariant();
        var first = label.IndexOf(words[0], StringComparison.Ordinal);
        return first < 0 || words.Any(word => !label.Contains(word, StringComparison.Ordinal))
            ? null
            : new PaletteMatch(entry, first, first + words[0].Length);
    }
}

using Avalonia;
using Avalonia.Controls;

namespace Orblit.Editor;

using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>The bones of the target, each with the menu that says what moves it.</summary>
internal static class RetargetPairs
{
    /// <summary>One row for every bone of <paramref name="table"/>.</summary>
    /// <param name="table">What moves each bone now.</param>
    /// <param name="sources">The bones of the source a hand can pair, one to a name.</param>
    /// <param name="onChange">Called with the map once a choice has been made.</param>
    public static IEnumerable<Control> Rows(PairTable table, IReadOnlyList<string> sources, Action<BoneMap> onChange) =>
        table.Rows.Select((row, index) => Row(index, row, sources, table.Map, onChange));

    /// <summary>What the pairing left out and could not do, one to a line, or nothing.</summary>
    public static string Problems(PairTable table)
    {
        var lines = new List<string>();
        if (table.Stale.Count > 0)
        {
            lines.Add(
                $"The map names {table.Stale.Count} bones these models lack, which are left out: "
                + $"{string.Join(", ", table.Stale.Take(4))}{(table.Stale.Count > 4 ? " and more" : "")}.");
        }

        lines.AddRange(table.Problems.Take(3));
        return string.Join('\n', lines);
    }

    // The button is named by the row's place, which is as unique as the row. A bone's name may not make a name.
    private static Grid Row(int index, PairRow row, IReadOnlyList<string> sources, BoneMap map, Action<BoneMap> onChange)
    {
        var pick = Dropdown.Of($"pair_{index}", Shown(row), () => Choices(row, sources, map, onChange));
        Grid.SetColumn(pick, 1);
        var label = new TextBlock
        {
            Text = row.Bone,
            FontSize = OrblitText.Label,
            Foreground = OrblitColors.Ink,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        };
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2*,3*"),
            Height = 30,
            Margin = new Thickness(0, 0, Space.Md, 0),
            Children = { label, pick },
        };
    }

    private static string Shown(PairRow row) => (row.How, row.Driver) switch
    {
        (PairHow.Hand, { } driver) => driver,
        (PairHow.Auto, var driver) => $"Automatic: {driver ?? "no match"}",
        _ => "Nothing",
    };

    private static IEnumerable<(string Text, Action Choose)> Choices(PairRow row, IReadOnlyList<string> sources, BoneMap map, Action<BoneMap> onChange)
    {
        // A row set by hand or to nothing cannot say who names and the body would pick for it.
        yield return (row.How == PairHow.Auto ? Shown(row) : "Automatic", () => onChange(map.WithAuto(row.Bone)));
        yield return ("Nothing", () => onChange(map.WithUnpaired(row.Bone)));
        foreach (var source in sources)
        {
            yield return (source, () => onChange(map.WithPair(row.Bone, source)));
        }
    }
}

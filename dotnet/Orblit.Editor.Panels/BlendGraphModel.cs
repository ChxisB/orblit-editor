namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>Where a state is placed in the drawn graph.</summary>
public readonly record struct BlendCell(int Column, int Row);

/// <summary>Every change from one state to another, drawn as one arrow.</summary>
/// <param name="From">The state the arrow leaves.</param>
/// <param name="To">The state the arrow enters.</param>
/// <param name="Changes">Indexes into <see cref="BlendDocument.WrittenChanges"/>, in order of precedence.</param>
public sealed record BlendEdge(string From, string To, IReadOnlyList<int> Changes);

/// <summary>
/// What the blend graph editor knows of a blend's shape, apart from drawing it: which arrows join the
/// states and where each state is placed. Nothing here reads a file or a control.
/// </summary>
public static class BlendGraphModel
{
    /// <summary>
    /// The state of <paramref name="blend"/>, as a file writes its states, that <paramref name="name"/>
    /// belongs to: the state itself, or the one whose nested graph holds a leaf named <c>state/leaf</c>.
    /// Null for a name no state has.
    /// </summary>
    public static string? StateOwning(BlendDocument blend, string name)
    {
        var names = blend.WrittenStates.Select(static state => state.Name).ToHashSet();
        if (names.Contains(name))
        {
            return name;
        }

        var slash = name.IndexOf('/');
        return slash > 0 && names.Contains(name[..slash]) ? name[..slash] : null;
    }

    /// <summary>
    /// One arrow for each pair of states a change joins.
    /// </summary>
    /// <remarks>
    /// A change from anywhere has no arrow, and neither does one that goes nowhere a state is.
    /// <see cref="ChangesFromAnywhere"/> answers the first.
    /// </remarks>
    public static IReadOnlyList<BlendEdge> Edges(BlendDocument blend)
    {
        var byPair = new OrderedDictionary<(string From, string To), List<int>>();
        for (var index = 0; index < blend.WrittenChanges.Count; index++)
        {
            var change = blend.WrittenChanges[index];
            if (change.From is null
                || StateOwning(blend, change.From) is not { } source
                || StateOwning(blend, change.To) is not { } target)
            {
                continue;
            }

            if (!byPair.TryGetValue((source, target), out var changes))
            {
                byPair[(source, target)] = changes = [];
            }

            changes.Add(index);
        }

        return [.. byPair.Select(static pair => new BlendEdge(pair.Key.From, pair.Key.To, pair.Value))];
    }

    /// <summary>
    /// Indexes into <see cref="BlendDocument.WrittenChanges"/> of the changes that can be taken from any
    /// state, by the state each one enters.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<int>> ChangesFromAnywhere(BlendDocument blend)
    {
        var found = new Dictionary<string, IReadOnlyList<int>>();
        for (var index = 0; index < blend.WrittenChanges.Count; index++)
        {
            var change = blend.WrittenChanges[index];
            if (change.From is null && StateOwning(blend, change.To) is { } target)
            {
                found[target] = [.. found.GetValueOrDefault(target) ?? [], index];
            }
        }

        return found;
    }

    /// <summary>
    /// Where each state goes: a column for how many changes it is from the start, a row for where it came
    /// among them. A state nothing leads to is put in a column after the last, so it is seen rather than
    /// lost.
    /// </summary>
    /// <remarks>
    /// Worked out and not stored. A file has no positions, so two people who open one see the same graph
    /// and a merge cannot conflict over a drag.
    /// </remarks>
    public static OrderedDictionary<string, BlendCell> Layout(BlendDocument blend)
    {
        var columns = ColumnsFromStart(blend);
        var after = columns.Values.Max() + 1;
        foreach (var state in blend.WrittenStates.Where(state => !columns.ContainsKey(state.Name)))
        {
            columns[state.Name] = after;
        }

        var rows = new Dictionary<int, int>();
        var cells = new OrderedDictionary<string, BlendCell>();
        foreach (var (name, column) in columns)
        {
            rows[column] = rows.GetValueOrDefault(column, -1) + 1;
            cells[name] = new BlendCell(column, rows[column]);
        }

        return cells;
    }

    // In the order each state was reached, which is the order the rows are given out in.
    private static OrderedDictionary<string, int> ColumnsFromStart(BlendDocument blend)
    {
        var start = StateOwning(blend, blend.WrittenStart) ?? blend.WrittenStates[0].Name;
        var leadsTo = Edges(blend).ToLookup(static edge => edge.From, static edge => edge.To);
        var columns = new OrderedDictionary<string, int> { [start] = 0 };
        for (var i = 0; i < columns.Count; i++)
        {
            var (from, column) = columns.GetAt(i);
            foreach (var to in leadsTo[from].Where(to => !columns.ContainsKey(to)))
            {
                columns[to] = column + 1;
            }
        }

        return columns;
    }
}

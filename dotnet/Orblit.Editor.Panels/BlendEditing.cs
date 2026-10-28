namespace Orblit.Editor.Panels;

using Orblit.Motion;

/// <summary>
/// The ways a blend is changed, each answering another blend.
/// </summary>
/// <remarks>
/// Every one answers the same blend, by reference, when it would change nothing or the result is not a
/// blend: a name taken, the only state removed. A caller tests for that rather than for an exception.
/// </remarks>
public static class BlendEditing
{
    /// <summary>The name a new state would be given: <paramref name="baseName"/>, or it with a number when that is taken.</summary>
    public static string UniqueStateName(this BlendDocument blend, string baseName)
    {
        var taken = blend.WrittenStates.Select(static state => state.Name)
            .Concat(blend.Entries.Keys)
            .Concat(blend.States.Select(static state => state.Name))
            .ToHashSet();
        var name = baseName;
        for (var n = 2; taken.Contains(name); n++)
        {
            name = $"{baseName} {n}";
        }

        return name;
    }

    /// <summary>This blend with a state playing <paramref name="clip"/>, named after it.</summary>
    public static BlendDocument WithStateAdded(this BlendDocument blend, string clip)
    {
        // A clip with no name writes as a state that plays nothing, which a file then reads back without.
        var named = clip.Length == 0 ? "clip" : clip;
        var name = blend.UniqueStateName(named);
        return Tried(blend, () => blend.CopyWith(states: [.. blend.WrittenStates, new BlendState(name, new BlendClip(named))]));
    }

    /// <summary>
    /// This blend with state <paramref name="name"/> called <paramref name="to"/>, and every change and the
    /// start that named it naming it by the new name.
    /// </summary>
    public static BlendDocument WithStateRenamed(this BlendDocument blend, string name, string to)
    {
        if (to.Length == 0 || to == name || StateIndex(blend, name) < 0)
        {
            return blend;
        }

        string Rename(string old) => old == name ? to : OwnedBy(old, name) ? to + old[name.Length..] : old;
        return Tried(blend, () => blend.CopyWith(
            states: [.. blend.WrittenStates.Select(state => state.Name == name ? state.CopyWith(name: to) : state)],
            changes:
            [
                .. blend.WrittenChanges.Select(change => change.CopyWith(
                    from: change.From is null ? null : Rename(change.From),
                    to: Rename(change.To))),
            ],
            start: Rename(blend.WrittenStart)));
    }

    /// <summary>
    /// This blend without state <paramref name="name"/>, or the changes into or out of it. The start moves
    /// to the first state left when it was the one removed.
    /// </summary>
    public static BlendDocument WithStateRemoved(this BlendDocument blend, string name)
    {
        if (blend.WrittenStates.Count < 2 || StateIndex(blend, name) < 0)
        {
            return blend;
        }

        bool Names(string? state) => state is not null && (state == name || OwnedBy(state, name));
        var left = blend.WrittenStates.Where(state => state.Name != name).ToList();
        return Tried(blend, () => blend.CopyWith(
            states: left,
            changes: [.. blend.WrittenChanges.Where(change => !Names(change.From) && !Names(change.To))],
            start: Names(blend.WrittenStart) ? left[0].Name : blend.WrittenStart));
    }

    /// <summary>This blend with state <paramref name="name"/> made by <paramref name="change"/> of what it was.</summary>
    public static BlendDocument WithState(this BlendDocument blend, string name, Func<BlendState, BlendState> change)
    {
        var index = StateIndex(blend, name);
        return index < 0
            ? blend
            : Tried(blend, () => blend.CopyWith(states: [.. blend.WrittenStates.Select((state, i) => i == index ? change(state) : state)]));
    }

    /// <summary>This blend starting in state <paramref name="name"/>.</summary>
    public static BlendDocument WithStart(this BlendDocument blend, string name) =>
        StateIndex(blend, name) < 0 || name == blend.WrittenStart ? blend : Tried(blend, () => blend.CopyWith(start: name));

    /// <summary>
    /// This blend with a change from <paramref name="from"/> (anywhere for null) to <paramref name="to"/>,
    /// last in precedence.
    /// </summary>
    public static BlendDocument WithChangeAdded(this BlendDocument blend, string? from, string to) =>
        Tried(blend, () => blend.CopyWith(changes: [.. blend.WrittenChanges, new BlendChange(to, from)]));

    /// <summary>This blend with the change at <paramref name="index"/> made by <paramref name="change"/> of what it was.</summary>
    public static BlendDocument WithChange(this BlendDocument blend, int index, Func<BlendChange, BlendChange> change) =>
        index < 0 || index >= blend.WrittenChanges.Count
            ? blend
            : Tried(blend, () => blend.CopyWith(changes: [.. blend.WrittenChanges.Select((one, i) => i == index ? change(one) : one)]));

    /// <summary>This blend without the change at <paramref name="index"/>.</summary>
    public static BlendDocument WithChangeRemoved(this BlendDocument blend, int index) =>
        index < 0 || index >= blend.WrittenChanges.Count
            ? blend
            : blend.CopyWith(changes: [.. blend.WrittenChanges.Where((_, i) => i != index)]);

    /// <summary>
    /// This blend with the change at <paramref name="index"/> moved <paramref name="by"/> places in
    /// precedence, stopping at either end.
    /// </summary>
    public static BlendDocument WithChangeMoved(this BlendDocument blend, int index, int by)
    {
        // The range is checked before the clamp, which throws when there is no change to be its upper limit.
        if (index < 0 || index >= blend.WrittenChanges.Count)
        {
            return blend;
        }

        var to = Math.Clamp(index + by, 0, blend.WrittenChanges.Count - 1);
        if (to == index)
        {
            return blend;
        }

        var changes = blend.WrittenChanges.ToList();
        changes.RemoveAt(index);
        changes.Insert(to, blend.WrittenChanges[index]);
        return blend.CopyWith(changes: changes);
    }

    /// <summary>This blend called <paramref name="name"/>.</summary>
    public static BlendDocument WithName(this BlendDocument blend, string name) =>
        name.Length == 0 || name == blend.Name ? blend : blend.CopyWith(name: name);

    /// <summary>
    /// This blend with input <paramref name="name"/> at nought, or the same blend when it already has an
    /// input by that name.
    /// </summary>
    public static BlendDocument WithInputAdded(this BlendDocument blend, string name) =>
        name.Length == 0 || blend.Inputs.ContainsKey(name) ? blend : blend.WithInput(name, 0);

    /// <summary>This blend with input <paramref name="name"/> at <paramref name="value"/> until something sets it.</summary>
    public static BlendDocument WithInput(this BlendDocument blend, string name, double value)
    {
        if (name.Length == 0 || !double.IsFinite(value) || (blend.Inputs.TryGetValue(name, out var now) && now == value))
        {
            return blend;
        }

        return blend.CopyWith(inputs: new Dictionary<string, double>(blend.Inputs) { [name] = value });
    }

    /// <summary>This blend without input <paramref name="name"/>.</summary>
    public static BlendDocument WithInputRemoved(this BlendDocument blend, string name)
    {
        var left = new Dictionary<string, double>(blend.Inputs);
        return left.Remove(name) ? blend.CopyWith(inputs: left) : blend;
    }

    // A leaf of a nested graph is named state/leaf.
    private static bool OwnedBy(string leaf, string state) => leaf.StartsWith($"{state}/", StringComparison.Ordinal);

    private static int StateIndex(BlendDocument blend, string name) => blend.WrittenStates.ToList().FindIndex(state => state.Name == name);

    private static BlendDocument Tried(BlendDocument blend, Func<BlendDocument> make)
    {
        try
        {
            return make();
        }
        catch (ArgumentException)
        {
            return blend;
        }
    }
}

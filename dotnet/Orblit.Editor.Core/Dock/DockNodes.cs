namespace Orblit.Editor.Core;

/// <summary>Whether a split puts its children side by side or one above another.</summary>
public enum DockAxis
{
    Horizontal,
    Vertical,
}

/// <summary>Where a dropped panel goes relative to what it was dropped on.</summary>
public enum DockSide
{
    Left,
    Right,
    Top,
    Bottom,
    Centre,
}

/// <summary>A place in the layout: either a stack of tabs or a row or column of places.</summary>
public abstract class DockNode
{
    private protected DockNode(string id) => Id = id;

    /// <summary>
    /// Stable across edits, so a drop can name what it landed on and per-panel state can be filed
    /// under something that does not move.
    /// </summary>
    public string Id { get; }

    public abstract OrderedDictionary<string, object?> ToJson();

    public static DockNode? FromJson(object? value, IEnumerable<PanelKind> kinds)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return null;
        }

        return map.GetValueOrDefault("split") is string
            ? DockSplit.FromJson(map, kinds)
            : DockGroup.FromJson(map, kinds);
    }
}

/// <summary>Panels sharing one space, one showing at a time.</summary>
public sealed class DockGroup : DockNode
{
    public DockGroup(string id, IReadOnlyList<DockPanel> panels, int active = 0, bool collapsed = false)
        : base(id)
    {
        Panels = panels;
        Active = active;
        Collapsed = collapsed;
    }

    public IReadOnlyList<DockPanel> Panels { get; }

    /// <summary>
    /// Which one is showing. Clamped on read rather than on write, because a panel closing can take
    /// the active one with it and a layout mid-edit is allowed to be briefly out of step.
    /// </summary>
    public int Active { get; }

    /// <summary>
    /// Whether it is folded down to its tabs.
    /// <para>
    /// Folded rather than closed, so the console and the timeline are one click away without taking
    /// height from the view while nobody is reading them. Only a group stacked in a column folds: in a
    /// row it would be a strip of tabs as tall as the window and as narrow as nothing.
    /// </para>
    /// </summary>
    public bool Collapsed { get; }

    public int Showing => Panels.Count == 0 ? 0 : Math.Clamp(Active, 0, Panels.Count - 1);

    public DockPanel? Current => Panels.Count == 0 ? null : Panels[Showing];

    public DockGroup CopyWith(IReadOnlyList<DockPanel>? panels = null, int? active = null, bool? collapsed = null) =>
        new(Id, panels ?? Panels, active ?? Active, collapsed ?? Collapsed);

    public override OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["id"] = Id, ["active"] = (long)Active };
        if (Collapsed)
        {
            json["collapsed"] = true;
        }

        json["panels"] = Panels.Select(panel => (object?)panel.ToJson()).ToList();
        return json;
    }

    public static DockGroup? FromJson(IReadOnlyDictionary<string, object?> map, IEnumerable<PanelKind> kinds)
    {
        if (map.GetValueOrDefault("id") is not string { Length: > 0 } id)
        {
            return null;
        }

        var panels = map.GetValueOrDefault("panels") is IReadOnlyList<object?> raw
            ? raw.Select(entry => DockPanel.FromJson(entry, kinds)).OfType<DockPanel>().ToList()
            : [];

        // shortcut: an index past int reads as the end of the range, when a layout has one that large.
        var active = map.GetValueOrDefault("active") is long at ? (int)Math.Clamp(at, int.MinValue, int.MaxValue) : 0;
        return new DockGroup(id, panels, active, map.GetValueOrDefault("collapsed") is true);
    }
}

/// <summary>Places side by side, or one above another.</summary>
public sealed class DockSplit : DockNode
{
    public DockSplit(string id, DockAxis axis, IReadOnlyList<DockNode> children, IReadOnlyList<double> weights)
        : base(id)
    {
        Axis = axis;
        Children = children;
        Weights = weights;
    }

    public DockAxis Axis { get; }

    public IReadOnlyList<DockNode> Children { get; }

    /// <summary>
    /// How the space is shared, one per child. Kept as fractions so a window that is resized keeps
    /// the proportions somebody chose rather than the pixels they happened to drag to.
    /// </summary>
    public IReadOnlyList<double> Weights { get; }

    /// <summary>
    /// The weights, made to add up to one and to have one per child.
    /// <para>
    /// Read rather than enforced, because a file written by an older build or edited by hand is
    /// allowed to be wrong and a layout that refuses to open is worse than one that evens itself out.
    /// </para>
    /// </summary>
    public IReadOnlyList<double> Shares
    {
        get
        {
            if (Weights.Count != Children.Count || Children.Count == 0)
            {
                return Even();
            }

            var total = Weights.Sum(weight => weight <= 0 ? 0 : weight);
            return total <= 0 ? Even() : [.. Weights.Select(weight => (weight <= 0 ? 0 : weight) / total)];
        }
    }

    /// <summary>
    /// Which children are drawn folded down to their tabs, one per child.
    /// <para>
    /// A folded group only makes sense stacked in a column, and a column with every child folded
    /// would have nothing to give the room to, so then none of them are.
    /// </para>
    /// </summary>
    public IReadOnlyList<bool> Folded
    {
        get
        {
            var wants = Children
                .Select(child => Axis == DockAxis.Vertical && child is DockGroup { Collapsed: true })
                .ToList();
            return wants.TrueForAll(fold => fold) ? [.. wants.Select(_ => false)] : wants;
        }
    }

    public DockSplit CopyWith(IReadOnlyList<DockNode>? children = null, IReadOnlyList<double>? weights = null) =>
        new(Id, Axis, children ?? Children, weights ?? Weights);

    public override OrderedDictionary<string, object?> ToJson() => new()
    {
        ["id"] = Id,
        ["split"] = Axis == DockAxis.Horizontal ? "row" : "column",
        ["weights"] = Shares.Select(share => (object?)share).ToList(),
        ["children"] = Children.Select(child => (object?)child.ToJson()).ToList(),
    };

    public static DockSplit? FromJson(IReadOnlyDictionary<string, object?> map, IEnumerable<PanelKind> kinds)
    {
        if (map.GetValueOrDefault("id") is not string { Length: > 0 } id)
        {
            return null;
        }

        var children = map.GetValueOrDefault("children") is IReadOnlyList<object?> raw
            ? raw.Select(entry => DockNode.FromJson(entry, kinds)).OfType<DockNode>().ToList()
            : [];
        if (children.Count == 0)
        {
            return null;
        }

        var weights = map.GetValueOrDefault("weights") is IReadOnlyList<object?> rawWeights
            ? rawWeights.Select(NumberOf).OfType<double>().ToList()
            : [];
        return new DockSplit(
            id,
            map.GetValueOrDefault("split") as string == "column" ? DockAxis.Vertical : DockAxis.Horizontal,
            children,
            weights);
    }

    private static double? NumberOf(object? value) => value switch
    {
        long whole => whole,
        double number => number,
        _ => null,
    };

    private List<double> Even() => [.. Children.Select(_ => 1.0 / Children.Count)];
}

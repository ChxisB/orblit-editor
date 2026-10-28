namespace Orblit.Editor.Core;

using Orblit.Json;

/// <summary>
/// The arrangement of the editor's panels.
/// </summary>
/// <remarks>
/// Data rather than a control tree, so it can be saved with the project, put back exactly, compared
/// in a test, and changed by dragging a tab without any of that being a special case. Every operation
/// returns a new layout: an arrangement half-way through being rebuilt is not something anything
/// else should be able to see.
/// </remarks>
public sealed class DockLayout
{
    public const int FormatVersion = 1;

    private const string Marker = "orblit.layout";

    private static int _next;

    public DockLayout(DockNode root, bool locked = false, int revision = 0)
    {
        Root = root;
        Locked = locked;
        Revision = revision;
    }

    public DockNode Root { get; }

    /// <summary>
    /// Whether the arrangement can be changed by dragging.
    /// <para>
    /// Somebody who has their panels where they want them should be able to say so. A layout that can
    /// always be pulled apart is a layout that eventually is, by a drag that was meant to be
    /// something else.
    /// </para>
    /// </summary>
    public bool Locked { get; }

    /// <summary>
    /// Which version of its mode's own arrangement this one grew from.
    /// <para>
    /// A mode that changes the panels it opens with raises its number. A layout saved under a lower
    /// one is set aside for the new arrangement. Otherwise somebody who never moved a panel would
    /// never see the new ones.
    /// </para>
    /// </summary>
    public int Revision { get; }

    /// <summary>Every panel in the layout, in the order it is laid out.</summary>
    public IReadOnlyList<DockPanel> Panels
    {
        get
        {
            var all = new List<DockPanel>();
            Walk(Root, all);
            return all;
        }
    }

    /// <summary>
    /// The arrangement the editor opens with, which is the Scene mode's.
    /// <para>
    /// Three columns, each the height of the window apart from the middle one: what is in the scene on
    /// the left, the view in the middle with the project and the console open under it, and the
    /// inspector down the whole right side, because a component with twenty fields is the one thing
    /// that is always short of height.
    /// </para>
    /// <para>
    /// Revision 1 took the modelling, UV and timeline panels out. Each has a mode of its own now, and
    /// a panel for a job nobody is doing is one more thing for a newcomer to wonder about. Revision 2
    /// moved the project from under the tree to under the view, open, so a mesh is dragged across the
    /// shortest distance and the tree has the whole column.
    /// </para>
    /// </summary>
    public static DockLayout Standard() => Columns(
        new DockGroup("centre", [new DockPanel("scene", PanelKind.Viewport), new DockPanel("game", PanelKind.Game)]),
        bottom: [new DockPanel("project", PanelKind.Project), new DockPanel("console", PanelKind.Console)],
        right: [new DockPanel("inspector", PanelKind.Inspector)],
        folded: false,
        revision: 2);

    /// <summary>
    /// Shaping one object. The modelling tools come first on the right, and the UVs are open under
    /// the view, because unwrapping is half the work of a shape. No game view: nothing is played while
    /// a shape is being made.
    /// </summary>
    public static DockLayout Modelling() => Columns(
        new DockGroup("centre", [new DockPanel("scene", PanelKind.Viewport)]),
        bottom: [new DockPanel("uvs", PanelKind.Uvs)],
        right: [new DockPanel("modelling", PanelKind.Modelling), new DockPanel("inspector", PanelKind.Inspector)],
        folded: false,
        below: 0.3,
        revision: 1);

    /// <summary>
    /// Making things move. The timeline is open under the view and taller than the console would be,
    /// because keys are the work here. The inspector has the key buttons.
    /// </summary>
    public static DockLayout Animation() => Columns(
        new DockGroup("centre", [new DockPanel("scene", PanelKind.Viewport)]),
        bottom: [new DockPanel("timeline", PanelKind.Timeline)],
        right: [new DockPanel("inspector", PanelKind.Inspector)],
        folded: false,
        below: 0.4);

    /// <summary>
    /// Shaping the ground. The brush is in front on the right, since its settings are what is reached
    /// for most while shaping, and only the console is under the view: the other tools there have
    /// nothing to say about the ground. The console is a short open strip, so a brush that cannot
    /// reach a place says so where it is seen.
    /// </summary>
    public static DockLayout Terrain() => Columns(
        new DockGroup("centre", [new DockPanel("scene", PanelKind.Viewport), new DockPanel("game", PanelKind.Game)]),
        bottom: [new DockPanel("console", PanelKind.Console)],
        right: [new DockPanel("brush", PanelKind.TerrainBrush), new DockPanel("inspector", PanelKind.Inspector)],
        folded: false,
        below: 0.14,
        revision: 1);

    /// <summary>
    /// Making cutscenes. The scene view has the finished shot beside it, so a camera is framed against
    /// what it will show. The shots come first on the right. The cutscene is open under the view and as
    /// tall as the timeline is in Animation, because timing is the work here.
    /// </summary>
    public static DockLayout Cinematics() => Columns(
        new DockSplit(
            "views",
            DockAxis.Horizontal,
            [
                new DockGroup("centre", [new DockPanel("scene", PanelKind.Viewport)]),
                new DockGroup("finished", [new DockPanel("shot", PanelKind.ShotPreview)]),
            ],
            [0.5, 0.5]),
        bottom: [new DockPanel("cutsceneTimeline", PanelKind.CutsceneTimeline)],
        right: [new DockPanel("shots", PanelKind.ShotList), new DockPanel("inspector", PanelKind.Inspector)],
        folded: false,
        below: 0.4);

    /// <summary>
    /// Laying out the screens a game shows over its scene. The canvas is in the middle, where the scene
    /// view is in the other workspaces, since a screen is laid out at a fixed size and wants all the
    /// room there is. The elements are where the scene's tree is, and only the console is under the
    /// view: nothing else there says anything about a screen.
    /// </summary>
    public static DockLayout Interface() => Columns(
        new DockGroup("centre", [new DockPanel("canvas", PanelKind.InterfaceCanvas)]),
        bottom: [new DockPanel("console", PanelKind.Console)],
        right: [new DockPanel("design", PanelKind.InterfaceDesign)],
        left: [new DockPanel("elements", PanelKind.InterfaceElements)],
        folded: false,
        below: 0.14,
        revision: 2);

    /// <summary>
    /// Four scene views onto the same world, the way a modelling tool arranges them. The reason the
    /// layout is data at all.
    /// </summary>
    public static DockLayout FourViews() => Columns(
        new DockSplit(
            "views",
            DockAxis.Vertical,
            [ViewsRow("viewsTop", "scene", "scene2"), ViewsRow("viewsBottom", "scene3", "scene4")],
            [0.5, 0.5]),
        bottom: [new DockPanel("project", PanelKind.Project), new DockPanel("console", PanelKind.Console)],
        right: [new DockPanel("inspector", PanelKind.Inspector)]);

    /// <summary>
    /// The three columns every built-in arrangement shares, around whatever goes in the middle. A
    /// mode's own arrangement starts from here too, so switching mode moves panels rather than the
    /// whole editor.
    /// <para>
    /// The panels under the view start <paramref name="folded"/> unless a mode says otherwise: the
    /// view is what somebody opens the editor to look at, and the console is a click away when there
    /// is something in it worth reading. <paramref name="below"/> is how much of the middle column
    /// they take once open. <paramref name="left"/> is the tree, which is the scene's unless a mode
    /// edits something else.
    /// </para>
    /// </summary>
    // F2: the centre, three columns and the three things a mode changes about them.
    public static DockLayout Columns(
        DockNode centre,
        IReadOnlyList<DockPanel> bottom,
        IReadOnlyList<DockPanel> right,
        IReadOnlyList<DockPanel>? left = null,
        bool folded = true,
        double below = 0.28,
        int revision = 0) =>
        new(
            new DockSplit(
                "root",
                DockAxis.Horizontal,
                [
                    new DockGroup("left", left ?? [new DockPanel("outliner", PanelKind.Outliner)]),
                    new DockSplit(
                        "middle",
                        DockAxis.Vertical,
                        [centre, new DockGroup("bottom", bottom, collapsed: folded)],
                        [1 - below, below]),
                    new DockGroup("right", right),
                ],
                [0.2, 0.58, 0.22]),
            revision: revision);

    /// <summary>
    /// Reads one, or null when it is not a layout.
    /// <para>
    /// Null rather than an exception: a layout file written by an older build is not worth refusing
    /// to open the editor over, and the standard arrangement is always there to fall back to.
    /// </para>
    /// <para>
    /// <paramref name="kinds"/> is every kind of panel something has registered. A panel of any other
    /// kind is left out, and the rest of the layout opens without it.
    /// </para>
    /// </summary>
    public static DockLayout? Read(string text, IEnumerable<PanelKind>? kinds = null)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (JsonFormatException)
        {
            return null;
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map || map.GetValueOrDefault("kind") as string != Marker)
        {
            return null;
        }

        if (DockNode.FromJson(map.GetValueOrDefault("root"), kinds ?? PanelKind.BuiltIn) is not { } root)
        {
            return null;
        }

        // Missing from every layout saved before there were revisions, which is what 0 means.
        // shortcut: a revision past int reads as int.MaxValue, when a layout has one that large.
        var revision = map.GetValueOrDefault("revision") is long saved
            ? (int)Math.Clamp(saved, int.MinValue, int.MaxValue)
            : 0;
        return new DockLayout(root, map.GetValueOrDefault("locked") is true, revision);
    }

    public bool Holds(string panelId) => Panels.Any(panel => panel.Id == panelId);

    /// <summary>The group containing a panel, wherever it has been docked.</summary>
    public DockGroup? GroupOf(string panelId) => GroupHolding(Root, panelId);

    /// <summary>Reveals a panel below the view, also in layouts with no bottom strip.</summary>
    public DockLayout OpenBottom(DockPanel panel)
    {
        if (Holds(panel.Id))
        {
            return Show(panel.Id);
        }

        if (HasGroup(Root, "bottom"))
        {
            return Add(panel, intoGroup: "bottom");
        }

        return CopyWith(
            root: new DockSplit(
                FreshId("split"),
                DockAxis.Vertical,
                [Root, new DockGroup("bottom", [panel])],
                [0.75, 0.25]));
    }

    public DockLayout CopyWith(DockNode? root = null, bool? locked = null, int? revision = null) =>
        new(root ?? Root, locked ?? Locked, revision ?? Revision);

    /// <summary>Shows a panel, opening its group's tab.</summary>
    public DockLayout Show(string panelId) => CopyWith(root: ShowIn(Root, panelId));

    /// <summary>Adds a panel next to whatever is showing, or shows it if it is already somewhere.</summary>
    public DockLayout Add(DockPanel panel, string? intoGroup = null)
    {
        if (Holds(panel.Id))
        {
            return Show(panel.Id);
        }

        var target = intoGroup ?? FirstGroup(Root)?.Id;
        return target is null ? this : CopyWith(root: IntoGroup(Root, target, panel));
    }

    /// <summary>
    /// Takes a panel out. Empty groups collapse and a split with one child left is replaced by that
    /// child, so the tree never grows a level that holds nothing.
    /// </summary>
    public DockLayout Close(string panelId) =>
        // Never nothing: a layout with no root has nowhere to put the panel somebody opens next.
        CopyWith(root: Prune(Remove(Root, panelId)) ?? Standard().Root);

    /// <summary>
    /// Moves a panel onto a side of a group, or into it as another tab.
    /// <para>
    /// The one operation dragging a tab performs. Everything about the shape of the layout, such as
    /// new splits, collapsed ones and reordered tabs, comes out of this rather than out of the control
    /// that drew the drag.
    /// </para>
    /// </summary>
    public DockLayout Dock(string panelId, string ontoGroup, DockSide side)
    {
        if (Locked)
        {
            return this;
        }

        var panel = Panels.FirstOrDefault(one => one.Id == panelId);
        if (panel is null)
        {
            return this;
        }

        // Onto its own group's centre is a no-op rather than a panel that disappears and comes back
        // somewhere else.
        if (side == DockSide.Centre && GroupHolding(Root, panelId)?.Id == ontoGroup)
        {
            return this;
        }

        var without = Prune(Remove(Root, panelId));
        if (without is null)
        {
            return CopyWith(root: new DockGroup(FreshId("group"), [panel]));
        }

        // The group it was dropped on may have gone with it: the panel was the only one in it, and
        // taking it out collapsed the group. Nowhere to put it means it stays where it was, rather
        // than being dropped on the floor.
        return HasGroup(without, ontoGroup) ? CopyWith(root: DockInto(without, ontoGroup, panel, side)) : this;
    }

    /// <summary>
    /// Folds a group down to its tabs, or opens it out again.
    /// <para>
    /// Allowed while the layout is locked, the way choosing a tab is: it changes what is showing, not
    /// where anything is.
    /// </para>
    /// </summary>
    public DockLayout Collapse(string groupId, bool collapsed = true) =>
        collapsed && !CanCollapse(groupId) ? this : CopyWith(root: CollapseIn(Root, groupId, collapsed));

    /// <summary>
    /// Whether a group can fold: it is stacked in a column under something else, and something in that
    /// column would still be open to take the room.
    /// <para>
    /// Under something, because a group folds down: the console under the view folds and the view
    /// grows into its room, but the view itself folding up to a strip at the top of the window would
    /// leave the editor with nothing to look at.
    /// </para>
    /// </summary>
    public bool CanCollapse(string groupId) => Within(Root, groupId);

    /// <summary>Changes how two neighbours in a split share their space.</summary>
    public DockLayout Resize(string splitId, int index, double fraction) =>
        Locked ? this : CopyWith(root: ResizeIn(Root, splitId, index, fraction));

    public string ToText() => JsonText.Encode(
        new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = (long)FormatVersion,
            ["locked"] = Locked,
            ["revision"] = (long)Revision,
            ["root"] = Root.ToJson(),
        },
        indent: "  ") + "\n";

    private static DockSplit ViewsRow(string row, params string[] pair) => new(
        row,
        DockAxis.Horizontal,
        [
            .. pair.Select(one => new DockGroup(
                $"group_{one}",
                [new DockPanel(one, PanelKind.Viewport, one == "scene" ? "Scene" : $"Scene {one[5..]}")])),
        ],
        [0.5, 0.5]);

    private static void Walk(DockNode node, List<DockPanel> into)
    {
        if (node is DockGroup group)
        {
            into.AddRange(group.Panels);
        }
        else if (node is DockSplit split)
        {
            foreach (var child in split.Children)
            {
                Walk(child, into);
            }
        }
    }

    private static int PanelIndex(DockGroup group, string panelId)
    {
        for (var i = 0; i < group.Panels.Count; i++)
        {
            if (group.Panels[i].Id == panelId)
            {
                return i;
            }
        }

        return -1;
    }

    private static int GroupIndex(DockSplit split, string groupId)
    {
        for (var i = 0; i < split.Children.Count; i++)
        {
            if (split.Children[i] is DockGroup group && group.Id == groupId)
            {
                return i;
            }
        }

        return -1;
    }

    private static DockNode ShowIn(DockNode node, string panelId)
    {
        if (node is DockGroup group)
        {
            // Unfolded too: asking to see a panel is asking to see it, not its tab.
            var at = PanelIndex(group, panelId);
            return at < 0 ? node : group.CopyWith(active: at, collapsed: false);
        }

        return node is DockSplit split
            ? split.CopyWith(children: [.. split.Children.Select(child => ShowIn(child, panelId))])
            : node;
    }

    private static DockNode IntoGroup(DockNode node, string groupId, DockPanel panel)
    {
        if (node is DockGroup group && group.Id == groupId)
        {
            return group.CopyWith(panels: [.. group.Panels, panel], active: group.Panels.Count, collapsed: false);
        }

        return node is DockSplit split
            ? split.CopyWith(children: [.. split.Children.Select(child => IntoGroup(child, groupId, panel))])
            : node;
    }

    private static DockNode Remove(DockNode node, string panelId)
    {
        if (node is DockGroup group)
        {
            var kept = group.Panels.Where(panel => panel.Id != panelId).ToList();
            return kept.Count == group.Panels.Count
                ? node

                // Stays on the one before, which is where the eye already is.
                : group.CopyWith(panels: kept, active: Math.Clamp(group.Showing, 0, kept.Count == 0 ? 0 : kept.Count - 1));
        }

        return node is DockSplit split
            ? split.CopyWith(children: [.. split.Children.Select(child => Remove(child, panelId))])
            : node;
    }

    // Drops empty groups and splits that hold one thing.
    private static DockNode? Prune(DockNode node)
    {
        if (node is DockGroup group)
        {
            return group.Panels.Count == 0 ? null : group;
        }

        if (node is not DockSplit split)
        {
            return node;
        }

        var kept = new List<DockNode>();
        var weights = new List<double>();
        var shares = split.Shares;
        for (var i = 0; i < split.Children.Count; i++)
        {
            if (Prune(split.Children[i]) is not { } child)
            {
                continue;
            }

            kept.Add(child);
            weights.Add(shares[i]);
        }

        // A split of one is a level of nothing, and left in place it would collect more of them every
        // time a panel was closed.
        return kept.Count switch
        {
            0 => null,
            1 => kept[0],
            _ => split.CopyWith(children: kept, weights: weights),
        };
    }

    private static DockNode DockInto(DockNode node, string groupId, DockPanel panel, DockSide side)
    {
        if (node is DockGroup group && group.Id == groupId)
        {
            if (side == DockSide.Centre)
            {
                return group.CopyWith(panels: [.. group.Panels, panel], active: group.Panels.Count, collapsed: false);
            }

            var fresh = new DockGroup(FreshId("group"), [panel]);
            var before = side is DockSide.Left or DockSide.Top;
            return new DockSplit(
                FreshId("split"),
                side is DockSide.Left or DockSide.Right ? DockAxis.Horizontal : DockAxis.Vertical,
                before ? [fresh, group] : [group, fresh],

                // A new panel takes a third, which is enough to be usable and little enough that what
                // was there is still the thing being worked in.
                before ? [0.34, 0.66] : [0.66, 0.34]);
        }

        return node is DockSplit split
            ? split.CopyWith(children: [.. split.Children.Select(child => DockInto(child, groupId, panel, side))])
            : node;
    }

    private static DockNode CollapseIn(DockNode node, string groupId, bool collapsed)
    {
        if (node is DockGroup group && group.Id == groupId)
        {
            return group.CopyWith(collapsed: collapsed);
        }

        return node is DockSplit split
            ? split.CopyWith(children: [.. split.Children.Select(child => CollapseIn(child, groupId, collapsed))])
            : node;
    }

    private static bool Within(DockNode node, string groupId)
    {
        if (node is not DockSplit split)
        {
            return false;
        }

        var at = GroupIndex(split, groupId);
        if (at < 0)
        {
            return split.Children.Any(child => Within(child, groupId));
        }

        if (split.Axis != DockAxis.Vertical || at == 0)
        {
            return false;
        }

        var folded = split.Folded;
        return Enumerable.Range(0, split.Children.Count).Any(i => i != at && !folded[i]);
    }

    private static DockNode ResizeIn(DockNode node, string splitId, int index, double fraction)
    {
        if (node is not DockSplit split)
        {
            return node;
        }

        if (split.Id != splitId)
        {
            return split.CopyWith(children: [.. split.Children.Select(child => ResizeIn(child, splitId, index, fraction))]);
        }

        if (index < 0 || index + 1 >= split.Children.Count)
        {
            return node;
        }

        // Only the two either side of the handle move. Everything else keeps what it had, which is
        // what dragging one divider means.
        var shares = split.Shares.ToArray();
        var pair = shares[index] + shares[index + 1];
        var wanted = Math.Clamp(fraction, 0.08, 0.92);
        shares[index] = pair * wanted;
        shares[index + 1] = pair * (1 - wanted);
        return split.CopyWith(weights: shares);
    }

    private static DockGroup? FirstGroup(DockNode node)
    {
        if (node is DockGroup group)
        {
            return group;
        }

        return node is DockSplit split ? split.Children.Select(FirstGroup).FirstOrDefault(found => found is not null) : null;
    }

    private static DockGroup? GroupHolding(DockNode node, string panelId)
    {
        if (node is DockGroup group)
        {
            return PanelIndex(group, panelId) >= 0 ? group : null;
        }

        return node is DockSplit split
            ? split.Children.Select(child => GroupHolding(child, panelId)).FirstOrDefault(found => found is not null)
            : null;
    }

    private static bool HasGroup(DockNode node, string groupId)
    {
        if (node is DockGroup group)
        {
            return group.Id == groupId;
        }

        return node is DockSplit split && split.Children.Any(child => HasGroup(child, groupId));
    }

    // F10: the time only keeps the ids of this run apart from those a layout saved in an earlier one holds.
    private static string FreshId(string prefix) =>
        $"{prefix}{(DateTime.UtcNow - DateTime.UnixEpoch).Ticks / 10}_{Interlocked.Increment(ref _next) - 1}";
}

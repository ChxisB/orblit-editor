namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class DockTests
{
    private static DockGroup? FindGroup(DockNode at, string groupId)
    {
        if (at is DockGroup group)
        {
            return group.Id == groupId ? group : null;
        }

        return at is DockSplit split
            ? split.Children.Select(child => FindGroup(child, groupId)).FirstOrDefault(found => found is not null)
            : null;
    }

    // The ids of the panels in a group, for reading assertions easily.
    private static string[] PanelsIn(DockNode node, string groupId) =>
        [.. (FindGroup(node, groupId)?.Panels ?? []).Select(panel => panel.Id)];

    // Every built-in panel in one arrangement, the way the Scene mode's looked before each job had a
    // mode of its own. Busy on purpose, so the dock's rules are tested against groups with several
    // panels in them.
    private static DockLayout Crowded() => DockLayout.Columns(
        new DockGroup(
            "centre",
            [new DockPanel("scene", PanelKind.Viewport), new DockPanel("game", PanelKind.Game)]),
        bottom:
        [
            new DockPanel("console", PanelKind.Console),
            new DockPanel("uvs", PanelKind.Uvs),
            new DockPanel("timeline", PanelKind.Timeline),
        ],
        right: [new DockPanel("inspector", PanelKind.Inspector), new DockPanel("modelling", PanelKind.Modelling)]);

    private static DockSplit MiddleOf(DockLayout layout) => (DockSplit)((DockSplit)layout.Root).Children[1];

    private static int SplitsIn(DockNode node) =>
        node is DockSplit split ? 1 + split.Children.Sum(SplitsIn) : 0;

    private static DockSplit FindSplit(DockNode node, string id)
    {
        if (node is DockSplit split)
        {
            if (split.Id == id)
            {
                return split;
            }

            foreach (var child in split.Children)
            {
                if (child is DockSplit)
                {
                    try
                    {
                        return FindSplit(child, id);
                    }
                    catch (InvalidOperationException)
                    {
                        continue;
                    }
                }
            }
        }

        throw new InvalidOperationException($"no {id}");
    }

    private static DockLayout Reread(string text) =>
        DockLayout.Read(text) ?? throw new InvalidOperationException("not a layout");

    [Fact]
    public void TheStandardArrangementHoldsThePanelsTheEditorOpensWith()
    {
        var layout = DockLayout.Standard();
        var ids = layout.Panels.Select(panel => panel.Id).ToList();

        Assert.Contains("outliner", ids);
        Assert.Contains("scene", ids);
        Assert.Contains("inspector", ids);
        Assert.Contains("project", ids);
        Assert.True(layout.Holds("console"));
    }

    [Fact]
    public void TheStandardArrangementTheSceneAndTheGameShareOneSpace()
    {
        Assert.Equal(["scene", "game"], PanelsIn(DockLayout.Standard().Root, "centre"));
    }

    [Fact]
    public void TheStandardArrangementTheSceneKeepsToTheViewTheInspectorAndTheConsole()
    {
        var layout = DockLayout.Standard();

        Assert.Equal(["inspector"], PanelsIn(layout.Root, "right"));
        Assert.Equal(["outliner"], PanelsIn(layout.Root, "left"));
        Assert.Equal(["project", "console"], PanelsIn(layout.Root, "bottom"));
        Assert.Equal(2, layout.Revision);
    }

    [Fact]
    public void TheStandardArrangementTheProjectAndTheConsoleAreOpenUnderTheView()
    {
        Assert.Equal(new[] { false, false }, MiddleOf(DockLayout.Standard()).Folded);
    }

    [Fact]
    public void TheStandardArrangementModellingPutsItsToolsFirstAndOpensTheUvs()
    {
        var layout = DockLayout.Modelling();

        Assert.Equal(["modelling", "inspector"], PanelsIn(layout.Root, "right"));
        Assert.Equal(["uvs"], PanelsIn(layout.Root, "bottom"));
        Assert.Equal(new[] { false, false }, MiddleOf(layout).Folded);
        Assert.False(layout.Holds("game"));
    }

    [Fact]
    public void TheStandardArrangementAnimationOpensTheTimelineUnderTheViewAndTall()
    {
        var layout = DockLayout.Animation();

        Assert.Equal(["timeline"], PanelsIn(layout.Root, "bottom"));
        Assert.Equal(new[] { false, false }, MiddleOf(layout).Folded);
        Assert.Equal(0.4, MiddleOf(layout).Shares[^1], 1e-9);
    }

    [Fact]
    public void TheStandardArrangementEveryModeLooksAtTheSameSceneView()
    {
        // One id, so one camera: switching mode never moves the view.
        foreach (var layout in new[] { DockLayout.Standard(), DockLayout.Modelling(), DockLayout.Animation() })
        {
            Assert.True(layout.Holds("scene"));
        }
    }

    [Fact]
    public void TheStandardArrangementFourViewsIsFourScenePanels()
    {
        var layout = DockLayout.FourViews();
        var viewports = layout.Panels.Where(panel => panel.Kind.Equals(PanelKind.Viewport)).ToList();

        Assert.Equal(4, viewports.Count);

        // Ids of their own, or the second would be looking wherever the first was: a camera is filed
        // under the panel it belongs to.
        Assert.Equal(4, viewports.Select(one => one.Id).Distinct().Count());
    }

    [Fact]
    public void DockingOntoASideMakesASplit()
    {
        var was = Crowded();

        // The console, because the group it leaves still has panels in it, so nothing collapses to
        // cancel out the split being made.
        var now = was.Dock("console", "right", DockSide.Bottom);

        Assert.True(SplitsIn(now.Root) > SplitsIn(was.Root));
        Assert.True(now.Holds("console"));
        Assert.Equal(["uvs", "timeline"], PanelsIn(now.Root, "bottom"));
    }

    [Fact]
    public void DockingOntoTheCentreMakesItAnotherTab()
    {
        var now = Crowded().Dock("inspector", "left", DockSide.Centre);

        Assert.Equal(["outliner", "inspector"], PanelsIn(now.Root, "left"));

        // And the one just dropped is the one showing.
        Assert.Equal(SplitsIn(Crowded().Root), SplitsIn(now.Root));
    }

    [Fact]
    public void DockingAGroupLeftEmptyCollapsesAndSoDoesTheSplitAroundIt()
    {
        var was = Crowded();

        // The outliner is the only panel in its group.
        var now = was.Dock("outliner", "right", DockSide.Centre);

        Assert.Empty(PanelsIn(now.Root, "left"));

        // The left column held the outliner alone, so with it gone the row is the middle and the
        // right rather than three with one of them empty.
        var root = (DockSplit)now.Root;
        Assert.Equal(2, root.Children.Count);
        Assert.Equal("middle", Assert.IsType<DockSplit>(root.Children[0]).Id);
    }

    [Fact]
    public void DockingASplitLeftWithOneChildIsReplacedByThatChild()
    {
        var layout = Crowded();
        foreach (var panel in new[] { "inspector", "modelling", "scene", "game" })
        {
            layout = layout.Dock(panel, "left", DockSide.Centre);
        }

        // The view and the inspector's column are empty, so the row is down to two columns, and the
        // middle one, the bottom panel with no view over it, is that group rather than a column of one.
        var root = (DockSplit)layout.Root;
        Assert.Equal(2, root.Children.Count);
        Assert.Equal("bottom", Assert.IsType<DockGroup>(root.Children[^1]).Id);
    }

    [Fact]
    public void DockingDroppingAPanelOnItsOwnGroupDoesNothing()
    {
        var was = Crowded();
        var now = was.Dock("scene", "centre", DockSide.Centre);

        Assert.Equal(["scene", "game"], PanelsIn(now.Root, "centre"));
        Assert.Equal(SplitsIn(was.Root), SplitsIn(now.Root));
    }

    [Fact]
    public void DockingDroppingTheOnlyPanelOfAGroupOntoItselfKeepsIt()
    {
        var was = Crowded();
        var now = was.Dock("outliner", "left", DockSide.Right);

        Assert.True(now.Holds("outliner"));
        Assert.Equal(was.Panels.Count, now.Panels.Count);
    }

    [Fact]
    public void DockingALockedLayoutRefusesToBeRearranged()
    {
        var locked = Crowded().CopyWith(locked: true);
        var after = locked.Dock("inspector", "left", DockSide.Bottom);

        Assert.Equal(["inspector", "modelling"], PanelsIn(after.Root, "right"));
        Assert.Same(locked.Root, after.Root);
    }

    [Fact]
    public void DockingNothingIsLostWhateverIsDraggedWhere()
    {
        var layout = Crowded();
        var before = layout.Panels.Count;

        foreach (var side in Enum.GetValues<DockSide>())
        {
            foreach (var panel in new[] { "outliner", "inspector", "console", "scene" })
            {
                layout = layout.Dock(panel, "centre", side);
                Assert.True(layout.Panels.Count == before, $"{panel} to {side}: {layout.Panels.Count} of {before}");
            }
        }
    }

    [Fact]
    public void FoldingThePanelsUnderTheViewStartFolded()
    {
        var bottom = (DockSplit)((DockSplit)Crowded().Root).Children[1];

        Assert.Equal(new[] { false, true }, bottom.Folded);
    }

    [Fact]
    public void FoldingAGroupUnderAnotherFoldsAndOpensAgain()
    {
        var open = Crowded().Collapse("bottom", collapsed: false);
        Assert.True(open.CanCollapse("bottom"));

        var folded = open.Collapse("bottom");
        Assert.Equal(new[] { false, true }, MiddleOf(folded).Folded);

        // Folding is not closing: every panel is still there.
        Assert.Equal(open.Panels.Count, folded.Panels.Count);
    }

    [Fact]
    public void FoldingTheGroupAtTheTopOfAColumnDoesNotFold()
    {
        var layout = Crowded();

        Assert.False(layout.CanCollapse("centre"));
        Assert.False(layout.CanCollapse("left"));
        Assert.Same(layout.Root, layout.Collapse("centre").Root);
    }

    [Fact]
    public void FoldingAGroupBesideOthersRatherThanUnderOneDoesNotFold()
    {
        Assert.False(Crowded().CanCollapse("right"));
    }

    [Fact]
    public void FoldingShowingAPanelInAFoldedGroupOpensIt()
    {
        var middle = MiddleOf(Crowded().Show("timeline"));

        Assert.Equal(new[] { false, false }, middle.Folded);
        Assert.Equal("timeline", ((DockGroup)middle.Children[1]).Current?.Id);
    }

    [Fact]
    public void FoldingAColumnWhereEverythingAsksToFoldShowsEverything()
    {
        var split = new DockSplit(
            "s",
            DockAxis.Vertical,
            [new DockGroup("a", [], collapsed: true), new DockGroup("b", [], collapsed: true)],
            [0.5, 0.5]);

        Assert.Equal(new[] { false, false }, split.Folded);
    }

    [Fact]
    public void FoldingItFoldsWhileTheLayoutIsLockedAsChoosingATabDoes()
    {
        var locked = Crowded().Collapse("bottom", collapsed: false).CopyWith(locked: true);

        Assert.Equal(new[] { false, true }, MiddleOf(locked.Collapse("bottom")).Folded);
    }

    [Fact]
    public void FoldingItIsKeptInTheFile()
    {
        var now = Reread(Crowded().ToText());

        Assert.Equal(new[] { false, true }, MiddleOf(now).Folded);
        var open = Reread(Crowded().Collapse("bottom", collapsed: false).ToText());
        Assert.Equal(new[] { false, false }, MiddleOf(open).Folded);
    }

    [Fact]
    public void ClosingTakesThePanelOutAndCollapsesWhatItLeaves()
    {
        // The outliner, which is the panel with a group to itself: closing it leaves the left column
        // empty, for the row to close up around.
        var now = Crowded().Close("outliner");

        Assert.False(now.Holds("outliner"));
        var root = (DockSplit)now.Root;
        Assert.Equal(2, root.Children.Count);
        Assert.IsType<DockSplit>(root.Children[0]);
    }

    [Fact]
    public void ClosingATabBesideOthersLeavesTheOthersAlone()
    {
        Assert.Equal(["scene"], PanelsIn(Crowded().Close("game").Root, "centre"));
    }

    [Fact]
    public void ClosingEverythingLeavesSomethingToOpenInto()
    {
        var layout = Crowded();
        foreach (var panel in layout.Panels.ToList())
        {
            layout = layout.Close(panel.Id);
        }

        Assert.NotEmpty(layout.Panels);
    }

    [Fact]
    public void OpeningAPanelAlreadyThereIsShownRatherThanAddedTwice()
    {
        var now = Crowded().Add(new DockPanel("console", PanelKind.Console));

        Assert.Single(now.Panels, one => one.Id == "console");
    }

    [Fact]
    public void OpeningOneThatIsNotThereIsAdded()
    {
        var now = Crowded().Close("console").Add(new DockPanel("console", PanelKind.Console));

        Assert.True(now.Holds("console"));
    }

    [Fact]
    public void SharingTheSpaceWeightsAreReadAsFractionsThatAddUp()
    {
        var split = new DockSplit(
            "s",
            DockAxis.Horizontal,
            [new DockGroup("a", []), new DockGroup("b", [])],
            [3, 1]);

        Assert.Equal(new[] { 0.75, 0.25 }, split.Shares);
    }

    [Fact]
    public void SharingTheSpaceWeightsThatDoNotMatchTheChildrenAreEvenedOut()
    {
        var split = new DockSplit(
            "s",
            DockAxis.Horizontal,
            [new DockGroup("a", []), new DockGroup("b", [])],
            [1]);

        // A file from an older build is allowed to be wrong; refusing to open is worse than evening
        // it out.
        Assert.Equal(new[] { 0.5, 0.5 }, split.Shares);
    }

    [Fact]
    public void SharingTheSpaceDraggingADividerOnlyMovesTheTwoEitherSideOfIt()
    {
        var was = DockLayout.FourViews();
        var now = was.Resize("viewsTop", 0, 0.7);

        Assert.Equal(0.7, FindSplit(now.Root, "viewsTop").Shares[0], 1e-9);

        // The split above them is untouched.
        Assert.Equal(FindSplit(was.Root, "views").Shares, FindSplit(now.Root, "views").Shares);
    }

    [Fact]
    public void SharingTheSpaceADividerCannotBeDraggedOffTheEdge()
    {
        var now = DockLayout.FourViews().Resize("viewsTop", 0, 5);

        Assert.Equal(DockLayout.FourViews().Panels.Count, now.Panels.Count);
    }

    [Fact]
    public void SharingTheSpaceALockedLayoutRefusesToBeResized()
    {
        var locked = DockLayout.FourViews().CopyWith(locked: true);

        Assert.Same(locked.Root, locked.Resize("viewsTop", 0, 0.9).Root);
    }

    [Fact]
    public void TheFileSurvivesARoundTrip()
    {
        var was = DockLayout.FourViews().CopyWith(locked: true);
        var now = Reread(was.ToText());

        Assert.True(now.Locked);
        Assert.Equal(was.Panels.Select(one => one.Id), now.Panels.Select(one => one.Id));
        Assert.Equal(SplitsIn(was.Root), SplitsIn(now.Root));
    }

    [Fact]
    public void TheFileTheTitlesComeBackSoFourViewsStayToldApart()
    {
        var now = Reread(DockLayout.FourViews().ToText());
        var titles = now.Panels.Select(panel => panel.Label).ToList();

        Assert.Contains("Scene", titles);
        Assert.Contains("Scene 2", titles);
        Assert.Contains("Scene 4", titles);
    }

    [Fact]
    public void TheFileKeepsTheRevisionItGrewFrom()
    {
        Assert.Equal(2, Reread(DockLayout.Standard().ToText()).Revision);
    }

    [Fact]
    public void TheFileOneSavedBeforeThereWereRevisionsReadsAsTheFirst()
    {
        var text = Crowded().ToText().Replace("\"revision\": 0,", "", StringComparison.Ordinal);

        Assert.DoesNotContain("revision", text, StringComparison.Ordinal);
        Assert.Equal(0, Reread(text).Revision);
    }

    [Fact]
    public void TheFileIsNotReadFromSomethingThatIsNotOne()
    {
        Assert.Null(DockLayout.Read("nonsense"));
        Assert.Null(DockLayout.Read("{\"kind\":\"orblit.ui\"}"));
        Assert.Null(DockLayout.Read("{\"kind\":\"orblit.layout\"}"));
    }

    [Fact]
    public void TheFileAPanelKindItDoesNotKnowIsLeftOutRatherThanGuessed()
    {
        var layout = Reread(
            """
            {"kind":"orblit.layout","root":{"id":"g","panels":[
              {"id":"a","kind":"outliner"},
              {"id":"b","kind":"holodeck"}
            ]}}
            """);

        Assert.Equal(["a"], layout.Panels.Select(one => one.Id));
    }
}

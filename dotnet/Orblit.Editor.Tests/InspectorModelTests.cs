namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Scene;

public sealed class InspectorModelTests
{
    internal static InspectorTarget TargetFor(EditorScene scene, string id) => new()
    {
        SceneId = "main",
        Scene = scene,
        Item = scene[id]!,
        History = new History(new NoHost()),
    };

    [Theory]
    [InlineData(0.25, "0.25 lx")]
    [InlineData(9.99, "9.99 lx")]
    [InlineData(10, "10 lx")]
    [InlineData(10.5, "11 lx")]
    [InlineData(28000, "28000 lx")]
    public void ALightLevelKeepsTwoDecimalsBelowTenAndIsWholeFromThere(double lux, string shown) =>
        Assert.Equal(shown, InspectorModel.Lux(lux));

    [Theory]
    [InlineData(10, "10:00")]
    [InlineData(13.5, "13:30")]
    [InlineData(0, "00:00")]
    [InlineData(24, "00:00")]
    [InlineData(-1, "23:00")]
    [InlineData(23.999, "00:00")]
    [InlineData(6.25, "06:15")]
    public void AnHourReadsAsAClockDoes(double hour, string shown) =>
        Assert.Equal(shown, InspectorModel.Clock(hour));

    [Fact]
    public void DraggingMovesOneNumberByThePixelsTimesTheStepAndLeavesTheOthers()
    {
        var next = InspectorModel.Dragged([1, 2, 3], 1, 10, 0.02, null);

        Assert.Equal(1, next[0]);
        Assert.Equal(2.2, next[1], 9);
        Assert.Equal(3, next[2]);
    }

    [Fact]
    public void DraggingStopsAtTheMinimumSoAScaleNeverReachesZero()
    {
        var next = InspectorModel.Dragged([0.002, 1, 1], 0, -1, 0.02, 0.001);

        Assert.Equal(0.001, next[0]);
    }

    [Fact]
    public void DraggingDoesNotChangeWhatItWasGiven()
    {
        double[] was = [1, 2, 3];

        InspectorModel.Dragged(was, 0, 5, 1, null);

        Assert.Equal([1, 2, 3], was);
    }

    [Fact]
    public void ThePrefabBandSaysAPrefabThatCannotBeReadIsOnlyALink() =>
        Assert.Equal(
            "Lamp.oprefab could not be read. Until it can, this is only a link.",
            InspectorModel.PrefabNote("prefabs/Lamp.oprefab", readable: false, partOf: null));

    [Fact]
    public void ThePrefabBandNamesTheInstanceAPartBelongsTo() =>
        Assert.Equal(
            "Part of Lamp 1. Changes here are changes to that instance.",
            InspectorModel.PrefabNote("prefabs/Lamp.oprefab", readable: true, partOf: "Lamp 1"));

    [Fact]
    public void ThePrefabBandHasNothingToSayAboutTheInstanceItself() =>
        Assert.Null(InspectorModel.PrefabNote("prefabs/Lamp.oprefab", readable: true, partOf: null));

    [Fact]
    public void AnInstanceIsTheInstanceHoldingItAndAPartIsTheOneItIsInside()
    {
        var scene = new EditorScene(
        [
            new SceneObject("lamp", "Lamp", ObjectKind.Group, prefab: new PrefabComponent(asset: "prefabs/Lamp.oprefab")),
            new SceneObject("lamp/bulb", "Bulb", ObjectKind.Mesh, parentId: "lamp"),
            new SceneObject("crate", "Crate", ObjectKind.Mesh),
        ]);

        Assert.Same(scene["lamp"], InspectorModel.InstanceHolding(scene, scene["lamp"]!));
        Assert.Same(scene["lamp"], InspectorModel.InstanceHolding(scene, scene["lamp/bulb"]!));
        Assert.Null(InspectorModel.InstanceHolding(scene, scene["crate"]!));
    }

    [Fact]
    public void AnObjectIsHiddenAnywayWhenSomethingItIsInsideIsHidden()
    {
        var scene = EditorScene.Starter();
        scene["props"]!.Visible = false;

        Assert.True(TargetFor(scene, "crate").HiddenAbove);
        Assert.False(TargetFor(scene, "props").HiddenAbove);
        Assert.False(TargetFor(scene, "ground").HiddenAbove);
    }

    [Fact]
    public void FourLayerRowsShowAtLeastAndOneMoreThanTheLastNamed()
    {
        Assert.Equal(4, InspectorModel.LayerRows([], all: false));
        Assert.Equal(4, InspectorModel.LayerRows(["Fast"], all: false));
        Assert.Equal(7, InspectorModel.LayerRows(["", "", "", "", "", "Slow"], all: false));
        Assert.Equal(32, InspectorModel.LayerRows(Enumerable.Repeat("x", 32).ToArray(), all: false));
    }

    [Fact]
    public void EveryLayerRowShowsWhenAskedFor() =>
        Assert.Equal(32, InspectorModel.LayerRows([], all: true));

    [Fact]
    public void RenamingALayerPadsTheNamesBeforeItAndTrimsTheTextAroundIt() =>
        Assert.Equal(["", "", "Fast"], InspectorModel.RenamedLayers([], 2, "  Fast "));

    [Fact]
    public void RenamingTheLastNameToNothingDropsTheEmptyNamesHangingOffTheEnd() =>
        Assert.Equal(["A"], InspectorModel.RenamedLayers(["A", "B"], 1, ""));

    [Fact]
    public void OnlyTheSectionsThatApplyAreShownInTheOrderRegistered()
    {
        var scene = EditorScene.Starter();
        InspectorSection<string>[] sections =
        [
            new("transform", _ => true, _ => "T"),
            new("light", target => target.Item.Kind == ObjectKind.Light, _ => "L"),
            new("mesh", target => target.Item.Kind == ObjectKind.Mesh, _ => "M"),
        ];

        Assert.Equal(["T", "M"], InspectorModel.Shown(sections, TargetFor(scene, "crate")));
        Assert.Equal(["T", "L"], InspectorModel.Shown(sections, TargetFor(scene, "sun")));
    }

    // A history over nothing: the target only needs one to hold.
    private sealed class NoHost : ISceneHost
    {
        public EditorScene? SceneFor(string sceneId) => null;
    }
}

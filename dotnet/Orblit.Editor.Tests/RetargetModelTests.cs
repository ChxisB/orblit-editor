namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// What the retarget table says of a pair of skeletons, without a dialog. The test of a draft keeping what it
// was not told to change is not ported: a record's with does that.
public sealed class RetargetModelTests
{
    // Each bone the child of the one before and a metre above it.
    internal static RestSkeleton Chain(params string[] names) => new(
        names,
        [.. names.Select(static (_, index) => index - 1)],
        [.. names.Select(static (_, index) => Matrix4.TranslationValues(0, index == 0 ? 0 : 1, 0))]);

    private static readonly RestSkeleton Hero = Chain("hips", "spine", "head");
    private static readonly RestSkeleton Dwarf = Chain("hips", "spine", "neck", "tail");

    private static PairRow RowOf(PairTable table, string bone) => table.Rows.Single(row => row.Bone == bone);

    [Fact]
    public void HasARowForEveryBoneOfTheTargetInItsOrder()
    {
        var table = PairTable.Of(Hero, Dwarf, new BoneMap());

        Assert.Equal(Dwarf.Names, table.Rows.Select(row => row.Bone));
    }

    [Fact]
    public void PairsByNameAndLeavesTheRestWithoutADriver()
    {
        var table = PairTable.Of(Hero, Dwarf, new BoneMap());

        Assert.Equal("spine", RowOf(table, "spine").Driver);
        Assert.Equal(PairHow.Auto, RowOf(table, "spine").How);
        Assert.Null(RowOf(table, "neck").Driver);
        Assert.Equal(2, table.Driven);
    }

    [Fact]
    public void SaysWhatWasChosenByHand()
    {
        var table = PairTable.Of(Hero, Dwarf, new BoneMap().WithPair("neck", "head"));

        Assert.Equal("head", RowOf(table, "neck").Driver);
        Assert.Equal(PairHow.Hand, RowOf(table, "neck").How);
        Assert.Equal(3, table.Driven);
    }

    [Fact]
    public void LeavesABoneAloneWhenANameWouldHavePairedIt()
    {
        var table = PairTable.Of(Hero, Dwarf, new BoneMap().WithUnpaired("spine"));

        Assert.Null(RowOf(table, "spine").Driver);
        Assert.Equal(PairHow.None, RowOf(table, "spine").How);
    }

    [Fact]
    public void NamesWhatASkeletonLacksAndHandsBackAMapWithoutIt()
    {
        var map = new BoneMap(
            bones: new Dictionary<string, string> { ["neck"] = "head", ["wing"] = "head", ["tail"] = "horn" },
            unpaired: new HashSet<string> { "fin" });

        var table = PairTable.Of(Hero, Dwarf, map);

        Assert.Equal(["fin", "tail", "wing"], table.Stale);
        var kept = Assert.Single(table.Map.Bones);
        Assert.Equal(("neck", "head"), (kept.Key, kept.Value));
        Assert.Empty(table.Map.Unpaired);
        // The map given is not touched, so saving it keeps what was written.
        Assert.Equal(3, map.Bones.Count);
    }

    [Fact]
    public void IsWhatRetargetingDoesWithTheMapItHandsBack()
    {
        var clip = new ClipDocument(
            "nod",
            1,
            channels:
            [
                new ClipChannel<Quaternion>(
                    "",
                    "rotation",
                    ChannelKind.Rotation,
                    [
                        new Key<Quaternion>(0, Quaternion.Identity(), Hold.Linear),
                        new Key<Quaternion>(1, Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5), Hold.Linear),
                    ],
                    bone: "head"),
            ]);
        var table = PairTable.Of(Hero, Dwarf, new BoneMap().WithPair("neck", "head"));

        var made = table.Map.Apply(clip, Hero, Dwarf);

        Assert.Equal(["neck"], made.Clip.Channels.Select(channel => channel.Bone).Distinct());
    }

    [Fact]
    public void ARetargetedClipIsNamedForTheClipAndTheModelItMoves()
    {
        Assert.Equal("Walk (dwarf)", RetargetWords.NameFor("Walk", "models/dwarf.glb"));
    }

    [Fact]
    public void ReadsAModelWithNoFolderOrAWindowsSeparator()
    {
        Assert.Equal("Walk (dwarf)", RetargetWords.NameFor("Walk", "dwarf.gltf"));
        Assert.Equal("Walk (dwarf)", RetargetWords.NameFor("Walk", @"models\dwarf.glb"));
    }

    [Fact]
    public void AFileWhoseNameStartsWithADotKeepsItWhole()
    {
        Assert.Equal("Walk (.glb)", RetargetWords.NameFor("Walk", "models/.glb"));
    }
}

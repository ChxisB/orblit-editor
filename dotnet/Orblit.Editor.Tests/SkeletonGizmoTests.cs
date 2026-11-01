namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// The gizmo group. A model's bones drawn over the scene, posed by the open clip and ghosted either side of
// the playhead. The strokes are read as the gizmo hands them to the view. Reading a model's skeleton is in
// ModelSkeletonsTests.
public sealed class SkeletonGizmoTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_skeleton");
    private readonly EditorScene _scene;
    private readonly ClipBench _bench;
    private readonly ModelSkeletons _models;
    private readonly SkeletonLook _look = new() { Value = new SkeletonSettings(Bones: true) };

    public SkeletonGizmoTests()
    {
        File.WriteAllBytes(Path.Combine(_root.FullName, "man.glb"), SkinnedGlb.ChainGlb("hips", "spine", "head"));
        _scene = new EditorScene(
        [
            new SceneObject("man", "Man", ObjectKind.Mesh, meshAsset: "man.glb"),
            new SceneObject("rock", "Rock", ObjectKind.Mesh),
        ]);
        var workspace = new Workspace(_root.FullName);
        workspace.Add(new SceneEntry("a", "A", scene: _scene));
        _bench = new ClipBench(new History(workspace), () => _scene);
        _models = new ModelSkeletons(_root.FullName);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private GizmoType Gizmo() => SkeletonGizmo.Type(_bench, _models, _look);

    private IReadOnlyList<GizmoStroke> Drawn() => Gizmo().Overlay!(GizmoView.TargetOf(_scene, "man"));

    // Where the strokes in a tone at an alpha land on screen.
    private Box BoundsIn(GizmoTone tone, byte alpha = byte.MaxValue)
    {
        var stroke = Drawn().FirstOrDefault(one => one.Tone == tone && one.Alpha == alpha);
        Assert.True(stroke?.Lines.Bounds is not null, $"nothing was drawn in {tone} at {alpha}");
        return stroke!.Lines.Bounds!.Value;
    }

    // A quarter turn of the spine over one second, which lays the head out to the side of the hips by the
    // end of it.
    private void Bend()
    {
        _bench.OpenDocument(
            "/project/bend.oclip",
            new ClipDocument(
                "bend",
                1,
                rate: 10,
                channels:
                [
                    new ClipChannel<Quaternion>(
                        string.Empty,
                        "rotation",
                        ChannelKind.Rotation,
                        [
                            new Key<Quaternion>(0, Quaternion.Identity(), Hold.Linear),
                            new Key<Quaternion>(1, Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2)),
                        ],
                        bone: "spine"),
                ]));
        _bench.Owner = "man";
    }

    [Fact]
    public void ItIsThereForASelectionWithASkinnedModelAndOnlyThen()
    {
        Assert.True(Gizmo().AppliesTo(GizmoView.TargetOf(_scene, "man")));
        Assert.False(Gizmo().AppliesTo(GizmoView.TargetOf(_scene, "rock")));
        Assert.True(Gizmo().AppliesTo(GizmoView.TargetOf(_scene, "rock", "man")));
    }

    [Fact]
    public void ItDrawsNothingUntilBonesAreTurnedOn()
    {
        _look.Value = new SkeletonSettings();
        Assert.Empty(Drawn());

        _look.Value = new SkeletonSettings(Bones: true);
        BoundsIn(GizmoTone.AxisZ);
    }

    [Fact]
    public void ItDrawsTheRestPoseWithNoClipTallAndThin()
    {
        var rest = BoundsIn(GizmoTone.AxisZ);

        Assert.True(rest.Height > rest.Width);
    }

    [Fact]
    public void ItPosesTheBonesWhereTheClipHasThemAtThePlayhead()
    {
        Bend();
        var start = BoundsIn(GizmoTone.AxisZ);

        _bench.At = 1;
        var end = BoundsIn(GizmoTone.AxisZ);

        // At the end the head is out beside the hips, so it is wider and no taller than it stood.
        Assert.True(end.Width > start.Width * 2);
        Assert.True(end.Height < start.Height);
    }

    [Fact]
    public void ItLeavesTheClipOffAModelItIsNotPlayedOn()
    {
        var rest = BoundsIn(GizmoTone.AxisZ);

        Bend();
        _bench.Owner = "rock";
        _bench.At = 1;

        Assert.Equal(rest, BoundsIn(GizmoTone.AxisZ));
    }

    [Fact]
    public void ItDrawsGhostsEitherSideOfThePlayhead()
    {
        Bend();
        _bench.At = 0.5;
        _look.Value = new SkeletonSettings(Bones: true, Onion: true, Before: 1, After: 1, Stride: 2);

        var before = BoundsIn(GizmoTone.AxisX, 190);
        var after = BoundsIn(GizmoTone.AxisY, 190);
        var now = BoundsIn(GizmoTone.AxisZ);

        // Each is a different point on the way round.
        Assert.True(before.Width < now.Width);
        Assert.True(after.Width > now.Width);
    }

    [Fact]
    public void ItDrawsNoGhostsWithOnionSkinOff()
    {
        Bend();
        _bench.At = 0.5;

        var drawn = Drawn();

        Assert.Equal([GizmoTone.AxisZ], drawn.Select(stroke => stroke.Tone));
        Assert.Equal(byte.MaxValue, drawn[0].Alpha);
    }

    // Added in C#. A ghost is fainter the further it is from the playhead, counted on each side apart.
    [Fact]
    public void FartherGhostsAreFainter()
    {
        Bend();
        _bench.At = 0.5;
        _look.Value = new SkeletonSettings(Bones: true, Onion: true, Before: 2, After: 1, Stride: 1);

        Assert.Equal(
            [(GizmoTone.AxisX, 190), (GizmoTone.AxisX, 145), (GizmoTone.AxisY, 190), (GizmoTone.AxisZ, 255)],
            Drawn().Select(stroke => (stroke.Tone, (int)stroke.Alpha)));
    }

    // Added in C#. A model the clip does not reach would only ghost its rest pose under itself.
    [Fact]
    public void AModelTheClipDoesNotReachHasNoGhosts()
    {
        Bend();
        _bench.Owner = "rock";
        _bench.At = 0.5;
        _look.Value = new SkeletonSettings(Bones: true, Onion: true, Before: 1, After: 1, Stride: 2);

        Assert.Equal([GizmoTone.AxisZ], Drawn().Select(stroke => stroke.Tone));
    }

    // Added in C#. The bones are drawn where the model stands, not at the origin.
    [Fact]
    public void TheBonesMoveWithTheModel()
    {
        var moved = new EditorScene([new SceneObject("man", "Man", ObjectKind.Mesh, position: new Vector3(3, 0, 0), meshAsset: "man.glb")]);

        var drawn = Gizmo().Overlay!(GizmoView.TargetOf(moved, "man")).Single();

        Assert.NotEqual(BoundsIn(GizmoTone.AxisZ), drawn.Lines.Bounds);
    }

    // Added in C#. The timeline's Bones chip is the one the scene view reads.
    [Fact]
    public Task TheBonesChipDrawsTheSelectedModelInTheView() => Headless.Run(() =>
    {
        var starter = EditorScene.Starter();
        starter["cube"]!.MeshAsset = "man.glb";
        Directory.CreateDirectory(Path.Combine(_root.FullName, "scenes"));
        File.WriteAllText(Path.Combine(_root.FullName, "scenes/main.oscene"), SceneDocument.Encode(starter));
        File.WriteAllText(Path.Combine(_root.FullName, "still.oclip"), new ClipDocument("still", 1).Encode());
        var opened = ProjectWorkspace.Open(_root.FullName, null);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();
        session.OpenClip(Path.Combine(_root.FullName, "still.oclip"));
        session.SelectObject("cube");
        Headless.Render();
        var view = Headless.All<SceneViewport>(window).First();
        Assert.DoesNotContain(view.State.Overlays(), drawn => drawn.Name == "skeleton" && drawn.Strokes.Count > 0);

        Headless.Click(window, "timeline_bones");

        Assert.Contains(view.State.Overlays(), drawn => drawn.Name == "skeleton" && drawn.Strokes.Count > 0);
        window.Close();
    });
}

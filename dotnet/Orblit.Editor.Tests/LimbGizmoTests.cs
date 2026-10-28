namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

// Dart: orblit-editor/test/limb_gizmo_test.dart. The hands and feet of a posed model, drawn as handles over
// the view and dragged to key the limb where they are put. The rings are read as the strokes the gizmo
// hands the view, where the Dart read them back off the canvas.
public sealed class LimbGizmoTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_limb_gizmo");
    private readonly EditorScene _scene;
    private readonly History _history;
    private readonly ClipBench _bench;
    private readonly ModelSkeletons _models;
    private readonly SkeletonLook _look = new() { Value = new SkeletonSettings(Bones: true) };

    public LimbGizmoTests()
    {
        File.WriteAllBytes(Path.Combine(_root.FullName, "man.glb"), SkinnedGlb.PersonGlb());
        _scene = new EditorScene(
        [
            new SceneObject("man", "Man", ObjectKind.Mesh, meshAsset: "man.glb"),
            new SceneObject("rock", "Rock", ObjectKind.Mesh),
        ]);
        var workspace = new Workspace(_root.FullName);
        workspace.Add(new SceneEntry("a", "A", scene: _scene));
        _history = new History(workspace);
        _bench = new ClipBench(_history, () => _scene);
        _bench.OpenDocument("/project/pose.oclip", new ClipDocument("pose", 1, rate: 10));
        _bench.Owner = "man";
        _models = new ModelSkeletons(_root.FullName);
    }

    public void Dispose() => _root.Delete(recursive: true);

    private GizmoType Gizmo() => LimbGizmo.Type(_bench, _models, _look);

    private LimbHandle HandleOf(string name) =>
        LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), _bench, _models).First(handle => handle.Limb.Name == name);

    private static Offset ScreenOf(Vector3 world) => GizmoView.Projection.Project(world)!.Value;

    private bool Send(GizmoType type, ViewportPhase phase, Offset at) =>
        type.Input!(GizmoView.TargetOf(_scene, "man"), new ViewportGesture(phase, at));

    // Where the left hand stands in the clip at the playhead, in the model's space, which is the world
    // since the model is at the origin.
    private Vector3 LeftHand()
    {
        var limb = HandleOf("left arm").Limb;
        var skeleton = _models.OfAsset("man.glb")!;
        var origins = skeleton.PosedOrigins(_bench.Clip!.SampleAt(_bench.Frame).Bones.GetValueOrDefault(string.Empty) ?? new());
        return origins[skeleton.IndexOf(limb.Tip)!.Value];
    }

    [Fact]
    public void TheHandlesAreOneOnEachHandAndFootOfTheSelectedModel()
    {
        var handles = LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), _bench, _models);

        Assert.Equal(["left arm", "right arm", "left leg", "right leg"], handles.Select(handle => handle.Limb.Name));
        Assert.All(handles, handle => Assert.Equal("man", handle.Id));
    }

    [Fact]
    public void TheHandlesStandWhereTheBonesPutTheTipInTheWorld()
    {
        var left = HandleOf("left arm");

        Assert.True(left.Tip.X > 0.3);
        Assert.Equal(1.55, left.Tip.Y, 0.05);
    }

    [Fact]
    public void TheHandlesFollowThePoseTheClipGivesAtThePlayhead()
    {
        var before = HandleOf("left arm").Tip;

        _bench.Edit(
            "Turn spine",
            clip => clip.CopyWith(channels:
            [
                new ClipChannel<Quaternion>(
                    string.Empty,
                    "rotation",
                    ChannelKind.Rotation,
                    [new Key<Quaternion>(0, Quaternion.AxisAngle(new Vector3(0, 1, 0), 1.0))],
                    bone: "hips"),
            ]));

        Assert.True((HandleOf("left arm").Tip - before).Length > 0.1);
    }

    // Added in C#. A turn keyed later in the clip moves the handles only once the playhead is there.
    [Fact]
    public void TheHandlesReadThePoseAtThePlayheadAndNotTheStart()
    {
        var before = HandleOf("left arm").Tip;
        _bench.Edit(
            "Turn spine",
            clip => clip.CopyWith(channels:
            [
                new ClipChannel<Quaternion>(
                    string.Empty,
                    "rotation",
                    ChannelKind.Rotation,
                    [new Key<Quaternion>(0, Quaternion.Identity()), new Key<Quaternion>(1, Quaternion.AxisAngle(new Vector3(0, 1, 0), 1.0))],
                    bone: "hips"),
            ]));

        _bench.At = 1;

        Assert.True((HandleOf("left arm").Tip - before).Length > 0.1);
    }

    [Fact]
    public void TheHandlesCallTheModelWhatTheClipCallsIt()
    {
        Assert.Equal(string.Empty, HandleOf("left arm").Target);

        // Played on something else, the model is a part of the scene the clip names by id, as a field
        // keyed on it would be.
        _bench.Owner = "rock";

        Assert.Equal("man", HandleOf("left arm").Target);
    }

    [Fact]
    public void TheHandlesAreNotOfferedWhileTheClipPlays()
    {
        _bench.Playing = true;

        Assert.Empty(LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), _bench, _models));
    }

    [Fact]
    public void TheHandlesAreNotOfferedWithNoClipOpen()
    {
        _bench.Close("/project/pose.oclip");

        Assert.Empty(LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), _bench, _models));
    }

    [Fact]
    public void TheHandlesAreNotOfferedInACutsceneWhichCannotMoveBones()
    {
        var cutscene = new ClipBench(_history, () => _scene, PlayedOn.Scene);
        cutscene.OpenDocument("/project/intro.ocut", new CutsceneDocument(new ClipDocument("intro", 1, rate: 10)));

        Assert.Empty(LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), cutscene, _models));
    }

    [Fact]
    public void ItDrawsARingOnEachTipAndNothingWithBonesOff()
    {
        var drawn = Gizmo().Overlay!(GizmoView.TargetOf(_scene, "man"));
        var tips = LimbGizmo.Handles(GizmoView.TargetOf(_scene, "man"), _bench, _models).Select(handle => ScreenOf(handle.Tip)).ToList();

        Assert.Equal(4, drawn.Count);
        for (var i = 0; i < 4; i++)
        {
            var ring = drawn[i].Lines.Bounds!.Value;
            Assert.Equal(GizmoTone.Ember, drawn[i].Tone);
            Assert.Equal(14, ring.Width, 0.01);
            Assert.Equal(tips[i].Dx, (ring.Left + ring.Right) / 2, 0.01);
            Assert.Equal(tips[i].Dy, (ring.Top + ring.Bottom) / 2, 0.01);
        }

        _look.Value = new SkeletonSettings();

        Assert.Empty(Gizmo().Overlay!(GizmoView.TargetOf(_scene, "man")));
    }

    [Fact]
    public void ItAppliesToASelectionWithASkinnedModelAndOnlyThen()
    {
        Assert.True(Gizmo().AppliesTo(GizmoView.TargetOf(_scene, "man")));
        Assert.False(Gizmo().AppliesTo(GizmoView.TargetOf(_scene, "rock")));
    }

    [Fact]
    public void ItTakesADragThatStartsOnAHandAndNotOneBesideIt()
    {
        var type = Gizmo();
        var hand = ScreenOf(HandleOf("left arm").Tip);

        Assert.False(Send(type, ViewportPhase.DragStart, hand + new Offset(40, 0)));
        Assert.True(Send(type, ViewportPhase.DragStart, hand + new Offset(3, 2)));
        Send(type, ViewportPhase.DragCancel, hand);
    }

    [Fact]
    public void ItTakesNothingWhileBonesAreOff()
    {
        _look.Value = new SkeletonSettings();

        Assert.False(Send(Gizmo(), ViewportPhase.DragStart, ScreenOf(HandleOf("left arm").Tip)));
    }

    [Fact]
    public void ItTakesATapOnAHandleSoTheSelectionIsKept() =>
        Assert.True(Send(Gizmo(), ViewportPhase.Tap, ScreenOf(HandleOf("left arm").Tip)));

    [Fact]
    public void ItLeavesHoveringToTheStagesBehindIt() =>
        Assert.False(Send(Gizmo(), ViewportPhase.Hover, ScreenOf(HandleOf("left arm").Tip)));

    [Fact]
    public void ItDragsAHandToWhereThePointerGoesKeyedAtThePlayhead()
    {
        var type = Gizmo();
        var start = HandleOf("left arm").Tip;
        var to = ScreenOf(start + new Vector3(-0.15, 0.1, 0.1));
        // The pointer moves in the plane through the hand facing the camera, so that is where the hand
        // is brought to.
        var meets = GizmoView.Projection.RayThrough(to).Meets(start, GizmoView.Projection.Forward)!;

        _bench.At = 0.5;
        Assert.True(Send(type, ViewportPhase.DragStart, ScreenOf(start)));
        Send(type, ViewportPhase.DragUpdate, to);
        Send(type, ViewportPhase.DragEnd, to);

        Assert.False(Send(type, ViewportPhase.Hover, to));
        Assert.True((LeftHand() - meets).Length < 1e-3);
        var keyed = _bench.Clip!.Channels.Where(channel => channel.Bone is not null).ToList();
        Assert.Equal(2, keyed.Count);
        Assert.All(keyed, channel => Assert.Equal(0.5, channel.BoxedKeys.Single().At));
    }

    [Fact]
    public void ItKeepsAWholeDragAsOneStepAndUndoTakesItBack()
    {
        var type = Gizmo();
        var start = HandleOf("left arm").Tip;

        Assert.True(Send(type, ViewportPhase.DragStart, ScreenOf(start)));
        foreach (var step in new[] { 1, 2, 3 })
        {
            Send(type, ViewportPhase.DragUpdate, ScreenOf(start + new Vector3(-0.03 * step, 0.02 * step, 0)));
        }

        Send(type, ViewportPhase.DragEnd, ScreenOf(start));

        Assert.Equal(["Pose left arm"], _history.Labels);
        _history.Undo();
        Assert.Empty(_bench.Clip!.Channels);
        _history.Redo();
        Assert.Equal(2, _bench.Clip!.Channels.Count);
    }

    [Fact]
    public void ItDoesNotJumpWhenItIsGrabbedOffTheMiddleOfTheRing()
    {
        var type = Gizmo();
        var start = HandleOf("left arm").Tip;
        var grab = ScreenOf(start) + new Offset(6, -5);

        Send(type, ViewportPhase.DragStart, grab);
        Send(type, ViewportPhase.DragUpdate, grab);
        Send(type, ViewportPhase.DragEnd, grab);

        Assert.True((LeftHand() - start).Length < 1e-3);
    }

    [Fact]
    public void ItEndsADragThatIsCancelledKeepingWhatItKeyed()
    {
        var type = Gizmo();
        var start = HandleOf("left arm").Tip;

        Send(type, ViewportPhase.DragStart, ScreenOf(start));
        Send(type, ViewportPhase.DragUpdate, ScreenOf(start + new Vector3(-0.1, 0.1, 0)));
        Send(type, ViewportPhase.DragCancel, ScreenOf(start));
        Assert.Equal(["Pose left arm"], _history.Labels);

        // The next drag is a new step and not a continuation.
        Assert.True(Send(type, ViewportPhase.DragStart, ScreenOf(LeftHand())));
        Send(type, ViewportPhase.DragUpdate, ScreenOf(start));
        Send(type, ViewportPhase.DragEnd, ScreenOf(start));

        Assert.Equal(["Pose left arm", "Pose left arm"], _history.Labels);
    }

    // Added in C#. Of two handles under the pointer, the nearer is the one dragged, and a model away from
    // the origin has its hand put where the pointer is in the world.
    [Fact]
    public void TheNearerOfTwoHandlesIsTaken()
    {
        var scene = new EditorScene(
        [
            new SceneObject("man", "Man", ObjectKind.Mesh, meshAsset: "man.glb"),
            new SceneObject("twin", "Twin", ObjectKind.Mesh, position: new Vector3(0.02, 0, 0), meshAsset: "man.glb"),
        ]);
        var bench = new ClipBench(_history, () => scene);
        bench.OpenDocument("/project/pose.oclip", new ClipDocument("pose", 1, rate: 10));
        bench.Owner = "man";
        var target = GizmoView.TargetOf(scene, "man", "twin");
        var type = LimbGizmo.Type(bench, _models, _look);
        var twin = LimbGizmo.Handles(target, bench, _models).First(handle => handle.Id == "twin" && handle.Limb.Name == "left arm");

        var to = ScreenOf(twin.Tip + new Vector3(0, 0.1, 0));
        var meets = GizmoView.Projection.RayThrough(to).Meets(twin.Tip, GizmoView.Projection.Forward)!;

        Assert.True(type.Input!(target, new ViewportGesture(ViewportPhase.DragStart, ScreenOf(twin.Tip))));
        type.Input!(target, new ViewportGesture(ViewportPhase.DragUpdate, to));

        Assert.All(bench.Clip!.Channels, channel => Assert.Equal("twin", channel.Target));
        var moved = LimbGizmo.Handles(target, bench, _models).First(handle => handle.Id == "twin" && handle.Limb.Name == "left arm");
        Assert.True((moved.Tip - meets).Length < 1e-3);
    }

    // Added in C#. The window's views draw the handles of the model the open clip is played on.
    [Fact]
    public Task TheViewDrawsTheHandlesOfTheSelectedModel() => Headless.Run(() =>
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
        session.Clips.Owner = "cube";
        session.SelectObject("cube");
        session.SkeletonLook.Value = new SkeletonSettings(Bones: true);
        Headless.Render();

        var view = Headless.All<SceneViewport>(window).First();

        Assert.Equal(4, view.State.Overlays().Single(drawn => drawn.Name == "limb").Strokes.Count);
        window.Close();
    });
}

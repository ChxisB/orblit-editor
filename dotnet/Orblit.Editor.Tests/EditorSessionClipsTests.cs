namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

/// <summary>A clip on the timeline: opened, given to an object, followed with the selection and saved.</summary>
public sealed class EditorSessionClipsTests : IDisposable
{
    private const string Made = "clips/clip.oclip";

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_clips");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => TestPaths.In(_project, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private static SceneObject Cube(EditorSession session) => session.Workspace.Loaded!.Scene!["cube"]!;

    private static ClipDocument Bounce() => new(
        "Bounce",
        1,
        WhenDone.Loop,
        channels:
        [
            new ClipChannel<Vector3>(
                "",
                "transform.position",
                ChannelKind.Vector,
                [new Key<Vector3>(0, Vector3.Zero()), new Key<Vector3>(1, new Vector3(0, 2, 0))]),
        ]);

    private static void MoveInto(SceneObject target, Doc.MotionComponent motion) =>
        target.Components[Doc.SceneComponents.Motion] = motion;

    [Fact]
    public void OpeningAClipShowsItOnTheTimelineInTheAnimationMode()
    {
        File.WriteAllText(PathOf("wave.oclip"), new ClipDocument("wave", 2).Encode());
        var session = Open();

        session.OpenClip(PathOf("wave.oclip"));

        Assert.Equal("animation", session.Mode.Name);
        Assert.Equal("wave", session.Clips.Clip!.Name);
        Assert.True(session.Layout.Holds("timeline"));
    }

    [Fact]
    public void AFileThatIsNotAClipIsSaidAndNotShown()
    {
        File.WriteAllText(PathOf("bad.oclip"), "not a clip");
        var session = Open();

        session.OpenClip(PathOf("bad.oclip"));

        Assert.Null(session.Clips.Shown);
        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("bad.oclip is not a clip: ", session.Last.Value.Text);
    }

    [Fact]
    public void AnEmptyTimelineMakesAClipAndShowsIt()
    {
        var session = Open();
        session.EnterMode("animation");

        session.NewClip();

        Assert.True(File.Exists(PathOf(Made)));
        Assert.Equal(PathOf(Made), session.Clips.Shown!.Path);
    }

    [Fact]
    public void AMadeClipIsShownInPlaceOfTheOneBefore()
    {
        File.WriteAllText(PathOf("wave.oclip"), new ClipDocument("wave", 2).Encode());
        var session = Open();
        session.OpenClip(PathOf("wave.oclip"));

        session.NewClip();

        Assert.Equal("clip", session.Clips.Clip!.Name);
        Assert.Equal(PathOf(Made), session.Clips.Shown!.Path);
    }

    [Fact]
    public void ANewClipBelongsToItsObjectAndSurvivesSaving()
    {
        var session = Open();
        session.Select(["cube"]);
        session.EnterMode("animation");

        session.NewClip();

        var motion = EditorSession.MotionOf(Cube(session))!;
        Assert.Equal([Made], motion.Clips);
        Assert.Equal(Made, motion.Autoplay);
        Assert.Equal("cube", session.Clips.Owner);

        session.Save();
        Assert.Contains(Made, File.ReadAllText(PathOf("scenes/main.oscene")));

        session.Undo();
        Assert.Null(EditorSession.MotionOf(Cube(session)));
        Assert.True(File.Exists(PathOf(Made)));
    }

    [Fact]
    public void AClipAlreadyAttachedIsNotAttachedAgain()
    {
        var session = Open();
        session.Select(["cube"]);
        session.NewClip();

        session.AttachClip(PathOf(Made));

        // One step, so one undo takes the animation off.
        session.Undo();
        Assert.Null(EditorSession.MotionOf(Cube(session)));
    }

    [Fact]
    public void SceneUnfoldsAndFoldsTheTimelineWithTheSelection()
    {
        var session = Open();
        session.Select(["cube"]);
        session.NewClip();
        session.EnterMode("scene");
        Assert.True(session.Layout.Holds("timeline"));

        session.Add(ObjectKind.Camera);
        Assert.True(session.Layout.GroupOf("timeline")!.Collapsed);

        session.Select(["cube"]);
        Assert.False(session.Layout.GroupOf("timeline")!.Collapsed);
        Assert.EndsWith(Made, session.Clips.Shown!.Path);
    }

    [Fact]
    public void AnimationReadsTheFirstAssignedClipAndReportsMissingFiles()
    {
        File.WriteAllText(PathOf("bounce.oclip"), new ClipDocument("Bounce", 2).Encode());
        var session = Open();
        MoveInto(Cube(session), new Doc.MotionComponent(["bounce.oclip"], null));
        session.Select(["cube"]);

        session.EnterMode("animation");
        Assert.Equal("Bounce", session.Clips.Clip!.Name);

        MoveInto(Cube(session), new Doc.MotionComponent(["missing.oclip"], null));
        session.EnterMode("scene");
        Assert.Null(session.Clips.Shown);
        Assert.Contains("could not be read", session.Last!.Value.Text);
    }

    [Fact]
    public void AKeyIsSavedWithTheScene()
    {
        File.WriteAllText(PathOf("wave.oclip"), new ClipDocument("wave", 2).Encode());
        var session = Open();
        session.OpenClip(PathOf("wave.oclip"));
        session.Select(["cube"]);

        session.Clips.Key(Cube(session), "transform.position");
        Assert.True(session.Clips.AnyUnsaved);
        session.Save();

        Assert.False(session.Clips.AnyUnsaved);
        Assert.Contains("transform.position", File.ReadAllText(PathOf("wave.oclip")));
    }

    [Fact]
    public void AClipWithChangesCountsAsUnsavedOnItsOwn()
    {
        File.WriteAllText(PathOf("wave.oclip"), new ClipDocument("wave", 2).Encode());
        var session = Open();
        session.Save();
        session.OpenClip(PathOf("wave.oclip"));
        Assert.False(session.AnyUnsaved);

        session.Clips.Edit("Lengthen", _ => new ClipDocument("wave", 3));
        Assert.True(session.AnyUnsaved);

        session.Save();
        Assert.False(session.AnyUnsaved);
    }

    [Fact]
    public void ASceneIsWrittenAsItRestsWhereverThePlayheadIs()
    {
        var session = Open();
        var rest = Cube(session).Position.Y;
        session.Clips.OpenDocument(PathOf("bounce.oclip"), Bounce());
        session.Clips.Owner = "cube";

        session.Clips.At = 0.5;
        Assert.Equal(1, Cube(session).Position.Y, 1e-9);
        session.Save();

        var written = SceneDocument.Decode(File.ReadAllText(PathOf("scenes/main.oscene"))).Scene;
        Assert.Equal(rest, written["cube"]!.Position.Y, 1e-9);
        Assert.Equal(1, Cube(session).Position.Y, 1e-9);
    }

    [Fact]
    public void AClipOpenedWithChangesTellsTheWindowOnlyWhenWhetherItHasChangesFlips()
    {
        var session = Open();
        session.Clips.OpenDocument(PathOf("bounce.oclip"), Bounce());
        session.Clips.Owner = "cube";
        var told = 0;
        session.Changed += () => told++;

        session.Clips.At = 0.25;
        session.Clips.At = 0.5;
        Assert.Equal(0, told);

        session.Clips.Edit("Shorten", _ => new ClipDocument("Bounce", 2));
        session.Clips.Edit("Lengthen", _ => new ClipDocument("Bounce", 3));
        Assert.Equal(1, told);
    }

    private static ClipDocument Moved(string name = "wave (dwarf)") => new(name, 2);

    [Fact]
    public void ARetargetedClipIsWrittenInClipsAndShownAndTheFirstIsLeftAlone()
    {
        File.WriteAllText(PathOf("wave.oclip"), new ClipDocument("wave", 2).Encode());
        var session = Open();
        session.OpenClip(PathOf("wave.oclip"));

        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        Assert.Equal(PathOf("clips/wave (dwarf).oclip"), session.Clips.Shown!.Path);
        Assert.Equal("wave (dwarf)", ClipDocument.Decode(File.ReadAllText(PathOf("clips/wave (dwarf).oclip"))).Clip.Name);
        Assert.Equal("wave", ClipDocument.Decode(File.ReadAllText(PathOf("wave.oclip"))).Clip.Name);
        Assert.Equal(("Made wave (dwarf).oclip.", SayLevel.Info), session.Last);
    }

    [Fact]
    public void ARetargetedClipIsNeverWrittenOverAnother()
    {
        var session = Open();
        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        Assert.Equal(2, Directory.GetFiles(PathOf("clips")).Length);
        Assert.NotEqual(PathOf("clips/wave (dwarf).oclip"), session.Clips.Shown!.Path);
    }

    [Fact]
    public void WhatCouldNotBeCarriedIsCountedAndTheFirstIsGiven()
    {
        var session = Open();

        session.SaveRetargeted(Moved(), "dwarf.glb", ["No arm.", "No leg."]);

        Assert.Equal(("Made wave (dwarf).oclip. 2 things could not be carried. First: No arm.", SayLevel.Warning), session.Last);
    }

    [Fact]
    public void ARetargetedClipGoesOnTheChosenObjectOnlyWhenItIsTheModelItMoves()
    {
        var session = Open();
        Cube(session).MeshAsset = "dwarf.glb";
        session.Select(["cube"]);

        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        Assert.Equal(["clips/wave (dwarf).oclip"], EditorSession.MotionOf(Cube(session))!.Clips);
        Assert.Equal("cube", session.Clips.Owner);
    }

    [Fact]
    public void ARetargetedClipIsNotGivenToAnObjectThatIsAnotherModel()
    {
        var session = Open();
        Cube(session).MeshAsset = "hero.glb";
        session.Select(["cube"]);

        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        Assert.Null(EditorSession.MotionOf(Cube(session)));
        Assert.Equal(PathOf("clips/wave (dwarf).oclip"), session.Clips.Shown!.Path);
    }

    [Fact]
    public void SaysSoWhenTheClipsFolderCannotBeMade()
    {
        File.WriteAllText(PathOf("clips"), "in the way");
        var session = Open();

        session.SaveRetargeted(Moved(), "dwarf.glb", []);

        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("Could not make the clips folder: ", session.Last.Value.Text);
        Assert.Null(session.Clips.Shown);
    }

    [Fact]
    public void SaysWhyARetargetedClipCouldNotBeWritten()
    {
        var session = Open();

        session.SaveRetargeted(Moved("no/such/folder"), "dwarf.glb", []);

        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("Could not save the clip: ", session.Last.Value.Text);
        Assert.Null(session.Clips.Shown);
    }

    [Fact]
    public void TheModelIsTheOwnersAndNotTheChosenObjectsWhenTheyDiffer()
    {
        var session = Open();
        Cube(session).MeshAsset = "hero.glb";
        session.Select(["cube"]);
        session.NewClip();

        session.Add(ObjectKind.Camera);
        session.Clips.Owner = "cube";

        Assert.Equal("hero.glb", session.OwnerModel);
    }

    [Fact]
    public void TheOwnerOfTheShownClipHasItsModelAndNoOwnerHasNone()
    {
        var session = Open();
        Cube(session).MeshAsset = "hero.glb";
        Assert.Null(session.OwnerModel);

        session.Select(["cube"]);
        session.NewClip();

        Assert.Equal("hero.glb", session.OwnerModel);
    }
}

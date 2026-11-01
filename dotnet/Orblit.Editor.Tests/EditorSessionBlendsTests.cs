namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Motion;

/// <summary>A blend in the graph: opened, made, offered the project's clips and saved with everything else.</summary>
public sealed class EditorSessionBlendsTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_blends");

    public void Dispose() => _project.Delete(recursive: true);

    private string PathOf(string name) => TestPaths.In(_project, name);

    private EditorSession Open()
    {
        Directory.CreateDirectory(PathOf("scenes"));
        File.WriteAllText(PathOf("scenes/main.oscene"), SceneDocument.Encode(EditorScene.Starter()));
        var opened = ProjectWorkspace.Open(_project.FullName, null);
        return new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
    }

    private string WriteMoves(params string[] reads)
    {
        Directory.CreateDirectory(PathOf("blends"));
        var path = PathOf("blends/moves.oblend");
        File.WriteAllText(
            path,
            new BlendDocument(
                "Moves",
                [new BlendState("idle", new BlendClip("idle")), new BlendState("move", new BlendClip("walk"))],
                reads.ToDictionary(static name => name, static _ => 0.0),
                [new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1))]).Encode());
        return path;
    }

    [Fact]
    public void OpeningABlendShowsItsGraphInTheAnimationMode()
    {
        var session = Open();

        session.OpenBlend(WriteMoves("speed"));

        Assert.Equal("animation", session.Mode.Name);
        Assert.Equal("Moves", session.Blends.Blend!.Name);
        Assert.True(session.Layout.Holds("blendGraph"));
        Assert.Equal(("Blend", "hub_outlined"), (PanelKind.BlendGraph.Label, PanelKind.BlendGraph.Icon));
        Assert.NotNull(session.Panels.TypeOf(PanelKind.BlendGraph));
    }

    [Fact]
    public void AFileThatIsNotABlendIsSaidAndNotShown()
    {
        Directory.CreateDirectory(PathOf("blends"));
        File.WriteAllText(PathOf("blends/bad.oblend"), "{\"kind\":\"orblit.clip\"}");
        var session = Open();

        session.OpenBlend(PathOf("blends/bad.oblend"));

        Assert.Null(session.Blends.Shown);
        Assert.False(session.Layout.Holds("blendGraph"));
        Assert.Equal(SayLevel.Error, session.Last!.Value.Level);
        Assert.StartsWith("bad.oblend is not a blend: ", session.Last.Value.Text);
    }

    [Fact]
    public void AFileThatIsNotThereIsSaidAndNotShown()
    {
        var session = Open();

        session.OpenBlend(PathOf("blends/gone.oblend"));

        Assert.Null(session.Blends.Shown);
        Assert.StartsWith("gone.oblend could not be read: ", session.Last!.Value.Text);
    }

    [Fact]
    public void WhatCouldNotBeReadInABlendIsSaidAsAWarning()
    {
        var session = Open();

        session.OpenBlend(WriteMoves());

        Assert.NotNull(session.Blends.Shown);
        Assert.Equal(SayLevel.Warning, session.Last!.Value.Level);
        Assert.Contains("speed", session.Last.Value.Text);
    }

    [Fact]
    public void SeveralThingsThatCouldNotBeReadAreCountedAndTheFirstIsGiven()
    {
        Directory.CreateDirectory(PathOf("blends"));
        File.WriteAllText(
            PathOf("blends/two.oblend"),
            new BlendDocument(
                "Two",
                [new BlendState("a", new BlendClip("a"))],
                changes:
                [
                    new BlendChange("a", when: BlendCondition.Above("speed", 1)),
                    new BlendChange("a", when: BlendCondition.On("jump")),
                ]).Encode());
        var session = Open();

        session.OpenBlend(PathOf("blends/two.oblend"));

        Assert.StartsWith("2 things in that blend could not be read. First: ", session.Last!.Value.Text);
    }

    [Fact]
    public void AnEmptyGraphMakesABlendAndShowsIt()
    {
        var session = Open();

        session.NewBlend();

        Assert.True(File.Exists(PathOf("blends/blend.oblend")));
        Assert.Equal(["idle"], session.Blends.Blend!.WrittenStates.Select(static state => state.Name));
        Assert.True(session.Layout.Holds("blendGraph"));
    }

    [Fact]
    public void AStateIsOfferedTheNamesOfTheProjectsClips()
    {
        Directory.CreateDirectory(PathOf("clips"));
        File.WriteAllText(PathOf("clips/walk.oclip"), "{}");
        File.WriteAllText(PathOf("clips/run.oclip"), "{}");
        File.WriteAllText(PathOf("clips/notes.txt"), "not a clip");
        var session = Open();

        Assert.Equal(["run", "walk"], session.ClipNames().Order());
    }

    [Fact]
    public void ABlendWithChangesMarksTheSessionUntilItIsSaved()
    {
        var session = Open();
        session.OpenBlend(WriteMoves("speed"));
        Assert.False(session.AnyUnsaved);

        session.Blends.Edit("Add state", blend => blend.WithStateAdded("jump"));
        Assert.True(session.AnyUnsaved);

        session.Save();
        Assert.False(session.AnyUnsaved);
        Assert.Equal(["idle", "move", "jump"], BlendDocument.Decode(File.ReadAllText(PathOf("blends/moves.oblend"))).Blend.WrittenStates.Select(static state => state.Name));
    }

    [Fact]
    public void TheSessionIsToldWhenAGraphSavesItselfAndNotWhenItIsEdited()
    {
        var session = Open();
        session.OpenBlend(WriteMoves("speed"));
        var told = 0;
        session.Changed += () => told++;

        session.Blends.Edit("Add state", blend => blend.WithStateAdded("jump"));
        Assert.Equal(0, told);

        Assert.Null(session.Blends.Save(session.Blends.Shown!));
        Assert.Equal(1, told);
    }

    [Fact]
    public void ABlendThatCannotBeWrittenIsSaid()
    {
        var session = Open();
        session.OpenBlend(WriteMoves("speed"));
        session.Blends.Edit("Add state", blend => blend.WithStateAdded("jump"));
        Directory.Delete(PathOf("blends"), recursive: true);
        var said = new List<(string Text, SayLevel Level)>();
        session.Said += (text, level) => said.Add((text, level));

        session.Save();

        var (text, level) = Assert.Single(said, static one => one.Level == SayLevel.Error);
        Assert.StartsWith("Moves could not be saved: ", text);
        Assert.True(session.AnyUnsaved);
    }
}

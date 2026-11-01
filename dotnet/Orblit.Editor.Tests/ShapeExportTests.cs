namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Mesh;

// These check what the session writes and says. ModellingPanelTests drives the prompt.
public sealed class ShapeExportTests
{
    private static string Exports(ModellingRig rig) => Path.Combine(rig.Session.Workspace.ProjectDirectory, "exports");

    private static List<(string Text, SayLevel Level)> Listen(ModellingRig rig)
    {
        var said = new List<(string, SayLevel)>();
        rig.Session.Said += (text, level) => said.Add((text, level));
        return said;
    }

    [Fact]
    public void TheFormatStartsAsObjAndIsChosenFromTheSession()
    {
        using var rig = new ModellingRig(cube: true);
        var changes = 0;
        rig.Session.Changed += () => changes++;
        Assert.Equal(MeshFormat.Obj, rig.Session.ExportFormat);

        rig.Session.SetExportFormat(MeshFormat.Stl);

        Assert.Equal(MeshFormat.Stl, rig.Session.ExportFormat);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void AnObjWritesTheModelAndSaysSo()
    {
        using var rig = new ModellingRig(cube: true);
        var said = Listen(rig);

        rig.Session.ExportShape("steps");

        var text = File.ReadAllText(Path.Combine(Exports(rig), "steps.obj"));
        Assert.Contains("o steps", text);
        Assert.Contains("\nf ", text);
        Assert.Equal([("Exported steps.obj to exports/.", SayLevel.Info)], said);
    }

    [Fact]
    public void AnObjThatNamesMaterialsBringsTheLibraryItNames()
    {
        using var rig = new ModellingRig(cube: true);
        rig.Session.ReplaceSurfaces([new Surface { Name = "Paint" }], live: false);
        var said = Listen(rig);

        rig.Session.ExportShape("steps");

        Assert.True(File.Exists(Path.Combine(Exports(rig), "steps.mtl")));
        Assert.Equal("Exported steps.obj and steps.mtl to exports/.", Assert.Single(said).Text);
    }

    [Theory]
    [InlineData(MeshFormat.Glb)]
    [InlineData(MeshFormat.Stl)]
    [InlineData(MeshFormat.Ply)]
    public void EveryOtherFormatWritesSomethingReadable(MeshFormat format)
    {
        using var rig = new ModellingRig(cube: true);
        rig.Session.SetExportFormat(format);

        rig.Session.ExportShape("steps");

        var file = new FileInfo(Path.Combine(Exports(rig), $"steps.{format.Extension}"));
        Assert.True(file.Exists);
        Assert.True(file.Length > 80);
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("..\\b")]
    public void ANameWithAPathInItIsRefusedAndNothingIsWritten(string name)
    {
        using var rig = new ModellingRig(cube: true);
        var said = Listen(rig);

        rig.Session.ExportShape(name);

        Assert.False(Directory.Exists(Exports(rig)));
        Assert.Equal([("A file name cannot contain a path.", SayLevel.Warning)], said);
    }

    [Fact]
    public void NothingToExportSaysSo()
    {
        using var rig = new ModellingRig();
        var said = Listen(rig);

        rig.Session.ExportShape("steps");

        Assert.False(Directory.Exists(Exports(rig)));
        Assert.Equal([("There is no geometry to export.", SayLevel.Warning)], said);
    }

    [Fact]
    public void AFolderThatCannotBeMadeIsSaidWithItsReason()
    {
        using var rig = new ModellingRig(cube: true);
        File.WriteAllText(Exports(rig), "in the way");
        var said = Listen(rig);

        rig.Session.ExportShape("steps");

        var (text, level) = Assert.Single(said);
        Assert.StartsWith("Could not write the export: ", text);
        Assert.Equal(SayLevel.Error, level);
    }
}

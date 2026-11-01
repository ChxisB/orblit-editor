namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Native;

// The cases that compile. They compile real C++ against the real engine headers, which is slower than the
// rest and the only way to know that what the editor writes builds. The cases that read the generated text
// are DataObjectTests, and the one about which files offer a build is AssetsTests.
public sealed class ScriptBuilderTests : IDisposable
{
    private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("orblit_cpp");

    public void Dispose() => _root.Delete(recursive: true);

    private string At(string name) => Path.Combine(_root.FullName, name);

    private static DataObject Ball() => new(
        "Ball settings",
        [
            new DataField("speed", DataType.Number, 12.5, "Metres a second"),
            new DataField("bouncy", DataType.Toggle, true),
            new DataField("label", DataType.Text, "Ball"),
            new DataField("tint", DataType.Colour, "#D9634F"),
            new DataField("spawn", DataType.Vector, new List<object?> { 0L, 1L, 2L }),
        ],
        "How the ball behaves");

    private BuildResult Build(string name, string source)
    {
        File.WriteAllText(At($"{name}.cpp"), source);
        return new ScriptBuilder(_root.FullName).Build(At($"{name}.cpp"));
    }

    private string Make(NewAsset what, string name)
    {
        using var tree = new AssetTree(_root.FullName);
        var made = tree.Create(_root.FullName, what, name);
        Assert.Null(made.Problem);
        return made.Path!;
    }

    [Fact]
    public void ThereIsAToolchainAndThereAreHeaders() =>
        // Without these the cases below would pass by not compiling.
        Assert.Null(new ScriptBuilder(_root.FullName).Problem);

    [Fact]
    public void TheCppPairItWritesCompilesAsItIs()
    {
        var built = new ScriptBuilder(_root.FullName).Build(Make(NewAsset.Native, "movement"));

        Assert.True(built.Ok, built.Output);
    }

    [Fact]
    public void TheHeaderItWritesIsWhatTheSourceIncludes()
    {
        var source = Make(NewAsset.Native, "movement");
        File.Delete(At("movement.h"));

        var built = new ScriptBuilder(_root.FullName).Build(source);

        Assert.False(built.Ok);
        Assert.Contains("movement.h", built.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeaderOnItsOwnCompilesWhenSomethingIncludesIt()
    {
        Make(NewAsset.Header, "shared");

        var built = Build("user", """
            #include "shared.h"
            ORBLIT_SCRIPT { (void)sizeof(Drift); }
            extern "C" void orblit_step(double d) { (void)d; }
            extern "C" void orblit_stop(void) {}
            """);

        Assert.True(built.Ok, built.Output);
    }

    [Fact]
    public void TheDataObjectHeaderCompilesAndReadsEveryKindOfField()
    {
        File.WriteAllText(At("ball.h"), Ball().ToCpp("ball"));

        var built = Build("reader", """
            #include "ball.h"

            ORBLIT_SCRIPT {
              double speed = BallSettings::speed(1.0);
              bool bouncy = BallSettings::bouncy(false);
              const char *label = BallSettings::label();
              const char *tint = BallSettings::tint();
              double y = BallSettings::spawn(1);
              (void)speed; (void)bouncy; (void)label; (void)tint; (void)y;
            }

            extern "C" void orblit_step(double d) { (void)d; }
            extern "C" void orblit_stop(void) {}
            """);

        Assert.True(built.Ok, built.Output);
    }

    [Fact]
    public void AFieldRenamedHereBreaksTheBuildThere()
    {
        var ball = Ball();
        var renamed = new DataObject(ball.Name, [ball.Fields[0] with { Key = "velocity" }, .. ball.Fields.Skip(1)], ball.Note);
        File.WriteAllText(At("ball.h"), renamed.ToCpp("ball"));

        var built = Build("stale", """
            #include "ball.h"
            ORBLIT_SCRIPT { (void)BallSettings::speed(1.0); }
            extern "C" void orblit_step(double d) { (void)d; }
            extern "C" void orblit_stop(void) {}
            """);

        Assert.False(built.Ok);
        Assert.Contains("speed", built.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void AScriptThatDoesNotCompileSaysWhy()
    {
        var built = Build("broken", "#include \"orblit_script.h\"\nnonsense here");

        Assert.False(built.Ok);
        Assert.NotEmpty(built.Output);
    }

    [Fact]
    public void WhatIsBuiltGoesSomewhereDisposableInsideTheProject()
    {
        new ScriptBuilder(_root.FullName).Build(Make(NewAsset.Native, "system"));

        Assert.True(Directory.Exists(Path.Combine(_root.FullName, ".orblit", "build")));
    }

    // A library already loaded cannot be closed, so a rebuild has to be a different file or it would go
    // on running the old code.
    [Fact]
    public void TwoBuildsOfOneFileDoNotFightOverAPath()
    {
        var source = Make(NewAsset.Native, "system");
        new ScriptBuilder(_root.FullName).Build(source);
        new ScriptBuilder(_root.FullName).Build(source);

        var built = Directory.GetFiles(Path.Combine(_root.FullName, ".orblit", "build")).Where(file => file.EndsWith(Toolchain.LibrarySuffix, StringComparison.Ordinal));
        Assert.True(built.Count() > 1);
    }
}

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

/// <summary>The folders trusted to run their code: kept in a file of the editor's own, and failing closed.</summary>
public sealed class ProjectTrustTests : IDisposable
{
    private readonly DirectoryInfo _support = Directory.CreateTempSubdirectory("orblit_trust");

    public void Dispose() => _support.Delete(recursive: true);

    private string TrustFile => Path.Combine(_support.FullName, "trusted-projects.json");

    private string Folder(string name) => Path.Combine(_support.FullName, "projects", name);

    [Fact]
    public void NothingIsTrustedUntilItIsGranted()
    {
        var trust = new ProjectTrust(TrustFile);

        Assert.False(trust.IsTrusted(Folder("crates")));
        Assert.False(File.Exists(TrustFile));
    }

    [Fact]
    public void AGrantIsKeptForTheNextRunOfTheEditor()
    {
        new ProjectTrust(TrustFile).Grant(Folder("crates"));

        Assert.True(new ProjectTrust(TrustFile).IsTrusted(Folder("crates")));
        Assert.False(new ProjectTrust(TrustFile).IsTrusted(Folder("barrels")));
    }

    [Fact]
    public void RevokingTakesTheTrustBackAndLeavesTheOthers()
    {
        var trust = new ProjectTrust(TrustFile);
        trust.Grant(Folder("crates"));
        trust.Grant(Folder("barrels"));

        trust.Revoke(Folder("crates"));

        Assert.False(new ProjectTrust(TrustFile).IsTrusted(Folder("crates")));
        Assert.True(new ProjectTrust(TrustFile).IsTrusted(Folder("barrels")));
    }

    [Fact]
    public void ADifferentSpellingOfTheSameFolderGivesTheSameAnswer()
    {
        var trust = new ProjectTrust(TrustFile);
        trust.Grant(Folder("crates") + Path.DirectorySeparatorChar);

        Assert.True(trust.IsTrusted(Folder("crates")));
        Assert.True(trust.IsTrusted(Path.Combine(Folder("barrels"), "..", "crates")));

        trust.Revoke(Path.Combine(Folder("barrels"), "..", "crates", ""));
        Assert.False(trust.IsTrusted(Folder("crates")));
    }

    [Fact]
    public void ANeighbouringFolderAndAFolderInsideAreNotTrusted()
    {
        var trust = new ProjectTrust(TrustFile);
        trust.Grant(Folder("crates"));

        Assert.False(trust.IsTrusted(Folder("crates-two")));
        Assert.False(trust.IsTrusted(Path.Combine(Folder("crates"), "inner")));
        Assert.False(trust.IsTrusted(Path.GetDirectoryName(Folder("crates"))!));
    }

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{ \"crates\": true }")]
    [InlineData("[ 4, null, { \"a\": 1 } ]")]
    [InlineData("[ \"/projects/cr\\u0000ates\" ]")]
    public void AFileThatIsNotAListOfFoldersTrustsNothingAndTheNextGrantMendsIt(string text)
    {
        File.WriteAllText(TrustFile, text);
        var trust = new ProjectTrust(TrustFile);

        Assert.False(trust.IsTrusted(Folder("crates")));

        trust.Grant(Folder("crates"));
        Assert.True(new ProjectTrust(TrustFile).IsTrusted(Folder("crates")));
    }

    // A folder that is not written in full would mean a different place for each place the editor starts in.
    [Fact]
    public void AnEntryThatIsNotAFullPathTrustsNothing()
    {
        File.WriteAllText(TrustFile, """[ "crates", "./crates" ]""");
        var trust = new ProjectTrust(TrustFile);

        Assert.False(trust.IsTrusted(Path.GetFullPath("crates")));
        Assert.False(trust.IsTrusted(Directory.GetCurrentDirectory()));
    }

    [Fact]
    public void TheFileIsMadeWithItsFolderWhenThereIsNone()
    {
        var nested = Path.Combine(_support.FullName, "config", "orblit", "trusted-projects.json");

        new ProjectTrust(nested).Grant(Folder("crates"));

        Assert.True(new ProjectTrust(nested).IsTrusted(Folder("crates")));
    }

    [Fact]
    public void RevokingAFolderThatIsNotTrustedWritesNothing()
    {
        new ProjectTrust(TrustFile).Revoke(Folder("crates"));

        Assert.False(File.Exists(TrustFile));
    }
}

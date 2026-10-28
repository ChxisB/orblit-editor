namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// Added in C#. The Dart CodeEditor ran its processes directly and had no tests. Here the programs are a
// fake, so what is asked and what is started can be read back.
public sealed class CodeEditorTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_code");
    private readonly FakePrograms _programs = new();

    public void Dispose() => _project.Delete(recursive: true);

    private string Folder => _project.FullName;

    private string Script()
    {
        var path = Path.Combine(Folder, "hero.ts");
        File.WriteAllText(path, "");
        return path;
    }

    private CodeEditor On(HostPlatform platform) => new(_programs, platform);

    [Fact]
    public void TheFirstCommandOnThePathWins()
    {
        _programs.Answers.Add("which zed");
        _programs.Answers.Add("which cursor");

        Assert.Equal("Cursor", On(HostPlatform.Linux).Available);
    }

    [Fact]
    public void AnAppWithoutItsCommandCountsOnAMacOnly()
    {
        _programs.Answers.Add("open -Ra Sublime Text");

        Assert.Equal("Sublime Text", On(HostPlatform.MacOS).Available);
        Assert.Null(On(HostPlatform.Linux).Available);
    }

    [Fact]
    public void ItLooksOnce()
    {
        var code = On(HostPlatform.Linux);
        _ = code.Available;
        var asked = _programs.Asked.Count;

        _ = code.Available;

        Assert.Equal(asked, _programs.Asked.Count);
    }

    [Fact]
    public void WindowsAsksWhereAndNotWhich()
    {
        _programs.Answers.Add("where code");

        Assert.Equal("Visual Studio Code", On(HostPlatform.Windows).Available);
    }

    [Fact]
    public void AFileOpensInsideItsFolderAtTheFile()
    {
        _programs.Answers.Add("which code");
        var file = Script();

        Assert.Null(On(HostPlatform.MacOS).Open(Folder, file));

        Assert.Equal([$"code {Folder} --goto {file}"], _programs.Started);
    }

    [Fact]
    public void TheFolderOpensAloneWhenNoFileIsNamed()
    {
        _programs.Answers.Add("which code");

        Assert.Null(On(HostPlatform.MacOS).Open(Folder));

        Assert.Equal([$"code {Folder}"], _programs.Started);
    }

    [Fact]
    public void AnAppWithoutItsCommandOpensByItsBundle()
    {
        _programs.Answers.Add("open -Ra Visual Studio Code");
        var file = Script();

        Assert.Null(On(HostPlatform.MacOS).Open(Folder, file));

        Assert.Equal([$"open -b com.microsoft.VSCode {Folder} {file}"], _programs.Started);
    }

    [Fact]
    public void OneThatWillNotStartHandsOnToTheNext()
    {
        _programs.Answers.Add("which code");
        _programs.Answers.Add("which zed");
        _programs.Refused.Add("code");

        Assert.Null(On(HostPlatform.Linux).Open(Folder));

        Assert.Equal([$"zed {Folder}"], _programs.Started);
    }

    [Fact]
    public void SaysSoWhenThereIsNoEditor() =>
        Assert.StartsWith("No code editor found.", On(HostPlatform.MacOS).Open(Folder), StringComparison.Ordinal);

    [Fact]
    public void AFolderThatHasGoneIsNotOpened()
    {
        _programs.Answers.Add("which code");

        Assert.Equal("That folder is not there any more.", On(HostPlatform.MacOS).Open(Path.Combine(Folder, "gone")));
        Assert.Empty(_programs.Started);
    }

    [Theory]
    [InlineData(HostPlatform.MacOS, "open")]
    [InlineData(HostPlatform.Linux, "xdg-open")]
    public void RevealHandsThePathToTheDesktop(HostPlatform platform, string program)
    {
        Assert.Null(On(platform).Reveal(Folder));

        Assert.Equal([$"{program} {Folder}"], _programs.Started);
    }

    [Fact]
    public void WindowsShowsAFolderInExplorerAndAFileThroughStart()
    {
        var file = Script();

        On(HostPlatform.Windows).Reveal(Folder);
        On(HostPlatform.Windows).Reveal(file);

        Assert.Equal([$"explorer {Folder}", $"cmd /c start  {file}"], _programs.Started);
    }

    [Fact]
    public void SaysSoWhenTheDesktopWillNotOpenIt()
    {
        _programs.Refused.Add("open");

        Assert.Equal($"Could not open {Folder}.", On(HostPlatform.MacOS).Reveal(Folder));
    }

    [Fact]
    public void SomethingThatHasGoneIsNotRevealed()
    {
        Assert.Equal("That is not there any more.", On(HostPlatform.MacOS).Reveal(Path.Combine(Folder, "gone")));
        Assert.Empty(_programs.Started);
    }
}

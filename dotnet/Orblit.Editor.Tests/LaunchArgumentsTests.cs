namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class LaunchArgumentsTests
{
    [Fact]
    public void TheFolderNamedOnTheCommandLineIsTheFirstThingThatIsNotASwitch()
    {
        Assert.Equal("/Users/me/Projects/Thing", LaunchArguments.ProjectPath(["/Users/me/Projects/Thing"]));
        Assert.Null(LaunchArguments.ProjectPath([]));
    }

    [Fact]
    public void MacOsOwnSwitchesAreNotProjects()
    {
        // YES is the value of a switch, not a folder.
        Assert.Null(LaunchArguments.ProjectPath(["-NSDocumentRevisionsDebugMode", "YES"]));
        Assert.Equal(
            "/Users/me/Projects/Thing",
            LaunchArguments.ProjectPath(["-NSDocumentRevisionsDebugMode", "YES", "/Users/me/Projects/Thing"]));
    }

    [Fact]
    public void ASwitchIsAssumedToTakeAValueWhichIsTheLimitOfThis()
    {
        Assert.Null(LaunchArguments.ProjectPath(["-flag", "/tmp/p"]));
        Assert.Equal("/tmp/p", LaunchArguments.ProjectPath(["/tmp/p", "-flag"]));
    }
}

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class HostPathsTests
{
    private static string? Home(params (string Name, string Value)[] variables) =>
        HostPaths.HomeDirectory(variables.ToDictionary(pair => pair.Name, pair => pair.Value));

    [Fact]
    public void HomeAsMacOsAndLinuxSetIt() =>
        Assert.Equal("/Users/robin", Home(("HOME", "/Users/robin")));

    [Fact]
    public void UserprofileWhereHomeIsNotSet() =>
        Assert.Equal(@"C:\Users\Robin", Home(("USERPROFILE", @"C:\Users\Robin")));

    [Fact]
    public void HomedriveAndHomepathTogetherWhereNeitherOfTheAboveIsSet() =>
        Assert.Equal(@"C:\Users\Robin", Home(("HOMEDRIVE", "C:"), ("HOMEPATH", @"\Users\Robin")));

    [Fact]
    public void HomeWinsWhenMoreThanOneIsSet() =>
        Assert.Equal(
            "/Users/robin",
            Home(
                ("HOME", "/Users/robin"),
                ("USERPROFILE", @"C:\Users\Robin"),
                ("HOMEDRIVE", "C:"),
                ("HOMEPATH", @"\Users\Robin")));

    [Fact]
    public void UserprofileWinsOverHomedriveHomepath() =>
        Assert.Equal(
            @"C:\Users\Robin",
            Home(("USERPROFILE", @"C:\Users\Robin"), ("HOMEDRIVE", "C:"), ("HOMEPATH", @"\Someone\Else")));

    [Fact]
    public void NullWhenNothingSaysWhereHomeIs() => Assert.Null(Home());

    // Seen on some CI runners and restricted shells, where the variable exists but was cleared rather
    // than removed.
    [Fact]
    public void AnEmptyHomeDoesNotCountAsSet() =>
        Assert.Equal(@"C:\Users\Robin", Home(("HOME", ""), ("USERPROFILE", @"C:\Users\Robin")));

    [Fact]
    public void HomedriveWithoutHomepathIsNotEnough() => Assert.Null(Home(("HOMEDRIVE", "C:")));
}

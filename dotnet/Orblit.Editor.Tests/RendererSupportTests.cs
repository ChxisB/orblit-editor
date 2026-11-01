namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The platform is an argument.
public sealed class RendererSupportTests
{
    [Fact]
    public void TheRendererIsAvailableOnMacOS() =>
        Assert.True(HostPlatform.MacOS.HasRenderer());

    [Theory]
    [InlineData(HostPlatform.Windows)]
    [InlineData(HostPlatform.Linux)]
    [InlineData(HostPlatform.Android)]
    [InlineData(HostPlatform.IOS)]
    [InlineData(HostPlatform.Fuchsia)]
    public void TheRendererIsNotAvailableElsewhere(HostPlatform platform) =>
        Assert.False(platform.HasRenderer());

    [Fact]
    public void ThePlaceholderReadsAsNotYetAndNotAsSomethingBroken()
    {
        var saying = ViewportPlaceholder.Unavailable.ToLowerInvariant();

        Assert.Contains("not available", saying, StringComparison.Ordinal);
        Assert.Contains("yet", saying, StringComparison.Ordinal);
        Assert.DoesNotContain("error", saying, StringComparison.Ordinal);
        Assert.DoesNotContain("fail", saying, StringComparison.Ordinal);
    }
}

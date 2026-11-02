namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class DotnetBuildTests : IDisposable
{
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_test");

    public void Dispose() => _temp.Delete(recursive: true);

    private string Missing => Path.Combine(_temp.FullName, "Missing.csproj");

    private string Output => Path.Combine(_temp.FullName, "out");

    [Fact]
    public async Task AProjectThatIsNotThereFailsInTheToolsOwnWords()
    {
        var result = await new DotnetBuild().RunAsync(Missing, Output, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("MSB1009", result.Text);
    }

    [Fact]
    public async Task ABuildPastItsTimeLimitIsStoppedAndSaysSo()
    {
        var result = await new DotnetBuild(TimeSpan.FromMilliseconds(1)).RunAsync(Missing, Output, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains("time limit", result.Text);
    }

    [Fact]
    public async Task ACancelledBuildIsStoppedAndTheCallerHearsIt()
    {
        using var cancel = new CancellationTokenSource();
        await cancel.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new DotnetBuild().RunAsync(Missing, Output, cancel.Token));
    }
}

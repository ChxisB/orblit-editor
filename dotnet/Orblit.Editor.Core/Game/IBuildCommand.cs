namespace Orblit.Editor.Core;

/// <summary>What a build said, whole. A person reads it when the build fails.</summary>
public sealed record BuildOutput(bool Succeeded, string Text);

/// <summary>
/// The one thing <see cref="GameBuilder"/> needs from a build tool, so a test can answer without
/// starting one.
/// </summary>
public interface IBuildCommand
{
    /// <summary>Builds <paramref name="projectFile"/> into <paramref name="outputFolder"/>.</summary>
    /// <exception cref="OperationCanceledException">The caller cancelled. The build is stopped first.</exception>
    Task<BuildOutput> RunAsync(string projectFile, string outputFolder, CancellationToken cancel);
}

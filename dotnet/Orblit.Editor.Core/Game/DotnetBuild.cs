namespace Orblit.Editor.Core;

using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;

/// <summary>Builds with the <c>dotnet</c> on the path, the way a person would at a terminal.</summary>
/// <remarks>
/// A build that never ends would leave Play waiting for ever, so each build has a time limit. Past it the
/// build and its child processes are stopped and the result says so.
/// </remarks>
public sealed class DotnetBuild(TimeSpan? limit = null) : IBuildCommand
{
    private readonly TimeSpan _limit = limit ?? TimeSpan.FromMinutes(5);

    public async Task<BuildOutput> RunAsync(string projectFile, string outputFolder, CancellationToken cancel)
    {
        cancel.ThrowIfCancellationRequested();
        var lines = new ConcurrentQueue<string>();
        using var process = new Process { StartInfo = StartFor(projectFile, outputFolder) };
        process.OutputDataReceived += (_, line) => Keep(lines, line.Data);
        process.ErrorDataReceived += (_, line) => Keep(lines, line.Data);

        try
        {
            process.Start();
        }
        catch (Win32Exception failure)
        {
            return new BuildOutput(false, $"Could not start dotnet. Install the .NET SDK and try again.\n{failure.Message}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(_limit);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            Stop(process);
            cancel.ThrowIfCancellationRequested();
            return new BuildOutput(
                false,
                $"The build ran past the time limit of {_limit.TotalSeconds:0} seconds and was stopped.\n{string.Join('\n', lines)}");
        }

        return new BuildOutput(process.ExitCode == 0, string.Join('\n', lines));
    }

    private static ProcessStartInfo StartFor(string projectFile, string outputFolder)
    {
        // The folder the build runs in picks the global.json that chooses the SDK, so it is the game's.
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = Path.GetDirectoryName(projectFile) ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // Both paths are full, so neither can be read as an option. Each is one argument, with no shell between.
        // A library leaves its packages behind unless told to copy them, and the loader looks for them beside the game.
        var arguments = new[]
        {
            "build", projectFile, "-c", "Debug", "-o", outputFolder, "-p:CopyLocalLockFileAssemblies=true", "--nologo", "-v", "minimal",
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    private static void Keep(ConcurrentQueue<string> lines, string? line)
    {
        if (line is not null)
        {
            lines.Enqueue(line);
        }
    }

    private static void Stop(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
        }
    }
}

using System.ComponentModel;
using System.Diagnostics;

namespace Orblit.Editor;

/// <summary>This machine's own programs.</summary>
internal sealed class Programs : IPrograms
{
    public bool Start(string program, IReadOnlyList<string> arguments)
    {
        try
        {
            // Not waited on. The editor it starts outlives this call, and one that does not fork would hang the window.
            using var started = Process.Start(Info(program, arguments));
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public bool Succeeds(string program, IReadOnlyList<string> arguments)
    {
        var info = Info(program, arguments);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        try
        {
            // Not null: Start without the shell always makes a new process or throws.
            using var run = Process.Start(info)!;
            var errors = run.StandardError.ReadToEndAsync();
            run.StandardOutput.ReadToEnd();
            errors.Wait();
            run.WaitForExit();
            return run.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static ProcessStartInfo Info(string program, IReadOnlyList<string> arguments)
    {
        var info = new ProcessStartInfo(program) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        return info;
    }
}

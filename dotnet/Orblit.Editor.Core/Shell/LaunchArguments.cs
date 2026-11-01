namespace Orblit.Editor.Core;

public static class LaunchArguments
{
    /// <summary>
    /// The folder to open out of what the app was launched with, or null when it was not told one.
    /// <para>
    /// macOS puts its own switches in, such as <c>-NSDocumentRevisionsDebugMode YES</c> when a debug
    /// build is started by Xcode. Anything beginning with a dash is not a path somebody typed, and
    /// neither is the value that follows it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// shortcut: a switch is assumed to take a value. The editor defines no switches of its own, so
    /// every dash argument it sees is one of macOS's pairs. Change this when it takes a flag.
    /// </remarks>
    public static string? ProjectPath(IReadOnlyList<string> arguments)
    {
        for (var at = 0; at < arguments.Count; at++)
        {
            if (!arguments[at].StartsWith('-'))
            {
                return arguments[at];
            }

            // Skip its value as well, or the YES in a Xcode run opens a project called YES.
            if (at + 1 < arguments.Count && !arguments[at + 1].StartsWith('-'))
            {
                at++;
            }
        }

        return null;
    }
}

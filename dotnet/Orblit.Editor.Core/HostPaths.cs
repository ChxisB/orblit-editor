namespace Orblit.Editor.Core;

/// <summary>Where the host keeps things the editor needs to find.</summary>
public static class HostPaths
{
    /// <summary>
    /// The current user's home directory, or null if nothing says where it is.
    /// <para>
    /// <c>HOME</c> first, because that is what macOS and Linux both set. Then <c>USERPROFILE</c>, which
    /// is the equivalent Windows sets directly. Then <c>HOMEDRIVE</c> joined with <c>HOMEPATH</c>, an
    /// older pair Windows also sets, and the one still answered inside some restricted shells and CI
    /// runners where <c>USERPROFILE</c> has been stripped out but the drive-and-path pair survives.
    /// </para>
    /// <para>
    /// <paramref name="environment"/> is the process environment by default. A test passes its own map
    /// instead of faking the real environment to exercise a particular platform's variables.
    /// </para>
    /// </summary>
    public static string? HomeDirectory(IReadOnlyDictionary<string, string>? environment = null)
    {
        string? Read(string name) => environment is null
            ? Environment.GetEnvironmentVariable(name)
            : environment.GetValueOrDefault(name);

        if (Read("HOME") is { Length: > 0 } home)
        {
            return home;
        }

        if (Read("USERPROFILE") is { Length: > 0 } profile)
        {
            return profile;
        }

        return Read("HOMEDRIVE") is { Length: > 0 } drive && Read("HOMEPATH") is { Length: > 0 } path
            ? drive + path
            : null;
    }

    /// <summary>
    /// The folder the editor keeps its own files in: the recent projects, the store's settings and the
    /// kits downloaded. They belong to no one project, so a kit is fetched once for all of them.
    /// </summary>
    public static string SupportFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Orblit");
}

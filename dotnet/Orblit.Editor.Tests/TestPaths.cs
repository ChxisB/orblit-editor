namespace Orblit.Editor.Tests;

internal static class TestPaths
{
    /// <summary>
    /// A path in <paramref name="project"/> written with forward slashes, spelt as the host spells it. The editor
    /// builds its paths with the host's separator, and a test that expects a mix of both fails on Windows.
    /// </summary>
    public static string In(DirectoryInfo project, string name) =>
        Path.Combine(project.FullName, name.Replace('/', Path.DirectorySeparatorChar));
}

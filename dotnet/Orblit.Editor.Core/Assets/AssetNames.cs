namespace Orblit.Editor.Core;

using IoPath = System.IO.Path;

/// <summary>
/// The parts of a file name the browser reads. A name that starts with a dot is a dotfile with no
/// extension, which is not what <see cref="System.IO.Path.GetExtension(string)"/> answers.
/// </summary>
internal static class AssetNames
{
    /// <summary>The last dot of the file name and what follows it, or nothing for a dotfile and a name with no dot.</summary>
    public static string Extension(string path)
    {
        var name = NameOf(path);
        var dot = name.LastIndexOf('.');
        return dot <= 0 ? "" : name[dot..];
    }

    public static string Stem(string path)
    {
        var name = NameOf(path);
        return name[..(name.Length - Extension(path).Length)];
    }

    /// <summary>The last segment, with a trailing separator ignored.</summary>
    public static string NameOf(string path) => IoPath.GetFileName(IoPath.TrimEndingDirectorySeparator(path));
}

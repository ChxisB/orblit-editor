namespace Orblit.Editor.Core;

using System.Globalization;

/// <summary>One entry in the project folder.</summary>
/// <param name="Bytes">Null for a folder, whose size is not a useful thing to show.</param>
public sealed record Asset(string Name, string Path, AssetKind Kind, long? Bytes = null)
{
    public bool IsFolder => Kind == AssetKind.Folder;

    /// <summary>
    /// Whether asking to compile this makes sense. A source file, not a header: a header is built
    /// as part of whatever includes it, and offering to compile one on its own would offer an
    /// action whose answer is always the same.
    /// </summary>
    public bool CanBuild => AssetNames.Extension(Path).ToLowerInvariant() is ".cpp" or ".cc";

    /// <summary>A size somebody can read at a glance.</summary>
    public string Size => Bytes is { } bytes ? SizeLabel.Of(bytes) : "";
}

/// <summary>A byte count as something to read at a glance.</summary>
public static class SizeLabel
{
    /// <summary>Bytes, then kilobytes, then megabytes to a tenth.</summary>
    public static string Of(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024.0:F0} KB"),
        _ => string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024.0 * 1024):F1} MB"),
    };
}

namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Json;

/// <summary>
/// The project folders the person has trusted to run their code, kept in one file of the editor's own.
/// <para>
/// The file is never inside a project, because a project could then ship its own trust. It is read on
/// every question, so two windows and two editors see the same answer. A file that is missing, damaged
/// or cannot be read trusts nothing, and the next grant writes it again.
/// </para>
/// </summary>
/// <param name="file">
/// Where the list lives. The caller owns the choice, because the folder for an application's data is
/// the host's to say and not the model's.
/// </param>
public sealed class ProjectTrust(string file) : IProjectTrust
{
    public bool IsTrusted(string projectFolder) => Read().Contains(Spelling(projectFolder));

    public void Grant(string projectFolder)
    {
        var trusted = Read();
        if (trusted.Add(Spelling(projectFolder)))
        {
            Write(trusted);
        }
    }

    public void Revoke(string projectFolder)
    {
        var trusted = Read();
        if (trusted.Remove(Spelling(projectFolder)))
        {
            Write(trusted);
        }
    }

    // One spelling per folder: in full, with no dots and no separator on the end. The comparison is
    // exact, because a folder that differs only by case may be another folder on some disks, and asking
    // twice costs less than trusting the wrong one.
    private static string Spelling(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));

    private SortedSet<string> Read()
    {
        var trusted = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            if (JsonText.Decode(IoFile.ReadAllText(file)) is IReadOnlyList<object?> decoded)
            {
                // A folder that is not written in full would mean a different place for each folder the editor starts in.
                foreach (var folder in decoded.OfType<string>().Where(Path.IsPathFullyQualified))
                {
                    trusted.Add(Spelling(folder));
                }
            }
        }
        catch (Exception error) when (error is JsonFormatException or IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            // Failing closed: what cannot be read, or names a folder that cannot exist, trusts nothing. The
            // last two are what a path with a character no disk allows throws.
            trusted.Clear();
        }

        return trusted;
    }

    private void Write(SortedSet<string> trusted)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); // A file path always has a folder.
        IoFile.WriteAllText(file, JsonText.Encode(trusted.Select(folder => (object?)folder).ToList(), indent: "  "));
    }
}

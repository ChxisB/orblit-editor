namespace Orblit.Editor;

using Orblit.Editor.Core;

/// <summary>What <see cref="CodeEditor"/> needs of the machine: programs to start and programs to ask.</summary>
internal interface IPrograms
{
    /// <summary>Starts a program and leaves it running. False when it could not be started.</summary>
    bool Start(string program, IReadOnlyList<string> arguments);

    /// <summary>Runs a program to its end and answers whether it exited with 0.</summary>
    bool Succeeds(string program, IReadOnlyList<string> arguments);
}

/// <summary>Handing a file or a folder to whatever somebody writes code in.</summary>
/// <remarks>
/// The engine has no text editor of its own and is not going to grow one. Scripts are ordinary files in an
/// ordinary folder, so the useful thing is to open the project where the person already has their
/// extensions, their keys and their terminal.
/// </remarks>
internal sealed class CodeEditor
{
    // Best first. A command takes --goto, opens into the window already open and returns at once. The
    // bundle is for somebody who installed VS Code and never put its command on the PATH, which is most.
    private static readonly Candidate[] Candidates =
    [
        new("code", "Visual Studio Code", "com.microsoft.VSCode"),
        new("cursor", "Cursor", "com.todesktop.230313mzl4w4u92"),
        new("code-insiders", "Visual Studio Code - Insiders", "com.microsoft.VSCodeInsiders"),
        new("subl", "Sublime Text", "com.sublimetext.4"),
        new("zed", "Zed", "dev.zed.Zed"),
    ];

    private readonly IPrograms _programs;
    private readonly HostPlatform _platform;

    // Read on every refresh of the menu, and only an install part way through a session changes it.
    private readonly Lazy<string?> _available;

    public CodeEditor(IPrograms programs, HostPlatform platform)
    {
        _programs = programs;
        _platform = platform;
        _available = new Lazy<string?>(() => Candidates.FirstOrDefault(one => OnPath(one) || Installed(one))?.Label);
    }

    /// <summary>The one over this machine's programs. Shared, so a run looks for editors once.</summary>
    public static CodeEditor Here { get; } = new(new Programs(), HostPlatforms.Current);

    /// <summary>The name of the editor installed, to show on the menu, or null.</summary>
    public string? Available => _available.Value;

    /// <summary>Opens a folder, with the cursor in one file inside it when there is one. Answers what went wrong, or null.</summary>
    /// <remarks>The folder even when a file is named, so the editor can find the other scripts and the types beside it.</remarks>
    public string? Open(string folder, string? file = null)
    {
        if (!Directory.Exists(folder))
        {
            return "That folder is not there any more.";
        }

        string[] opened = file is null ? [folder] : [folder, "--goto", file];
        if (Candidates.Any(one => OnPath(one) && _programs.Start(one.Command, opened)))
        {
            return null;
        }

        string[] bundled = file is null ? [folder] : [folder, file];
        return Candidates.Any(one => Installed(one) && _programs.Start("open", ["-b", one.Bundle, .. bundled]))
            ? null
            : "No code editor found. Install VS Code, or its \"code\" command from the command palette.";
    }

    /// <summary>Shows a file or a folder in Finder, Explorer or whatever the desktop uses. Answers what went wrong, or null.</summary>
    public string? Reveal(string path)
    {
        var isFolder = Directory.Exists(path);
        if (!isFolder && !File.Exists(path))
        {
            return "That is not there any more.";
        }

        var started = _platform switch
        {
            // Explorer shows a folder given to it but opens a file in the file's own program, so a file goes
            // through start. Start reads its first quoted argument as a window title, hence the empty one.
            HostPlatform.Windows when isFolder => _programs.Start("explorer", [path]),
            HostPlatform.Windows => _programs.Start("cmd", ["/c", "start", "", path]),
            HostPlatform.MacOS => _programs.Start("open", [path]),
            _ => _programs.Start("xdg-open", [path]),
        };
        return started ? null : $"Could not open {path}.";
    }

    private bool OnPath(Candidate editor) => _programs.Succeeds(_platform == HostPlatform.Windows ? "where" : "which", [editor.Command]);

    // -R finds the app without opening it, so asking whether it is there puts no window up.
    private bool Installed(Candidate editor) => _platform == HostPlatform.MacOS && _programs.Succeeds("open", ["-Ra", editor.Label]);

    private sealed record Candidate(string Command, string Label, string Bundle);
}

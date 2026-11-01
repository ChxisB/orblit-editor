namespace Orblit.Editor;

using Orblit.Native;

/// <summary>Compiling a C++ script from the editor.</summary>
/// <remarks>
/// The editor builds and checks. It does not load. Loading runs somebody's native code in the editor's own
/// process, where a bad pointer would take the editor down with whatever was unsaved. The runtime loads.
/// This says whether it could, with the runtime's own compiler and headers, so a script the editor passes
/// is one the engine takes.
/// </remarks>
internal sealed class ScriptBuilder(string projectRoot)
{
    private readonly Toolchain? _toolchain = Toolchain.Find();
    private readonly IReadOnlyList<string> _includes = ScriptRunner.EngineIncludes();

    /// <summary>Where built libraries go: inside the project, out of the way and disposable.</summary>
    public DirectoryInfo Output { get; } = new(Path.Combine(projectRoot, ".orblit", "build"));

    /// <summary>What went wrong before a compiler was reached, or null.</summary>
    public string? Problem =>
        _toolchain is null ? "No C++ compiler found. Install the Xcode command line tools, or clang, or gcc, and try again."
        : _includes.Count == 0 ? "The engine headers could not be found. Set ORBLIT_INCLUDE to the include folders of orblit_native and orblit_core."
        : null;

    /// <summary>Compiles one source file.</summary>
    /// <remarks>
    /// The revision is from the clock rather than a counter, so every build goes to a file of its own. A
    /// library once loaded cannot be closed, and a reused path goes on running the old code.
    /// </remarks>
    public BuildResult Build(string path)
    {
        if (Problem is { } stopped)
        {
            return new BuildResult(null, stopped, string.Empty);
        }

        // The clock read directly breaks F10: the revision only has to differ between builds, and no test needs a particular one.
        var revision = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000);
        // Not null: there is no problem, so there is a toolchain.
        return _toolchain!.Compile(new FileInfo(path), Output, _includes, revision);
    }
}

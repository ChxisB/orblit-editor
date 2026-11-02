namespace Orblit.Editor.Core;

using System.Reflection;
using System.Runtime.Loader;

/// <summary>
/// Where one build of a game lives, apart from the editor, so a rebuilt game can replace it.
/// </summary>
/// <remarks>
/// <para>
/// An assembly the editor already has is the editor's, never the game's own copy. A second copy of
/// <c>Orblit.Game</c> would make a second <c>IGame</c>, and the editor could not call the game. Only what
/// the editor lacks, the game and its own packages, comes from the game's folder.
/// </para>
/// <para>
/// Assemblies are read into memory. A file that stayed open would stop the next build from replacing
/// it on Windows. So the game's <c>Assembly.Location</c> is empty.
/// </para>
/// <para>
/// Native libraries are not looked for here. The base class answers none, and the runtime then uses
/// its usual search, which is the editor's. A game that brings a native library of its own is not covered.
/// </para>
/// </remarks>
internal sealed class GameLoadContext(string assemblyPath)
    : AssemblyLoadContext($"game:{Path.GetFileName(assemblyPath)}", isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    /// <summary>Reads the game's own assembly into this context.</summary>
    public Assembly LoadGame() => LoadFromFile(assemblyPath);

    protected override Assembly? Load(AssemblyName name)
    {
        if (FromEditor(name) is { } shared)
        {
            return shared;
        }

        return _resolver.ResolveAssemblyToPath(name) is { } path ? LoadFromFile(path) : null;
    }

    // A copy that is there but the wrong version throws a different exception, and that one is let through.
    private static Assembly? FromEditor(AssemblyName name)
    {
        try
        {
            return Default.LoadFromAssemblyName(name);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private Assembly LoadFromFile(string path)
    {
        using var assembly = File.OpenRead(path);
        var symbols = Path.ChangeExtension(path, ".pdb");
        if (!File.Exists(symbols))
        {
            return LoadFromStream(assembly);
        }

        using var pdb = File.OpenRead(symbols);
        return LoadFromStream(assembly, pdb);
    }
}

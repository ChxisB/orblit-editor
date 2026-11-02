namespace Orblit.Editor.Core;

using System.Reflection;
using Orblit.Game;

/// <summary>A game could not be loaded. The message says why, for the person who wrote the game.</summary>
public sealed class GameLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Makes the game a built assembly holds.</summary>
public sealed class GameLoader
{
    private readonly Version _engine;

    /// <param name="engine">
    /// The version of <c>Orblit.Game</c> the editor runs. Left out, it is the one the editor was built with.
    /// A test names another to see a game refused.
    /// </param>
    public GameLoader(Version? engine = null) =>
        _engine = engine ?? typeof(IGame).Assembly.GetName().Version ?? new Version();

    /// <exception cref="GameLoadException">The assembly or the type cannot be a game.</exception>
    public LoadedGame Load(string assemblyPath, string typeName)
    {
        // Said here, before a context exists, because the dependency resolver fails on a missing file in its own words.
        if (!File.Exists(assemblyPath))
        {
            throw new GameLoadException($"The game is not built. {assemblyPath} is not there.");
        }

        var context = new GameLoadContext(assemblyPath);
        LoadedGame? loaded = null;
        try
        {
            var assembly = context.LoadGame();
            if (VersionProblem(_engine, BuiltFor(assembly)) is { } problem)
            {
                throw new GameLoadException(problem);
            }

            loaded = new LoadedGame(context, Make(TypeNamed(assembly, typeName)));
            return loaded;
        }
        catch (Exception failure) when (failure is BadImageFormatException or IOException or TypeLoadException)
        {
            throw new GameLoadException($"The game could not be loaded. {failure.Message}", failure);
        }
        finally
        {
            if (loaded is null)
            {
                context.Unload();
            }
        }
    }

    /// <summary>
    /// Why a game built for <paramref name="built"/> cannot run on <paramref name="engine"/>, or null when it
    /// can. A change of the first two numbers can break a game, and a change of the third cannot.
    /// </summary>
    internal static string? VersionProblem(Version engine, Version? built)
    {
        if (built is null)
        {
            return "The game does not use Orblit.Game, so the editor cannot run it.";
        }

        return built.Major == engine.Major && built.Minor == engine.Minor
            ? null
            : $"The game was built for Orblit.Game {built.ToString(3)}. This editor runs {engine.ToString(3)}. Build the game against Orblit.Game {engine.Major}.{engine.Minor}.x.";
    }

    private static Version? BuiltFor(Assembly assembly) =>
        assembly.GetReferencedAssemblies().FirstOrDefault(one => one.Name == "Orblit.Game")?.Version;

    private static Type TypeNamed(Assembly assembly, string typeName) =>
        assembly.GetType(typeName) ?? throw new GameLoadException($"The game has no type named {typeName}.");

    private static IGame Make(Type type)
    {
        if (!typeof(IGame).IsAssignableFrom(type))
        {
            throw new GameLoadException($"{type.FullName} is not an IGame.");
        }

        if (type.IsAbstract)
        {
            throw new GameLoadException($"{type.FullName} is abstract, so the editor cannot make one.");
        }

        var constructor = type.GetConstructor(Type.EmptyTypes)
            ?? throw new GameLoadException($"{type.FullName} needs a public constructor with no parameters.");
        try
        {
            return (IGame)constructor.Invoke(null);
        }
        catch (TargetInvocationException failure)
        {
            throw new GameLoadException($"{type.FullName} failed when the editor made it. {failure.InnerException?.Message}", failure.InnerException);
        }
    }
}

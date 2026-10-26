namespace Orblit.Editor.Core;

using Orblit.Filament;
using Orblit.Json;
using Orblit.Stage;
using IoDirectory = System.IO.Directory;
using IoPath = System.IO.Path;

/// <summary>
/// Keeps the last cook of each shader that worked.
/// </summary>
/// <remarks>
/// A shader is saved half written all the time, and the object wearing it should go on drawing the
/// last version that compiled until the next one does. So a failure adds a problem and leaves the
/// surface alone. Call it from the interface thread: a cook lands back on the thread that asked.
/// </remarks>
public sealed class SurfaceCooks : IDisposable
{
    private readonly Dictionary<string, CookedSurface> _good = [];
    private readonly Dictionary<string, string> _problems = [];
    private readonly Action<string>? _onProblem;
    private readonly Func<string, Task<IReadOnlyList<ShaderCooked>>> _cook;
    private string _stamp = "";
    private string? _lastFailure;
    private bool _running;
    private bool _again;
    private bool _disposed;

    /// <param name="projectDirectory">The project folder, the one whose assets folder holds the shaders.</param>
    /// <param name="onProblem">Told once for each shader whose cook fails with a new complaint.</param>
    /// <param name="cook">Cooks every shader in a project. The real cook where null.</param>
    public SurfaceCooks(
        string projectDirectory,
        Action<string>? onProblem = null,
        Func<string, Task<IReadOnlyList<ShaderCooked>>>? cook = null)
    {
        ProjectDirectory = projectDirectory;
        _onProblem = onProblem;
        _cook = cook ?? ShaderCook.CookAsync;
    }

    /// <summary>Said when a cook has landed, whether or not anything came out different.</summary>
    public event Action? Changed;

    public string ProjectDirectory { get; }

    /// <summary>What the shader at <paramref name="asset"/> declares, from its last good cook.</summary>
    public CookedSurface? CookedOf(string asset) => _good.GetValueOrDefault(KeyOf(asset));

    /// <summary>Why the last cook of <paramref name="asset"/> failed, or null when it did not.</summary>
    public string? ProblemWith(string asset) => _problems.GetValueOrDefault(KeyOf(asset));

    /// <summary>The surface <paramref name="item"/> draws, or null when its shader has not cooked yet.</summary>
    public CustomSurface? SurfaceFor(SceneObject item) =>
        item.MaterialAsset is { } asset
            ? CookedOf(asset)?.SurfaceFor(
                item.MaterialValues,
                path => IoPath.IsPathRooted(path) ? path : IoPath.Join(ProjectDirectory, path))
            : null;

    /// <summary>
    /// Cooks again when a shader file has changed since the last cook.
    /// </summary>
    /// <remarks>
    /// Called on every change to the project folder, so the check has to be cheap: a stamp of the
    /// shader files, not a cook. A call during a cook asks for one more look when it lands.
    /// </remarks>
    // shortcut: the stamp walks the assets folder on the interface thread, move it into the cook if a very large project stutters.
    public async Task RefreshAsync()
    {
        if (_running)
        {
            _again = true;
            return;
        }

        _running = true;
        try
        {
            do
            {
                _again = false;
                var stamp = StampOfShaders();
                if (stamp == _stamp)
                {
                    break;
                }

                _stamp = stamp;
                await CookOnceAsync();
            }
            while (_again);
        }
        finally
        {
            _running = false;
        }
    }

    public void Dispose() => _disposed = true;

    private async Task CookOnceAsync()
    {
        try
        {
            Take(await _cook(ProjectDirectory));
        }
        catch (Exception error)
        {
            // The editor outlives a cook that could not even start. The stamp is forgotten so the
            // next change to the folder tries again, and the same complaint is told once.
            _stamp = "";
            var problem = $"Could not cook the shaders: {error.Message}";
            if (problem != _lastFailure)
            {
                _onProblem?.Invoke(problem);
            }

            _lastFailure = problem;
        }
    }

    private void Take(IReadOnlyList<ShaderCooked> results)
    {
        // A cook can land after the editor that asked for it has closed.
        if (_disposed)
        {
            return;
        }

        _lastFailure = null;
        foreach (var result in results)
        {
            Keep(result);
        }

        var present = results.Select(result => result.Path).ToHashSet();
        foreach (var gone in _good.Keys.Concat(_problems.Keys).Where(key => !present.Contains(key)).ToList())
        {
            _good.Remove(gone);
            _problems.Remove(gone);
        }

        Changed?.Invoke();
    }

    private void Keep(ShaderCooked result)
    {
        var key = result.Path;
        if (result.Package is { } package)
        {
            _good[key] = CookedSurface.FromParams(package, JsonText.Decode(result.Params ?? "[]"));
            _problems.Remove(key);
            return;
        }

        var problem = result.Problem ?? $"{key} did not cook.";
        if (_problems.GetValueOrDefault(key) != problem)
        {
            _onProblem?.Invoke(problem);
        }

        _problems[key] = problem;
    }

    private static string KeyOf(string asset) => asset.Replace(IoPath.DirectorySeparatorChar, '/');

    private string StampOfShaders()
    {
        var assets = IoPath.Join(ProjectDirectory, "assets");
        if (!IoDirectory.Exists(assets))
        {
            return "";
        }

        var parts = IoDirectory.EnumerateFiles(assets, "*", SearchOption.AllDirectories)
            .Where(ShaderCook.IsShaderFile)
            .Select(file => new FileInfo(file))
            .Select(info => $"{info.FullName}:{info.LastWriteTimeUtc.Ticks}:{(info.Exists ? info.Length : -1)}")
            .Order(StringComparer.Ordinal);
        return string.Join('\n', parts);
    }
}

namespace Orblit.Editor.Core;

using Orblit.Asset;
using IoDirectory = System.IO.Directory;
using IoPath = System.IO.Path;

/// <summary>
/// Where every asset in a project stands with the cook: already cooked for this machine, waiting to
/// be cooked, or known not to cook at all.
/// </summary>
/// <remarks>
/// The browser draws a mark from this beside each file. The mark answers, without leaving the
/// editor, the question somebody otherwise answers by running a build and reading the log. The
/// answer comes from the cook rather than from the editor's own idea of one. This builds a
/// <see cref="CookProject"/> over the project's real assets folder, with the real cache and the real
/// importers, and asks it. An editor that worked it out another way would drift from the build the
/// first time an importer changed how it resolves a setting, and a green mark on an asset a build
/// has everything still to do is worse than no mark at all.
/// </remarks>
public sealed class CookStatusIndex
{
    private static readonly IReadOnlyDictionary<string, CookState> None = new Dictionary<string, CookState>();

    private readonly object _gate = new();
    private volatile IReadOnlyDictionary<string, CookState> _states = None;
    private volatile string? _problem;
    private volatile bool _asking;
    private bool _askAgain;
    private volatile bool _held;

    public CookStatusIndex(string projectDirectory, string? cachePath = null, CookTarget? target = null)
    {
        ProjectDirectory = projectDirectory;
        CachePath = cachePath;
        Target = target ?? CookProject.CurrentCookTarget;
    }

    /// <summary>Said when the answer changes, and when asking starts and stops. May arrive on any thread.</summary>
    public event Action? Changed;

    /// <summary>The project folder, the same one the asset browser is rooted at.</summary>
    public string ProjectDirectory { get; }

    /// <summary>
    /// The machine being asked about, which is this one. A build for a phone would say something
    /// different about the same files, and saying that here would be a mark about a build nobody is
    /// running.
    /// </summary>
    public CookTarget Target { get; }

    /// <summary>
    /// Where cooked bytes are kept, or null for the machine's own cache. That is the ordinary case
    /// and the point: the editor should be looking at the same cache the build fills, not one of
    /// its own that always disagrees with it.
    /// </summary>
    public string? CachePath { get; }

    /// <summary>The folder the cook reads. Only what is under here is cooked at all, so a scene or a script gets no mark rather than a bad one.</summary>
    public string AssetsPath => IoPath.Join(ProjectDirectory, "assets");

    /// <summary>
    /// Where a build would write its bundle. Nothing is written while only asking, but naming the
    /// real place keeps this from being a lie the day the editor runs the cook itself.
    /// </summary>
    public string OutPath => IoPath.Join(ProjectDirectory, "build", "assets");

    /// <summary>
    /// Whether an answer is being worked out. The browser says so, because hashing a large project
    /// takes long enough to look like nothing happening.
    /// </summary>
    public bool Asking => _asking;

    /// <summary>
    /// What went wrong the last time this asked, or null. Kept rather than thrown: failing to
    /// describe the assets should never take the browser away from somebody who was using it to find
    /// a file.
    /// </summary>
    public string? Problem => _problem;

    /// <summary>Whether anything is known at all yet.</summary>
    public bool Ready => _states.Count > 0 || !_asking;

    /// <summary>
    /// How <paramref name="path"/> stands, or null when it is not something the cook looks at: a
    /// folder, a scene, anything outside the assets folder.
    /// </summary>
    public CookState? this[string path] => _states.TryGetValue(IoPath.GetFullPath(path), out var state) ? state : null;

    /// <summary>How many assets are in each state, for a one-line summary.</summary>
    public IReadOnlyDictionary<CookState, int> Counts =>
        _states.Values.GroupBy(state => state).ToDictionary(group => group.Key, group => group.Count());

    /// <summary>
    /// Asks again, off the interface thread.
    /// </summary>
    /// <remarks>
    /// Two calls at once do not make two cooks: the second sets a flag and the first goes round again
    /// when it lands. A file watcher firing while a large project is being hashed is the ordinary
    /// case, not the odd one, and the interesting answer is the one after the last change rather than
    /// the one after the first.
    /// </remarks>
    public async Task RefreshAsync()
    {
        if (!TryClaim())
        {
            return;
        }

        var stopped = false;
        try
        {
            Changed?.Invoke();
            while (!stopped)
            {
                stopped = await AskOnceAsync();
            }
        }
        finally
        {
            if (!stopped)
            {
                Release();
            }

            Changed?.Invoke();
        }
    }

    // Takes the right to ask. A call while another is asking leaves a note for it and takes nothing.
    private bool TryClaim()
    {
        lock (_gate)
        {
            if (_held)
            {
                return false;
            }

            if (_asking)
            {
                _askAgain = true;
                return false;
            }

            _asking = true;
            return true;
        }
    }

    // One round. True when nobody asked again meanwhile. The flag goes down under the lock that
    // reads the note, so a call that lands between the two is not dropped.
    private async Task<bool> AskOnceAsync()
    {
        lock (_gate)
        {
            _askAgain = false;
        }

        var (states, problem) = await Task.Run(AskAsync);
        _problem = problem;
        _states = states;
        lock (_gate)
        {
            _asking = _askAgain;
            return !_askAgain;
        }
    }

    private void Release()
    {
        lock (_gate)
        {
            _asking = false;
        }
    }

    /// <summary>
    /// Takes <paramref name="states"/> as the answer and stops asking. For a test that wants the
    /// panel's drawing of an answer, not a cook of a folder it never made.
    /// </summary>
    internal void Hold(IReadOnlyDictionary<string, CookState> states)
    {
        _held = true;
        _states = states.ToDictionary(entry => IoPath.GetFullPath(entry.Key), entry => entry.Value);
        Changed?.Invoke();
    }

    /// <summary>Forgets everything, for a project being closed.</summary>
    public void Clear()
    {
        _states = None;
        _held = false;
        _problem = null;
        Changed?.Invoke();
    }

    /// <summary>
    /// The question itself. Answering it reads and hashes every asset in the project and everything
    /// each one depends on. That is cheap as work goes and ruinous as an interruption: a folder of 4K
    /// textures is a second or two of hashing, and a second or two on the interface thread is an
    /// editor that has stopped. The caller runs this on the pool for that reason.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, CookState> States, string? Problem)> AskAsync()
    {
        if (!IoDirectory.Exists(AssetsPath))
        {
            // A project made before there were assets, or one somebody has emptied. Nothing to say
            // about no files, and nothing wrong either.
            return (None, null);
        }

        if (CookTargets.Find(Target.Name) is not { } target)
        {
            return (None, $"There is no cook target called {Target.Name}.");
        }

        try
        {
            var states = await new CookProject(AssetsPath, OutPath, target, CachePath).StatesOfAsync().ConfigureAwait(false);
            return (states.ToDictionary(entry => PathOf(entry.Key), entry => entry.Value), null);
        }
        catch (Exception error)
        {
            // Whatever an importer or a damaged file throws is a thing to say, not a reason to lose the browser.
            return (None, error.Message);
        }
    }

    // Ids are always separated by '/', whatever this machine spells a path with, so the parts are
    // split apart and rejoined rather than pasted on.
    private string PathOf(AssetId id) => IoPath.GetFullPath(IoPath.Join([AssetsPath, .. id.ToString().Split('/')]));
}

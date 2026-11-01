namespace Orblit.Editor.Core;

public enum LauncherView
{
    Projects,
    Create,
}

/// <summary>
/// What the launcher is showing and doing, with no control in it.
/// </summary>
/// <remarks>
/// A launcher rather than an empty editor, because an editor with no project open has nothing true to
/// display. Every panel would be an empty state, and a screen full of empty states is worse than a
/// screen that asks one question.
/// <para>
/// shortcut: files are read and written on the calling thread, so a slow disk holds the first frame.
/// Move it off the UI thread when a project list is long enough to notice.
/// </para>
/// </remarks>
public sealed class LauncherSession
{
    private readonly ProjectStore _store;
    private readonly string? _home;
    private readonly TimeProvider _time;
    private string _query = "";

    public LauncherSession(ProjectStore store, string? home, TimeProvider? time = null)
    {
        _store = store;
        _home = home;
        _time = time ?? TimeProvider.System;
        Recents = store.Recents();
    }

    /// <summary>Raised when the view, the chosen template, the search or the list of projects changes.</summary>
    public event Action? Changed;

    /// <summary>Raised once a project is chosen or created. The shell takes it from here.</summary>
    public event Action<Project>? Opened;

    /// <summary>Raised with a title and a detail when something the person asked for could not be done.</summary>
    public event Action<string, string>? Complained;

    public LauncherView View { get; private set; }

    /// <summary>The template a new project starts from.</summary>
    public ProjectTemplate Template { get; private set; } = ProjectTemplate.Scene;

    /// <summary>Every project the editor has opened, most recent first.</summary>
    public IReadOnlyList<Project> Recents { get; private set; }

    /// <summary>The recent projects whose name has what was searched for.</summary>
    public IReadOnlyList<Project> Matching =>
        [.. Recents.Where(project => project.Name.ToLowerInvariant().Contains(_query, StringComparison.Ordinal))];

    /// <summary>
    /// Where a new project goes unless somebody says otherwise. Somewhere real rather than empty, so
    /// the common case is one click. Documents rather than home, because a project is a document.
    /// </summary>
    public string DefaultLocation => Path.Combine(_home ?? Directory.GetCurrentDirectory(), "Documents", "Orblit");

    public void Show(LauncherView view)
    {
        View = view;
        Changed?.Invoke();
    }

    public void StartFrom(ProjectTemplate template)
    {
        Template = template;
        Show(LauncherView.Create);
    }

    public void Search(string text)
    {
        _query = text.Trim().ToLowerInvariant();
        Changed?.Invoke();
    }

    public string PathOf(Project project) => project.DisplayPath(_home);

    public string OpenedAgo(Project project) => Elapsed.Ago(project.LastOpened, _time.GetLocalNow().DateTime);

    public void Open(Project project)
    {
        if (!project.Exists)
        {
            Complained?.Invoke("That folder has moved", $"{PathOf(project)} no longer holds a project.");
            return;
        }

        Enter(project);
    }

    public void OpenFolder(string directory)
    {
        if (ProjectStore.Open(directory) is not { } project)
        {
            Complained?.Invoke("No Orblit project there", $"That folder has no {Project.FileName} in it.");
            return;
        }

        Enter(project);
    }

    public void Forget(Project project)
    {
        _store.Forget(project);
        Recents = _store.Recents();
        Changed?.Invoke();
    }

    /// <summary>Makes a project from <see cref="Template"/>. A name that is only space is refused.</summary>
    public void Create(string name, string location)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Complained?.Invoke("A project needs a name", "Give it one and try again.");
            return;
        }

        Project made;
        try
        {
            made = _store.Create(name.Trim(), location, Template);
        }
        catch (InvalidOperationException error)
        {
            Complained?.Invoke("Could not create it", error.Message);
            return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Complained?.Invoke("Could not write there", error.Message);
            return;
        }

        // Outside the try, so a fault in whoever opens the project is not told to the person as a
        // folder that could not be written.
        Opened?.Invoke(made);
    }

    private void Enter(Project project)
    {
        _store.Remember(project);
        Opened?.Invoke(project);
    }
}

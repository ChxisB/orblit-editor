namespace Orblit.Editor.Core;

using IoFile = System.IO.File;
using Orblit.Motion;

// Playing the scene: its animation and its cutscenes run on a copy, and the game view shows that copy.
public sealed partial class EditorSession
{
    private const string CutsceneFolder = "cutscenes";

    private static readonly PlaybackFormat<ClipDocument> ClipFormat =
        new("clip", text => ClipDocument.Decode(text).Clip, error => error is ClipFormatException);

    private static readonly PlaybackFormat<CutsceneDocument> CutsceneFormat =
        new("cutscene", text => CutsceneDocument.Decode(text).Cutscene, error => error is CutsceneFormatException);

    /// <summary>
    /// The scene as it plays, or null when it is stopped. Kept while paused, so Play goes on from where
    /// it stopped.
    /// </summary>
    public ScenePlayback? Playback { get; private set; }

    /// <summary>Whether time is passing in <see cref="Playback"/>.</summary>
    public bool Playing { get; private set; }

    /// <summary>Raised when play starts, pauses or stops, and each time the playback moves on.</summary>
    public event Action? PlaybackChanged;

    /// <summary>
    /// Pauses what is playing, or starts the loaded scene and shows the game view. Does nothing when
    /// no scene is loaded.
    /// </summary>
    public void TogglePlayback()
    {
        if (Playing)
        {
            Playing = false;
            PlaybackChanged?.Invoke();
            return;
        }

        if (Workspace.Loaded?.Scene is null)
        {
            return;
        }

        Clips.Playing = false;
        Cutscenes.Playing = false;
        Playback ??= AtRest(() => new ScenePlayback(Workspace, ClipForPlayback, CutscenesForPlayback()));
        Playing = true;
        OpenPanel(PanelKind.Game);
        PlaybackChanged?.Invoke();
    }

    /// <summary>Stops the scene and puts the game view back to the scene as it is edited.</summary>
    public void StopPlayback()
    {
        if (Playback is null)
        {
            return;
        }

        Playback = null;
        Playing = false;
        PlaybackChanged?.Invoke();
    }

    /// <summary>Lets <paramref name="seconds"/> pass in the scene that is playing. Does nothing while it is paused.</summary>
    public void AdvancePlayback(double seconds)
    {
        if (Playing && Playback is { } running)
        {
            running.Advance(seconds);
            PlaybackChanged?.Invoke();
        }
    }

    // An edit made while the scene plays, or sits paused, shows in the copy. Otherwise the game view
    // goes on showing the scene as it was when Play was pressed.
    private void FollowEditsWhilePlaying()
    {
        if (Playback is { } running && ReferenceEquals(running.Source, Workspace.Loaded?.Scene))
        {
            running.Follow();
            PlaybackChanged?.Invoke();
        }
    }

    // A copy of another scene is not what is being edited, so it stops when the loaded scene changes.
    private void StopPlayingAnotherScene()
    {
        if (Playback is { } running && !ReferenceEquals(running.Source, Workspace.Loaded?.Scene))
        {
            StopPlayback();
        }
    }

    // The project folder as a path inside it begins, with no separator on the end.
    private string ProjectFolder => Path.TrimEndingDirectorySeparator(Path.GetFullPath(Workspace.ProjectDirectory));

    // A clip the playing scene names. One that is open is as it is now and not as last saved. A scene
    // is a file somebody may have been sent, so a name that leaves the project is not followed.
    private ClipDocument? ClipForPlayback(string relative)
    {
        var project = ProjectFolder;
        var path = Path.GetFullPath(Path.Combine(project, relative));
        if (!path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            Say($"{relative} is outside the project, so it was not played.", SayLevel.Warning);
            return null;
        }

        return Clips[path]?.Clip ?? ReadForPlayback(path, relative, ClipFormat);
    }

    // The project's cutscenes by name, for a mark to start as it would in the game.
    private Dictionary<string, CutsceneDocument> CutscenesForPlayback()
    {
        var byName = new Dictionary<string, CutsceneDocument>();
        var folder = Path.Combine(ProjectFolder, CutsceneFolder);
        if (!Directory.Exists(folder))
        {
            return byName;
        }

        var files = Directory.EnumerateFiles(folder).Where(file => Path.GetExtension(file) == CutsceneDocument.Extension);
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            var cutscene = Cutscenes[file]?.Cutscene ?? ReadForPlayback(file, Path.GetFileName(file), CutsceneFormat);
            if (cutscene is not null && !byName.TryAdd(cutscene.Name, cutscene))
            {
                Say($"Two cutscenes are called {cutscene.Name}. The game will not load both, so rename one.", SayLevel.Warning);
            }
        }

        return byName;
    }

    // Reads a file the playing scene needs and says why when it cannot be had.
    private T? ReadForPlayback<T>(string path, string name, PlaybackFormat<T> format)
        where T : class
    {
        try
        {
            return format.Decode(IoFile.ReadAllText(path));
        }
        catch (Exception error) when (format.IsFormatError(error))
        {
            Say($"{name} is not a {format.Noun}: {error.Message}", SayLevel.Error);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Say($"{name} could not be read: {error.Message}", SayLevel.Error);
        }

        return null;
    }

    // What a file the playing scene needs is called, how it is read, and which errors mean it is damaged.
    private sealed record PlaybackFormat<T>(string Noun, Func<string, T> Decode, Func<Exception, bool> IsFormatError);
}

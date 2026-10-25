namespace Orblit.Editor.Core;

using Orblit.Filament;
using Orblit.Motion;
using Orblit.Sequence;
using Doc = Orblit.Scene;

/// <summary>
/// Animation on a copy of the scene, so playing never edits the document.
/// </summary>
/// <remarks>
/// A mark named after one of <see cref="Cutscenes"/> starts it, as it does in the game. While it runs
/// it moves the loaded scene and <see cref="Camera"/> looks through its shots.
/// </remarks>
public sealed class ScenePlayback
{
    private readonly List<PlayingClip> _clips = [];
    private PlayingCutscene? _cutscene;

    public ScenePlayback(
        Workspace editing,
        Func<string, ClipDocument?> read,
        IReadOnlyDictionary<string, CutsceneDocument>? cutscenes = null)
    {
        Source = editing.Loaded?.Scene;
        Workspace = new Workspace(editing.ProjectDirectory);
        Cutscenes = cutscenes ?? new Dictionary<string, CutsceneDocument>();
        if (Source is { } original)
        {
            var scene = Copy(original);
            Workspace.Add(new SceneEntry("preview", scene.Name, scene: scene));
            Load(scene, read);
        }

        Workspace.SharedEntry.Scene = Copy(editing.Shared);
        Load(Workspace.Shared, read);
        Advance(0);
    }

    public EditorScene? Source { get; }

    public Workspace Workspace { get; }

    /// <summary>The cutscenes a mark can start, by name.</summary>
    public IReadOnlyDictionary<string, CutsceneDocument> Cutscenes { get; }

    /// <summary>
    /// What the game looks through now: the running cutscene's shots, or null for the scene's own
    /// camera.
    /// </summary>
    public RenderCamera? Camera =>
        _cutscene is { } running && Workspace.Loaded?.Scene is { } scene
            ? SceneCameras.CutsceneCamera(scene, running.Cutscene, running.Player.At)
            : null;

    public void Advance(double seconds)
    {
        var marks = new List<Mark>();
        foreach (var clip in _clips)
        {
            marks.AddRange(clip.Player.Advance(seconds).Marks);
            clip.Preview.Pose(clip.Scene, clip.Player.Released ? null : clip.Player.Clip, clip.Owner, clip.Player.At);
        }

        AdvanceCutscene(seconds, marks);
    }

    private static EditorScene Copy(EditorScene scene) => SceneDocument.Decode(SceneDocument.Encode(scene)).Scene;

    private void Load(EditorScene scene, Func<string, ClipDocument?> read)
    {
        foreach (var target in scene.Objects)
        {
            if (target.Components.GetValueOrDefault(Doc.SceneComponents.Motion) is not Doc.MotionComponent motion
                || motion.Autoplay is not { } path
                || !motion.Clips.Contains(path)
                || read(path) is not { } clip)
            {
                continue;
            }

            var player = new ClipPlayer(clip);
            player.Play();
            _clips.Add(new PlayingClip(scene, target.Id, player));
        }
    }

    // Plays the running cutscene on, then starts the one named by the first mark passed, if any. A
    // cutscene's own marks can start the next.
    private void AdvanceCutscene(double seconds, List<Mark> marks)
    {
        if (Workspace.Loaded?.Scene is not { } scene)
        {
            return;
        }

        if (_cutscene is { } running)
        {
            marks.AddRange(running.Player.Advance(seconds).Marks);
            running.Show(scene);
            if (running.Player.Finished)
            {
                End(running, scene);
            }
        }

        foreach (var mark in marks)
        {
            if (Cutscenes.GetValueOrDefault(mark.Name) is { } cutscene)
            {
                Start(cutscene, scene);
                return;
            }
        }
    }

    private void Start(CutsceneDocument cutscene, EditorScene scene)
    {
        if (_cutscene is { } running)
        {
            End(running, scene);
        }

        _cutscene = new PlayingCutscene(cutscene);
        _cutscene.Show(scene);
    }

    // One that holds leaves the scene where it put it, and one that releases puts back everything it
    // moved.
    private void End(PlayingCutscene running, EditorScene scene)
    {
        _cutscene = null;
        if (running.Player.WhenDone == WhenDone.Release)
        {
            running.Preview.Lift(scene);
        }
    }

    private sealed class PlayingClip(EditorScene scene, string owner, ClipPlayer player)
    {
        public EditorScene Scene { get; } = scene;

        public string Owner { get; } = owner;

        public ClipPlayer Player { get; } = player;

        public ClipPreview Preview { get; } = new();
    }

    private sealed class PlayingCutscene
    {
        public PlayingCutscene(CutsceneDocument cutscene)
        {
            Cutscene = cutscene;
            Player = new ClipPlayer(cutscene.Motion);
            Player.Play();
        }

        public CutsceneDocument Cutscene { get; }

        // Plays the keys and the marks. The shots are read from Cutscene at its playhead.
        public ClipPlayer Player { get; }

        public ClipPreview Preview { get; } = new();

        public void Show(EditorScene scene) => Preview.PoseAll(scene, Player.Clip, Player.At);
    }
}

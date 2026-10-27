namespace Orblit.Editor.Core;

using Orblit.Stage;
using Orblit.Terrain;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using Ground = Orblit.Terrain.Terrain;
using Population = Orblit.Filament.Population;
using RenderTerrain = Orblit.Filament.RenderTerrain;

/// <summary>
/// The terrains being edited, and the brush they are edited with.
/// </summary>
/// <remarks>
/// A model rather than state in a panel: the tool shelf, the brush panel, the ring on the ground and
/// the inspector all read the same brush, and a terrain has to stay open whichever of them is showing.
/// <para>
/// Terrains are opened the first time a scene asks to draw one and stay open, which is what lets a
/// stroke be undone after the object that showed it was deleted and brought back.
/// </para>
/// </remarks>
/// <param name="history">The undo stack every edit goes through.</param>
/// <param name="projectRoot">What a terrain's file is relative to.</param>
/// <param name="onProblem">
/// Told what could not be read, drawn or written, once each. Can be called while a frame is being
/// built, so it must not build anything itself.
/// </param>
public sealed class TerrainBench(History history, string projectRoot, Action<string>? onProblem = null)
{
    private readonly Dictionary<string, OpenTerrain> _open = [];

    // Files that could not be read. Not tried again, so a broken file is one message and not one a frame.
    private readonly HashSet<string> _unreadable = [];

    private readonly HashSet<string> _said = [];

    // Where each terrain's scatter stands, kept between frames so that a stroke places again only the
    // regions it touched, and what each last drew, against the placer's revision when it did.
    private readonly Dictionary<string, ScatterPlacer> _placers = [];
    private readonly Dictionary<string, (long Revision, IReadOnlyList<Population> Drawn)> _scattered = [];

    private BrushTool _tool = BrushTool.Raise;
    private Brush _brush = new();
    private int _set;
    private GroundColour _colour = GroundColour.Of(red: 150, green: 124, blue: 96);
    private double _roughness = 0.5;
    private Vector3? _hover;

    public event Action? Changed;

    // Apart from Changed, so a pointer moving over the ground redraws the ring and nothing else.
    public event Action? HoverChanged;

    public OpenTerrain? this[string file] => _open.GetValueOrDefault(file);

    public BrushTool Tool
    {
        get => _tool;
        set => Change(ref _tool, value);
    }

    /// <summary>The settings every tool shares.</summary>
    public Brush Brush
    {
        get => _brush;
        set => Change(ref _brush, value);
    }

    /// <summary>The set <see cref="BrushTool.Cover"/> lays, by its index in the terrain's sets.</summary>
    public int Set
    {
        get => _set;
        set => Change(ref _set, value);
    }

    /// <summary>The tint <see cref="BrushTool.Colour"/> paints. Its roughness is not painted with it.</summary>
    public GroundColour Colour
    {
        get => _colour;
        set => Change(ref _colour, value);
    }

    /// <summary>The nudge <see cref="BrushTool.Roughness"/> paints, from -1 to +1.</summary>
    public double Roughness
    {
        get => _roughness;
        set => Change(ref _roughness, value);
    }

    /// <summary>Where the pointer meets the ground, or null when it is not over any.</summary>
    public Vector3? Hover
    {
        get => _hover;
        set
        {
            if (Equals(_hover, value))
            {
                return;
            }

            _hover = value;
            HoverChanged?.Invoke();
        }
    }

    /// <summary>Bumped each time the sets change, so the renderer takes their pictures again.</summary>
    public int PicturesRevision { get; private set; }

    public bool AnyUnsaved => _open.Values.Any(IsUnsaved);

    /// <summary>
    /// A stroke across <paramref name="open"/> with the brush as it is now.
    /// </summary>
    /// <param name="open">The terrain to stroke.</param>
    /// <param name="invert">Turns the tool round, which is what holding a modifier does.</param>
    /// <param name="seed">What the brush's jitter is drawn from. Random when not given.</param>
    public TerrainStroke StrokeOn(OpenTerrain open, bool invert = false, int? seed = null)
    {
        var sets = open.Terrain.Sets.Count;
        return new TerrainStroke(
            open.Terrain,
            tool: _tool,
            brush: _brush,
            invert: invert,
            set: sets == 0 ? 0 : Math.Clamp(_set, 0, sets - 1),
            colour: _colour,
            roughness: _roughness,
            seed: seed ?? Random.Shared.Next(1 << 30));
    }

    /// <summary>
    /// The terrain at <paramref name="file"/>, read now if it is not open, or null when there is none
    /// or it cannot be read. Says what could not be read, once.
    /// </summary>
    public OpenTerrain? TerrainFor(string? file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return null;
        }

        if (_open.TryGetValue(file, out var had))
        {
            return had;
        }

        if (_unreadable.Contains(file))
        {
            return null;
        }

        try
        {
            return _open[file] = Read(file);
        }
        catch (Exception error) when (error is TerrainFormatException or IOException or UnauthorizedAccessException)
        {
            _unreadable.Add(file);
            Report($"The terrain {file} could not be read: {error.Message}");
            return null;
        }
    }

    /// <summary>Writes <paramref name="terrain"/> as a new file at <paramref name="file"/> and opens it, or says why it could not be written.</summary>
    public string? Create(string file, Ground terrain)
    {
        var open = new OpenTerrain(file, Path.Combine(projectRoot, file), terrain);
        if (Write(open) is { } problem)
        {
            return $"{open.Name} could not be written: {problem}";
        }

        _open[file] = open;
        _unreadable.Remove(file);
        Notify();
        return null;
    }

    public bool IsUnsaved(OpenTerrain open) => history.StampFor(open.Id) != open.SavedStamp;

    /// <summary>Writes every terrain with changes, and says which could not be written.</summary>
    public IReadOnlyList<string> SaveAll() => [.. _open.Values.Where(IsUnsaved).ToList().Select(Save).OfType<string>()];

    /// <summary>Writes <paramref name="open"/>, or says why it could not be.</summary>
    public string? Save(OpenTerrain open)
    {
        if (Write(open) is { } problem)
        {
            return $"{open.Name} could not be saved: {problem}";
        }

        open.SavedStamp = history.StampFor(open.Id);
        Notify();
        return null;
    }

    /// <summary>
    /// Changes <paramref name="open"/>'s settings through the undo stack. A <paramref name="gesture"/>
    /// ties a slider's worth of these into one step.
    /// </summary>
    public void Edit(
        OpenTerrain open,
        string label,
        Func<TerrainSettings, TerrainSettings> change,
        object? gesture = null)
    {
        var from = TerrainSettings.Of(open.Terrain);
        history.Run(new TerrainSettingsEdit(this, open.File, label, from, change(from), gesture));
    }

    /// <summary>
    /// The terrains <paramref name="objects"/> put in the scene, as the renderer takes them.
    /// </summary>
    /// <remarks>
    /// Pictures are not asked for yet, so every set draws as the plain one.
    /// </remarks>
    public IReadOnlyList<RenderTerrain> RenderFor(IEnumerable<SceneObject> objects)
    {
        var drawn = new List<RenderTerrain>();
        foreach (var (open, component) in GroundIn(objects))
        {
            try
            {
                drawn.Add(TerrainView.From(
                    open.Terrain,
                    open.Key,
                    new TerrainViewOptions
                    {
                        PicturesRevision = PicturesRevision,
                        CastShadows = component.CastShadows,
                        ReceiveShadows = component.ReceiveShadows,
                    }));
            }
            catch (ArgumentException error)
            {
                Report($"{open.Name} cannot be drawn: {error.Message}");
            }
        }

        return drawn;
    }

    /// <summary>
    /// What the terrains <paramref name="objects"/> put in the scene have scattered over them, as the
    /// renderer takes it: a population a region and layer.
    /// </summary>
    /// <remarks>
    /// Blocks only. A layer that names a model is left out here, since drawing it means resolving its
    /// mesh and material the way the scene's own objects are, and nothing does that for a terrain yet.
    /// </remarks>
    public IReadOnlyList<Population> ScatterFor(IEnumerable<SceneObject> objects)
    {
        var drawn = new List<Population>();
        foreach (var (open, _) in GroundIn(objects))
        {
            if (!_placers.TryGetValue(open.File, out var placer))
            {
                placer = _placers[open.File] = new ScatterPlacer();
            }

            placer.Update(open.Terrain);
            if (!_scattered.TryGetValue(open.File, out var last) || last.Revision != placer.Revision)
            {
                var blocks = ScatterView.From(placer, open.Key, new ScatterViewOptions { Models = false }).Populations;
                _scattered[open.File] = last = (placer.Revision, blocks);
            }

            drawn.AddRange(last.Drawn);
        }

        return drawn;
    }

    /// <summary>Where a settings edit puts a terrain's settings.</summary>
    internal void PutSettings(string file, TerrainSettings settings)
    {
        if (this[file]?.Terrain is not { } terrain)
        {
            return;
        }

        terrain.Sets.Clear();
        terrain.Sets.AddRange(settings.Sets);
        terrain.AutoCover = settings.AutoCover;
        terrain.BlendSharpness = settings.BlendSharpness;

        // A set's pictures may be different ones now, and the renderer takes pictures again only when told to.
        PicturesRevision++;
        Notify();
    }

    /// <summary>Where a region edit adds or takes away a region.</summary>
    internal void PutRegion(string file, TerrainRegion region, bool present)
    {
        if (this[file]?.Terrain is not { } terrain)
        {
            return;
        }

        if (present)
        {
            terrain.PutRegion(region);

            // Touched so the renderer, which let it go, takes it again.
            region.Touch();
        }
        else
        {
            terrain.RemoveRegion(region.Key);
        }

        Notify();
    }

    // Writes the regions that changed, then the settings that list them, then takes away the files of
    // regions that are gone. In that order so a save cut short leaves a settings file whose regions are
    // all there.
    private static string? Write(OpenTerrain open)
    {
        var written = new Dictionary<RegionKey, long>();
        try
        {
            Directory.CreateDirectory(open.Folder);
            foreach (var region in open.Terrain.Regions)
            {
                if (!open.SavedRevisions.TryGetValue(region.Key, out var saved) || saved != region.Revision)
                {
                    File.WriteAllBytes(Path.Combine(open.Folder, region.Key.FileName), region.Encode());
                }

                written[region.Key] = region.Revision;
            }

            File.WriteAllText(open.Path, open.Terrain.Encode());
            foreach (var gone in open.SavedRevisions.Keys.Where(key => !written.ContainsKey(key)))
            {
                File.Delete(Path.Combine(open.Folder, gone.FileName));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }

        open.SavedRevisions.Clear();
        foreach (var (key, revision) in written)
        {
            open.SavedRevisions[key] = revision;
        }

        return null;
    }

    private OpenTerrain Read(string file)
    {
        var path = Path.Combine(projectRoot, file);
        var load = Ground.Decode(File.ReadAllText(path));
        var open = new OpenTerrain(file, path, load.Terrain);
        var problems = load.Problems.Select(problem => $"{open.Name}: {problem}").ToList();
        foreach (var key in load.Regions)
        {
            ReadRegion(open, key, problems);
        }

        problems.ForEach(Report);
        return open;
    }

    // Reads one region into the terrain, or adds to problems why it was left out.
    private static void ReadRegion(OpenTerrain open, RegionKey key, List<string> problems)
    {
        RegionLoad load;
        try
        {
            load = TerrainRegion.Decode(File.ReadAllBytes(Path.Combine(open.Folder, key.FileName)));
        }
        catch (Exception error) when (error is RegionFormatException or IOException or UnauthorizedAccessException)
        {
            problems.Add($"{open.Name}: Region {key.FileName} was left out: {error.Message}");
            return;
        }

        var region = load.Region;
        if (region.Key != key || region.Size != open.Terrain.RegionSize)
        {
            problems.Add(
                $"{open.Name}: Region {key.FileName} was left out: it is not the region its name says, or not the size of the terrain's.");
            return;
        }

        open.Terrain.PutRegion(region);
        open.SavedRevisions[key] = region.Revision;
        problems.AddRange(load.Problems.Select(problem => $"{open.Name}, {key.FileName}: {problem}"));
    }

    // One terrain per file however many objects name it: two copies of the same ground would be drawn
    // into each other.
    private IEnumerable<(OpenTerrain Open, Doc.TerrainComponent Component)> GroundIn(IEnumerable<SceneObject> objects)
    {
        var seen = new HashSet<string>();
        foreach (var item in objects)
        {
            if (PhysicsComponents.Of<Doc.TerrainComponent>(item, Doc.SceneComponents.Terrain) is { File: { } file } component
                && seen.Add(file)
                && TerrainFor(file) is { } open)
            {
                yield return (open, component);
            }
        }
    }

    private void Report(string message)
    {
        if (_said.Add(message))
        {
            onProblem?.Invoke(message);
        }
    }

    private void Change<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        Notify();
    }

    private void Notify() => Changed?.Invoke();
}

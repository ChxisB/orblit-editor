namespace Orblit.Editor.Core;

using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

// Every change the terrain mode makes, as steps on the undo stack. None of them holds a copy of a
// region: a stroke keeps the tiles it touched, a settings edit the settings, and taking a region away
// keeps that one region so putting it back is the same object.

/// <summary>A terrain's settings, apart from its regions.</summary>
public sealed record TerrainSettings(IReadOnlyList<TerrainSet> Sets, AutoCover AutoCover, double BlendSharpness)
{
    public static TerrainSettings Of(Ground terrain) => new([.. terrain.Sets], terrain.AutoCover, terrain.BlendSharpness);
}

/// <summary>
/// One move of the brush: where in the world it was carried to, the drag it belongs to and what the
/// step is called next to Undo.
/// </summary>
/// <param name="X">Metres along x.</param>
/// <param name="Z">Metres along z.</param>
/// <param name="Gesture">Shared by every move of one drag, so a stroke is one step to undo.</param>
/// <param name="Label">What the step is called.</param>
public readonly record struct BrushMove(double X, double Z, object Gesture, string Label);

/// <summary>
/// One move of the brush across a terrain.
/// </summary>
/// <remarks>
/// The first time it is applied it moves the stroke, which writes the ground and says which tiles it
/// touched. After that it writes those tiles back. Every move of one drag shares a gesture, so a
/// stroke is one step to undo however many frames it took, and what that step keeps is the tiles the
/// whole stroke touched as they were before it and after.
/// </remarks>
public sealed class TerrainStrokeStep(TerrainBench bench, string file, TerrainStroke stroke, BrushMove move) : EditorCommand
{
    /// <summary>What the stroke has changed so far, or null before it is applied.</summary>
    public TerrainPatch? Patch { get; private set; }

    public override string SceneId => OpenTerrain.IdFor(file);

    public override string Label => move.Label;

    public override object? MergeKey => (file, move.Gesture);

    // The ground is not something any panel but a view shows, so a stroke is redrawn and nothing else
    // is rebuilt.
    public override bool OnlyMoves => true;

    public override void Apply(ISceneHost host)
    {
        if (bench[file]?.Terrain is not { } terrain)
        {
            return;
        }

        if (Patch is { } patch)
        {
            patch.Apply(terrain);
        }
        else
        {
            Patch = stroke.MoveTo(move.X, move.Z);
        }
    }

    public override void Revert(ISceneHost host)
    {
        if (bench[file]?.Terrain is { } terrain)
        {
            Patch?.Revert(terrain);
        }
    }

    public override void Absorb(EditorCommand later)
    {
        if (later is TerrainStrokeStep { Patch: { } theirs })
        {
            Patch = Patch?.FollowedBy(theirs) ?? theirs;
        }
    }
}

/// <summary>A change to a terrain's sets, its automatic cover or its blend.</summary>
internal sealed class TerrainSettingsEdit : EditorCommand
{
    private readonly TerrainBench _bench;
    private readonly string _file;
    private readonly TerrainSettings _from;
    private readonly object? _gesture;

    // Not readonly: a merged slider drag rewrites where it ends up.
    private TerrainSettings _to;

    // F2: an edit is a terrain's settings before and after, where it lives, what it is called and what ties it to a drag.
    public TerrainSettingsEdit(
        TerrainBench bench,
        string file,
        string label,
        TerrainSettings from,
        TerrainSettings to,
        object? gesture)
    {
        _bench = bench;
        _file = file;
        Label = label;
        _from = from;
        _to = to;
        _gesture = gesture;
    }

    public override string SceneId => OpenTerrain.IdFor(_file);

    public override string Label { get; }

    // A gesture ties a drag's worth of these together, and none ties nothing.
    public override object? MergeKey => _gesture is null ? null : (_file, _gesture);

    public override void Apply(ISceneHost host) => _bench.PutSettings(_file, _to);

    public override void Revert(ISceneHost host) => _bench.PutSettings(_file, _from);

    public override void Absorb(EditorCommand later)
    {
        if (later is TerrainSettingsEdit next)
        {
            _to = next._to;
        }
    }
}

/// <summary>
/// A region of ground added to a terrain, or taken away from one.
/// </summary>
/// <remarks>
/// Holds the region itself, so taking one away and undoing it puts back the ground that was there
/// and not a flat square.
/// </remarks>
/// <param name="bench">The terrains being edited.</param>
/// <param name="file">The terrain's project-relative file.</param>
/// <param name="region">The region to add or take away.</param>
/// <param name="adding">Whether this adds <paramref name="region"/>. False takes it away.</param>
public sealed class TerrainRegionEdit(TerrainBench bench, string file, TerrainRegion region, bool adding) : EditorCommand
{
    public override string SceneId => OpenTerrain.IdFor(file);

    public override string Label =>
        $"{(adding ? "Add ground to" : "Take ground from")} {OpenTerrain.NameOf(file)}";

    public override void Apply(ISceneHost host) => bench.PutRegion(file, region, present: adding);

    public override void Revert(ISceneHost host) => bench.PutRegion(file, region, present: !adding);
}

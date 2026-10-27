namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.Editor.Core;
using Orblit.Terrain;
using Orblit.VectorMath;

/// <summary>
/// What the brush does with the pointer in a scene view.
///
/// Takes a gesture only over the ground, so a click beside the terrain still selects what was clicked,
/// and the camera is still the right button and the wheel. A drag that leaves the ground carries on at
/// the height it was last at, so a stroke across a hole or off an edge does not stop dead.
/// </summary>
/// <param name="bench">Holds the brush, and is told where the pointer is.</param>
/// <param name="history">Takes each move of a stroke.</param>
/// <param name="target">
/// The terrain a brush works on, asked again for each gesture since which one it is follows the
/// selection.
/// </param>
public sealed class TerrainBrushInput(TerrainBench bench, History history, Func<OpenTerrain?> target)
{
    private OpenTerrain? _on;
    private TerrainStroke? _stroke;
    private object? _gesture;
    private Vector3? _last;

    /// <summary>Takes a gesture over the ground, and says whether it did.</summary>
    public bool Handle(ViewportGesture gesture) => gesture.Phase switch
    {
        ViewportPhase.Hover => Hover(gesture),
        ViewportPhase.Leave => Leave(),
        ViewportPhase.Tap or ViewportPhase.DragStart => Begin(gesture),
        ViewportPhase.DragUpdate => Continue(gesture),
        ViewportPhase.DragEnd or ViewportPhase.DragCancel => Finish(),
        _ => throw new UnreachableException(),
    };

    private bool Hover(ViewportGesture gesture)
    {
        var hit = HitOn(gesture, target());
        bench.Hover = hit;
        return hit is not null;
    }

    private bool Leave()
    {
        bench.Hover = null;
        return false;
    }

    private bool Begin(ViewportGesture gesture)
    {
        var open = target();
        var hit = HitOn(gesture, open);
        if (open is null || hit is null)
        {
            return false;
        }

        _on = open;
        _stroke = bench.StrokeOn(open, invert: gesture.Add);
        _gesture = new object();
        bench.Hover = hit;
        Move(hit);
        if (gesture.Phase == ViewportPhase.Tap)
        {
            End();
        }

        return true;
    }

    private bool Continue(ViewportGesture gesture)
    {
        if (_on is null)
        {
            return false;
        }

        var hit = HitOn(gesture, _on) ?? Level(gesture);
        bench.Hover = hit;
        if (hit is not null)
        {
            Move(hit);
        }

        return true;
    }

    private bool Finish()
    {
        if (_on is null)
        {
            return false;
        }

        End();
        return true;
    }

    // Only called with a drag in progress, which is what sets all three.
    private void Move(Vector3 at)
    {
        var open = _on ?? throw new InvalidOperationException("No stroke is in progress.");
        var stroke = _stroke ?? throw new InvalidOperationException("No stroke is in progress.");
        var gesture = _gesture ?? throw new InvalidOperationException("No stroke is in progress.");
        _last = at;
        history.Run(new TerrainStrokeStep(
            bench,
            open.File,
            stroke,
            new BrushMove(at.X, at.Z, gesture, BrushWords.StrokeLabel(stroke.Tool, open.Name))));
    }

    private void End()
    {
        history.Seal();
        _on = null;
        _stroke = null;
        _gesture = null;
        _last = null;
    }

    private static Vector3? HitOn(ViewportGesture gesture, OpenTerrain? open)
    {
        if (open is null || gesture.Projection is not { } projection)
        {
            return null;
        }

        var ray = projection.RayThrough(gesture.At);
        return open.Terrain.Raycast(ray.Origin, ray.Direction);
    }

    // Where the pointer meets the level the stroke was last at, for a drag that has gone past the ground.
    private Vector3? Level(ViewportGesture gesture)
    {
        if (_last is null || gesture.Projection is not { } projection)
        {
            return null;
        }

        return projection.RayThrough(gesture.At).Meets(_last, new Vector3(0, 1, 0));
    }
}

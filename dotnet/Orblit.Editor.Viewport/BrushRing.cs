namespace Orblit.Editor.Viewport;

using Orblit.Terrain;
using Orblit.VectorMath;
using Ground = Orblit.Terrain.Terrain;

/// <summary>The brush's circle laid on the ground, as lines on the screen.</summary>
public static class BrushRing
{
    private const int Steps = 64;

    /// <summary>
    /// The circle of <paramref name="radius"/> round <paramref name="centre"/>, each point at the height
    /// of the ground under it, so the ring follows a hill and does not cut through it. A piece with an end
    /// behind the camera is left out rather than drawn across the whole view.
    /// </summary>
    public static List<(Offset From, Offset To)> Segments(Ground ground, Vector3 centre, double radius, ViewportProjection projection)
    {
        var points = new List<Offset?>(Steps);
        for (var step = 0; step < Steps; step++)
        {
            var angle = step * 2 * Math.PI / Steps;
            var x = centre.X + (Math.Cos(angle) * radius);
            var z = centre.Z + (Math.Sin(angle) * radius);
            points.Add(projection.Project(new Vector3(x, ground.HeightAt(x, z) ?? centre.Y, z)));
        }

        var segments = new List<(Offset, Offset)>();
        for (var step = 0; step < Steps; step++)
        {
            if (points[step] is { } from && points[(step + 1) % Steps] is { } to)
            {
                segments.Add((from, to));
            }
        }

        return segments;
    }

    /// <summary>
    /// The radius inside which the brush is at full strength, or null when it is too close to either
    /// edge to be worth a second ring.
    /// </summary>
    public static double? CoreRadius(Brush brush)
    {
        var core = brush.Radius * (1 - brush.Falloff);
        return core > brush.Radius * 0.05 && core < brush.Radius * 0.95 ? core : null;
    }
}

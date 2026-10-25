namespace Orblit.Editor.Viewport;

using Orblit.Editor.Core;
using Orblit.VectorMath;

/// <summary>
/// Draws how far each selected sound source carries: a ring set at the distance it is at full
/// volume and another at the distance it falls silent.
/// </summary>
/// <remarks>
/// A sound heard from the wrong place is a range nobody could see. The rings are drawn about the
/// world's axes and not the object's, because a range is a distance and does not turn or stretch
/// with the object. It only draws and never takes the pointer.
/// </remarks>
public static class SoundGizmo
{
    /// <summary>The gizmo the shell registers.</summary>
    public static GizmoType Type() => new(
        "sound",
        target => target.Selected.Any(id => target.Scene[id] is { } item && PhysicsComponents.SoundSourceOf(item) is not null),
        Strokes);

    /// <summary>
    /// The far rings, then the near ones, so the near ring ends up on top where the two meet.
    /// </summary>
    public static IReadOnlyList<GizmoStroke> Strokes(GizmoTarget target)
    {
        var near = new WorldLines(target.Projection);
        var far = new WorldLines(target.Projection);
        foreach (var id in target.Selected)
        {
            if (target.Scene[id] is not { } item || PhysicsComponents.SoundSourceOf(item) is not { } source)
            {
                continue;
            }

            var centre = target.Scene.WorldOf(id).GetTranslation();
            Rings(near, centre, source.Near);
            Rings(far, centre, source.Far);
        }

        return [new GizmoStroke(far, GizmoTone.AxisZ), new GizmoStroke(near, GizmoTone.InkMid)];
    }

    private static void Rings(WorldLines lines, Vector3 centre, double radius)
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);
        var z = new Vector3(0, 0, 1);
        lines.Arc(centre, x, y, radius, 0, 2 * Math.PI);
        lines.Arc(centre, y, z, radius, 0, 2 * Math.PI);
        lines.Arc(centre, z, x, radius, 0, 2 * Math.PI);
    }
}

namespace Orblit.Editor.Viewport;

using Orblit.VectorMath;

/// <summary>A ray into the scene, from the eye through a pixel.</summary>
public sealed record ViewRay(Vector3 Origin, Vector3 Direction)
{
    /// <summary>Where this ray meets a plane, or null if it runs alongside it.</summary>
    public Vector3? Meets(Vector3 point, Vector3 normal)
    {
        var slope = normal.Dot(Direction);
        // Nearly parallel: the intersection is either nowhere or so far away that using it would
        // throw the object to the horizon.
        if (Math.Abs(slope) < 1e-6)
        {
            return null;
        }

        var t = normal.Dot(point - Origin) / slope;
        return t <= 0 ? null : Origin + (Direction * t);
    }
}

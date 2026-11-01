namespace Orblit.Editor.Viewport;

using Orblit.VectorMath;

/// <summary>An object's three axes in the world, as unit vectors or already stretched to the size of what is drawn along them.</summary>
internal readonly record struct Axes(Vector3 X, Vector3 Y, Vector3 Z)
{
    /// <summary>
    /// The axes of a turn, taken as the columns of its rotation.
    /// </summary>
    /// <remarks>
    /// Not a quaternion's own rotate call, which turns by the inverse and would draw a body turned
    /// the other way from its model.
    /// </remarks>
    public static Axes Of(Quaternion turn)
    {
        var turning = turn.AsRotationMatrix();
        Vector3 Column(int i) => new(turning.Entry(0, i), turning.Entry(1, i), turning.Entry(2, i));
        return new Axes(Column(0), Column(1), Column(2));
    }

    /// <summary>Each axis made as long as the matching part of <paramref name="by"/>.</summary>
    public Axes Stretched(Vector3 by) => new(X * by.X, Y * by.Y, Z * by.Z);
}

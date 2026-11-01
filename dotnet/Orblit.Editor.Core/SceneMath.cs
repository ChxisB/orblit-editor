namespace Orblit.Editor.Core;

using Orblit.VectorMath;

/// <summary>
/// The rotation conventions the editor stores objects in, and the way back out of a matrix.
/// </summary>
public static class SceneMath
{
    /// <summary>
    /// A rotation from XYZ degrees, applied X then Y then Z.
    /// </summary>
    /// <remarks>
    /// Built from three explicit axis rotations rather than from a library's Euler constructor,
    /// whose angles come in an order that does not match their argument names. Owning both
    /// directions makes the convention checkable, and <see cref="EulerDegreesOf"/> is its exact
    /// inverse.
    /// </remarks>
    public static Matrix4 RotationFromDegrees(Vector3 degreesXyz)
    {
        var rotation = Matrix4.RotationZ(Doubles.Radians(degreesXyz.Z));
        rotation.Multiply(Matrix4.RotationY(Doubles.Radians(degreesXyz.Y)));
        rotation.Multiply(Matrix4.RotationX(Doubles.Radians(degreesXyz.X)));
        return rotation;
    }

    /// <summary>
    /// A transform's rotation, back into the XYZ degrees the inspector shows. Scale is divided out
    /// first, because reading the angles off the upper 3x3 works only while the scale is one.
    /// </summary>
    public static Vector3 EulerDegreesOf(Matrix4 transform)
    {
        var m = transform.GetRotation();
        var scales = new[] { Column(m, 0), Column(m, 1), Column(m, 2) };

        double R(int row, int col)
        {
            var scale = scales[col];

            // A zero-scaled axis carries no direction. Treating it as unscaled keeps the matrix
            // well-formed rather than filling it with infinities.
            return scale < 1e-12 ? (row == col ? 1.0 : 0.0) : m.Entry(row, col) / scale;
        }

        // The inverse of Rz * Ry * Rx. Clamped before asin, because a value a hair outside
        // [-1, 1] from rounding returns NaN, and a NaN in a transform makes the object vanish.
        var sinPitch = Doubles.Clamp(-R(2, 0), -1.0, 1.0);
        var y = Math.Asin(sinPitch);

        double x;
        double z;
        if (Math.Abs(sinPitch) < 0.9999)
        {
            x = Math.Atan2(R(2, 1), R(2, 2));
            z = Math.Atan2(R(1, 0), R(0, 0));
        }
        else
        {
            // Gimbal lock: two of the three angles turn about the same axis and only their sum
            // survives. It all goes into one of them.
            x = Math.Atan2(-R(0, 1), R(1, 1));
            z = 0;
        }

        return new Vector3(Doubles.Degrees(x), Doubles.Degrees(y), Doubles.Degrees(z));
    }

    /// <summary>
    /// Sets an object's local transform so it lands on a given world matrix. It keeps a thing
    /// where it looks when its parent changes, on a reparent and on a paste into a scene whose
    /// parent chain is different.
    /// </summary>
    public static void PlaceInWorld(EditorScene scene, SceneObject target, Matrix4 world)
    {
        var parentId = target.ParentId;
        var local = parentId is null || !scene.Contains(parentId)
            ? world
            : Matrix4.Inverted(scene.WorldOf(parentId)).Multiplied(world);

        var position = Vector3.Zero();
        var rotation = Quaternion.Identity();
        var scale = Vector3.Zero();
        local.Decompose(position, rotation, scale);

        target.Position.SetFrom(position);
        target.Rotation.SetFrom(EulerDegreesOf(local));
        target.Scale.SetFrom(scale);
        scene.Invalidate();
    }

    /// <summary>
    /// A shift in the world, taken into an object's parent frame so it can be added to a local position.
    /// A world shift means something else inside a parent that is turned or scaled.
    /// </summary>
    public static Vector3 ShiftInParent(EditorScene scene, SceneObject item, Vector3 shift) =>
        item.ParentId is { } parent && scene.Contains(parent)
            ? Matrix4.Inverted(scene.WorldOf(parent)).Rotated3(shift)
            : shift;

    /// <summary>The inverse of a matrix, or null when it has none.</summary>
    internal static Matrix4? TryInvert(Matrix4 matrix)
    {
        var inverse = new Matrix4();
        return inverse.CopyInverse(matrix) == 0.0 ? null : inverse;
    }

    // A column's length is that axis's scale, because the rotation part is orthonormal before
    // scaling.
    private static double Column(Matrix3 m, int index) =>
        new Vector3(m.Entry(0, index), m.Entry(1, index), m.Entry(2, index)).Length;
}

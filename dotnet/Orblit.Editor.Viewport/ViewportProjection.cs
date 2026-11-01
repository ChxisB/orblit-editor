namespace Orblit.Editor.Viewport;

using Orblit.VectorMath;

/// <summary>
/// The camera as the viewport's pixels see it.
/// </summary>
/// <remarks>
/// The same view and projection the renderer is given, so what is drawn over the frame lands
/// exactly on what is drawn in it. Two nearly identical cameras is how an overlay ends up half a
/// pixel out at one zoom and a centimetre out at another.
/// </remarks>
public sealed class ViewportProjection
{
    private readonly Matrix4 _viewProjection;
    private readonly Matrix4 _inverse;

    public ViewportProjection(OrbitCamera camera, Size size)
    {
        var view = camera.ToRenderCamera();
        Size = size;
        Eye = view.Position;
        _viewProjection = PerspectiveMatrix(
            Doubles.Radians(view.FieldOfView),
            size.Width / Math.Max(size.Height, 1),
            0.1,
            1000).Multiplied(ViewMatrix(view.Position, view.Target, new Vector3(0, 1, 0)));
        _inverse = Matrix4.Inverted(_viewProjection);
        Forward = (view.Target - view.Position).Normalized();
    }

    public Size Size { get; }

    public Vector3 Eye { get; }

    /// <summary>Which way the camera is looking.</summary>
    public Vector3 Forward { get; }

    /// <summary>Where a world point lands on screen, or null when it is behind the eye.</summary>
    public Offset? Project(Vector3 world)
    {
        var clip = Transform(_viewProjection, world.X, world.Y, world.Z, 1);
        // Behind the camera the perspective divide flips the point to the far side of the screen,
        // which would draw the gizmo where the object is not.
        if (clip.W <= 1e-6)
        {
            return null;
        }

        return new Offset(
            ((clip.X / clip.W * 0.5) + 0.5) * Size.Width,
            (1 - ((clip.Y / clip.W * 0.5) + 0.5)) * Size.Height);
    }

    /// <summary>The ray from the eye through one pixel.</summary>
    public ViewRay RayThrough(Offset pixel)
    {
        var near = Transform(
            _inverse,
            (pixel.Dx / Size.Width * 2) - 1,
            1 - (pixel.Dy / Size.Height * 2),
            -1,
            1);
        var point = new Vector3(near.X, near.Y, near.Z) / near.W;
        return new ViewRay(Eye.Clone(), (point - Eye).Normalized());
    }

    /// <summary>
    /// How long a world-space handle has to be to look the same size on screen wherever the camera
    /// is.
    /// </summary>
    /// <remarks>
    /// A gizmo that shrinks with distance is one you cannot grab across a large scene, and one that
    /// does not is a gizmo the size of a building when you step back from a doorknob.
    /// </remarks>
    public double HandleLength(Vector3 pivot) => Math.Max((pivot - Eye).Length * 0.16, 0.05);

    // The perspective matrix the pick fixture was made with, so a projected point is the same number.
    private static Matrix4 PerspectiveMatrix(double fovYRadians, double aspectRatio, double zNear, double zFar)
    {
        var height = Math.Tan(fovYRadians * 0.5);
        var width = height * aspectRatio;
        var nearMinusFar = zNear - zFar;
        var matrix = Matrix4.Zero();
        matrix[0] = 1.0 / width;
        matrix[5] = 1.0 / height;
        matrix[10] = (zFar + zNear) / nearMinusFar;
        matrix[11] = -1.0;
        matrix[14] = 2.0 * zNear * zFar / nearMinusFar;
        return matrix;
    }

    // The look-at view matrix the pick fixture was made with.
    private static Matrix4 ViewMatrix(Vector3 cameraPosition, Vector3 cameraFocusPosition, Vector3 upDirection)
    {
        var z = (cameraPosition - cameraFocusPosition).Normalized();
        var x = upDirection.Cross(z).Normalized();
        var y = z.Cross(x).Normalized();

        return Matrix4.FromList(
        [
            x.X, y.X, z.X, 0.0,
            x.Y, y.Y, z.Y, 0.0,
            x.Z, y.Z, z.Z, 0.0,
            -x.Dot(cameraPosition), -y.Dot(cameraPosition), -z.Dot(cameraPosition), 1.0,
        ]);
    }

    private static Vector4 Transform(Matrix4 matrix, double x, double y, double z, double w) => new(
        (matrix[0] * x) + (matrix[4] * y) + (matrix[8] * z) + (matrix[12] * w),
        (matrix[1] * x) + (matrix[5] * y) + (matrix[9] * z) + (matrix[13] * w),
        (matrix[2] * x) + (matrix[6] * y) + (matrix[10] * z) + (matrix[14] * w),
        (matrix[3] * x) + (matrix[7] * y) + (matrix[11] * z) + (matrix[15] * w));
}

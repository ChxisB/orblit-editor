namespace Orblit.Editor.Viewport;

using Orblit.Filament;
using Orblit.VectorMath;

/// <summary>
/// Where the viewer is standing, in orbit terms.
/// </summary>
/// <remarks>
/// Orbit rather than free flight, because an editor viewport is nearly always used to look at
/// something, and yaw, pitch and distance cannot be driven into a state you have to reset your way
/// out of.
/// </remarks>
public sealed class OrbitCamera
{
    // Pitch is clamped just short of straight up and straight down. At exactly vertical the up
    // vector and the view direction are parallel and the view matrix collapses, which shows up as
    // the image flipping.
    internal const double PitchLimit = (Math.PI / 2) - 0.02;

    private const double NearEnough = 0.001;

    // F2: a camera is its angles, its distance, its lens and its target, and every one has a default.
    public OrbitCamera(
        double yaw = 0.6,
        double pitch = 0.35,
        double distance = 12,
        double fieldOfView = 50,
        Vector3? target = null)
    {
        Yaw = yaw;
        Pitch = pitch;
        Distance = distance;
        FieldOfView = fieldOfView;
        Target = target ?? new Vector3(0, 0.5, 0);
    }

    public double Yaw { get; }

    public double Pitch { get; }

    public double Distance { get; }

    public double FieldOfView { get; }

    /// <summary>What the camera turns around and looks at.</summary>
    public Vector3 Target { get; }

    /// <summary>
    /// The three axes the camera sees along, in world space.
    /// </summary>
    /// <remarks>
    /// Right, up and forward. Every movement here is expressed in this basis, because "left" means
    /// left of where somebody is looking and not left of the world.
    /// </remarks>
    public (Vector3 Right, Vector3 Up, Vector3 Forward) Basis
    {
        get
        {
            var right = RightAxis();
            var up = UpAxis();
            // Towards what the camera is looking at, which is the way the eye is offset from the
            // target, reversed.
            var forward = new Vector3(
                -Math.Sin(Yaw) * Math.Cos(Pitch),
                -Math.Sin(Pitch),
                -Math.Cos(Yaw) * Math.Cos(Pitch));
            return (right, up, forward);
        }
    }

    public OrbitCamera Orbit(Offset delta) => new(
        Yaw - (delta.Dx * 0.008),
        Doubles.Clamp(Pitch + (delta.Dy * 0.008), -PitchLimit, PitchLimit),
        Distance,
        FieldOfView,
        Target);

    public OrbitCamera Zoom(double amount) => new(
        Yaw,
        Pitch,
        Doubles.Clamp(Distance * Math.Exp(amount * 0.0016), 1.5, 200.0),
        FieldOfView,
        Target);

    /// <summary>
    /// Turns on the spot, without moving.
    /// </summary>
    /// <remarks>
    /// What <see cref="Orbit"/> does not do. Orbiting swings the eye around a fixed target, and
    /// looking around keeps the eye still and moves the target. From inside a room the difference is
    /// the whole difference between the two.
    /// </remarks>
    public OrbitCamera Looking(Offset delta)
    {
        var was = ToRenderCamera().Position;
        var nextYaw = Yaw - (delta.Dx * 0.005);
        var nextPitch = Doubles.Clamp(Pitch + (delta.Dy * 0.005), -PitchLimit, PitchLimit);

        // The target is put back behind the new direction so the eye stays exactly where it was.
        // Everything else in the editor works in orbit terms, such as framing, panning and the
        // gizmos, and this keeps it that way rather than giving the camera a second mode with its
        // own maths.
        var horizontal = Distance * Math.Cos(nextPitch);
        return new OrbitCamera(
            nextYaw,
            nextPitch,
            Distance,
            FieldOfView,
            new Vector3(
                was.X - (horizontal * Math.Sin(nextYaw)),
                was.Y - (Distance * Math.Sin(nextPitch)),
                was.Z - (horizontal * Math.Cos(nextYaw))));
    }

    /// <summary>
    /// Moves the camera itself, along the axes it is looking down.
    /// </summary>
    /// <param name="along">Right, up and forward in metres.</param>
    /// <remarks>
    /// Both the eye and what it looks at move together, which is what flying is. The view does not
    /// swing around anything, it goes somewhere.
    /// </remarks>
    public OrbitCamera Flying(Vector3 along)
    {
        var axes = Basis;
        return new OrbitCamera(
            Yaw,
            Pitch,
            Distance,
            FieldOfView,
            Target + (axes.Right * along.X) + (axes.Up * along.Y) + (axes.Forward * along.Z));
    }

    /// <summary>Slides the camera sideways, keeping its angle. What a middle drag does.</summary>
    public OrbitCamera Pan(Offset delta)
    {
        // Scaled by distance so panning feels the same close up and far away, and moved along the
        // camera's own axes rather than the world's.
        var scale = Distance * 0.0016;
        var right = RightAxis();
        var up = UpAxis();

        return new OrbitCamera(
            Yaw,
            Pitch,
            Distance,
            FieldOfView,
            Target - (right * (delta.Dx * scale)) + (up * (delta.Dy * scale)));
    }

    /// <summary>
    /// Points the camera at a box, far enough back to see all of it.
    /// </summary>
    /// <remarks>
    /// Keeps the angle it was already at, because framing something should not also spin the view.
    /// Somebody pressing F wants the thing on screen, not a different shot of it.
    /// </remarks>
    public OrbitCamera Framing(Vector3 centre, double radius)
    {
        // Fitted to the vertical field of view with room to spare, and floored so framing a flat or
        // tiny object does not put the camera inside it.
        var fitted = radius / Math.Tan(Doubles.Radians(FieldOfView) / 2) * 1.6;

        return new OrbitCamera(Yaw, Pitch, Doubles.Clamp(fitted, 1.5, 200.0), FieldOfView, centre.Clone());
    }

    /// <summary>
    /// One step of the way to <paramref name="goal"/>, covering the share of what is left that
    /// <paramref name="seconds"/> is of <paramref name="tau"/>.
    /// </summary>
    /// <remarks>
    /// The share is <c>1 - exp(-seconds / tau)</c>, so two steps of a sixtieth of a second land where
    /// one of a thirtieth does, and the ease looks the same on a slow frame as on a fast one. Distance
    /// moves by ratio, because a zoom is a ratio and halving the way to a far goal should feel like
    /// halving the way to a near one. Yaw takes the short way round.
    /// </remarks>
    public OrbitCamera Toward(OrbitCamera goal, double seconds, double tau)
    {
        var share = 1 - Math.Exp(-seconds / tau);
        return new OrbitCamera(
            Yaw + (TurnTo(goal) * share),
            Pitch + ((goal.Pitch - Pitch) * share),
            Distance * Math.Pow(goal.Distance / Distance, share),
            FieldOfView + ((goal.FieldOfView - FieldOfView) * share),
            Target + ((goal.Target - Target) * share));
    }

    /// <summary>
    /// Whether <paramref name="other"/> is too close to tell apart on screen: under a thousandth of
    /// a radian, of a distance, and of the distance in metres. Where an ease stops, because chasing
    /// the last of an exponential would keep redrawing a view that is not changing.
    /// </summary>
    public bool IsNear(OrbitCamera other) =>
        Math.Abs(TurnTo(other)) < NearEnough
        && Math.Abs(other.Pitch - Pitch) < NearEnough
        && Math.Abs(other.FieldOfView - FieldOfView) < NearEnough
        && Math.Abs(Math.Log(other.Distance / Distance)) < NearEnough
        && (other.Target - Target).Length < NearEnough * Distance;

    public RenderCamera ToRenderCamera()
    {
        var horizontal = Distance * Math.Cos(Pitch);
        return new RenderCamera
        {
            Position = new Vector3(
                Target.X + (horizontal * Math.Sin(Yaw)),
                Target.Y + (Distance * Math.Sin(Pitch)),
                Target.Z + (horizontal * Math.Cos(Yaw))),
            Target = Target.Clone(),
            FieldOfView = FieldOfView,
        };
    }

    // The signed way round to the other yaw, never more than half a turn. Yaw is not wrapped while
    // somebody orbits, so two yaws a whole turn apart are the same view.
    private double TurnTo(OrbitCamera other) => Math.IEEERemainder(other.Yaw - Yaw, 2 * Math.PI);

    private Vector3 RightAxis() => new(Math.Cos(Yaw), 0, -Math.Sin(Yaw));

    private Vector3 UpAxis() => new(
        -Math.Sin(Yaw) * Math.Sin(Pitch),
        Math.Cos(Pitch),
        -Math.Cos(Yaw) * Math.Sin(Pitch));
}

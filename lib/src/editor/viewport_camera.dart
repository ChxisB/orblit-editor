part of 'viewport.dart';

/// Where the viewer is standing, in orbit terms.
///
/// Orbit rather than free flight, because an editor viewport is nearly always
/// used to look *at* something, and yaw/pitch/distance cannot be driven into a
/// state you have to reset your way out of.
class OrbitCamera {
  OrbitCamera({
    this.yaw = 0.6,
    this.pitch = 0.35,
    this.distance = 12,
    this.fieldOfView = 50,
    Vector3? target,
  }) : target = target ?? Vector3(0, 0.5, 0);

  final double yaw;
  final double pitch;
  final double distance;
  final double fieldOfView;

  /// What the camera turns around and looks at.
  final Vector3 target;

  /// Pitch is clamped just short of straight up and straight down: at exactly
  /// vertical the up vector and the view direction are parallel and the view
  /// matrix collapses, which shows up as the image flipping.
  static const _pitchLimit = math.pi / 2 - 0.02;

  OrbitCamera orbit(Offset delta) => OrbitCamera(
    yaw: yaw - delta.dx * 0.008,
    pitch: (pitch + delta.dy * 0.008).clamp(-_pitchLimit, _pitchLimit),
    distance: distance,
    fieldOfView: fieldOfView,
    target: target,
  );

  OrbitCamera zoom(double amount) => OrbitCamera(
    yaw: yaw,
    pitch: pitch,
    distance: (distance * math.exp(amount * 0.0016)).clamp(1.5, 200.0),
    fieldOfView: fieldOfView,
    target: target,
  );

  /// The three axes the camera sees along, in world space.
  ///
  /// Right, up and forward — the basis every movement here is expressed in,
  /// because "left" means left of where somebody is looking and not left of
  /// the world.
  ({Vector3 right, Vector3 up, Vector3 forward}) get basis {
    final right = Vector3(math.cos(yaw), 0, -math.sin(yaw));
    final up = Vector3(
      -math.sin(yaw) * math.sin(pitch),
      math.cos(pitch),
      -math.cos(yaw) * math.sin(pitch),
    );
    // Towards what the camera is looking at, which is the way the eye is
    // offset from the target, reversed.
    final forward = Vector3(
      -math.sin(yaw) * math.cos(pitch),
      -math.sin(pitch),
      -math.cos(yaw) * math.cos(pitch),
    );
    return (right: right, up: up, forward: forward);
  }

  /// Turns on the spot, without moving.
  ///
  /// What [orbit] does not do: orbiting swings the eye around a fixed target,
  /// and looking around keeps the eye still and moves the target. From inside
  /// a room the difference is the whole difference between the two.
  OrbitCamera looking(Offset delta) {
    final was = toRenderCamera().position;
    final nextYaw = yaw - delta.dx * 0.005;
    final nextPitch = (pitch + delta.dy * 0.005).clamp(
      -_pitchLimit,
      _pitchLimit,
    );

    // The target is put back behind the new direction so the eye stays exactly
    // where it was. Everything else in the editor works in orbit terms —
    // framing, panning, the gizmos — and this keeps it that way rather than
    // giving the camera a second mode with its own maths.
    final horizontal = distance * math.cos(nextPitch);
    return OrbitCamera(
      yaw: nextYaw,
      pitch: nextPitch,
      distance: distance,
      fieldOfView: fieldOfView,
      target: Vector3(
        was.x - horizontal * math.sin(nextYaw),
        was.y - distance * math.sin(nextPitch),
        was.z - horizontal * math.cos(nextYaw),
      ),
    );
  }

  /// Moves the camera itself, along the axes it is looking down.
  ///
  /// [along] is right, up and forward in metres. Both the eye and what it
  /// looks at move together, which is what flying is: the view does not swing
  /// around anything, it goes somewhere.
  OrbitCamera flying(Vector3 along) {
    final axes = basis;
    return OrbitCamera(
      yaw: yaw,
      pitch: pitch,
      distance: distance,
      fieldOfView: fieldOfView,
      target:
          target +
          axes.right * along.x +
          axes.up * along.y +
          axes.forward * along.z,
    );
  }

  /// Slides the camera sideways, keeping its angle. What a middle-drag does.
  OrbitCamera pan(Offset delta) {
    // Scaled by distance so panning feels the same close up and far away, and
    // moved along the camera's own axes rather than the world's.
    final scale = distance * 0.0016;
    final right = Vector3(math.cos(yaw), 0, -math.sin(yaw));
    final up = Vector3(
      -math.sin(yaw) * math.sin(pitch),
      math.cos(pitch),
      -math.cos(yaw) * math.sin(pitch),
    );

    return OrbitCamera(
      yaw: yaw,
      pitch: pitch,
      distance: distance,
      fieldOfView: fieldOfView,
      target: target - right * (delta.dx * scale) + up * (delta.dy * scale),
    );
  }

  /// Points the camera at a box, far enough back to see all of it.
  ///
  /// Keeps the angle it was already at, because framing something should not
  /// also spin the view — somebody pressing F wants the thing on screen, not a
  /// different shot of it.
  OrbitCamera framing({required Vector3 centre, required double radius}) {
    // Fitted to the vertical field of view with room to spare, and floored so
    // framing a flat or tiny object does not put the camera inside it.
    final fitted = radius / math.tan(radians(fieldOfView) / 2) * 1.6;

    return OrbitCamera(
      yaw: yaw,
      pitch: pitch,
      distance: fitted.clamp(1.5, 200.0),
      fieldOfView: fieldOfView,
      target: centre.clone(),
    );
  }

  OrblitCamera toRenderCamera() {
    final horizontal = distance * math.cos(pitch);
    return OrblitCamera(
      position: Vector3(
        target.x + horizontal * math.sin(yaw),
        target.y + distance * math.sin(pitch),
        target.z + horizontal * math.cos(yaw),
      ),
      target: target.clone(),
      fieldOfView: fieldOfView,
    );
  }
}

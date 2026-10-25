namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// Draws every selected joint: its frame, what it joins, and its limits.
/// </summary>
/// <remarks>
/// A joint you cannot see is a joint you tune by guessing. Which way a hinge turns is its entity's
/// x axis, how far it goes is an arc, and which two bodies it holds is decided by where the entity
/// sits in the tree, so all three are drawn rather than left to be found out when the scene runs.
///
/// The frame is drawn with x longer than y and z, because x is the axis a hinge turns about, a
/// slider runs along and a cone opens round. Limits are drawn from where things stand in the scene,
/// since that is where every limit is measured from: a hinge's arc starts at nought on the entity's
/// y axis and turns towards z. In amber, so it reads apart from the green of the bodies it joins
/// when both are selected.
///
/// It only draws: the handles that place a joint are its entity's own.
/// </remarks>
public static class JointGizmo
{
    /// <summary>The gizmo the shell registers.</summary>
    public static GizmoType Type() => new(
        "joint",
        target => target.Selected.Any(id => target.Scene[id] is { } item && PhysicsComponents.JointOf(item) is not null),
        target => [new GizmoStroke(Lines(target), GizmoTone.Warn)]);

    /// <summary>Every selected joint as lines in the view.</summary>
    public static WorldLines Lines(GizmoTarget target)
    {
        var lines = new WorldLines(target.Projection);
        var drawing = new Drawing(lines);
        foreach (var id in target.Selected)
        {
            if (target.Scene[id] is { } item && PhysicsComponents.JointOf(item) is { } joint)
            {
                drawing.Joint(joint, id, target.Scene);
            }
        }

        return lines;
    }

    // A joint's own place, axes and size on screen, for the shapes drawn from it.
    private readonly record struct Frame(Vector3 At, Vector3 X, Vector3 Y, Vector3 Z, double Reach);

    // What a joint holds at each end, as the point its line is drawn to.
    private readonly record struct Ends(Vector3? Body, Vector3? Holder);

    private sealed class Drawing(WorldLines lines)
    {
        public void Joint(JointComponent joint, string id, EditorScene scene)
        {
            var at = Vector3.Zero();
            var turn = Quaternion.Identity();
            scene.WorldOf(id).Decompose(at, turn, Vector3.Zero());

            // The entity's own axes without its scale, as the physics takes them.
            var axes = Axes.Of(turn);

            // As big on screen wherever the camera is, as the handles are: a frame and its arcs have
            // no size in the world to be drawn at.
            var shape = new Frame(at, axes.X, axes.Y, axes.Z, lines.Projection.HandleLength(at) * 0.6);

            lines.Line(at, at + (axes.X * shape.Reach));
            lines.Line(at, at + (axes.Y * (shape.Reach * 0.35)));
            lines.Line(at, at + (axes.Z * (shape.Reach * 0.35)));

            var ends = JoinedTo(scene, id);
            if (ends.Body is not null)
            {
                lines.Line(at, ends.Body);
            }

            if (ends.Holder is not null)
            {
                lines.Line(at, ends.Holder);
            }

            Limits(joint, shape, Matrix4.Compose(at, turn, Vector3.All(1)), ends);
        }

        private static Ends JoinedTo(EditorScene scene, string id)
        {
            var (body, holder) = PhysicsComponents.JointEndsIn(scene, id);
            return new Ends(
                body is null ? null : PhysicsComponents.BodyMiddleIn(scene, body),
                holder is null ? null : PhysicsComponents.BodyMiddleIn(scene, holder));
        }

        private static JointRange? RangeOf(IReadOnlyDictionary<JointAxis, JointRange> limits, JointAxis axis) =>
            limits.TryGetValue(axis, out var range) ? range : null;

        // What the kind of joint adds to the frame.
        [SuppressMessage("Maintainability", "CA1502", Justification = "F2: a list of cases is data.")]
        private void Limits(JointComponent joint, Frame shape, Matrix4 frame, Ends ends)
        {
            var limits = joint.Limits;
            switch (joint.Kind)
            {
                case JointKind.Fixed:
                    break;
                case JointKind.Point:
                    Ball(shape, shape.Reach * 0.2);
                    break;
                case JointKind.Hinge:
                    Turn(shape.At, shape.Y, shape.Z, RangeOf(limits, JointAxis.AboutX), shape.Reach * 0.5);
                    break;
                case JointKind.Slider:
                    Travel(shape.At, shape.X, shape.Y, RangeOf(limits, JointAxis.AlongX), shape.Reach);
                    break;
                case JointKind.Distance:
                    Rope(shape, RangeOf(limits, JointAxis.AlongX), ends.Body);
                    break;
                case JointKind.Cone:
                    Cone(shape, joint.Swing);
                    if (RangeOf(limits, JointAxis.AboutX) is { } twist)
                    {
                        Turn(shape.At, shape.Y, shape.Z, twist, shape.Reach * 0.3);
                    }

                    break;
                case JointKind.SixAxis:
                    SixAxes(shape, limits);
                    break;
                case JointKind.Gear or JointKind.Rack:
                    Pinion(shape, frame, joint.AxisB);
                    break;
                case JointKind.Pulley:
                    Pulley(shape, frame, joint.Over, ends);
                    break;
                case JointKind.Path:
                    PathOf(shape, frame, joint.Path);
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        // A distance joint's range along the line to its body, or along x when the two are at the
        // same place and there is no line.
        private void Rope(Frame shape, JointRange? range, Vector3? body)
        {
            if (range is null)
            {
                return;
            }

            var toward = body is null || (body - shape.At).Length < 1e-6
                ? shape.X
                : (body - shape.At).Normalized();
            Travel(shape.At, toward, Across(toward), range, shape.Reach);
        }

        // The three ways along and the three about, each from the axis after it.
        private void SixAxes(Frame shape, IReadOnlyDictionary<JointAxis, JointRange> limits)
        {
            Vector3[] axes = [shape.X, shape.Y, shape.Z];
            for (var i = 0; i < 3; i++)
            {
                if (RangeOf(limits, (JointAxis)i) is { } along)
                {
                    Travel(shape.At, axes[i], axes[(i + 1) % 3], along, shape.Reach);
                }

                if (RangeOf(limits, (JointAxis)(i + 3)) is { } about)
                {
                    Turn(shape.At, axes[(i + 1) % 3], axes[(i + 2) % 3], about, shape.Reach * 0.5);
                }
            }
        }

        // The axis a gear or a rack's body turns about, when it is not the joint's own x, which is
        // drawn already.
        private void Pinion(Frame shape, Matrix4 frame, Vector3 axisB)
        {
            if (axisB.Length2 == 0)
            {
                return;
            }

            var across = frame.Rotated3(axisB).Normalized();
            lines.Line(shape.At, shape.At + (across * (shape.Reach * 0.7)));
        }

        // A rope over two fixed points: from the body it hangs from to the first, across to the
        // second, and down to the body. A ring at each point, so there is something to find when
        // they are dragged.
        private void Pulley(Frame shape, Matrix4 frame, IReadOnlyList<Vector3> over, Ends ends)
        {
            if (over.Count != 2)
            {
                return;
            }

            var first = frame.Transformed3(over[0]);
            var second = frame.Transformed3(over[1]);
            lines.Line(first, second);
            if (ends.Holder is not null)
            {
                lines.Line(ends.Holder, first);
            }

            if (ends.Body is not null)
            {
                lines.Line(second, ends.Body);
            }

            foreach (var point in new[] { first, second })
            {
                Ball(shape with { At = point }, shape.Reach * 0.12);
            }
        }

        // A path's line in the world, with a ring at its start, which is where the body is measured
        // from.
        private void PathOf(Frame shape, Matrix4 frame, IReadOnlyList<Vector3> points)
        {
            if (points.Count == 0)
            {
                return;
            }

            var world = points.Select(frame.Transformed3).ToList();
            for (var i = 0; i + 1 < world.Count; i++)
            {
                lines.Line(world[i], world[i + 1]);
            }

            Ball(shape with { At = world[0] }, shape.Reach * 0.12);
        }

        // How far a joint turns: a whole ring when it is free, an arc with a spoke at each end when
        // it is a range, and one spoke where it is locked. Angles run from u towards v, which is the
        // right-hand sense about the axis they are square to.
        // F2: a centre, two directions, a range and a radius are what a turn is.
        private void Turn(Vector3 at, Vector3 u, Vector3 v, JointRange? range, double radius)
        {
            if (range is null)
            {
                lines.Arc(at, u, v, radius, 0, 2 * Math.PI);
                return;
            }

            var low = Doubles.Radians(range.Low);
            var high = Doubles.Radians(range.High);
            Vector3 Spoke(double angle) => at + (u * (radius * Math.Cos(angle))) + (v * (radius * Math.Sin(angle)));
            lines.Line(at, Spoke(low));
            if (range.Locked)
            {
                return;
            }

            lines.Line(at, Spoke(high));
            lines.Arc(at, u, v, radius, low, high);
        }

        // How far a joint travels along an axis: a track through the point when it is free, and the
        // stretch between its ends, with a bar across each end, when it is limited. Metres in the
        // world, not a size on screen, because that is what a slider's travel is.
        // F2: a centre, an axis, a direction for the bars, a range and a length are what a track is.
        private void Travel(Vector3 at, Vector3 axis, Vector3 across, JointRange? range, double reach)
        {
            if (range is null)
            {
                lines.Line(at - (axis * reach), at + (axis * reach));
                return;
            }

            var bar = across * (reach * 0.12);
            var low = at + (axis * range.Low);
            var high = at + (axis * range.High);
            lines.Line(low, high);
            lines.Line(low - bar, low + bar);
            if (!range.Locked)
            {
                lines.Line(high - bar, high + bar);
            }
        }

        // The cone a body's x axis is kept inside: a ring where it opens out to swing degrees, and
        // four lines from the point to it.
        private void Cone(Frame shape, double swing)
        {
            var angle = Doubles.Radians(Doubles.Clamp(swing, 0, 180));
            var rim = shape.At + (shape.X * (shape.Reach * Math.Cos(angle)));
            var radius = shape.Reach * Math.Sin(angle);
            lines.Arc(rim, shape.Y, shape.Z, radius, 0, 2 * Math.PI);
            foreach (var side in new[] { shape.Y, -shape.Y, shape.Z, -shape.Z })
            {
                lines.Line(shape.At, rim + (side * radius));
            }
        }

        private void Ball(Frame shape, double radius)
        {
            lines.Arc(shape.At, shape.X, shape.Y, radius, 0, 2 * Math.PI);
            lines.Arc(shape.At, shape.Y, shape.Z, radius, 0, 2 * Math.PI);
            lines.Arc(shape.At, shape.Z, shape.X, radius, 0, 2 * Math.PI);
        }

        // Some direction square to the axis.
        private static Vector3 Across(Vector3 axis)
        {
            var other = Math.Abs(axis.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
            var across = axis.Cross(other);
            across.Normalize();
            return across;
        }
    }
}

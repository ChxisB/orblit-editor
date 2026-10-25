namespace Orblit.Editor.Viewport;

using System.Diagnostics;
using Orblit.Editor.Core;
using Orblit.Scene;
using Orblit.VectorMath;

/// <summary>
/// Draws the shape the physics sees for every selected object with a body.
/// </summary>
/// <remarks>
/// Seeing the collider is how collider bugs get found: a crate that floats a hand above the floor is
/// a body fitted to the wrong mesh, and a ball that rolls off a ledge it should rest on is a radius
/// nobody could see. Drawn the way the simulation reads it. Scaled with the object: a ball by its
/// largest scale, a capsule or cylinder by its sideways and upright ones and a hull by each of its
/// three. What is on screen is what will fall, not what the inspector's numbers say before the scale
/// is applied.
///
/// It only draws and never takes the pointer: the handles that move a body are the object's own.
/// </remarks>
public static class BodyGizmo
{
    private static readonly Vector3 Unit = Vector3.All(1);

    /// <summary>The gizmo the shell registers.</summary>
    public static GizmoType Type() => new(
        "body",
        target => target.Selected.Any(id => target.Scene[id] is { } item && PhysicsComponents.PhysicsBodyOf(item) is not null),
        Strokes);

    /// <summary>
    /// The wireframe of each selected body: solid bodies first, then places, which are warm so a
    /// trigger or a zone is not taken for a wall.
    /// </summary>
    public static IReadOnlyList<GizmoStroke> Strokes(GizmoTarget target)
    {
        var solid = new WorldLines(target.Projection);
        var places = new WorldLines(target.Projection);
        foreach (var id in target.Selected)
        {
            if (target.Scene[id] is not { } item || PhysicsComponents.PhysicsBodyOf(item) is not { } body)
            {
                continue;
            }

            new Outline(PhysicsComponents.IsPlace(item) ? places : solid).Body(body, target.Scene.WorldOf(id));
        }

        return [new GizmoStroke(solid, GizmoTone.Good), new GizmoStroke(places, GizmoTone.Warn)];
    }

    // What a body or one part of a compound has to say about its shape.
    private readonly record struct Solid(BodyShape Shape, Vector3 Size, double Radius, double Height, IReadOnlyList<double> Hull);

    private sealed class Outline(WorldLines lines)
    {
        /// <summary>
        /// <paramref name="body"/> laid out the way the simulation places it: turned with its object,
        /// at the object's point for its centre, and sized by the object's scale.
        /// </summary>
        public void Body(BodyComponent body, Matrix4 world)
        {
            var turn = Quaternion.Identity();
            var scale = Vector3.Zero();
            world.Decompose(Vector3.Zero(), turn, scale);
            scale.SetValues(Math.Abs(scale.X), Math.Abs(scale.Y), Math.Abs(scale.Z));

            // The object's own axes in the world, without its scale.
            var axes = Axes.Of(turn);
            var centre = world.Transformed3(body.Centre);
            if (body.Shape == BodyShape.Compound || !body.ShapeScale.Equals(Unit))
            {
                Place(body, centre, axes.Stretched(Times(scale, body.ShapeScale)), Unit);
                return;
            }

            Place(body, centre, axes, scale);
        }

        private void Place(BodyComponent body, Vector3 centre, Axes axes, Vector3 scale)
        {
            switch (body.Shape)
            {
                case BodyShape.Mesh:
                    Mesh(centre, axes.Stretched(scale), body.MeshVertices, body.MeshIndices);
                    break;
                case BodyShape.Compound:
                    foreach (var part in body.Parts)
                    {
                        Part(part, centre, axes);
                    }

                    break;
                default:
                    Primitive(new Solid(body.Shape, body.Size, body.Radius, body.Height, body.Hull), centre, axes, scale);
                    break;
            }
        }

        private void Primitive(Solid solid, Vector3 centre, Axes axes, Vector3 scale)
        {
            switch (solid.Shape)
            {
                case BodyShape.Box:
                    Box(centre, axes.Stretched(Times(solid.Size, scale).Scaled(0.5)));
                    break;
                case BodyShape.Sphere:
                    Ball(centre, axes, solid.Radius * Math.Max(scale.X, Math.Max(scale.Y, scale.Z)));
                    break;
                case BodyShape.Capsule:
                    Capsule(solid, centre, axes, scale);
                    break;
                case BodyShape.Cylinder:
                    Tube(centre, axes, solid.Radius * Math.Max(scale.X, scale.Z), solid.Height * scale.Y / 2);
                    break;
                case BodyShape.Hull:
                    Hull(centre, axes.Stretched(scale), solid.Hull);
                    break;
                case BodyShape.Plane:
                    Ground(centre, axes);
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        private void Mesh(Vector3 centre, Axes axes, IReadOnlyList<double> vertices, IReadOnlyList<long> indices)
        {
            Vector3 Point(long i) =>
                centre + (axes.X * vertices[(int)(i * 3)]) + (axes.Y * vertices[(int)((i * 3) + 1)]) + (axes.Z * vertices[(int)((i * 3) + 2)]);

            for (var i = 0; i + 2 < indices.Count; i += 3)
            {
                var triangle = new[] { indices[i], indices[i + 1], indices[i + 2] };
                if (triangle.Any(v => v < 0 || (v * 3) + 2 >= vertices.Count))
                {
                    continue;
                }

                for (var k = 0; k < 3; k++)
                {
                    lines.Line(Point(triangle[k]), Point(triangle[(k + 1) % 3]));
                }
            }
        }

        private void Part(BodyPart part, Vector3 centre, Axes axes)
        {
            if (part.Shape is BodyShape.Mesh or BodyShape.Compound or BodyShape.Plane)
            {
                return;
            }

            Vector3 Mapped(Vector3 p) => (axes.X * p.X) + (axes.Y * p.Y) + (axes.Z * p.Z);
            var turn = Axes.Of(part.Rotation.Normalized());
            var own = new Axes(Mapped(turn.X), Mapped(turn.Y), Mapped(turn.Z)).Stretched(part.Scale);
            Primitive(new Solid(part.Shape, part.Size, part.Radius, part.Height, part.Hull), centre + Mapped(part.Centre), own, Unit);
        }

        private static Vector3 Times(Vector3 a, Vector3 b) => new(a.X * b.X, a.Y * b.Y, a.Z * b.Z);

        private void Box(Vector3 centre, Axes half)
        {
            Vector3 Corner(int i) =>
                centre
                + (half.X * ((i & 1) == 0 ? -1.0 : 1.0))
                + (half.Y * ((i & 2) == 0 ? -1.0 : 1.0))
                + (half.Z * ((i & 4) == 0 ? -1.0 : 1.0));

            // Two corners share an edge when they differ along exactly one axis.
            for (var i = 0; i < 8; i++)
            {
                foreach (var axis in new[] { 1, 2, 4 })
                {
                    if ((i & axis) == 0)
                    {
                        lines.Line(Corner(i), Corner(i | axis));
                    }
                }
            }
        }

        // A ring about each of the object's axes, so a ball's turn shows as well as its size.
        private void Ball(Vector3 centre, Axes axes, double radius)
        {
            lines.Arc(centre, axes.X, axes.Y, radius, 0, 2 * Math.PI);
            lines.Arc(centre, axes.Y, axes.Z, radius, 0, 2 * Math.PI);
            lines.Arc(centre, axes.Z, axes.X, radius, 0, 2 * Math.PI);
        }

        private void Capsule(Solid solid, Vector3 centre, Axes axes, Vector3 scale)
        {
            var radius = solid.Radius * Math.Max(scale.X, scale.Z);
            var straight = (solid.Height * scale.Y / 2) - radius;

            // All ends and no middle, which the simulation treats as a ball.
            if (straight <= 0)
            {
                Ball(centre, axes, radius);
                return;
            }

            // A ring at each end of the straight part, four lines joining them, and a dome over each
            // end drawn as two half rings across each other.
            Tube(centre, axes, radius, straight);
            var top = centre + (axes.Y * straight);
            var bottom = centre - (axes.Y * straight);
            foreach (var across in new[] { axes.X, axes.Z })
            {
                lines.Arc(top, across, axes.Y, radius, 0, Math.PI);
                lines.Arc(bottom, across, axes.Y, radius, Math.PI, 2 * Math.PI);
            }
        }

        // A ring at each end, half up and down from the centre, and four lines joining them.
        private void Tube(Vector3 centre, Axes axes, double radius, double half)
        {
            var top = centre + (axes.Y * half);
            var bottom = centre - (axes.Y * half);
            lines.Arc(top, axes.X, axes.Z, radius, 0, 2 * Math.PI);
            lines.Arc(bottom, axes.X, axes.Z, radius, 0, 2 * Math.PI);
            foreach (var side in new[] { axes.X, -axes.X, axes.Z, -axes.Z })
            {
                lines.Line(top + (side * radius), bottom + (side * radius));
            }
        }

        // The edges of the solid round the corners, which are written flat and measured from the
        // centre. Nothing for points that enclose no volume, which is a body that does nothing.
        private void Hull(Vector3 centre, Axes axes, IReadOnlyList<double> corners)
        {
            var outline = ConvexOutline.Of(corners);
            Vector3 At(int i)
            {
                var p = outline.Corners[i];
                return centre + (axes.X * p.X) + (axes.Y * p.Y) + (axes.Z * p.Z);
            }

            foreach (var (a, b) in outline.Edges)
            {
                lines.Line(At(a), At(b));
            }
        }

        // A square of the surface round the point it goes through, with a line standing up from it
        // to say which side is open. Sized to look the same wherever the camera is, as the handles
        // are, because ground has no size of its own to draw.
        private void Ground(Vector3 centre, Axes axes)
        {
            var reach = lines.Projection.HandleLength(centre);
            const int Grid = 2;
            for (var i = -Grid; i <= Grid; i++)
            {
                var along = (double)i / Grid * reach;
                lines.Line(
                    centre + (axes.X * along) - (axes.Z * reach),
                    centre + (axes.X * along) + (axes.Z * reach));
                lines.Line(
                    centre + (axes.Z * along) - (axes.X * reach),
                    centre + (axes.Z * along) + (axes.X * reach));
            }

            lines.Line(centre, centre + (axes.Y * (reach / 2)));
        }
    }
}

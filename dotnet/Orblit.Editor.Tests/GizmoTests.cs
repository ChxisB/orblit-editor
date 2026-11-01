namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.VectorMath;

public sealed class GizmoTests
{
    private static readonly Size ViewSize = new(800, 600);
    private static readonly OrbitCamera Camera = new();

    private static ViewportProjection ProjectionOf(OrbitCamera? other = null) =>
        new(other ?? Camera, ViewSize);

    private static EditorScene SceneWith(params SceneObject[] objects) => new([.. objects]);

    private static SceneObject Cube(string id, string name, Vector3? position = null, Vector3? scale = null) =>
        new(id, name, ObjectKind.Mesh, position: position, scale: scale);

    private static Gizmo GizmoAt(Vector3 pivot, GizmoMode mode = GizmoMode.Move) =>
        new(mode, pivot, ProjectionOf());

    [Fact]
    public void WhatTheCameraIsPointedAtLandsInTheMiddle()
    {
        var centre = ProjectionOf().Project(Camera.Target)!.Value;
        Assert.Equal(ViewSize.Width / 2, centre.Dx, 1);
        Assert.Equal(ViewSize.Height / 2, centre.Dy, 1);
    }

    [Fact]
    public void ARayThroughTheMiddleGoesWhereTheCameraLooks()
    {
        var projection = ProjectionOf();
        var ray = projection.RayThrough(new Offset(ViewSize.Width / 2, ViewSize.Height / 2));

        var wanted = (Camera.Target - projection.Eye).Normalized();
        Assert.True((ray.Direction - wanted).Length < 1e-6);
    }

    [Fact]
    public void SomethingBehindTheCameraDoesNotLandAnywhere()
    {
        var projection = ProjectionOf();
        var behind = projection.Eye + ((projection.Eye - Camera.Target) * 2);
        Assert.Null(projection.Project(behind));
    }

    [Fact]
    public void AHandleKeepsItsSizeOnScreenAsTheCameraPullsBack()
    {
        var near = new ViewportProjection(new OrbitCamera(distance: 5), ViewSize);
        var far = new ViewportProjection(new OrbitCamera(distance: 40), ViewSize);

        // Longer in the world when further away, which is what keeps it the same number of pixels.
        Assert.True(far.HandleLength(Vector3.Zero()) > near.HandleLength(Vector3.Zero()) * 4);
    }

    [Fact]
    public void ARayThroughACubeFindsIt()
    {
        var scene = SceneWith(Cube("cube", "Cube"));

        Assert.Equal("cube", scene.ObjectAlong(new Vector3(0, 0, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void ARayPastEverythingFindsNothing()
    {
        var scene = SceneWith(Cube("cube", "Cube"));

        Assert.Null(scene.ObjectAlong(new Vector3(0, 40, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void TheNearerOfTwoWins()
    {
        var scene = SceneWith(Cube("far", "Far"), Cube("near", "Near", position: new Vector3(0, 0, 5)));

        Assert.Equal("near", scene.ObjectAlong(new Vector3(0, 0, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void WhatIsBehindTheEyeIsNotInFrontOfIt()
    {
        var scene = SceneWith(Cube("cube", "Cube"));

        // Looking away from the cube.
        Assert.Null(scene.ObjectAlong(new Vector3(0, 0, 10), new Vector3(0, 0, 1)));
    }

    [Fact]
    public void AHiddenObjectIsNotInTheWay()
    {
        var hidden = Cube("cube", "Cube");
        hidden.Visible = false;
        var scene = SceneWith(hidden);

        Assert.Null(scene.ObjectAlong(new Vector3(0, 0, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void AnObjectIsHitWhereItStandsNotWhereItStarted()
    {
        var scene = SceneWith(Cube("cube", "Cube", position: new Vector3(6, 0, 0)));

        Assert.Null(scene.ObjectAlong(new Vector3(0, 0, 10), new Vector3(0, 0, -1)));
        Assert.Equal("cube", scene.ObjectAlong(new Vector3(6, 0, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void ARotatedSquashedObjectIsHitAtItsOwnAngle()
    {
        var scene = SceneWith(Cube("plank", "Plank", scale: new Vector3(4, 0.1, 0.5)));

        // Along the plank: inside. Above where it would be if it were still a cube: past it.
        Assert.Equal("plank", scene.ObjectAlong(new Vector3(3, 0, 10), new Vector3(0, 0, -1)));
        Assert.Null(scene.ObjectAlong(new Vector3(0, 0.6, 10), new Vector3(0, 0, -1)));
    }

    [Fact]
    public void ThePointerOnAnArmPicksThatAxis()
    {
        var gizmo = GizmoAt(Vector3.Zero());
        var onX = gizmo.Projection.Project(gizmo.EndOf(GizmoAxis.X))!.Value;

        Assert.Equal(GizmoAxis.X, gizmo.AxisAt(onX));
    }

    [Fact]
    public void ThePointerNowhereNearPicksNothing()
    {
        var gizmo = GizmoAt(Vector3.Zero());
        Assert.Null(gizmo.AxisAt(new Offset(5, 5)));
    }

    [Fact]
    public void ARingIsGrabbedAtItsEdgeNotThroughItsMiddle()
    {
        var gizmo = GizmoAt(Vector3.Zero(), GizmoMode.Rotate);
        var centre = gizmo.Projection.Project(gizmo.Pivot)!.Value;

        // An eighth of the way round, which is between the axes. All three rings pass through the
        // six points where the axes are, so those belong to no one ring and the nearest is
        // whichever happens to be nearest.
        var ring = gizmo.RingOnScreen(GizmoAxis.Y);
        var edge = ring[ring.Count / 8];

        Assert.Equal(GizmoAxis.Y, gizmo.AxisAt(edge));
        // The middle of a ring is a hole, and a gizmo that grabbed there would take the ring
        // nobody was pointing at.
        Assert.Null(gizmo.AxisAt(centre));
    }

    [Fact]
    public void ADragAlongAnArmOnlyMovesAlongThatArm()
    {
        var gizmo = GizmoAt(Vector3.Zero());
        var from = gizmo.Projection.Project(gizmo.Pivot)!.Value;
        var to = from + new Offset(60, 20);

        var start = gizmo.PointOnAxis(from, GizmoAxis.X)!;
        var end = gizmo.PointOnAxis(to, GizmoAxis.X)!;
        var shift = end - start;

        Assert.True(Math.Abs(shift.Y) < 1e-9);
        Assert.True(Math.Abs(shift.Z) < 1e-9);
        Assert.True(Math.Abs(shift.X) > 0);
    }

    [Fact]
    public void AQuarterTurnMeasuresAQuarterTurn()
    {
        var gizmo = GizmoAt(Vector3.Zero(), GizmoMode.Rotate);

        var angle = gizmo.AngleBetween(new Vector3(1, 0, 0), new Vector3(0, 0, -1), GizmoAxis.Y);
        Assert.Equal(Math.PI / 2, angle, 1e-9);
    }

    [Fact]
    public void TurningTheOtherWayMeasuresTheOtherSign()
    {
        var gizmo = GizmoAt(Vector3.Zero(), GizmoMode.Rotate);

        var angle = gizmo.AngleBetween(new Vector3(1, 0, 0), new Vector3(0, 0, 1), GizmoAxis.Y);
        Assert.Equal(-Math.PI / 2, angle, 1e-9);
    }

    [Fact]
    public void PastAQuarterTurnKeepsGoingRatherThanSpringingBack()
    {
        var gizmo = GizmoAt(Vector3.Zero(), GizmoMode.Rotate);

        // Two hundred degrees: the short way round would report the wrong direction, and an object
        // being turned would snap backwards.
        var angle = gizmo.AngleBetween(
            new Vector3(1, 0, 0),
            new Vector3(Math.Cos(Doubles.Radians(-200)), 0, Math.Sin(Doubles.Radians(200))),
            GizmoAxis.Y);
        Assert.True(Math.Abs(angle) > Math.PI / 2);
    }

    [Fact]
    public void TheHandlesSitWhereTheObjectIs()
    {
        var gizmo = GizmoAt(new Vector3(3, 1, -2));
        var centre = gizmo.Projection.Project(new Vector3(3, 1, -2))!.Value;
        var drawn = gizmo.Projection.Project(gizmo.Pivot)!.Value;

        Assert.True((centre - drawn).Distance < 1e-9);
    }
}

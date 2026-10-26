namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Editor.Viewport;
using Orblit.Mesh;
using Orblit.VectorMath;
using Doc = Orblit.Scene;

// What a body is made to fit, what choosing a shape starts it as, and the words the section says.
public sealed class BodyFitTests
{
    // Nothing of its own to draw yet, so the placeholder, which is two metres across.
    private static SceneObject Crate(ObjectKind kind = ObjectKind.Mesh) => new("crate", "Crate", kind);

    private static SceneObject Cube() => new("cube", "Cube", ObjectKind.Shape, shape: Shape.Of(ShapeKind.Cube));

    [Fact]
    public void ABoxIsTheSizeOfWhatIsDrawnAndCentredOnIt()
    {
        var box = BodyFit.Fitted(new Doc.BodyComponent(), Crate(), (new Vector3(-1, 0, -1), new Vector3(1, 4, 1)));

        Assert.Equal(new Vector3(2, 4, 2), box.Size);
        Assert.Equal(new Vector3(0, 2, 0), box.Centre);
    }

    [Fact]
    public void ABoxOnTheBarePlaceholderIsTwoMetresAcross()
    {
        var box = BodyFit.Fitted(new Doc.BodyComponent(), Crate());

        Assert.Equal(Vector3.All(2), box.Size);
        Assert.Equal(Vector3.Zero(), box.Centre);
    }

    [Fact]
    public void ABoxOnAPlaceWithNothingDrawnIsAMetre() =>
        Assert.Equal(Vector3.All(1), BodyFit.Fitted(new Doc.BodyComponent(), Crate(ObjectKind.Group)).Size);

    [Fact]
    public void ABallTakesTheLargestSideAndACapsuleOrCylinderTheLargerSideways()
    {
        var bounds = (new Vector3(-1, -2, -0.5), new Vector3(1, 2, 0.5));

        var ball = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Sphere), Crate(), bounds);
        var capsule = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Capsule), Crate(), bounds);
        var cylinder = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Cylinder), Crate(), bounds);

        Assert.Equal(2, ball.Radius);
        Assert.Equal((1, 4), (capsule.Radius, capsule.Height));
        Assert.Equal((1, 4), (cylinder.Radius, cylinder.Height));
    }

    [Fact]
    public void GroundIsTheTopOfWhatIsDrawn()
    {
        var plane = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Plane), Crate(), (new Vector3(-3, -1, -3), new Vector3(3, 0.5, 3)));

        Assert.Equal(new Vector3(0, 0.5, 0), plane.Centre);
    }

    [Fact]
    public void ACompoundStartsAsOnePartTheSizeOfWhatIsDrawn()
    {
        var compound = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Compound), Crate());

        Assert.Equal(Vector3.All(2), Assert.Single(compound.Parts).Size);
    }

    [Fact]
    public void AMeshIsNeverRefitted()
    {
        var mesh = new Doc.BodyComponent(shape: Doc.BodyShape.Mesh);

        Assert.Same(mesh, BodyFit.Fitted(mesh, Crate()));
    }

    [Fact]
    public void AHullWithNoMeshToCutFromIsTheCornersOfItsBoxMeasuredFromTheMiddle()
    {
        var hull = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Hull), Crate()).Hull;

        Assert.Equal(24, hull.Count);
        Assert.All(hull, value => Assert.Equal(1, Math.Abs(value)));
    }

    [Fact]
    public void AHullCutFromAMeshKeepsTheCornersThatStandOut()
    {
        var hull = BodyFit.Fitted(new Doc.BodyComponent(shape: Doc.BodyShape.Hull), Cube()).Hull;

        // A cube has eight corners however many points its faces were built from.
        Assert.Equal(8, ConvexOutline.Of(hull).Corners.Count);
    }

    [Fact]
    public void ChoosingAHullWithNoPointsCutsOneAndChoosingItAgainKeepsTheOnesItHas()
    {
        var cut = BodyFit.Reshaped(new Doc.BodyComponent(), Doc.BodyShape.Hull, Crate());
        Assert.Equal(24, cut.Hull.Count);

        var own = new Doc.BodyComponent(shape: Doc.BodyShape.Sphere, hull: [0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1]);
        Assert.Equal(own.Hull, BodyFit.Reshaped(own, Doc.BodyShape.Hull, Crate()).Hull);
    }

    [Fact]
    public void ChoosingACompoundWithNoPartsGivesOneAndOtherShapesAreOnlyRenamed()
    {
        Assert.Single(BodyFit.Reshaped(new Doc.BodyComponent(), Doc.BodyShape.Compound, Crate()).Parts);

        var ball = BodyFit.Reshaped(new Doc.BodyComponent(size: new Vector3(3, 3, 3)), Doc.BodyShape.Sphere, Crate());
        // Written whole, so switching back is the box it was.
        Assert.Equal(Vector3.All(3), ball.Size);
        Assert.Equal(Doc.BodyShape.Sphere, ball.Shape);
    }

    [Fact]
    public void EveryShapeHasALabelAndTheLabelsComeBackAsTheirShape()
    {
        Assert.Equal(Enum.GetValues<Doc.BodyShape>().Length, BodyModel.Shapes.Count);
        foreach (var (shape, label) in BodyModel.Shapes)
        {
            Assert.Equal(shape, BodyModel.ShapeOf(label));
            Assert.Equal(label, BodyModel.LabelOf(shape));
        }

        foreach (var (motion, label) in BodyModel.Motions)
        {
            Assert.Equal(motion, BodyModel.MotionOf(label));
        }
    }

    [Fact]
    public void TheShapesAreLaidOutThreeToARow() =>
        Assert.Equal(
            [["Box", "Ball", "Capsule"], ["Cylinder", "Hull", "Compound"], ["Ground", "Mesh"]],
            BodyModel.ShapeRows());

    [Fact]
    public void AHullSaysHowManyCornersItHasOrWhatIsMissing()
    {
        Assert.Equal("8 corners.", BodyModel.HullNote(8, hasVolume: true));
        Assert.Equal(
            "300 corners. The simulation keeps the 255 that stand out most.",
            BodyModel.HullNote(300, hasVolume: true));
        Assert.Equal(
            "Needs four points that enclose a volume. Until then the body does nothing.",
            BodyModel.HullNote(0, hasVolume: false));
    }

    [Fact]
    public void AMeshSaysHowManyTrianglesItHas() =>
        Assert.Equal("12 triangles. Fixed, two-sided surface.", BodyModel.MeshNote(36));

    [Fact]
    public void ALockIsHeldAndThenGivenBackWithoutTouchingTheOthers()
    {
        var held = BodyModel.Locked(new Doc.BodyComponent(), Doc.BodyLock.MoveX);
        var both = BodyModel.Locked(held, Doc.BodyLock.TurnY);

        Assert.Equal([Doc.BodyLock.MoveX, Doc.BodyLock.TurnY], both.Locks.Order());
        Assert.Equal([Doc.BodyLock.TurnY], BodyModel.Locked(both, Doc.BodyLock.MoveX).Locks);
        Assert.Empty(held.Locks.Except([Doc.BodyLock.MoveX]));
    }
}

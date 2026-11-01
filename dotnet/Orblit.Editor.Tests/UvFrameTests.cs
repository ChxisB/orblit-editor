namespace Orblit.Editor.Tests;

using Orblit.Editor.Panels;
using Orblit.Mesh;
using Orblit.VectorMath;

// The frame arithmetic, apart from any control.
public sealed class UvFrameTests
{
    private static (Mesh Mesh, List<Face> Faces) FittedFace()
    {
        var mesh = Shape.Of(ShapeKind.Cube).Build();
        List<Face> faces = [mesh.Faces[0]];
        mesh.FitUvs(faces);
        return (mesh, faces);
    }

    [Fact]
    public void AViewWithNothingToFrameShowsTheSquareWithRoomAround()
    {
        var start = UvFrame.Start;

        Assert.Equal(1.4, start.Span);
        Assert.Equal(0.5, start.Centre.X);
        Assert.Equal(0.5, start.Centre.Y);
    }

    [Fact]
    public void AFaceInsideTheSquareIsFramedWithAQuarterToSpare()
    {
        var (mesh, faces) = FittedFace();

        var frame = UvFrame.Around(mesh, faces);

        Assert.Equal(1.25, frame.Span, 1e-9);
        Assert.Equal(0.5, frame.Centre.X, 1e-9);
        Assert.Equal(0.5, frame.Centre.Y, 1e-9);
    }

    [Fact]
    public void AFaceFarFromTheSquareIsFramedHalfwayToIt()
    {
        var (mesh, faces) = FittedFace();
        MeshUvs.NudgeUvs(faces, new Vector2(3, 0));

        var frame = UvFrame.Around(mesh, faces);

        // Halfway, so the face and the square it is read against are both in shot.
        Assert.Equal(2, frame.Centre.X, 1e-9);
        Assert.Equal(0.5, frame.Centre.Y, 1e-9);
    }

    [Fact]
    public void NoFacesFrameLikeNothingAtAll()
    {
        var mesh = Shape.Of(ShapeKind.Cube).Build();

        var frame = UvFrame.Around(mesh, []);

        Assert.Equal(UvFrame.Start.Span, frame.Span);
    }

    [Fact]
    public void ARangeBiggerThanTheSquareZoomsOut()
    {
        var (mesh, faces) = FittedFace();
        mesh.ScaleUvs(faces, new Vector2(4, 4));

        Assert.Equal(5, UvFrame.Around(mesh, faces).Span, 1e-9);
    }

    [Fact]
    public void APointAndItsCoordinateAreEachOthersInverse()
    {
        var frame = new UvFrame(2.5, new Vector2(0.3, 0.8));

        var uv = frame.ToUv(37, 80, 200, 160);
        var (x, y) = frame.At(uv, 200, 160);

        Assert.Equal(37, x, 1e-9);
        Assert.Equal(80, y, 1e-9);
    }

    [Fact]
    public void UpTheViewIsUpInTheTexture()
    {
        var uv = UvFrame.Start.ToUv(100, 0, 200, 200);

        Assert.Equal(0.5, uv.X, 1e-9);
        Assert.Equal(0.5 + (100 * 1.4 / 200), uv.Y, 1e-9);
    }

    [Fact]
    public void ASpanOfNothingIsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new UvFrame(0, new Vector2(0, 0)));

    [Fact]
    public void FacesFarFromTheSquareAreFramedWithBothInShot()
    {
        var (mesh, faces) = FittedFace();
        MeshUvs.NudgeUvs(faces, new Vector2(3, 0));

        var frame = UvFrame.Around(mesh, faces);

        // The faces span 3 to 4 and the square 0 to 1, so the view has to reach from one to the other.
        Assert.True(frame.Centre.X - (frame.Span / 2) <= 0);
        Assert.True(frame.Centre.X + (frame.Span / 2) >= 4);
    }

    [Fact]
    public void AFaceTuckedIntoACornerStillShowsTheWholeSquare()
    {
        var (mesh, faces) = FittedFace();
        mesh.ScaleUvs(faces, new Vector2(0.2, 0.2));
        MeshUvs.NudgeUvs(faces, new Vector2(0.3, 0));

        var frame = UvFrame.Around(mesh, faces);

        Assert.True(frame.Centre.X - (frame.Span / 2) <= 0);
        Assert.True(frame.Centre.X + (frame.Span / 2) >= 1);
    }
}

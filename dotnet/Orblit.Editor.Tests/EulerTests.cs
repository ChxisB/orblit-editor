namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;

public sealed class EulerTests
{
    // Reparenting rewrites an object's local transform so it does not jump, and that means turning
    // a rotation matrix back into the degrees the inspector shows. Getting the convention wrong is
    // silent: the object lands somewhere plausible and only looks wrong once it is animated.
    [Fact]
    public void DegreesSurviveATripThroughARotationAndBack()
    {
        var random = new Random(7);

        for (var i = 0; i < 200; i++)
        {
            // Kept off the poles, where two of the three angles are the same axis and no extraction
            // can recover the pair that went in.
            var input = new Vector3(
                (random.NextDouble() * 340) - 170,
                (random.NextDouble() * 160) - 80,
                (random.NextDouble() * 340) - 170);

            var recovered = SceneMath.EulerDegreesOf(Rotated("a", input).LocalTransform);

            // Compared as rotations rather than as numbers: a different triple can describe the
            // same orientation, and that is not an error.
            var wanted = Rotated("b", input).LocalTransform.GetRotation();
            var got = Rotated("c", recovered).LocalTransform.GetRotation();

            foreach (var probe in new[] { new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(0, 0, 1) })
            {
                var difference = wanted.Transformed(probe) - got.Transformed(probe);
                Assert.True(difference.Length < 1e-6, $"input {input} came back as {recovered}");
            }
        }
    }

    private static SceneObject Rotated(string id, Vector3 degrees) =>
        new(id, id, ObjectKind.Mesh, rotation: degrees);
}

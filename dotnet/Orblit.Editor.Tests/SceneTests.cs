namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

// The tests in scene_test.dart that read the render scene belong to the viewport task.
public sealed class SceneTests
{
    [Fact]
    public void ColourIsConvertedOutOfSrgbBecauseShadingIsLinear()
    {
        // Mid grey in sRGB is a good deal darker than a half in linear.
        var linear = EditorScene.LinearFromColour(new Colour(0xFF808080));
        Assert.Equal(0.2158, linear.X, 0.001);
    }

    [Fact]
    public void ACopyIsANewObjectNotTheSameOneSomewhereElse()
    {
        var original = new SceneObject("a", "A", ObjectKind.Mesh);
        var copy = original.CopyAs("b");

        Assert.NotEqual(original.RenderKey, copy.RenderKey);
    }
}

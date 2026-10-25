namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;

public sealed class SurfaceTests
{
    [Fact]
    public void AMaterialWithNothingSaidAboutItIsAPlainGreyOne()
    {
        var surface = new Surface();
        Assert.Equal(0, surface.Metallic);
        Assert.True(surface.Roughness > 0);
        Assert.False(surface.DoubleSided);
    }

    [Fact]
    public void TheColourIsConvertedOnTheWayToTheFile()
    {
        var glb = new Surface { Colour = new Colour(0xFF808080) }.ToGlb();
        // Mid grey in sRGB is a good deal darker than a half in linear, which is the whole reason
        // the conversion is not skipped.
        Assert.True(glb.Colour[0] < 0.3);
        Assert.True(glb.Colour[0] > 0.2);
        Assert.Equal(1.0, glb.Colour[3]);
    }

    [Fact]
    public void EmissiveIsTheColourScaled()
    {
        var dark = new Surface { Colour = new Colour(0xFFFFFFFF) };
        Assert.Equal([0.0, 0.0, 0.0], dark.ToGlb().Emissive);
        var lit = new Surface { Colour = new Colour(0xFFFFFFFF), Emissive = 2 };
        Assert.Equal(2, lit.ToGlb().Emissive[0], 1e-9);
    }

    [Fact]
    public void ItSurvivesBeingWrittenAndRead()
    {
        var surface = new Surface
        {
            Name = "Brass",
            Colour = new Colour(0xFFB5873A),
            Metallic = 1,
            Roughness = 0.25,
            Emissive = 0.5,
            DoubleSided = true,
        };
        var back = Surface.FromJson(surface.ToJson())!;
        Assert.Equal("Brass", back.Name);
        Assert.Equal(surface.Colour.Argb, back.Colour.Argb);
        Assert.Equal(1, back.Metallic);
        Assert.Equal(0.25, back.Roughness);
        Assert.Equal(0.5, back.Emissive);
        Assert.True(back.DoubleSided);
    }

    [Fact]
    public void ADefaultedMaterialWritesAlmostNothing() =>
        // A scene full of shapes would otherwise carry six numbers a slot that all say what the
        // default already says.
        Assert.Equal(["name", "colour"], new Surface().ToJson().Keys);

    [Fact]
    public void RubbishReadsBackAsNothingRatherThanAsADefault()
    {
        Assert.Null(Surface.FromJson(null));
        Assert.Null(Surface.FromJson("brass"));
        Assert.NotNull(Surface.FromJson(new Dictionary<string, object?>()));
    }
}

using Avalonia.VisualTree;

namespace Orblit.Editor.Tests;

// Dart: orblit-editor/test/outline_chip_test.dart. The Dart view was told whether to outline and told
// the shell when its chip was pressed. The C# view keeps the choice itself, so a press is read off what
// the overlay draws.
public sealed class OutlineChipTests
{
    private static ViewportRig Choosing()
    {
        var rig = new ViewportRig();
        rig.Session.SelectObject("ground");
        Headless.Render();
        return rig;
    }

    private static ViewportOverlay Overlay(ViewportRig rig) => rig.View.GetVisualDescendants().OfType<ViewportOverlay>().Single();

    private static void Press(ViewportRig rig, string chip) => rig.Click(rig.Where(rig.Chip(chip)));

    [Fact]
    public Task WithTheOutlineOnNothingIsDrawnOverThePicture() => Headless.Run(() =>
    {
        using var rig = Choosing();

        Assert.True(Overlay(rig).OutlineSelection);
        Assert.Contains("Outline", rig.Words());
    });

    [Fact]
    public Task WithItOffTheBoundaryIsDrawnAsItUsedToBe() => Headless.Run(() =>
    {
        using var rig = Choosing();

        Press(rig, "Outline");

        Assert.False(Overlay(rig).OutlineSelection);
        Assert.Contains("Boundary", rig.Words());
    });

    [Fact]
    public Task TheChipSwitchesBetweenThem() => Headless.Run(() =>
    {
        using var rig = Choosing();

        Press(rig, "Outline");
        Press(rig, "Boundary");

        Assert.True(Overlay(rig).OutlineSelection);
        Assert.Contains("Outline", rig.Words());
    });

    [Fact]
    public Task WithNothingSelectedThereIsNoChipToPress() => Headless.Run(() =>
    {
        using var rig = new ViewportRig();

        Assert.DoesNotContain("Outline", rig.Words());
        Assert.DoesNotContain("Boundary", rig.Words());
    });
}

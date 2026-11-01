using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Panels;
using Orblit.Mesh;
using Orblit.VectorMath;

public sealed class UvPanelTests : IDisposable
{
    private readonly UvRig _rig = new();

    public void Dispose() => _rig.Dispose();

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static string TextOf(Control root, string name) => Headless.Find<TextBlock>(root, name).Text ?? "";

    private static void Press(Window window, string name)
    {
        Headless.Find<Control>(window, name).BringIntoView();
        Headless.Render();
        Headless.Click(window, name);
    }

    [Fact]
    public Task SaysNothingIsSelectedAndOffersNothingToPress() => Headless.Run(() =>
    {
        var window = _rig.Show();

        Assert.StartsWith("Select faces", TextOf(window, "uv_nothing"));
        Assert.Equal("Nothing selected.", TextOf(window, "uv_status"));
        Assert.False(Has(window, "uv_canvas"));
        Assert.All(Enum.GetValues<UvAction>(), action => Assert.False(Headless.Find<Button>(window, $"button_{Fields.Key(action.Label())}").IsEnabled));
        window.Close();
    });

    [Fact]
    public Task AFaceIsDrawnAndFollowsTheRuleAndItsNumbersAreShown() => Headless.Run(() =>
    {
        var window = _rig.Show();

        _rig.Select(0);
        Headless.Render();

        Assert.Equal("1 selected · following the rule", TextOf(window, "uv_status"));
        Assert.True(Has(window, "uv_canvas"));
        Assert.True(Has(window, "slider_repeat_across"));
        Assert.True(Has(window, "choice_fill_tile"));
        window.Close();
    });

    [Fact]
    public Task TwoFacesGetNoRuleBecauseTheyMayNotAgree() => Headless.Run(() =>
    {
        var window = _rig.Show();

        _rig.Select(0, 1);
        Headless.Render();

        Assert.Equal("2 selected · following the rule", TextOf(window, "uv_status"));
        Assert.False(Has(window, "slider_repeat_across"));
        window.Close();
    });

    [Fact]
    public Task FreezingFromThePanelDrawsTheFaceByHandAndDropsTheRule() => Headless.Run(() =>
    {
        var window = _rig.Show();
        _rig.Select(0);
        Headless.Render();

        Press(window, "button_freeze");
        Headless.Render();

        Assert.True(_rig.Mesh.Faces[0].Uv.IsManual);
        Assert.Equal("1 selected · drawn by hand", TextOf(window, "uv_status"));
        Assert.False(Has(window, "slider_repeat_across"));
        window.Close();
    });

    [Fact]
    public Task TheRuleSlidersChangeTheFace() => Headless.Run(() =>
    {
        var window = _rig.Show();
        _rig.Select(0);
        Headless.Render();

        Press(window, "choice_fill_stretch");
        Headless.Render();

        Assert.Equal(UvFit.Stretch, _rig.Mesh.Faces[0].Uv.Fit);
        window.Close();
    });

    [Fact]
    public Task ItSaysSoWhenTheWrongElementModeIsOn() => Headless.Run(() =>
    {
        var window = _rig.Show();
        Assert.False(Has(window, "uv_wrong_mode"));

        _rig.Session.SetElementMode(ElementMode.Vertex);
        Headless.Render();

        Assert.Contains("belong to faces", TextOf(window, "uv_wrong_mode"));
        window.Close();
    });

    [Fact]
    public Task PickingAGestureChangesWhatADragDoes() => Headless.Run(() =>
    {
        var window = _rig.Show();

        Press(window, "choice_drag_turn");

        Assert.Equal(UvGesture.Turn, _rig.Session.UvGesture);
        window.Close();
    });

    [Fact]
    public Task ADragInTheSquareMovesTheTextureAndIsOneStepToUndo() => Headless.Run(() =>
    {
        var window = _rig.Show();
        _rig.Select(0);
        Headless.Render();
        var canvas = Headless.Find<UvCanvas>(window, "uv_canvas");
        var frame = UvFrame.Around(_rig.Mesh, _rig.Session.UvSelection.FacesIn(_rig.Mesh));
        var before = _rig.Corners(0)[0].X;
        var from = Headless.Centre(window, canvas);

        window.MouseDown(from, MouseButton.Left);
        for (var step = 1; step <= 4; step++)
        {
            window.MouseMove(from + new Vector(step * 10, 0));
        }

        window.MouseUp(from + new Vector(40, 0), MouseButton.Left);
        Headless.Render();

        Assert.Equal(before + (40 * frame.Span / canvas.Bounds.Width), _rig.Corners(0)[0].X, 1e-6);
        _rig.Session.Undo();
        Assert.Equal(before, _rig.Corners(0)[0].X, 1e-9);
        window.Close();
    });

    [Fact]
    public Task ScalingIsMeasuredFromTheMiddleOfTheFacesNotTheMiddleOfTheView() => Headless.Run(() =>
    {
        var window = _rig.Show();
        _rig.Select(0);
        _rig.Session.NudgeUvs(new Vector2(0.3, 0));
        _rig.Select(1);
        Headless.Render();
        _rig.Select(0);
        Headless.Render();
        Press(window, "choice_drag_scale");
        var canvas = Headless.Find<UvCanvas>(window, "uv_canvas");
        var faces = _rig.Session.UvSelection.FacesIn(_rig.Mesh);
        var frame = UvFrame.Around(_rig.Mesh, faces);
        var middle = MiddleOf(faces);
        var width = Width(_rig.Corners(0));

        Point At(double along)
        {
            var (x, y) = frame.At(new Vector2(middle.X + along, middle.Y + along), canvas.Bounds.Width, canvas.Bounds.Height);
            return canvas.TranslatePoint(new Point(x, y), window)!.Value;
        }

        // From a fifth of a unit out to two fifths, so the faces should come out twice as wide.
        window.MouseDown(At(0.2), MouseButton.Left);
        for (var step = 1; step <= 4; step++)
        {
            window.MouseMove(At(0.2 + (0.05 * step)));
        }

        window.MouseUp(At(0.4), MouseButton.Left);

        Assert.Equal(2 * width, Width(_rig.Corners(0)), 1e-6);
        window.Close();
    });

    private Vector2 MiddleOf(List<Face> faces) => _rig.Mesh.UvBoundsOf(faces) is { } box
        ? (box.Min + box.Max) * 0.5
        : throw new InvalidOperationException("No faces.");

    private static double Width(List<Vector2> corners) => corners.Max(one => one.X) - corners.Min(one => one.X);
}

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Mesh;

public sealed class ModellingPanelTests : IDisposable
{
    private readonly ModellingRig _rig = new();
    private readonly DirectoryInfo _temp = Directory.CreateTempSubdirectory("orblit_modelling_dock");

    public void Dispose()
    {
        _rig.Dispose();
        _temp.Delete(recursive: true);
    }

    private static bool Has(Control root, string name) => Headless.All<Control>(root).Any(one => one.Name == name);

    private static string TextOf(Control root, string name) => Headless.Find<TextBlock>(root, name).Text ?? "";

    private static void Press(Window window, string name)
    {
        Headless.Find<Control>(window, name).BringIntoView();
        Headless.Render();
        Headless.Click(window, name);
    }

    private static Window WithACube(ModellingRig rig)
    {
        rig.Session.AddShape(ShapeKind.Cube);
        return rig.Show();
    }

    [Fact]
    public Task OpensFromTheModellingModeOfTheDock() => Headless.Run(() =>
    {
        var opened = ProjectWorkspace.Open(_temp.FullName);
        var session = new EditorSession(opened.Workspace, new History(opened.Workspace), EditorModes.Standard(), PanelTypes.BuiltIn());
        var window = new EditorWindow(session, HostPlatform.MacOS);
        window.Show();

        Headless.Find<Button>(window, "mode_modelling").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Headless.Render();

        Assert.True(Has(window, "modelling_fields"));
        window.Close();
    });

    [Fact]
    public Task WithNothingSelectedItOffersDrawingAndSaysSo() => Headless.Run(() =>
    {
        var window = _rig.Show();

        Assert.StartsWith("Select a shape", TextOf(window, "modelling_empty"));
        Assert.True(Headless.Find<Button>(window, "button_draw_shape").IsEnabled);
        Assert.False(Headless.Find<Button>(window, "button_cut").IsEnabled);
        Assert.False(Has(window, "modelling_group_whole_shape"));
        window.Close();
    });

    [Fact]
    public Task ASelectedObjectThatIsNotAShapeIsNamed() => Headless.Run(() =>
    {
        var window = _rig.Show();

        _rig.Session.History.Run(new AddObject(new SceneObject(id: "extra", name: "Extra", kind: ObjectKind.Group), "a"));
        _rig.Session.SelectObject("extra");
        Headless.Render();

        Assert.StartsWith("Extra is not a shape", TextOf(window, "modelling_empty"));
        window.Close();
    });

    [Fact]
    public Task CuttingIsOfferedOnceThereIsAShape() => Headless.Run(() =>
    {
        var window = _rig.Show();

        _rig.Session.AddShape(ShapeKind.Cube);
        Headless.Render();

        Assert.True(Headless.Find<Button>(window, "button_cut").IsEnabled);
        Assert.False(Has(window, "modelling_empty"));
        window.Close();
    });

    [Fact]
    public Task TheShelvesFollowWhatIsSelected() => Headless.Run(() =>
    {
        var window = WithACube(_rig);

        Assert.True(Has(window, "modelling_group_whole_shape"));
        Assert.False(Has(window, "modelling_group_selection"));

        _rig.Edit(0);
        Headless.Render();

        Assert.True(Has(window, "modelling_group_geometry"));
        Assert.True(Has(window, "modelling_group_selection"));
        Assert.StartsWith("1 selected", TextOf(window, "modelling_parts"));
        window.Close();
    });

    [Fact]
    public Task DrawingAShapeStartsTheToolAndSaysHowToFinish() => Headless.Run(() =>
    {
        var window = _rig.Show();

        Press(window, "button_draw_shape");

        Assert.Equal(ViewportTool.PolyShape, _rig.Session.Sketch.Tool);
        Assert.StartsWith("0 points", TextOf(window, "modelling_drawing"));
        Assert.Contains("Enter finishes", TextOf(window, "modelling_drawing"));
        window.Close();
    });

    [Fact]
    public Task SnappingIsOneToggleAndAChoiceAndFollowsAChangeMadeElsewhere() => Headless.Run(() =>
    {
        var window = _rig.Show();
        Assert.True(Has(window, "modelling_snap_target"));

        Press(window, "toggle_snap_to_grid");

        Assert.False(_rig.Snapping.On);
        Assert.False(Has(window, "modelling_snap_target"));

        Press(window, "toggle_snap_to_grid");

        Assert.True(Has(window, "modelling_snap_target"));

        Press(window, "choice_put_on_the_line_top");

        Assert.Equal(SnapTo.Top, _rig.Snapping.To);

        _rig.Snapping.On = false;
        Headless.Render();

        Assert.False(Has(window, "modelling_snap_target"));
        window.Close();
    });

    [Fact]
    public Task AnActionRunsFromItsButtonAndUndoesAsOneStep() => Headless.Run(() =>
    {
        var window = WithACube(_rig);
        _rig.Edit(0);
        Headless.Render();
        var before = _rig.Mesh.Faces.Count;

        Press(window, "button_extrude");

        Assert.True(_rig.Mesh.Faces.Count > before);
        Assert.StartsWith("Extrude", _rig.Session.History.UndoLabel);
        window.Close();
    });

    [Fact]
    public Task AnActionsAmountIsASliderUnderItsButton() => Headless.Run(() =>
    {
        var window = WithACube(_rig);
        _rig.Edit(0);
        Headless.Render();

        Assert.True(Has(Headless.Find<StackPanel>(window, "action_extrude"), "slider_distance"));
        window.Close();
    });

    [Fact]
    public Task NewMaterialsAreListedAndPaintingGivesTheSelectedFacesThatSlot() => Headless.Run(() =>
    {
        var window = WithACube(_rig);
        Assert.True(Has(window, "modelling_no_materials"));

        Press(window, "button_add_material");
        Press(window, "button_add_material");
        _rig.Edit(0);
        Headless.Render();

        Assert.True(Has(window, "surface_0"));
        Assert.True(Has(window, "surface_1"));
        Assert.False(Has(window, "modelling_no_materials"));

        Press(window, "paint_1");

        Assert.Equal(1, _rig.Mesh.Faces[0].Material);
        window.Close();
    });

    [Fact]
    public Task AMaterialHasNoPaintButtonWithNothingToPaint() => Headless.Run(() =>
    {
        var window = WithACube(_rig);

        Press(window, "button_add_material");

        Assert.True(Has(window, "surface_0"));
        Assert.False(Has(window, "paint_0"));
        window.Close();
    });

    [Fact]
    public Task AMaterialSaysHowManyFacesWearIt() => Headless.Run(() =>
    {
        var window = WithACube(_rig);

        Press(window, "button_add_material");

        Assert.Contains("6", string.Join(' ', Headless.All<TextBlock>(Headless.Find<Border>(window, "surface_0")).Select(one => one.Text)));
        window.Close();
    });
}

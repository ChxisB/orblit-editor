using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.Editor.Viewport;
using Orblit.Filament;
using static Orblit.Editor.Tests.InspectorRig;

// An author-written shader in the editor: cooked as it is saved, kept when a later save does not
// compile, worn by an object with values of its own, and shown in the inspector as the controls
// its hints ask for.
public sealed class ShaderTests : IDisposable
{
    private const string Shader = "assets/pulse.oshader";

    private const string Declared = """
        [
          {"name": "tint", "type": "vec4", "instance": true, "hints": {"source_color": []}, "default": [1.0, 0.5, 0.2, 1.0]},
          {"name": "speed", "type": "float", "instance": true, "hints": {"hint_range": ["0.0", "10.0", "0.1"]}, "default": 2.0},
          {"name": "glow", "type": "float", "instance": false, "default": 1.0}
        ]
        """;

    public static bool HasMatc { get; } = Environment.GetEnvironmentVariable("ORBLIT_MATC") is not null;

    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_shader");
    private readonly List<string> _told = [];
    private IReadOnlyList<ShaderCooked> _next = [Good()];
    private int _cooks;
    private SurfaceCooks _surfaces;

    public ShaderTests()
    {
        _surfaces = new SurfaceCooks(_project.FullName, _told.Add, CountingCook);
        Save("shader_type spatial;");
    }

    public void Dispose()
    {
        _surfaces.Dispose();
        _project.Delete(recursive: true);
    }

    private static ShaderCooked Good() => new(Shader, "/cook/pulse.filamat", Declared);

    private static ShaderCooked Bad() => new(Shader, Problem: "assets/pulse.oshader:7: no such name");

    private static RenderCamera Camera() => new OrbitCamera().ToRenderCamera();

    private static SceneObject Wearing(string id, params (string Name, object Value)[] values)
    {
        var stated = new OrderedDictionary<string, object>();
        foreach (var (name, value) in values)
        {
            stated[name] = value;
        }

        return new SceneObject(id, id, ObjectKind.Mesh, materialAsset: Shader, materialValues: stated);
    }

    private Task<IReadOnlyList<ShaderCooked>> CountingCook(string project)
    {
        _cooks++;
        return Task.FromResult(_next);
    }

    /// <summary>Saves the shader, so the next refresh sees a file that has changed.</summary>
    private void Save(string text)
    {
        var path = Path.Combine(_project.FullName, Shader);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private RenderScene Drawn(EditorScene scene, EditorScene? shared = null) => scene.ToRenderScene(
        Camera(),
        new SceneRenderOptions { ProjectRoot = _project.FullName, Shared = shared, SurfaceOf = _surfaces.SurfaceFor });

    [Fact]
    public async Task AnObjectDrawsTheCookWithItsDefaultsThenItsValues()
    {
        await _surfaces.RefreshAsync();

        var plain = _surfaces.SurfaceFor(Wearing("a"))!;
        Assert.Equal("/cook/pulse.filamat", plain.Package);
        Assert.Equal([2.0], plain.Values["speed"]);
        Assert.Equal([1.0, 0.5, 0.2, 1.0], plain.Values["tint"]);

        var stated = _surfaces.SurfaceFor(Wearing("b", ("speed", 4L)))!;
        Assert.Equal([4.0], stated.Values["speed"]);
        Assert.Equal([1.0, 0.5, 0.2, 1.0], stated.Values["tint"]);
    }

    [Fact]
    public async Task NothingIsCookedAgainUntilAShaderChanges()
    {
        await _surfaces.RefreshAsync();
        await _surfaces.RefreshAsync();
        Assert.Equal(1, _cooks);

        Save("shader_type spatial; // edited");
        await _surfaces.RefreshAsync();
        Assert.Equal(2, _cooks);
    }

    [Fact]
    public async Task ASaveThatDoesNotCompileLeavesTheLastGoodSurface()
    {
        await _surfaces.RefreshAsync();

        _next = [Bad()];
        Save("shader_type spatial; oops");
        await _surfaces.RefreshAsync();

        Assert.NotNull(_surfaces.SurfaceFor(Wearing("a")));
        Assert.Contains("pulse.oshader:7", _surfaces.ProblemWith(Shader));
        Assert.Equal(["assets/pulse.oshader:7: no such name"], _told);

        // The same complaint on the next save is not told again.
        Save("shader_type spatial; oops again");
        await _surfaces.RefreshAsync();
        Assert.Single(_told);

        _next = [Good()];
        Save("shader_type spatial;");
        await _surfaces.RefreshAsync();
        Assert.Null(_surfaces.ProblemWith(Shader));
    }

    [Fact]
    public async Task AShaderNobodyCookedDrawsNothingAndOneDeletedGoes()
    {
        Assert.Null(_surfaces.SurfaceFor(Wearing("a")));

        await _surfaces.RefreshAsync();
        Assert.NotNull(_surfaces.SurfaceFor(Wearing("a")));

        _next = [];
        File.Delete(Path.Combine(_project.FullName, Shader));
        await _surfaces.RefreshAsync();
        Assert.Null(_surfaces.SurfaceFor(Wearing("a")));
    }

    [Fact]
    public async Task ACookThatCannotStartIsToldOnceAndTriedAgain()
    {
        var tries = 0;
        _surfaces = new SurfaceCooks(
            _project.FullName,
            _told.Add,
            _ => ++tries < 3 ? throw new IOException("no matc") : Task.FromResult(_next));

        await _surfaces.RefreshAsync();
        await _surfaces.RefreshAsync();
        Assert.StartsWith("Could not cook the shaders", Assert.Single(_told));

        await _surfaces.RefreshAsync();
        Assert.NotNull(_surfaces.SurfaceFor(Wearing("a")));
    }

    [Fact]
    public void AnObjectKeepsItsValuesThroughTheSceneFile()
    {
        var scene = new EditorScene([Wearing("a", ("speed", 4L), ("tint", new List<object?> { 0.0, 1.0, 0.0, 1.0 }))]);

        var back = SceneDocument.Decode(SceneDocument.Encode(scene)).Scene["a"]!;

        Assert.True(back.WearsSurface);
        Assert.Equal(["speed", "tint"], back.MaterialValues.Keys);
        Assert.Equal(4.0, Convert.ToDouble(back.MaterialValues["speed"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal([0.0, 1.0, 0.0, 1.0], Assert.IsAssignableFrom<IEnumerable<object?>>(back.MaterialValues["tint"]));
    }

    [Fact]
    public void NoValuesAreWrittenForAPicturePutOnAfterIt()
    {
        var item = Wearing("a", ("speed", 4L));
        item.MaterialAsset = "assets/colormap.png";

        var back = SceneDocument.Decode(SceneDocument.Encode(new EditorScene([item]))).Scene["a"]!;

        Assert.Empty(back.MaterialValues);
    }

    [Fact]
    public void ACopyHasValuesOfItsOwn()
    {
        var item = Wearing("a", ("speed", 4L));
        var copy = item.CopyAs("b");
        copy.MaterialValues = new OrderedDictionary<string, object> { ["speed"] = 9L };
        Assert.Equal(4L, item.MaterialValues["speed"]);
    }

    [Fact]
    public async Task AnObjectIsDrawnWithItsSurfaceAndOneThatStatesNothingShares()
    {
        await _surfaces.RefreshAsync();

        var drawn = Drawn(new EditorScene([Wearing("a"), Wearing("b"), Wearing("c", ("speed", 5L))]));

        var keys = drawn.Objects.Select(item => item.Material).ToList();
        Assert.All(keys, key => Assert.NotNull(key));
        Assert.Equal(keys[0], keys[1]);
        Assert.NotEqual(keys[0], keys[2]);
        Assert.Equal(2, drawn.Materials.Count(material => material.Surface is not null));
    }

    [Fact]
    public async Task ASharedSceneObjectWithTheSameIdHasASurfaceOfItsOwn()
    {
        await _surfaces.RefreshAsync();

        var drawn = Drawn(
            new EditorScene([Wearing("a", ("speed", 5L))]),
            shared: new EditorScene([Wearing("a", ("speed", 6L))]));

        var speeds = drawn.Materials.Where(material => material.Surface is not null).Select(material => material.Surface!.Values["speed"][0]);
        Assert.Equal([5.0, 6.0], speeds.Order());
    }

    [Fact]
    public void AnObjectKeepsTheMeshItCameWithUntilTheShaderIsCooked()
    {
        var drawn = Drawn(new EditorScene([Wearing("a")]));

        Assert.Null(drawn.Objects.Single().Material);
        Assert.DoesNotContain(drawn.Materials, material => material.Surface is not null);
    }

    // The one place the cook runs for real, off the interface thread as the editor runs it.
    [Fact(Skip = "ORBLIT_MATC is not set", SkipUnless = nameof(HasMatc))]
    public async Task BothStartersCookWithTheRealMatcAndOfferTheirInspectorControls()
    {
        File.Delete(Path.Combine(_project.FullName, Shader));
        WriteStarters();
        using var real = new SurfaceCooks(_project.FullName, _told.Add);

        await real.RefreshAsync();

        Assert.Empty(_told);
        foreach (var name in new[] { "lit", "unlit" })
        {
            var cooked = real.CookedOf($"assets/{name}.oshader")!;
            Assert.True(File.Exists(cooked.Package), name);
            Assert.Single(cooked.Params, param => param.Instance && param.IsColour);
        }

        var unlit = new SceneObject("a", "a", ObjectKind.Mesh, materialAsset: "assets/unlit.oshader");
        Assert.NotNull(real.SurfaceFor(unlit));
    }

    [Fact(Skip = "ORBLIT_MATC is not set", SkipUnless = nameof(HasMatc))]
    public async Task ASaveThatFailsLeavesTheLastPackageAndASaveLeavesTheRest()
    {
        File.Delete(Path.Combine(_project.FullName, Shader));
        WriteStarters();
        using var real = new SurfaceCooks(_project.FullName, _told.Add);
        await real.RefreshAsync();
        var lit = real.CookedOf("assets/lit.oshader")!.Package;
        var unlit = real.CookedOf("assets/unlit.oshader")!.Package;
        var before = File.GetLastWriteTimeUtc(unlit);
        var bytes = new FileInfo(lit).Length;

        File.WriteAllText(Path.Combine(_project.FullName, "assets", "lit.oshader"), "this is not a shader");
        await real.RefreshAsync();

        Assert.NotNull(real.ProblemWith("assets/lit.oshader"));
        Assert.Equal(bytes, new FileInfo(lit).Length);
        Assert.Equal(before, File.GetLastWriteTimeUtc(unlit));
    }

    [Fact]
    public async Task AProjectWithNoShaderIsNotCooked()
    {
        File.Delete(Path.Combine(_project.FullName, Shader));
        File.WriteAllText(Path.Combine(_project.FullName, "assets", "note.txt"), "not a shader");

        Assert.Empty(await ShaderCook.CookAsync(_project.FullName));
    }

    // Without a compiler the cook cannot make a package, and says so against the shader and not as a throw.
    [Fact]
    public async Task AShaderThatCannotBeCompiledIsAProblemAgainstThatShader()
    {
        Assert.SkipWhen(HasMatc, "A compiler is set, so this shader may cook.");
        Save("shader_type spatial;");

        var cooked = Assert.Single(await ShaderCook.CookAsync(_project.FullName));

        Assert.Equal(Shader, cooked.Path);
        Assert.Null(cooked.Package);
        Assert.False(string.IsNullOrWhiteSpace(cooked.Problem));
    }

    private void WriteStarters()
    {
        File.WriteAllText(Path.Combine(_project.FullName, "assets", "lit.oshader"), NewAsset.Shader.Contents("lit"));
        File.WriteAllText(Path.Combine(_project.FullName, "assets", "unlit.oshader"), NewAsset.UnlitShader.Contents("unlit"));
    }

    private (EditorSession Session, Window Window) Show(EditorScene scene) =>
        Selecting("a", scene, new InspectorLinks { Surfaces = _surfaces });

    [Fact]
    public Task TheSectionSaysTheShaderIsCookingUntilItHasBeen() => Headless.Run(() =>
    {
        var (_, window) = Show(new EditorScene([Wearing("a")]));

        Assert.True(Has(window, "section_shader"));
        Assert.Contains(Headless.All<TextBlock>(window), text => text.Text == "Cooking the shader.");
    });

    [Fact]
    public Task TheSectionOffersAColourForSourceColourAndASliderForHintRange() => Headless.Run(async () =>
    {
        await _surfaces.RefreshAsync();
        var (_, window) = Show(new EditorScene([Wearing("a")]));

        Assert.True(Has(window, "swatch_tint_0"));
        Assert.True(Has(window, "slider_speed"));
        // Set for the shader as a whole, so not offered for each object.
        Assert.DoesNotContain(Headless.All<TextBlock>(window), text => text.Text == "glow");
    });

    [Fact]
    public Task ASliderEditIsOneStepAndUndoPutsTheValueBack() => Headless.Run(async () =>
    {
        await _surfaces.RefreshAsync();
        var scene = new EditorScene([Wearing("a")]);
        var (session, window) = Show(scene);

        Headless.Find<Slider>(window, "slider_speed").Value = 5;
        Headless.Render();

        Assert.Equal(5.0, Convert.ToDouble(scene["a"]!.MaterialValues["speed"], System.Globalization.CultureInfo.InvariantCulture), 6);
        Assert.Equal("Set a speed", session.History.Labels[^1]);

        session.History.Undo();
        Headless.Render();
        Assert.Empty(scene["a"]!.MaterialValues);
    });

    [Fact]
    public Task TheSectionShowsTheProblemAtTheAuthorLineWhenASaveFails() => Headless.Run(async () =>
    {
        await _surfaces.RefreshAsync();
        _next = [Bad()];
        Save("shader_type spatial; oops");
        await _surfaces.RefreshAsync();

        var (_, window) = Show(new EditorScene([Wearing("a")]));

        Assert.Single(Headless.All<TextBlock>(window), text => text.Text?.Contains("pulse.oshader:7", StringComparison.Ordinal) == true);
        // The last good cook still offers its controls.
        Assert.True(Has(window, "slider_speed"));
    });
}

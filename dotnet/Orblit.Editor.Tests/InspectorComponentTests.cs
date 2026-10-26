using Avalonia.Controls;

namespace Orblit.Editor.Tests;

using Orblit.Editor.Core;
using Orblit.VectorMath;
using Doc = Orblit.Scene;
using static Orblit.Editor.Tests.InspectorRig;

/// <summary>The sound, particles, vehicle, wheel and zone sections, as a person meets them in the inspector.</summary>
public sealed class InspectorComponentTests : IDisposable
{
    private readonly DirectoryInfo _project = Directory.CreateTempSubdirectory("orblit_inspector_components");

    public void Dispose() => _project.Delete(recursive: true);

    // A car that moves, a slab that does not, a wheel that is a child of the car, and things to make noise.
    private static EditorScene World() => new(
    [
        Thing("car", ObjectKind.Mesh, body: new Doc.BodyComponent()),
        Thing("slab", ObjectKind.Mesh, body: new Doc.BodyComponent(motion: Doc.BodyMotion.Fixed)),
        Thing("wheel", ObjectKind.Mesh, parent: "car"),
        Thing("hub", ObjectKind.Group, parent: "wheel"),
        Thing("fire", ObjectKind.Group),
        Thing("rock", ObjectKind.Mesh),
        Thing("sky", ObjectKind.Weather),
    ]);

    private static SceneObject Thing(string id, ObjectKind kind, string? parent = null, Doc.BodyComponent? body = null)
    {
        var parts = new OrderedDictionary<string, Doc.SceneComponent>();
        if (body is not null)
        {
            parts[Doc.SceneComponents.Body] = body;
        }

        return new SceneObject(id, id, kind, parentId: parent, components: parts);
    }

    private static SceneObject In(EditorSession session, string id) => session.Workspace.Loaded!.Scene![id]!;

    [Fact]
    public Task SoundIsOnEveryObjectAndNotOnTheWeatherOrTheScene() => Headless.Run(() =>
    {
        var (_, window) = Selecting("rock", World());
        Assert.True(Has(window, "section_sound"));

        var (_, weather) = Selecting("sky", World());
        Assert.False(Has(weather, "section_sound"));

        var (_, scene) = Open(World());
        Assert.False(Has(scene, "section_sound"));
    });

    [Fact]
    public Task AddingASourceAndTurningOffLoopIsTwoStepsAndLeavesTheRangeAlone() => Headless.Run(() =>
    {
        var (session, window) = Selecting("fire", World());

        Press(window, "button_add_sound_source");
        Press(window, "choice_loop_no");

        var source = PhysicsComponents.SoundSourceOf(In(session, "fire"))!;
        Assert.False(source.Loop);
        Assert.Equal(50, source.Far);
        session.History.Undo();
        Assert.True(PhysicsComponents.SoundSourceOf(In(session, "fire"))!.Loop);
        session.History.Undo();
        Assert.Null(PhysicsComponents.SoundSourceOf(In(session, "fire")));
    });

    [Fact]
    public Task TypingAClipNamesItAndAnEmptyTextClearsIt() => Headless.Run(() =>
    {
        var (session, window) = Selecting("fire", World());
        Press(window, "button_add_sound_source");

        Type(window, "entry_clip", "a.ogg");
        Assert.Equal("a.ogg", PhysicsComponents.SoundSourceOf(In(session, "fire"))!.Clip);

        Type(window, "entry_clip", "");
        Assert.Null(PhysicsComponents.SoundSourceOf(In(session, "fire"))!.Clip);
    });

    [Fact]
    public Task TypingASurfaceAddsOneAndAnEmptyTextTakesItOff() => Headless.Run(() =>
    {
        var (session, window) = Selecting("rock", World());

        Type(window, "entry_surface", "stone");
        Assert.Equal("stone", Surface(session, "rock")?.Name);

        Type(window, "entry_surface", "");
        Assert.Null(Surface(session, "rock"));
    });

    [Fact]
    public Task ChoosingWhatIsAlreadyChosenLeavesNothingToUndo() => Headless.Run(() =>
    {
        var (session, window) = Selecting("fire", World());
        Press(window, "button_add_sound_source");
        session.History.Clear();

        Press(window, "choice_loop_yes");
        Press(window, "choice_listener_no");

        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task TheListenerSwitchesOnAndOff() => Headless.Run(() =>
    {
        var (session, window) = Selecting("fire", World());

        Press(window, "choice_listener_yes");
        Assert.True(In(session, "fire").Components.ContainsKey(Doc.SceneComponents.SoundListener));

        Press(window, "choice_listener_no");
        Assert.False(In(session, "fire").Components.ContainsKey(Doc.SceneComponents.SoundListener));
    });

    [Fact]
    public Task AddingAnEmitterAndMovingItToLocalSpaceIsTwoSteps() => Headless.Run(() =>
    {
        var (session, window) = Selecting("fire", World());

        Press(window, "button_add_particle_emitter");
        Press(window, "choice_space_local");

        var emitter = Emitter(session, "fire")!;
        Assert.Equal(Doc.EmitterSpace.Local, emitter.Space);
        Assert.Equal(10, emitter.Rate);
        session.History.Undo();
        Assert.Equal(Doc.EmitterSpace.World, Emitter(session, "fire")!.Space);
        session.History.Undo();
        Assert.Null(Emitter(session, "fire"));
    });

    [Fact]
    public Task EachShapeShowsTheSizesItHas() => Headless.Run(() =>
    {
        var (_, window) = Selecting("fire", World());
        Press(window, "button_add_particle_emitter");
        Assert.False(Has(window, "drag_radius_0"));
        Assert.False(Has(window, "drag_half_x_0"));

        Press(window, "choice_shape_cone");
        Assert.True(Has(window, "drag_radius_0"));
        Assert.True(Has(window, "drag_angle_0"));

        Press(window, "choice_shape_box");
        Assert.False(Has(window, "drag_radius_0"));
        Assert.True(Has(window, "drag_half_x_0"));

        Press(window, "choice_shape_point");
        Assert.False(Has(window, "drag_half_x_0"));
    });

    [Fact]
    public Task AnEmitterTakenOffComesBackWithUndoAndItsRate() => Headless.Run(() =>
    {
        var scene = World();
        scene["fire"]!.Components[Doc.SceneComponents.Particles] = new Doc.ParticleEmitterComponent(rate: 33);
        var (session, window) = Selecting("fire", scene);

        Press(window, "choice_space_world");
        Press(window, "choice_shape_point");
        Assert.False(session.History.CanUndo);

        Press(window, "button_remove_particle_emitter");
        Assert.Null(Emitter(session, "fire"));
        session.History.Undo();
        Assert.Equal(33, Emitter(session, "fire")!.Rate);
    });

    [Fact]
    public Task TheWeatherHasNoParticles() => Headless.Run(() =>
    {
        var (_, window) = Selecting("sky", World());

        Assert.False(Has(window, "section_particles"));
    });

    [Fact]
    public Task AVehicleIsOfferedOnABodyThatMovesAndNotOnOneThatDoesNot() => Headless.Run(() =>
    {
        var (_, car) = Selecting("car", World());
        Assert.True(Has(car, "section_vehicle"));

        var (_, slab) = Selecting("slab", World());
        Assert.False(Has(slab, "section_vehicle"));

        var (_, wheel) = Selecting("wheel", World());
        Assert.False(Has(wheel, "section_vehicle"));
    });

    [Fact]
    public Task AVehicleAFilePutOnAFixedBodyIsStillShownSoItCanBeTakenOff() => Headless.Run(() =>
    {
        var scene = World();
        scene["slab"]!.Components[Doc.SceneComponents.Vehicle] = new Doc.VehicleComponent();
        var (_, window) = Selecting("slab", scene);

        Assert.True(Has(window, "section_vehicle"));
    });

    [Fact]
    public Task AWheelIsOfferedOnTheChildOfACarAndNotOnTheCarOrADeeperChild() => Headless.Run(() =>
    {
        var scene = World();
        scene["car"]!.Components[Doc.SceneComponents.Vehicle] = new Doc.VehicleComponent();

        var (_, wheel) = Selecting("wheel", scene);
        Assert.True(Has(wheel, "section_wheel"));

        var (_, car) = Selecting("car", scene);
        Assert.False(Has(car, "section_wheel"));

        var (_, hub) = Selecting("hub", scene);
        Assert.False(Has(hub, "section_wheel"));
    });

    [Fact]
    public Task AWheelIsNotOfferedWhereThereIsNoCar() => Headless.Run(() =>
    {
        var (_, window) = Selecting("wheel", World());

        Assert.False(Has(window, "section_wheel"));
    });

    [Fact]
    public Task DraggingTheSteerLockIsOneUndoStep() => Headless.Run(() =>
    {
        var scene = World();
        scene["car"]!.Components[Doc.SceneComponents.Vehicle] = new Doc.VehicleComponent();
        var (session, window) = Selecting("car", scene);
        var start = PhysicsComponents.VehicleOf(In(session, "car"))!.MaxSteer;

        Drag(window, "drag_steer_lock_0", 10);

        Assert.Equal(start + 2, PhysicsComponents.VehicleOf(In(session, "car"))!.MaxSteer, 6);
        session.History.Undo();
        Assert.Equal(start, PhysicsComponents.VehicleOf(In(session, "car"))!.MaxSteer, 6);
        Assert.False(session.History.CanUndo);
    });

    [Fact]
    public Task AddingAWheelAndMakingItSteerIsTwoSteps() => Headless.Run(() =>
    {
        var scene = World();
        scene["car"]!.Components[Doc.SceneComponents.Vehicle] = new Doc.VehicleComponent();
        var (session, window) = Selecting("wheel", scene);

        Press(window, "button_add_wheel");
        Press(window, "choice_steers_yes");

        var wheel = PhysicsComponents.WheelOf(In(session, "wheel"))!;
        Assert.True(wheel.Steers);
        Assert.Equal(0.35, wheel.Radius);
        session.History.Undo();
        Assert.False(PhysicsComponents.WheelOf(In(session, "wheel"))!.Steers);
    });

    [Fact]
    public Task AZoneIsOfferedOnABodyThatStaysPutAndNotOnOneThatMoves() => Headless.Run(() =>
    {
        var (_, slab) = Selecting("slab", World());
        Assert.True(Has(slab, "section_zone"));

        var (_, car) = Selecting("car", World());
        Assert.False(Has(car, "section_zone"));

        var (_, rock) = Selecting("rock", World());
        Assert.False(Has(rock, "section_zone"));
    });

    [Fact]
    public Task AZoneAFilePutOnAFreeBodyIsStillShownSoItCanBeTakenOff() => Headless.Run(() =>
    {
        var scene = World();
        scene["car"]!.Components[Doc.SceneComponents.Zone] = new Doc.ZoneComponent();
        var (_, window) = Selecting("car", scene);

        Assert.True(Has(window, "section_zone"));
    });

    [Fact]
    public Task OwningGravityGivesTheWorldsAndTheOtherFieldsStayWithTheBody() => Headless.Run(() =>
    {
        var (session, window) = Project();
        session.Select(["slab"]);
        Headless.Render();

        Press(window, "button_add_zone");
        Assert.False(Has(window, "row_pull"));
        Press(window, "choice_gravity_own");
        Assert.True(Has(window, "row_pull"));
        session.Save();

        var zone = PhysicsComponents.ZoneOf(In(session, "slab"))!;
        Assert.Equal(new Vector3(0, -9.81, 0), zone.Gravity);
        Assert.Null(zone.LinearDamping);
        Assert.Null(zone.AngularDamping);
        Assert.Contains("\"zone\"", File.ReadAllText(session.Workspace.Loaded!.Path!), StringComparison.Ordinal);
    });

    [Fact]
    public Task GivingGravityBackHidesItsRowAndUndoTakesTheZoneOff() => Headless.Run(() =>
    {
        var (session, window) = Selecting("slab", World());
        Press(window, "button_add_zone");
        Press(window, "choice_gravity_own");

        Press(window, "choice_gravity_same");
        Assert.False(Has(window, "row_pull"));
        Assert.Null(PhysicsComponents.ZoneOf(In(session, "slab"))!.Gravity);

        session.History.Undo();
        Headless.Render();
        Assert.True(Has(window, "row_pull"));

        Press(window, "button_remove_zone");
        Assert.Null(PhysicsComponents.ZoneOf(In(session, "slab")));
    });

    [Fact]
    public Task AZoneIsTakenOffAgainByUndoAndByItsOwnButton() => Headless.Run(() =>
    {
        var (session, window) = Selecting("slab", World());

        Press(window, "button_add_zone");
        session.History.Undo();
        Headless.Render();
        Assert.False(Has(window, "button_remove_zone"));

        Press(window, "button_add_zone");
        Press(window, "button_remove_zone");
        Assert.Null(PhysicsComponents.ZoneOf(In(session, "slab")));
    });

    // A project with the world saved as its main scene, so a save has somewhere to go.
    private (EditorSession Session, Window Window) Project()
    {
        Directory.CreateDirectory(Path.Combine(_project.FullName, "scenes"));
        File.WriteAllText(
            Path.Combine(_project.FullName, "scenes", "main" + Doc.SceneDocument.SceneExtension),
            SceneDocument.Encode(World()));
        return Show(ProjectWorkspace.Open(_project.FullName).Workspace, null);
    }

    // A rebuilt box announces its starting text, so the text is typed into a focused box and committed by leaving it.
    private static void Type(Window window, string name, string text)
    {
        var box = Headless.Find<TextBox>(window, name);
        Assert.True(box.Focus());
        box.Text = text;
        Headless.Render();
        Assert.True(Headless.Find<Button>(window, "elsewhere").Focus());
        Headless.Render();
    }

    private static Doc.SoundSurfaceComponent? Surface(EditorSession session, string id) =>
        PhysicsComponents.Of<Doc.SoundSurfaceComponent>(In(session, id), Doc.SceneComponents.SoundSurface);

    private static Doc.ParticleEmitterComponent? Emitter(EditorSession session, string id) =>
        PhysicsComponents.Of<Doc.ParticleEmitterComponent>(In(session, id), Doc.SceneComponents.Particles);
}
